// -----------------------------------------------------------------------
// <copyright file="EventHandler.cs" company="Mistaken">
// Copyright (c) Mistaken. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Exiled.Events.Extensions;
using static Exiled.Events.Events;

namespace Mistaken.BetterSCP.SCP500
{
    internal static class EventHandler
    {
        public static event CustomEventHandler<Scp500PlayerRevivedEventArgs> Scp500PlayerRevived;

        public static void OnScp500PlayerRevived(Scp500PlayerRevivedEventArgs ev)
        {
            Scp500PlayerRevived.InvokeSafely(ev);
        }
    }
}
