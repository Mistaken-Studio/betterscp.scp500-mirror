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
    [CommandHandler(typeof(ClientCommandHandler))]
    internal sealed class ResurrectCommand : IBetterCommand
    {
        public override string Description => "Resurrection";

        public override string Command => "u500";

        public override string[] Execute(ICommandSender sender, string[] args, out bool success)
        {
            success = false;
            var player = Player.Get(sender);

            if (player.CurrentItem?.Type != ItemType.SCP500)
                return new string[] { "Nie masz SCP 500 w ręce" };

            if (!Scp500Handler._resurrectableRagdolls.TryGetValue(player, out var ragdolls) || ragdolls.Count == 0)
                return new string[] { "Nie ma nikogo w pobliżu, kogo da się wskrzesić" };

            if (ragdolls.Count == 1)
            {
                if (!Scp500Handler.Instance.Resurect(player, ragdolls[0]))
                    return new string[] { "Nie udało się nikogo wskrzsić" };
            }
            else
            {
                if (args.Length == 0)
                {
                    var tor = new string[ragdolls.Count + 1];
                    tor[0] = "Musisz podać numer osoby, którą chcesz wskrzesić:";

                    for (int i = 0; i < ragdolls.Count; i++)
                        tor[i + 1] = $"Podaj argument '{i}' aby wskrzesić: {ragdolls[i].NetworkInfo.Nickname}";

                    return tor;
                }

                if (int.TryParse(args[0], out var id))
                {
                    if (id >= ragdolls.Count)
                        return new string[] { "Podałeś nieprawidłową wartość" };

                    if (!Scp500Handler.Instance.Resurect(player, ragdolls[id]))
                        return new string[] { "Nie udało się nikogo wskrzsić" };
                }
                else
                    return new string[] { "Podałeś nieprawidłową wartość" };
            }

            success = true;
            return new string[] { "Rozpoczynam" };
        }
    }
}
