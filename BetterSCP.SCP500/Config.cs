using System.ComponentModel;

namespace Mistaken.BetterSCP.SCP500;

internal sealed class Config
{
    public bool Debug { get; set; } = false;

    [Description("Plugin options")]
    public float MaximalDistance { get; set; } = 6f;

    public float MaxDeathTime { get; set; } = 45f;
}
