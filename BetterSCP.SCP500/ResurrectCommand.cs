// -----------------------------------------------------------------------
// <copyright file="ResurrectCommand.cs" company="Mistaken">
// Copyright (c) Mistaken. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using CommandSystem;
using Mistaken.API.Commands;
using Mistaken.API.Extensions;

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
            var player = sender.GetPlayer();
            if (player.CurrentItem?.Type != ItemType.SCP500)
                return new string[] { "Nie masz SCP 500 w ręce" };
            if (!Scp500Handler.Instance.Resurect(sender.GetPlayer()))
                return new string[] { "Nie udało się nikogo wskrzsić" };
            success = true;
            return new string[] { "Rozpoczynam" };
        }
    }
}
