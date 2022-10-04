// -----------------------------------------------------------------------
// <copyright file="Scp500Handler.cs" company="Mistaken">
// Copyright (c) Mistaken. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Interfaces;
using InventorySystem.Items.Usables;
using MEC;
using Mirror;
using Mistaken.API;
using Mistaken.API.Diagnostics;
using Mistaken.API.Extensions;
using Mistaken.API.GUI;
using Mistaken.RoundLogger;
using UnityEngine;

namespace Mistaken.BetterSCP.SCP500
{
    internal class Scp500Handler : Module
    {
        public static Scp500Handler Instance { get; private set; }

        public Scp500Handler(IPlugin<IConfig> plugin)
            : base(plugin)
        {
            Instance = this;
        }

        public override string Name => nameof(Scp500Handler);

        public override void OnEnable()
        {
            Exiled.Events.Handlers.Server.RestartingRound += this.Server_RestartingRound;
            Exiled.Events.Handlers.Player.ChangingItem += this.Player_ChangingItem;
            Exiled.Events.Handlers.Player.ChangingRole += this.Player_ChangingRole;
            Exiled.Events.Handlers.Player.Died += this.Player_Died;
            Exiled.Events.Handlers.Player.UsedItem += this.Player_UsedItem;
            Exiled.Events.Handlers.Scp049.FinishingRecall += this.Scp049_FinishingRecall;
        }

        public override void OnDisable()
        {
            Exiled.Events.Handlers.Server.RestartingRound -= this.Server_RestartingRound;
            Exiled.Events.Handlers.Player.ChangingItem -= this.Player_ChangingItem;
            Exiled.Events.Handlers.Player.ChangingRole -= this.Player_ChangingRole;
            Exiled.Events.Handlers.Player.Died -= this.Player_Died;
            Exiled.Events.Handlers.Player.UsedItem -= this.Player_UsedItem;
            Exiled.Events.Handlers.Scp049.FinishingRecall -= this.Scp049_FinishingRecall;
        }

        public bool Resurect(Player player)
        {
            float nearestDistance = 999;
            Ragdoll nearest = null;
            Player target;
            foreach (var ragdoll in Map.Ragdolls.ToArray())
            {
                if (ragdoll.NetworkInfo.ExistenceTime > PluginHandler.Instance.Config.MaxDeathTime)
                    continue;

                if ((ragdoll.NetworkInfo.RoleType.GetSide() == Exiled.API.Enums.Side.Scp && ragdoll.NetworkInfo.RoleType != RoleType.Scp0492) || ragdoll.NetworkInfo.RoleType.GetSide() == Exiled.API.Enums.Side.Tutorial)
                    continue;

                if (ragdoll.DamageHandler is PlayerStatsSystem.DisruptorDamageHandler)
                    continue;

                target = Player.Get(ragdoll.NetworkInfo.OwnerHub.playerId);
                if (!target.IsConnected() || target.IsOverwatchEnabled)
                {
                    player.SendConsoleMessage($"[SCP 500] {ragdoll.NetworkInfo.OwnerHub.name} nie ma na serwerze albo ma overwatcha", "red");
                    continue;
                }

                try
                {
                    var distance = Vector3.Distance(player.Position, ragdoll.Base.transform.position);
                    if (distance < PluginHandler.Instance.Config.MaximalDistance && distance < nearestDistance)
                    {
                        nearest = ragdoll.Base;
                        nearestDistance = distance;
                    }
                }
                catch (Exception ex)
                {
                    this.Log.Error("Failed to get ragdoll distance");
                    this.Log.Error(ex);
                }
            }

            if (nearestDistance == 999)
            {
                player.SendConsoleMessage("[SCP 500] No targets in range", "red");
                return false;
            }

            target = Player.Get(nearest.NetworkInfo.OwnerHub.playerId);
            if (!target.IsConnected())
            {
                player.SetGUI("u500_error", PseudoGUIPosition.TOP, "Nie udało się wskrzesić gracza | Gracza nie ma na serwerze", 5);
                return false;
            }

            if (Resurrected.Contains(target.UserId))
            {
                player.SetGUI("u500_error", PseudoGUIPosition.TOP, "Nie udało się wskrzesić gracza | Gracz może zostać wskrzeszony raz na życie", 5);
                return false;
            }

            if (nearest.NetworkInfo.RoleType == RoleType.Scp0492 && !RoleBeforeRecall.ContainsKey(target))
            {
                player.SetGUI("u500_error", PseudoGUIPosition.TOP, "Nie udało się wskrzesić gracza | Nie znaleziono roli gracza przed sprzed wskrzeszenia (zgłoś ten błąd do Xname#3824)", 5);
                return false;
            }

            this.RunCoroutine(this.ExecuteResurrection(player, target, nearest), "BetterSCP.SCP500_ExecuteResurrection", true);
            return true;
        }

