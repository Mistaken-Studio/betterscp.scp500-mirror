using Mistaken.BetterSCP.SCP500.EventArgs;
using System;

namespace Mistaken.BetterSCP.SCP500;

/// <summary>
/// Resurrection events.
/// </summary>
public static class Scp500
{
    /// <summary>
    /// Invoked when Player is revived.
    /// </summary>
    public static event Action<PlayerRevivedEventArgs> PlayerRevived;

    internal static void OnPlayerRevived(PlayerRevivedEventArgs ev)
        => PlayerRevived?.Invoke(ev);
}
