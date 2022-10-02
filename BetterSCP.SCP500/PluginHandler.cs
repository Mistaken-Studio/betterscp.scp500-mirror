// -----------------------------------------------------------------------
// <copyright file="PluginHandler.cs" company="Mistaken">
// Copyright (c) Mistaken. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using Exiled.API.Enums;
using Exiled.API.Features;

namespace Mistaken.BetterSCP.SCP500
{
    internal class PluginHandler : Plugin<Config>
    {
        public override string Author => "Mistaken Devs";

        public override string Name => "BetterSCP-SCP500";

        public override string Prefix => "MBSCP-500";

        public override PluginPriority Priority => PluginPriority.Default;

        public override Version RequiredExiledVersion => new (5, 2, 2);

        public override void OnEnabled()
        {
            Instance = this;

            new Scp500Handler(this);

            API.Diagnostics.Module.OnEnable(this);

            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            API.Diagnostics.Module.OnDisable(this);

            base.OnDisabled();
        }

        internal static PluginHandler Instance { get; private set; }
    }
}
