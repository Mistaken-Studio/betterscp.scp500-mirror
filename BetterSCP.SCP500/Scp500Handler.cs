using System;
using System.Collections.Generic;
using CustomPlayerEffects;
using InventorySystem.Items;
using InventorySystem.Items.Usables;
using MEC;
using Mirror;
using Mistaken.API;
using Mistaken.API.Extensions;
using Mistaken.BetterSCP.SCP500.EventArgs;
using Mistaken.PseudoGUI;
using NorthwoodLib.Pools;
using PlayerRoles;
using PlayerStatsSystem;
using PluginAPI.Core;
using PluginAPI.Core.Attributes;
using PluginAPI.Enums;
using PluginAPI.Events;
using UnityEngine;

namespace Mistaken.BetterSCP.SCP500;

internal sealed class Scp500Handler
{
    public static Scp500Handler Instance { get; private set; }

    public Scp500Handler()
    {
        Instance = this;
        EventManager.RegisterEvents(this);
    }

    ~Scp500Handler()
    {
        EventManager.UnregisterEvents(this);
    }

    public bool Resurect(Player player, BasicRagdoll ragdoll)
    {
        if (_runningResurrections.Contains(player))
        {
            player.SetGUI("u500_error", PseudoGUIPosition.TOP, $"Nie udało się wskrzesić gracza | Już jesteś w trakcie wskrzeszania jakiegoś gracza", 5);
            return false;
        }

        Player target = Player.Get(ragdoll.NetworkInfo.OwnerHub);

        if (!target.IsConnected())
        {
            player.SetGUI("u500_error", PseudoGUIPosition.TOP, $"Nie udało się wskrzesić gracza | Gracza {ragdoll.NetworkInfo.Nickname} nie ma na serwerze", 5);
            return false;
        }

        if (_resurrected.Contains(target))
        {
            player.SetGUI("u500_error", PseudoGUIPosition.TOP, "Nie udało się wskrzesić gracza | Gracz może zostać wskrzeszony raz na życie", 5);
            return false;
        }

        if (ragdoll.NetworkInfo.RoleType == RoleTypeId.Scp0492 && !_roleBeforeRecall.ContainsKey(target))
        {
            player.SetGUI("u500_error", PseudoGUIPosition.TOP, "Nie udało się wskrzesić gracza | Nie znaleziono roli gracza przed sprzed wskrzeszenia", 5);
            return false;
        }

        _runningResurrections.Add(player);
        Timing.RunCoroutine(ExecuteResurrection(player, target, ragdoll));
        return true;
    }

    internal static readonly Dictionary<Player, List<BasicRagdoll>> _resurrectableRagdolls = new();
    private static readonly HashSet<Player> _resurrected = new();
    private static readonly List<Player> _runningResurrections = new();
    private static readonly Dictionary<Player, RoleTypeId> _roleBeforeRecall = new();

