using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;

namespace OverlayDoctor;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/overlays";
    private const string Reason = "requested by Overlay Doctor";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commands;
    private readonly IChatGui chat;
    private readonly IPluginLog log;
    private readonly IFramework framework;

    private readonly ICallGateSubscriber<bool> iinactHealthy;
    private readonly ICallGateSubscriber<string> iinactStatus;
    private readonly ICallGateSubscriber<string, bool> iinactRestart;
    private readonly ICallGateSubscriber<bool> browsingwayHealthy;
    private readonly ICallGateSubscriber<string> browsingwayStatus;
    private readonly ICallGateSubscriber<string, bool> browsingwayRestart;

    private readonly DiagLog? diag;
    private int busy;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IChatGui chat, IPluginLog log, IFramework framework)
    {
        this.pluginInterface = pluginInterface;
        this.commands = commands;
        this.chat = chat;
        this.log = log;
        this.framework = framework;

        iinactHealthy = pluginInterface.GetIpcSubscriber<bool>("IINACT.Healthy");
        iinactStatus = pluginInterface.GetIpcSubscriber<string>("IINACT.Status");
        iinactRestart = pluginInterface.GetIpcSubscriber<string, bool>("IINACT.Restart");
        browsingwayHealthy = pluginInterface.GetIpcSubscriber<bool>("Browsingway.Healthy");
        browsingwayStatus = pluginInterface.GetIpcSubscriber<string>("Browsingway.Status");
        browsingwayRestart = pluginInterface.GetIpcSubscriber<string, bool>("Browsingway.Restart");

        diag = OpenDiagLog();
        diag?.Write($"Overlay Doctor {typeof(Plugin).Assembly.GetName().Version} loaded, pid {Environment.ProcessId}");

        commands.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "status: how the parser and the renderer are; fix: restart or load whatever is unwell",
        });
    }

    public void Dispose()
    {
        commands.RemoveHandler(Command);
        diag?.Write("unloading");
        diag?.Dispose();
    }

    private DiagLog? OpenDiagLog()
    {
        try
        {
            DateTime started;
            try { started = System.Diagnostics.Process.GetCurrentProcess().StartTime; }
            catch (Exception) { started = DateTime.Now; }
            return new DiagLog(Path.Combine(pluginInterface.ConfigDirectory.FullName, "diag"), started, Environment.ProcessId);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "diagnostic log unavailable");
            return null;
        }
    }

    private void OnCommand(string command, string args)
    {
        var (iinact, browsingway) = Probe();
        chat.Print($"Overlay Doctor: {Doctor.Summary(iinact, browsingway)}.");
        diag?.Write($"/overlays {args.Trim()}: {Doctor.Summary(iinact, browsingway)}");
        if (args.Trim() != "fix")
            return;

        var plan = Doctor.Plan(iinact, browsingway);
        chat.Print("Overlay Doctor: " + string.Join(", ", plan.Select(Doctor.Describe)) + ".");
        diag?.Write("plan: " + string.Join(", ", plan.Select(Doctor.Describe)));
        if (Interlocked.Exchange(ref busy, 1) != 0)
        {
            chat.PrintError("Overlay Doctor: a fix is already running.");
            return;
        }
        Task.Run(async () =>
        {
            try
            {
                foreach (var step in plan)
                {
                    await Run(step);
                    diag?.Write($"done: {Doctor.Describe(step)}");
                    // Restarts are asynchronous: see the layer go unhealthy first, or a stale "healthy" ends the wait early.
                    var watched = step is Step.RestartParser or Step.LoadIinact ? iinactHealthy : browsingwayHealthy;
                    await WaitUntilUnhealthy(watched, TimeSpan.FromSeconds(5));
                    // The renderer's fresh pages must find a live parser, so the parser settles first.
                    if (step is Step.RestartParser or Step.LoadIinact)
                        await WaitUntil(iinactHealthy, TimeSpan.FromSeconds(45));
                }
                var verdict = await WaitForHealth(plan);
                diag?.Write(verdict);
                chat.Print(verdict);
            }
            catch (Exception ex)
            {
                log.Error(ex, "fix failed");
                diag?.Write($"fix FAILED: {ex.GetType().Name}: {ex.Message}");
                chat.PrintError($"Overlay Doctor: {ex.Message}. Fall back to /xldisableplugintemp and /xlenableplugintemp for that plugin.");
            }
            finally
            {
                Interlocked.Exchange(ref busy, 0);
            }
        });
    }

    // "done" has to be the last line; the restarts settle asynchronously.
    private async Task<string> WaitForHealth(IReadOnlyList<Step> plan)
    {
        var watchParser = plan.Contains(Step.RestartParser) || plan.Contains(Step.LoadIinact);
        var watchRenderer = plan.Contains(Step.RestartRenderer) || plan.Contains(Step.LoadBrowsingway);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(45);
        while (DateTime.UtcNow < deadline)
        {
            var parserOk = !watchParser || await framework.RunOnFrameworkThread(() => Healthy(iinactHealthy));
            var rendererOk = !watchRenderer || await framework.RunOnFrameworkThread(() => Healthy(browsingwayHealthy));
            if (parserOk && rendererOk)
                return "Overlay Doctor: done; everything reports healthy.";
            await Task.Delay(500);
        }
        var stuck = new List<string>();
        if (watchParser && !await framework.RunOnFrameworkThread(() => Healthy(iinactHealthy)))
            stuck.Add("IINACT");
        if (watchRenderer && !await framework.RunOnFrameworkThread(() => Healthy(browsingwayHealthy)))
            stuck.Add("Browsingway");
        return $"Overlay Doctor: done, but {string.Join(" and ", stuck)} has not reported healthy after 45 s; "
               + "use /xldisableplugintemp then /xlenableplugintemp on it.";
    }

    private async Task WaitUntil(ICallGateSubscriber<bool> healthy, TimeSpan limit)
    {
        var deadline = DateTime.UtcNow + limit;
        while (DateTime.UtcNow < deadline && !await framework.RunOnFrameworkThread(() => Healthy(healthy)))
            await Task.Delay(500);
    }

    private async Task WaitUntilUnhealthy(ICallGateSubscriber<bool> healthy, TimeSpan limit)
    {
        var deadline = DateTime.UtcNow + limit;
        while (DateTime.UtcNow < deadline && await framework.RunOnFrameworkThread(() => Healthy(healthy)))
            await Task.Delay(250);
    }

    private static bool Healthy(ICallGateSubscriber<bool> healthy)
    {
        try { return healthy.InvokeFunc(); }
        catch (Exception) { return false; }
    }

    private async Task Run(Step step)
    {
        switch (step)
        {
            case Step.LoadIinact:
                chat.Print($"Overlay Doctor: IINACT {await PluginControl.Load(pluginInterface, "IINACT")}.");
                break;
            case Step.RestartParser:
                if (!iinactRestart.InvokeFunc(Reason))
                    chat.PrintError("Overlay Doctor: IINACT declined the restart (one is already running).");
                break;
            case Step.LoadBrowsingway:
                chat.Print($"Overlay Doctor: Browsingway {await PluginControl.Load(pluginInterface, "Browsingway")}.");
                break;
            case Step.RestartRenderer:
                if (!browsingwayRestart.InvokeFunc(Reason))
                    chat.PrintError("Overlay Doctor: Browsingway could not restart its renderer.");
                break;
        }
    }

    private (Report iinact, Report browsingway) Probe() =>
        (Ask("IINACT", iinactHealthy, iinactStatus), Ask("Browsingway", browsingwayHealthy, browsingwayStatus));

    private Report Ask(string internalName, ICallGateSubscriber<bool> healthy, ICallGateSubscriber<string> status)
    {
        try
        {
            return new Report(true, healthy.InvokeFunc(), status.InvokeFunc());
        }
        catch (IpcNotReadyError)
        {
            return Report.Absent($"not loaded ({PluginControl.State(pluginInterface, internalName)})");
        }
        catch (Exception ex)
        {
            log.Warning(ex, $"{internalName} status call failed");
            return new Report(true, false, $"error: {ex.Message}");
        }
    }
}
