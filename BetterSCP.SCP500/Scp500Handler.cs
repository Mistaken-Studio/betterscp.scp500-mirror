﻿// -----------------------------------------------------------------------
// <copyright file="Scp500Handler.cs" company="Mistaken">
// Copyright (c) Mistaken. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
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
        }

        public override void OnDisable()
        {
            Exiled.Events.Handlers.Server.RestartingRound -= this.Server_RestartingRound;
            Exiled.Events.Handlers.Player.ChangingItem -= this.Player_ChangingItem;
            Exiled.Events.Handlers.Player.UsingItem -= this.Player_UsingItem;
            Exiled.Events.Handlers.Player.ChangingRole -= this.Player_ChangingRole;
            Exiled.Events.Handlers.Player.Died -= this.Player_Died;
        }

        public bool Resurect(Player player)
        {
            if (player.CurrentItem.Type != ItemType.SCP500)
                return false;
            var originalRole = player.Role;
            float nearestDistance = 999;
            Ragdoll nearest = null;
            foreach (var ragdoll in UnityEngine.Object.FindObjectsOfType<Ragdoll>().Where(x => x.CurrentTime < PluginHandler.Instance.Config.MaxDeathTime))
            {
                if (ragdoll.Networkowner.FullName.ToLower().Contains("scp") || ragdoll.Networkowner.FullName.ToLower().Contains("tutorial"))
                    continue;
                var target = Player.Get(ragdoll.Networkowner.PlayerId);
                if (target == null || target.IsOverwatchEnabled || target.GameObject == null || !target.IsConnected)
                {
                    player.SendConsoleMessage($"[SCP 500] {ragdoll.Networkowner.Nick} nie ma na serwerze albo ma overwatcha", "red");
                    continue;
                }

                try
                {
                    var distance = Vector3.Distance(player.Position, ragdoll.transform.position);
                    if (distance < PluginHandler.Instance.Config.MaximalDistance && distance < nearestDistance)
                    {
                        nearest = ragdoll;
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
                var target = Player.Get(nearest.Networkowner.PlayerId);
                if (Resurected.Contains(target.UserId))
                {
                    player.SetGUI("u500", PseudoGUIPosition.TOP, "Nie udało się wskrzesić gracza | Gracz może zostać wskrzeszony raz na rundę", 5);
                    return false;
                }

                if (Resurections.Where(i => i == target.UserId).Count() > 3)
                {
                    player.SetGUI("u500", PseudoGUIPosition.TOP, "Nie udało się wskrzesić gracza | Możesz wskrzesić 3 osoby na rundę", 5);
                    return false;
                }

                player.EnableEffect<CustomPlayerEffects.Amnesia>(15);
                player.EnableEffect<CustomPlayerEffects.Ensnared>(11);
                player.SetGUI("u500", PseudoGUIPosition.MIDDLE, $"Używam <color=yellow>SCP 500</color> na {target.Nickname}", 9);
                this.CallDelayed(
                    10,
                    () =>
                    {
                        if (player.Role == originalRole)
                        {
                            target = Player.Get(nearest.Networkowner.PlayerId);
                            if (target == null || target.GameObject == null || !target.IsConnected)
                            {
                                player.SetGUI("u500", PseudoGUIPosition.TOP, "Nie udało się wskrzesić gracza | Gracza nie ma na serwerze", 5);
                                return;
                            }

                            if (target.IsOverwatchEnabled)
                            {
                                player.SetGUI("u500", PseudoGUIPosition.TOP, "Nie udało się wskrzesić gracza | Gracz chyba nie chce być wskrzeszony", 5);
                                return;
                            }

                            if (target.IsAlive)
                            {
                                player.SetGUI("u500", PseudoGUIPosition.TOP, "Nie udało się wskrzesić gracza | Jesteś pewien że ten gracz jest martwy?", 5);
                                return;
                            }

                            Vector3 pos = nearest.transform.position;
                            NetworkServer.Destroy(nearest.gameObject);
                            Resurected.Add(target.UserId);
                            Resurections.Add(player.UserId);
                            ((Consumable)player.CurrentItem.Base).ServerOnUsingCompleted();
                            target.SetSessionVariable(SessionVarType.NO_SPAWN_PROTECT, true);
                            target.SetSessionVariable(SessionVarType.ITEM_LESS_CLSSS_CHANGE, true);
                            foreach (var role in target.ReferenceHub.characterClassManager.Classes)
                            {
                                if (role.fullName == nearest.Networkowner.FullName)
                                    target.Role = role.roleId;
                            }

                            EventHandler.OnScp500PlayerRevived(new Scp500PlayerRevivedEventArgs(target, player));
                            target.SetSessionVariable(SessionVarType.NO_SPAWN_PROTECT, false);
                            target.SetSessionVariable(SessionVarType.ITEM_LESS_CLSSS_CHANGE, false);
                            target.ClearInventory();
                            this.CallDelayed(
                                0.5f,
                                () =>
                                {
                                    target.Position = pos + Vector3.up;
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
                    }, "Resurection.Resurect");
                return true;
            }
            else
                player.SendConsoleMessage("[SCP 500] No targers in range", "red");
            return false;
        }

        private static readonly List<string> Resurections = new List<string>();
        private static readonly HashSet<string> Resurected = new HashSet<string>();

        private void Player_ChangingRole(Exiled.Events.EventArgs.ChangingRoleEventArgs ev)
        {
            ev.Player.SetGUI("u500", PseudoGUIPosition.TOP, null);
        }

        private void Player_Died(Exiled.Events.EventArgs.DiedEventArgs ev)
        {
            ev.Target.SetGUI("u500", PseudoGUIPosition.TOP, null);
        }

        private void Server_RestartingRound()
        {
            Resurections.Clear();
            Resurected.Clear();
        }

        private void Player_UsingItem(Exiled.Events.EventArgs.UsingItemEventArgs ev)
        {
            if (ev.Item.Type == ItemType.SCP500 && ev.Player.GetEffectActive<CustomPlayerEffects.Amnesia>())
                ev.IsAllowed = false;
        }

        private void Player_ChangingItem(Exiled.Events.EventArgs.ChangingItemEventArgs ev)
        {
            if (ev.NewItem.Type == ItemType.SCP500)
                this.RunCoroutine(this.Interface(ev.Player), "Interface");
        }

        private IEnumerator<float> Interface(Player player)
        {
            yield return Timing.WaitForSeconds(1);

            while (player?.CurrentItem.Type == ItemType.SCP500)
            {
                try
                {
                    float nearestDistance = 999;
                    Ragdoll nearest = null;
                    Player target = null;
                    foreach (var ragdoll in UnityEngine.Object.FindObjectsOfType<Ragdoll>().Where(x => x.CurrentTime < PluginHandler.Instance.Config.MaxDeathTime).ToArray())
                    {
                        if (ragdoll.Networkowner.FullName.ToLower().Contains("scp"))
                            continue;
                        if (ragdoll.Networkowner.FullName.ToLower().Contains("tutorial"))
                            continue;
                        target = Player.Get(ragdoll.Networkowner.PlayerId);
                        if (target == null)
                            continue;
                        var distance = Vector3.Distance(player.Position, ragdoll.transform.position);
                        if (distance < PluginHandler.Instance.Config.MaximalDistance && distance < nearestDistance)
                        {
                            nearest = ragdoll;
                            nearestDistance = distance;
                        }
                    }

                    if (!target.GetEffectActive<CustomPlayerEffects.Amnesia>())
                    {
                        if (nearestDistance != 999)
                        {
                            player.SetGUI("u500", PseudoGUIPosition.TOP, $"Wpisz '.u500' w konsoli(~) aby <color=yellow>wskrzesić</color> {target.Nickname} ({PluginHandler.Instance.Config.MaxDeathTime - Math.Floor(nearest.CurrentTime)})");
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