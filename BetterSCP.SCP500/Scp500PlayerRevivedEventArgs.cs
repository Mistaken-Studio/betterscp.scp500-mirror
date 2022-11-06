// -----------------------------------------------------------------------
// <copyright file="Scp500PlayerRevivedEventArgs.cs" company="Mistaken">
// Copyright (c) Mistaken. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using Exiled.API.Features;

namespace Mistaken.BetterSCP.SCP500
{
    /// <summary>
    /// EventArgs for PlayerRevived Event.
    /// </summary>
    public sealed class Scp500PlayerRevivedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the Revived Player.
        /// </summary>
        public Player Revived { get; }

        /// <summary>
        /// Gets the Reviving Player.
        /// </summary>
        public Player Reviver { get; }

        internal Scp500PlayerRevivedEventArgs(Player revived, Player reviver)
        {
            this.Revived = revived;
            this.Reviver = reviver;
        }
    }
}