        private static readonly HashSet<string> Resurrected = new ();
        private static readonly Dictionary<Player, (RoleType RoleType, Vector3 Position)> RoleBeforeRecall = new ();

        private IEnumerator<float> ExecuteResurrection(Player player, Player target, Ragdoll ragdoll)
        {
            target.SetSessionVariable(SessionVarType.RESPAWN_BLOCK, true);
            player.SetSessionVariable(SessionVarType.BLOCK_INVENTORY_INTERACTION, true);
            player.EnableEffect<CustomPlayerEffects.Ensnared>(11);
            player.SetGUI("u500", PseudoGUIPosition.MIDDLE, $"Używam <color=yellow>SCP 500</color> na {target.Nickname}", 9);

            var originalRole = player.Role.Type;
            yield return Timing.WaitForSeconds(10f);

            if (player.Role.Type != originalRole)
                yield break;

            if (!target.IsConnected())
            {
                player.SetGUI("u500_error", PseudoGUIPosition.TOP, "Nie udało się wskrzesić gracza | Gracza nie ma na serwerze", 5);
                target.SetSessionVariable(SessionVarType.RESPAWN_BLOCK, false);
                player.SetSessionVariable(SessionVarType.BLOCK_INVENTORY_INTERACTION, false);
                yield break;
            }

            if (target.IsOverwatchEnabled)
            {
                player.SetGUI("u500_error", PseudoGUIPosition.TOP, "Nie udało się wskrzesić gracza | Gracz chyba nie chce być wskrzeszony", 5);
                target.SetSessionVariable(SessionVarType.RESPAWN_BLOCK, false);
                player.SetSessionVariable(SessionVarType.BLOCK_INVENTORY_INTERACTION, false);
                yield break;
            }

            if (target.IsAlive)
            {
                player.SetGUI("u500_error", PseudoGUIPosition.TOP, "Nie udało się wskrzesić gracza | Jesteś pewien że ten gracz jest martwy?", 5);
                target.SetSessionVariable(SessionVarType.RESPAWN_BLOCK, false);
                player.SetSessionVariable(SessionVarType.BLOCK_INVENTORY_INTERACTION, false);
                yield break;
            }

            try
            {
                NetworkServer.Destroy(ragdoll.gameObject);
            }
            catch
            {
                UnityEngine.Debug.LogError("ExecuteResurrection: Ragdoll GameObject didn't exist!");
            }

            var item = player.CurrentItem;
            player.RemoveItem(item, false);
            Resurrected.Add(target.UserId);
            target.SetSessionVariable(SessionVarType.NO_SPAWN_PROTECT, true);
            target.SetSessionVariable(SessionVarType.ITEM_LESS_CLSSS_CHANGE, true);

            if (ragdoll.NetworkInfo.RoleType == RoleType.Scp0492)
            {
                target.Role.Type = RoleBeforeRecall[target].RoleType;
                RoleBeforeRecall.Remove(target);
            }
            else
                target.Role.Type = ragdoll.NetworkInfo.RoleType;

            target.SetSessionVariable(SessionVarType.RESPAWN_BLOCK, false);
            player.SetSessionVariable(SessionVarType.BLOCK_INVENTORY_INTERACTION, false);
            target.SetSessionVariable(SessionVarType.NO_SPAWN_PROTECT, false);
            target.SetSessionVariable(SessionVarType.ITEM_LESS_CLSSS_CHANGE, false);
            target.ClearInventory();

            yield return Timing.WaitForSeconds(0.5f);
            target.Health = 5;
            target.ArtificialHealth = 75;
            target.AddItem(item);

            try
            {
                ((Consumable)item.Base).ServerOnUsingCompleted();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError(ex);
            }

            target.SetGUI("u500", PseudoGUIPosition.MIDDLE, $"Zostałeś <color=green>wskrzeszony</color> przez {player.Nickname}", 5);
            target.EnableEffect<CustomPlayerEffects.Blinded>(10);
            target.EnableEffect<CustomPlayerEffects.Deafened>(15);
            target.EnableEffect<CustomPlayerEffects.Disabled>(30);
            target.EnableEffect<CustomPlayerEffects.Concussed>(15);
            target.EnableEffect<CustomPlayerEffects.Flashed>(5);

            yield return Timing.WaitForSeconds(0.5f);
            target.Position = RoleBeforeRecall[target].Position;
            RLogger.Log("RESURECT", "RESURECT", $"Resurected {target.PlayerToString()}");
            EventHandler.OnScp500PlayerRevived(new Scp500PlayerRevivedEventArgs(target, player));
        }