    private IEnumerator<float> ExecuteResurrection(Player player, Player target, BasicRagdoll ragdoll)
    {
        ServerLogs.AddLog(
            ServerLogs.Modules.ClassChange,
            $"Player {player.ToStringLogs()} started resurrection of {target.ToStringLogs()}",
            ServerLogs.ServerLogType.GameEvent);
        target.SetSessionVariable(SessionVarType.RESPAWN_BLOCK, true);
        player.SetSessionVariable(SessionVarType.BLOCK_INVENTORY_INTERACTION, true);
        player.SetGUI("u500", PseudoGUIPosition.MIDDLE, $"Używam <color=yellow>SCP-500</color> na {target.GetDisplayName()}", 9);

        var originalRole = player.Role;
        Vector3 resurrectPosition = player.Position;

        for (int i = 0; i < 7; i++)
        {
            if (!player.IsConnected())
            {
                _runningResurrections.Remove(player);
                yield break;
            }

            if (Vector3.Distance(resurrectPosition, player.Position) > 2.5f)
            {
                _runningResurrections.Remove(player);
                yield break;
            }

            yield return Timing.WaitForSeconds(1f);
        }

        if (player.Role != originalRole)
        {
            _runningResurrections.Remove(player);
            yield break;
        }

        if (!target.IsConnected())
        {
            player.SetGUI("u500_error", PseudoGUIPosition.TOP, "Nie udało się wskrzesić gracza | Gracza nie ma na serwerze", 5);
            player.SetSessionVariable(SessionVarType.BLOCK_INVENTORY_INTERACTION, false);
            _runningResurrections.Remove(player);
            yield break;
        }

        if (target.IsOverwatchEnabled)
        {
            player.SetGUI("u500_error", PseudoGUIPosition.TOP, "Nie udało się wskrzesić gracza | Gracz chyba nie chce być wskrzeszony", 5);
            target.SetSessionVariable(SessionVarType.RESPAWN_BLOCK, false);
            player.SetSessionVariable(SessionVarType.BLOCK_INVENTORY_INTERACTION, false);
            _runningResurrections.Remove(player);
            yield break;
        }

        if (target.IsAlive)
        {
            player.SetGUI("u500_error", PseudoGUIPosition.TOP, "Nie udało się wskrzesić gracza | Jesteś pewien że ten gracz jest martwy?", 5);
            target.SetSessionVariable(SessionVarType.RESPAWN_BLOCK, false);
            player.SetSessionVariable(SessionVarType.BLOCK_INVENTORY_INTERACTION, false);
            _runningResurrections.Remove(player);
            yield break;
        }

        try
        {
            NetworkServer.Destroy(ragdoll.gameObject);
        }
        catch
        {
            Debug.LogError("ExecuteResurrection: Ragdoll GameObject didn't exist!");
        }

        var item = player.CurrentItem;
        player.ReferenceHub.inventory.UserInventory.Items.Remove(item.ItemSerial);
        player.ReferenceHub.inventory.SendItemsNextFrame = true;
        _resurrected.Add(target);
        target.SetSessionVariable(SessionVarType.NO_SPAWN_PROTECT, true);
        target.SetSessionVariable(SessionVarType.ITEM_LESS_CLSSS_CHANGE, true);

        if (ragdoll.NetworkInfo.RoleType == RoleTypeId.Scp0492)
        {
            target.Role = _roleBeforeRecall[target];
            _roleBeforeRecall.Remove(target);
        }
        else
            target.Role = ragdoll.NetworkInfo.RoleType;

        target.IsGodModeEnabled = true;

        void DisableGodMode()
        {
            target.Position = resurrectPosition;
            target.IsGodModeEnabled = false;
        }

        Timing.CallDelayed(3.5f, DisableGodMode);
        target.SetSessionVariable(SessionVarType.RESPAWN_BLOCK, false);
        player.SetSessionVariable(SessionVarType.BLOCK_INVENTORY_INTERACTION, false);
        target.SetSessionVariable(SessionVarType.NO_SPAWN_PROTECT, false);
        target.SetSessionVariable(SessionVarType.ITEM_LESS_CLSSS_CHANGE, false);
        target.ClearInventory();

        yield return Timing.WaitForSeconds(0.5f);
        target.Health = 5;
        target.ArtificialHealth = 75;
        target.ReferenceHub.inventory.UserInventory.Items.Add(item.ItemSerial, item);

        try
        {
            (item as Consumable).ServerOnUsingCompleted();
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
        }

        target.SetGUI("u500", PseudoGUIPosition.MIDDLE, $"Zostałeś <color=green>wskrzeszony</color> przez {player.GetDisplayName()}", 5);
        target.EffectsManager.EnableEffect<Blinded>(10);
        target.EffectsManager.EnableEffect<Deafened>(15);
        target.EffectsManager.EnableEffect<Disabled>(20);
        target.EffectsManager.EnableEffect<Concussed>(15);
        target.EffectsManager.EnableEffect<Flashed>(5);

        _runningResurrections.Remove(player);
        ServerLogs.AddLog(
            ServerLogs.Modules.ClassChange,
            $"Player {player.ToStringLogs()} successfully resurrected {target.ToStringLogs()}",
            ServerLogs.ServerLogType.GameEvent);
        Scp500.OnPlayerRevived(new PlayerRevivedEventArgs(target, player));
    }

    [PluginEvent(ServerEventType.PlayerUsedItem)]
    private void OnPlayerUsedItem(Player player, ItemBase item)
    {
        if (item.ItemTypeId != ItemType.SCP500)
            return;

        var effect = player.EffectsManager.GetEffect<MovementBoost>();
        byte oldIntensity = effect.Intensity;
        effect.Intensity = 10;
        effect.ServerChangeDuration(7, true);
        Timing.CallDelayed(8, () => effect.Intensity = oldIntensity);
    }

    [PluginEvent(ServerEventType.PlayerChangeRole)]
    private void OnPlayerChangeRole(Player player, PlayerRoleBase role, RoleTypeId newRole, RoleChangeReason reason)
    {
        if (player is null)
            return;

        if (_resurrected.Contains(player) && newRole != RoleTypeId.Spectator)
            _resurrected.Remove(player);

        player.SetGUI("u500", PseudoGUIPosition.TOP, null);
    }

