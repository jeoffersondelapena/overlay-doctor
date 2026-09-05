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

    private readonly ICallGateSubscriber<bool> iinactHealthy;
    private readonly ICallGateSubscriber<string> iinactStatus;
    private readonly ICallGateSubscriber<string, bool> iinactRestart;
    private readonly ICallGateSubscriber<bool> browsingwayHealthy;
    private readonly ICallGateSubscriber<string> browsingwayStatus;
    private readonly ICallGateSubscriber<string, bool> browsingwayRestart;

    private int busy;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IChatGui chat, IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commands = commands;
        this.chat = chat;
        this.log = log;

        iinactHealthy = pluginInterface.GetIpcSubscriber<bool>("IINACT.Healthy");
        iinactStatus = pluginInterface.GetIpcSubscriber<string>("IINACT.Status");
        iinactRestart = pluginInterface.GetIpcSubscriber<string, bool>("IINACT.Restart");
        browsingwayHealthy = pluginInterface.GetIpcSubscriber<bool>("Browsingway.Healthy");
        browsingwayStatus = pluginInterface.GetIpcSubscriber<string>("Browsingway.Status");
        browsingwayRestart = pluginInterface.GetIpcSubscriber<string, bool>("Browsingway.Restart");

        commands.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "status: how the parser and the renderer are; fix: restart or load whatever is unwell",
        });
    }

    public void Dispose()
    {
        commands.RemoveHandler(Command);
    }

    private void OnCommand(string command, string args)
    {
        var (iinact, browsingway) = Probe();
        chat.Print($"Overlay Doctor: {Doctor.Summary(iinact, browsingway)}.");
        if (args.Trim() != "fix")
            return;

        var plan = Doctor.Plan(iinact, browsingway);
        chat.Print("Overlay Doctor: " + string.Join(", ", plan.Select(Doctor.Describe)) + ".");
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
                    await Run(step);
            }
            catch (Exception ex)
            {
                log.Error(ex, "fix failed");
                chat.PrintError($"Overlay Doctor: {ex.Message}. Fall back to /xldisableplugintemp and /xlenableplugintemp for that plugin.");
            }
            finally
            {
                Interlocked.Exchange(ref busy, 0);
            }
        });
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
