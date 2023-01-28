using PluginAPI.Core;

namespace Mistaken.BetterSCP.SCP500.EventArgs;

/// <summary>
/// EventArgs for PlayerRevived Event.
/// </summary>
public sealed class PlayerRevivedEventArgs : System.EventArgs
{
    /// <summary>
    /// Gets the Revived Player.
    /// </summary>
    public Player Revived { get; }

    /// <summary>
    /// Gets the Reviving Player.
    /// </summary>
    public Player Reviver { get; }

    internal PlayerRevivedEventArgs(Player revived, Player reviver)
    {
        Revived = revived;
        Reviver = reviver;
    }
}