    [PluginEvent(ServerEventType.PlayerDeath)]
    private void OnPlayerDeath(Player player, Player attacker, DamageHandlerBase handler)
    {
        if (player is null)
            return;

        player.SetGUI("u500", PseudoGUIPosition.TOP, null);

        if (_resurrected.Contains(player))
            Timing.CallDelayed(Plugin.Instance.Config.MaxDeathTime + 1, () => _resurrected.Remove(player));
    }

    [PluginEvent(ServerEventType.RoundRestart)]
    private void OnRoundRestart()
    {
        _resurrected.Clear();
        _roleBeforeRecall.Clear();
        _runningResurrections.Clear();
        _resurrectableRagdolls.Clear();
    }

    [PluginEvent(ServerEventType.PlayerChangeItem)]
    private void OnPlayerChangeItem(Player player, ushort oldItem, ushort newItem)
    {
        if (player.ReferenceHub.inventory.UserInventory.Items[newItem].ItemTypeId == ItemType.SCP500)
            Timing.RunCoroutine(Interface(player), nameof(Interface));
    }

    [PluginEvent(ServerEventType.Scp049ResurrectBody)]
    private void OnScp049ResurrectBody(Player player, Player target, BasicRagdoll ragdoll)
    {
        _roleBeforeRecall[target] = ragdoll.NetworkInfo.RoleType;
    }

    private IEnumerator<float> Interface(Player player)
    {
        yield return Timing.WaitForSeconds(1);

        if (!_resurrectableRagdolls.ContainsKey(player))
            _resurrectableRagdolls.Add(player, new());

        while (player?.CurrentItem?.ItemTypeId == ItemType.SCP500)
        {
            _resurrectableRagdolls[player].Clear();

            try
            {
                foreach (var ragdoll in API.Utilities.Map.Ragdolls)
                {
                    if (ragdoll == null)
                        continue;

                    var data = ragdoll.NetworkInfo;
                    if (data.ExistenceTime > Plugin.Instance.Config.MaxDeathTime)
                        continue;

                    if ((data.RoleType.GetTeam() == Team.SCPs && data.RoleType != RoleTypeId.Scp0492) || data.RoleType.GetTeam() == Team.OtherAlive)
                        continue;

                    if (data.Handler is PlayerStatsSystem.DisruptorDamageHandler)
                        continue;

                    var target = Player.Get(data.OwnerHub);

                    if (!target.IsConnected())
                        continue;

                    if (_resurrected.Contains(target))
                        continue;

                    var distance = Vector3.Distance(player.Position, ragdoll.transform.position);

                    if (distance < Plugin.Instance.Config.MaximalDistance)
                        _resurrectableRagdolls[player].Add(ragdoll);
                }

                var ragdolls = _resurrectableRagdolls[player];

                if (ragdolls.Count > 0)
                {
                    RagdollData data;
                    if (ragdolls.Count == 1)
                    {
                        data = ragdolls[0].NetworkInfo;
                        var deathtime = Plugin.Instance.Config.MaxDeathTime - Math.Floor(data.ExistenceTime);
                        player.SetGUI(
                        "u500",
                        PseudoGUIPosition.TOP,
                        $"Wpisz '.u500' w konsoli (~) aby <color=green>wskrzesić</color> {data.Nickname} - {data.RoleType} ({deathtime})");
                    }
                    else
                    {
                        List<string> informResurrect = ListPool<string>.Shared.Rent();
                        informResurrect.Add("Wpisz '.u500 [ID]' w konsoli (~) aby <color=green>wskrzesić</color>:");

                        for (int i = 0; i < ragdolls.Count; i++)
                        {
                            data = ragdolls[i].NetworkInfo;
                            var deathtime = Plugin.Instance.Config.MaxDeathTime - Math.Floor(data.ExistenceTime);
                            informResurrect.Add($"<br>ID: {i} | {data.Nickname} - {data.RoleType} ({deathtime})");
                        }

                        player.SetGUI("u500", PseudoGUIPosition.TOP, string.Join(string.Empty, informResurrect));
                        ListPool<string>.Shared.Return(informResurrect);
                    }
                }
                else
                    player.SetGUI("u500", PseudoGUIPosition.TOP, null);
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                player.SetGUI("u500", PseudoGUIPosition.TOP, null);
            }

            yield return Timing.WaitForSeconds(1);
        }

        player.SetGUI("u500", PseudoGUIPosition.TOP, null);
    }
}
