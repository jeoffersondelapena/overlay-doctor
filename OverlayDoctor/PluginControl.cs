using System.Reflection;
using Dalamud.Plugin;

namespace OverlayDoctor;

// Dalamud has no public API to load or reload another plugin; this walks the same internals ECommons relies on.
internal static class PluginControl
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public static string State(IDalamudPluginInterface pluginInterface, string internalName)
    {
        var found = Find(pluginInterface, internalName);
        if (found is null)
            return "not installed";
        var (plugin, type) = found.Value;
        return type.GetProperty("State", Any)?.GetValue(plugin)?.ToString() ?? "unknown";
    }

    /// <summary>Reload a loaded plugin, or load one that is not; returns what was done.</summary>
    public static async Task<string> Load(IDalamudPluginInterface pluginInterface, string internalName)
    {
        var (plugin, type) = Find(pluginInterface, internalName)
                             ?? throw new InvalidOperationException($"{internalName} is not in Dalamud's plugin list");
        var isLoaded = type.GetProperty("IsLoaded", Any)?.GetValue(plugin) as bool? ?? false;
        if (isLoaded)
        {
            var reload = type.GetMethod("ReloadAsync", Any, Type.EmptyTypes)
                         ?? throw new InvalidOperationException("this Dalamud build has no ReloadAsync");
            await (Task)reload.Invoke(plugin, null)!;
            return "reloaded";
        }

        var load = type.GetMethods(Any).FirstOrDefault(m => m.Name == "LoadAsync")
                   ?? throw new InvalidOperationException("this Dalamud build has no LoadAsync");
        var args = load.GetParameters().Select(DefaultArgument).ToArray();
        await (Task)load.Invoke(plugin, args)!;
        return "loaded";
    }

    private static object? DefaultArgument(ParameterInfo parameter)
    {
        if (parameter.ParameterType.IsEnum)
        {
            var names = Enum.GetNames(parameter.ParameterType);
            var preferred = names.FirstOrDefault(n => n == "Installer") ?? names.FirstOrDefault(n => n == "Reload") ?? names[0];
            return Enum.Parse(parameter.ParameterType, preferred);
        }
        if (parameter.HasDefaultValue)
            return parameter.DefaultValue;
        return parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null;
    }

    private static (object plugin, Type type)? Find(IDalamudPluginInterface pluginInterface, string internalName)
    {
        var dalamud = pluginInterface.GetType().Assembly;
        var managerType = dalamud.GetType("Dalamud.Plugin.Internal.PluginManager", throwOnError: true)!;
        var serviceType = dalamud.GetType("Dalamud.Service`1", throwOnError: true)!.MakeGenericType(managerType);
        var manager = serviceType.GetMethod("Get", Any, Type.EmptyTypes)?.Invoke(null, null)
                      ?? throw new InvalidOperationException("Dalamud's plugin manager is not available");
        var plugins = managerType.GetProperty("InstalledPlugins", Any)?.GetValue(manager) as System.Collections.IEnumerable
                      ?? throw new InvalidOperationException("Dalamud's plugin list is not readable");
        foreach (var plugin in plugins)
        {
            var type = plugin.GetType();
            if (type.GetProperty("InternalName", Any)?.GetValue(plugin) as string == internalName)
                return (plugin, type);
        }
        return null;
    }
}
