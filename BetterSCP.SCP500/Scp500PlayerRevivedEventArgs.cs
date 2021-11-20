// -----------------------------------------------------------------------
// <copyright file="Scp500PlayerRevivedEventArgs.cs" company="Mistaken">
// Copyright (c) Mistaken. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using Exiled.API.Features;

namespace Mistaken.BetterSCP.SCP500
{
    public class Scp500PlayerRevivedEventArgs : EventArgs
    {
        public Scp500PlayerRevivedEventArgs(Player revived, Player reviver)
        {
            this.Revived = revived;
            this.Reviver = reviver;
        }

        public Player Revived { get; set; }

        public Player Reviver { get; set; }
    }
}
