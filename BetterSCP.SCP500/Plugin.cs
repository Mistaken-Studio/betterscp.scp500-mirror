using PluginAPI.Core.Attributes;

namespace Mistaken.BetterSCP.SCP500;

internal sealed class Plugin
{
    public static Plugin Instance { get; private set; }

    [PluginConfig]
    public Config Config;

    [PluginEntryPoint("BetterSCP SCP500", "1.0.0", "Allows resurrection of players using SCP-500", "Mistaken Devs")]
    private void Load()
    {
        Instance = this;
        new Scp500Handler();
    }

    [PluginUnload]
    private void Unload()
    {
    }
}
