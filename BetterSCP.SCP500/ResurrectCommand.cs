// -----------------------------------------------------------------------
// <copyright file="ResurrectCommand.cs" company="Mistaken">
// Copyright (c) Mistaken. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using CommandSystem;
using Exiled.API.Features;
using Mistaken.API.Commands;

namespace Mistaken.BetterSCP.SCP500
{
    [CommandSystem.CommandHandler(typeof(CommandSystem.ClientCommandHandler))]
    internal class ResurrectCommand : IBetterCommand
    {
        public override string Description => "Resurection";

        public override string Command => "u500";

        public override string[] Aliases => new string[] { };

        public override string[] Execute(ICommandSender sender, string[] args, out bool success)
        {
            success = false;
            var player = Player.Get(sender);
            if (player.CurrentItem?.Type != ItemType.SCP500)
                return new string[] { "Nie masz SCP 500 w ręce" };
            if (!Scp500Handler.Instance.Resurect(player))
                return new string[] { "Nie udało się nikogo wskrzsić" };
            success = true;
            return new string[] { "Rozpoczynam" };
        }
    }
}