        private void Player_UsedItem(Exiled.Events.EventArgs.UsedItemEventArgs ev)
        {
            if (ev.Item.Type != ItemType.SCP500)
                return;

            var effect = ev.Player.GetEffect(Exiled.API.Enums.EffectType.MovementBoost);
            byte oldIntensity = effect.Intensity;
            effect.Intensity = 10;
            effect.ServerChangeDuration(7, true);
            this.CallDelayed(8, () => effect.Intensity = oldIntensity, "BetterSCP.SCP500_Change_Intensity", true);
        }

        private void Player_ChangingRole(Exiled.Events.EventArgs.ChangingRoleEventArgs ev)
        {
            ev.Player.SetGUI("u500", PseudoGUIPosition.TOP, null);
        }

        private void Player_Died(Exiled.Events.EventArgs.DiedEventArgs ev)
        {
            ev.Target.SetGUI("u500", PseudoGUIPosition.TOP, null);
            if (Resurrected.Contains(ev.Target.UserId))
                this.CallDelayed(PluginHandler.Instance.Config.MaxDeathTime + 1, () => Resurrected.Remove(ev.Target.UserId), "BetterSCP.SCP500_Resurrected_Remove", true);
        }

        private void Server_RestartingRound()
        {
            Resurrected.Clear();
            RoleBeforeRecall.Clear();
        }

        private void Player_ChangingItem(Exiled.Events.EventArgs.ChangingItemEventArgs ev)
        {
            if (ev.NewItem?.Type == ItemType.SCP500)
                this.RunCoroutine(this.Interface(ev.Player), "BetterSCP.SCP500_Interface");
        }

        private void Scp049_FinishingRecall(Exiled.Events.EventArgs.FinishingRecallEventArgs ev)
        {
            if (!ev.IsAllowed)
                return;

            Exiled.API.Features.Ragdoll ragdoll = ev.Ragdoll;
            if (ragdoll == null || ragdoll.Base == null)
            {
                UnityEngine.Debug.LogError("Ragdoll was null (1)");
                ragdoll = Exiled.API.Features.Ragdoll.Get(ev.Target).LastOrDefault();
                if (ragdoll == null || ragdoll.Base == null)
                {
                    UnityEngine.Debug.LogError("Ragdoll was null (2)");
                    return;
                }
            }

            RoleBeforeRecall[ev.Target] = (ev.Ragdoll.NetworkInfo.RoleType, ev.Ragdoll.Base.transform.position + Vector3.up);
        }

        private IEnumerator<float> Interface(Player player)
        {
            yield return Timing.WaitForSeconds(1);

            while (player?.CurrentItem?.Type == ItemType.SCP500)
            {
                try
                {
                    float nearestDistance = 999;
                    Ragdoll nearest = null;
                    Player target = null;
                    foreach (var ragdoll in Map.Ragdolls.ToArray())
                    {
                        if (ragdoll.Base == null)
                            continue;

                        if (ragdoll.NetworkInfo.ExistenceTime > PluginHandler.Instance.Config.MaxDeathTime)
                            continue;

                        if ((ragdoll.NetworkInfo.RoleType.GetSide() == Exiled.API.Enums.Side.Scp && ragdoll.NetworkInfo.RoleType != RoleType.Scp0492) || ragdoll.NetworkInfo.RoleType.GetSide() == Exiled.API.Enums.Side.Tutorial)
                            continue;

                        if (ragdoll.DamageHandler is PlayerStatsSystem.DisruptorDamageHandler)
                            continue;

                        target = Player.Get(ragdoll.NetworkInfo.OwnerHub.playerId);
                        if (!target.IsConnected())
                            continue;

                        if (Resurrected.Contains(target.UserId))
                            continue;

                        var distance = Vector3.Distance(player.Position, ragdoll.Base.transform.position);
                        if (distance < PluginHandler.Instance.Config.MaximalDistance && distance < nearestDistance)
                        {
                            nearest = ragdoll.Base;
                            nearestDistance = distance;
                        }
                    }

                    if (nearestDistance != 999)
                        player.SetGUI("u500", PseudoGUIPosition.TOP, $"Wpisz '.u500' w konsoli(~) aby <color=yellow>wskrzesić</color> {target.Nickname} ({PluginHandler.Instance.Config.MaxDeathTime - Math.Floor(nearest.NetworkInfo.ExistenceTime)})");
                    else
                        player.SetGUI("u500", PseudoGUIPosition.TOP, null);
                }
                catch (Exception ex)
                {
                    this.Log.Error(ex);
                    player.SetGUI("u500", PseudoGUIPosition.TOP, null);
                }

                yield return Timing.WaitForSeconds(1);
            }

            player.SetGUI("u500", PseudoGUIPosition.TOP, null);
        }
    }
}
