using CommandSystem;
using PluginAPI.Core;
using System;

namespace Mistaken.BetterSCP.SCP500;

[CommandHandler(typeof(ClientCommandHandler))]
internal sealed class ResurrectCommand : ICommand
{
    public string Command => "u500";

    public string[] Aliases => Array.Empty<string>();

    public string Description => "Allows resurrection of a player";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        var player = Player.Get(sender);

        if (player.CurrentItem?.ItemTypeId != ItemType.SCP500)
        {
            response = "Nie masz SCP 500 w ręce" ;
            return false;
        }

        if (!Scp500Handler._resurrectableRagdolls.TryGetValue(player, out var ragdolls) || ragdolls.Count == 0)
        {
            response = "Nie ma nikogo w pobliżu, kogo da się wskrzesić";
            return false;
        }

        if (ragdolls.Count == 1)
        {
            if (!Scp500Handler.Instance.Resurect(player, ragdolls[0]))
            {
                response = "Nie udało się nikogo wskrzsić";
                return false;
            }
        }
        else
        {
            if (arguments.Count == 0)
            {
                response = "Musisz podać numer osoby, którą chcesz wskrzesić:\n";

                for (int i = 0; i < ragdolls.Count; i++)
                    response += $"Podaj argument '{i}' aby wskrzesić: {ragdolls[i].NetworkInfo.Nickname}\n";

                return false;
            }

            if (int.TryParse(arguments.At(0), out var id))
            {
                if (id >= ragdolls.Count)
                {
                    response = "Podałeś nieprawidłową wartość";
                    return false;
                }

                if (!Scp500Handler.Instance.Resurect(player, ragdolls[id]))
                {
                    response = "Nie udało się nikogo wskrzsić";
                    return false;
                }
            }
            else
            {
                response = "Podałeś nieprawidłową wartość";
                return false;
            }
        }

        response = "Rozpoczynam";
        return true;
    }
}
