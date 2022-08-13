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

        public override string Name => "Resurection";

        public override void OnEnable()
        {
            Exiled.Events.Handlers.Server.RestartingRound += this.Server_RestartingRound;
            Exiled.Events.Handlers.Player.ChangingItem += this.Player_ChangingItem;
            Exiled.Events.Handlers.Player.UsingItem += this.Player_UsingItem;
            Exiled.Events.Handlers.Player.ChangingRole += this.Player_ChangingRole;
            Exiled.Events.Handlers.Player.Died += this.Player_Died;
            Exiled.Events.Handlers.Player.UsedItem += this.Player_UsedItem;
        }

        public override void OnDisable()
        {
            Exiled.Events.Handlers.Server.RestartingRound -= this.Server_RestartingRound;
            Exiled.Events.Handlers.Player.ChangingItem -= this.Player_ChangingItem;
            Exiled.Events.Handlers.Player.UsingItem -= this.Player_UsingItem;
            Exiled.Events.Handlers.Player.ChangingRole -= this.Player_ChangingRole;
            Exiled.Events.Handlers.Player.Died -= this.Player_Died;
            Exiled.Events.Handlers.Player.UsedItem -= this.Player_UsedItem;
        }

        public bool Resurect(Player player)
        {
            if (player.CurrentItem.Type != ItemType.SCP500)
                return false;
            var originalRole = player.Role;
            float nearestDistance = 999;
            Ragdoll nearest = null;
            foreach (var ragdoll in Map.Ragdolls.ToArray())
            {
                if (ragdoll.NetworkInfo.ExistenceTime > PluginHandler.Instance.Config.MaxDeathTime)
                    continue;
                if (ragdoll.NetworkInfo.RoleType.GetSide() == Exiled.API.Enums.Side.Scp || ragdoll.NetworkInfo.RoleType.GetSide() == Exiled.API.Enums.Side.Tutorial)
                    continue;
                var target = Player.Get(ragdoll.NetworkInfo.OwnerHub.playerId);
                if (target == null || target.IsOverwatchEnabled || target.GameObject == null || !target.IsConnected)
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
                    this.Log.Error(ex.Message);
                    this.Log.Error(ex.StackTrace);
                }
            }

            if (nearestDistance != 999)
            {
                var target = Player.Get(nearest.NetworkInfo.OwnerHub.playerId);
                if (!target?.IsConnected ?? true)
                {
                    player.SetGUI("u500_error", PseudoGUIPosition.TOP, "Nie udało się wskrzesić gracza | Gracza nie ma na serwerze", 5);
                    return false;
                }

                if (Resurected.Contains(target.UserId))
                {
                    player.SetGUI("u500_error", PseudoGUIPosition.TOP, "Nie udało się wskrzesić gracza | Gracz może zostać wskrzeszony raz na życie", 5);
                    return false;
                }

                if (Resurections.Where(i => i == target.UserId).Count() > 3)
                {
                    player.SetGUI("u500_error", PseudoGUIPosition.TOP, "Nie udało się wskrzesić gracza | Możesz wskrzesić 3 osoby na rundę", 5);
                    return false;
                }

                target.SetSessionVariable(SessionVarType.RESPAWN_BLOCK, true);
                player.SetSessionVariable(SessionVarType.BLOCK_INVENTORY_INTERACTION, true);
                player.EnableEffect<CustomPlayerEffects.Amnesia>(11);
                player.EnableEffect<CustomPlayerEffects.Ensnared>(11);
                player.SetGUI("u500", PseudoGUIPosition.MIDDLE, $"Używam <color=yellow>SCP 500</color> na {target.Nickname}", 9);
                this.CallDelayed(
                    10,
                    () =>
                    {
                        try
                        {
                            if (player.Role == originalRole)
                            {
                                target = Player.Get(nearest.NetworkInfo.OwnerHub.playerId);
                                if (target == null || target.GameObject == null || !target.IsConnected)
                                {
                                    player.SetGUI("u500_error", PseudoGUIPosition.TOP, "Nie udało się wskrzesić gracza | Gracza nie ma na serwerze", 5);
                                    target.SetSessionVariable(SessionVarType.RESPAWN_BLOCK, false);
                                    player.SetSessionVariable(SessionVarType.BLOCK_INVENTORY_INTERACTION, false);
                                    return;
                                }

                                if (target.IsOverwatchEnabled)
                                {
                                    player.SetGUI("u500_error", PseudoGUIPosition.TOP, "Nie udało się wskrzesić gracza | Gracz chyba nie chce być wskrzeszony", 5);
                                    target.SetSessionVariable(SessionVarType.RESPAWN_BLOCK, false);
                                    player.SetSessionVariable(SessionVarType.BLOCK_INVENTORY_INTERACTION, false);
                                    return;
                                }

                                if (target.IsAlive)
                                {
                                    player.SetGUI("u500_error", PseudoGUIPosition.TOP, "Nie udało się wskrzesić gracza | Jesteś pewien że ten gracz jest martwy?", 5);
                                    target.SetSessionVariable(SessionVarType.RESPAWN_BLOCK, false);
                                    player.SetSessionVariable(SessionVarType.BLOCK_INVENTORY_INTERACTION, false);
                                    return;
                                }

                                Vector3 pos = nearest.transform.position;
                                NetworkServer.Destroy(nearest.gameObject);
                                Resurected.Add(target.UserId);
                                var item = player.CurrentItem;
                                player.RemoveItem(item, false);
                                target.SetSessionVariable(SessionVarType.NO_SPAWN_PROTECT, true);
                                target.SetSessionVariable(SessionVarType.ITEM_LESS_CLSSS_CHANGE, true);
                                target.Role.Type = nearest.NetworkInfo.RoleType;

                                target.SetSessionVariable(SessionVarType.RESPAWN_BLOCK, false);
                                player.SetSessionVariable(SessionVarType.BLOCK_INVENTORY_INTERACTION, false);
                                EventHandler.OnScp500PlayerRevived(new Scp500PlayerRevivedEventArgs(target, player));
                                target.SetSessionVariable(SessionVarType.NO_SPAWN_PROTECT, false);
                                target.SetSessionVariable(SessionVarType.ITEM_LESS_CLSSS_CHANGE, false);
                                target.ClearInventory();
                                this.CallDelayed(
                                    0.5f,
                                    () =>
                                    {
                                        target.AddItem(item);
                                        this.CallDelayed(
                                            0.5f,
                                            () =>
                                            {
                                                try
                                                {
                                                    ((Consumable)item.Base).ServerOnUsingCompleted();
                                                }
                                                catch
                                                {
                                                }
                                            });
                                        this.CallDelayed(0.5f, () => target.Position = pos + Vector3.up);
                                        target.SetGUI("u500", PseudoGUIPosition.MIDDLE, $"Zostałeś <color=yellow>wskrzeszony</color> przez {player.Nickname}", 5);
                                        target.Health = 5;
                                        target.ArtificialHealth = 75;
                                        target.EnableEffect<CustomPlayerEffects.Blinded>(10);
                                        target.EnableEffect<CustomPlayerEffects.Deafened>(15);
                                        target.EnableEffect<CustomPlayerEffects.Disabled>(30);
                                        target.EnableEffect<CustomPlayerEffects.Concussed>(15);
                                        target.EnableEffect<CustomPlayerEffects.Flashed>(5);
                                        RLogger.Log("RESURECT", "RESURECT", $"Resurected {target.PlayerToString()}");
                                    }, "Resurect.Respawn");
                            }
                        }
                        catch (Exception ex)
                        {
                            this.Log.Error(ex);
                            target.SetSessionVariable(SessionVarType.RESPAWN_BLOCK, false);
                            player.SetSessionVariable(SessionVarType.BLOCK_INVENTORY_INTERACTION, false);
                        }
                    }, "Resurection.Resurect");
                return true;
            }
            else
                player.SendConsoleMessage("[SCP 500] No targets in range", "red");
            return false;
        }

        private static readonly List<string> Resurections = new List<string>();
        private static readonly HashSet<string> Resurected = new HashSet<string>();

        private void Player_UsedItem(Exiled.Events.EventArgs.UsedItemEventArgs ev)
        {
            if (ev.Item.Type != ItemType.SCP500)
                return;

            // ev.Player.EnableEffect<CustomPlayerEffects.Invigorated>(30);
            var effect = ev.Player.GetEffect(Exiled.API.Enums.EffectType.MovementBoost);
            byte oldIntensity = effect.Intensity;
            effect.Intensity = 10;
            effect.ServerChangeDuration(7, true);
            MEC.Timing.CallDelayed(8, () => effect.Intensity = oldIntensity);

            // ev.Player.ArtificialHealth += 1;
            // SCP500Shield.Ini<SCP500Shield>(ev.Player);
        }

        private void Player_ChangingRole(Exiled.Events.EventArgs.ChangingRoleEventArgs ev)
        {
            ev.Player.SetGUI("u500", PseudoGUIPosition.TOP, null);
        }

        private void Player_Died(Exiled.Events.EventArgs.DiedEventArgs ev)
        {
            ev.Target.SetGUI("u500", PseudoGUIPosition.TOP, null);
            if (Resurected.Contains(ev.Target.UserId))
                Timing.CallDelayed(PluginHandler.Instance.Config.MaxDeathTime + 1, () => Resurected.Remove(ev.Target.UserId));
        }

        private void Server_RestartingRound()
        {
            Resurections.Clear();
            Resurected.Clear();
        }

        private void Player_UsingItem(Exiled.Events.EventArgs.UsingItemEventArgs ev)
        {
            if (!ev.IsAllowed)
                return;
            if (ev.Item.Type != ItemType.SCP500)
                return;
            if (ev.Player.GetEffectActive<CustomPlayerEffects.Amnesia>())
                ev.IsAllowed = false;
        }

        private void Player_ChangingItem(Exiled.Events.EventArgs.ChangingItemEventArgs ev)
        {
            if (ev.NewItem?.Type == ItemType.SCP500)
                this.RunCoroutine(this.Interface(ev.Player), "Interface");
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
                        if (ragdoll.NetworkInfo.ExistenceTime > PluginHandler.Instance.Config.MaxDeathTime)
                            continue;
                        if (ragdoll.NetworkInfo.RoleType.GetSide() == Exiled.API.Enums.Side.Scp || ragdoll.NetworkInfo.RoleType.GetSide() == Exiled.API.Enums.Side.Tutorial)
                            continue;
                        target = Player.Get(ragdoll.NetworkInfo.OwnerHub.playerId);
                        if (!target?.IsConnected ?? true)
                            continue;
                        if (Resurected.Contains(target.UserId))
                            continue;
                        var distance = Vector3.Distance(player.Position, ragdoll.Base.transform.position);
                        if (distance < PluginHandler.Instance.Config.MaximalDistance && distance < nearestDistance)
                        {
                            nearest = ragdoll.Base;
                            nearestDistance = distance;
                        }
                    }

                    if (!player.GetEffectActive<CustomPlayerEffects.Amnesia>())
                    {
                        if (nearestDistance != 999)
                        {
                            player.SetGUI("u500", PseudoGUIPosition.TOP, $"Wpisz '.u500' w konsoli(~) aby <color=yellow>wskrzesić</color> {target.Nickname} ({PluginHandler.Instance.Config.MaxDeathTime - Math.Floor(nearest.NetworkInfo.ExistenceTime)})");
                        }
                        else
                            player.SetGUI("u500", PseudoGUIPosition.TOP, null);
                    }
                }
                catch (Exception ex)
                {
                    this.Log.Error(ex.Message);
                    this.Log.Error(ex.StackTrace);
                    player.SetGUI("u500", PseudoGUIPosition.TOP, null);
                }

                yield return Timing.WaitForSeconds(1);
            }

            player.SetGUI("u500", PseudoGUIPosition.TOP, null);
        }
    }
}
