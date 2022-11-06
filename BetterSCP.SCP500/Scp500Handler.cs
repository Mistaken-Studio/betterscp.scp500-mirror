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
using NorthwoodLib.Pools;
using UnityEngine;

namespace Mistaken.BetterSCP.SCP500
{
    internal sealed class Scp500Handler : Module
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

        public bool Resurect(Player player, Ragdoll targetsRagdoll)
        {
            if (_runningResurrections.Contains(player))
            {
                player.SetGUI("u500_error", PseudoGUIPosition.TOP, $"Nie udało się wskrzesić gracza | Już jesteś w trakcie wskrzeszania jakiegoś gracza", 5);
                return false;
            }

            Player target = Player.Get(targetsRagdoll.NetworkInfo.OwnerHub);

            if (!target.IsConnected())
            {
                player.SetGUI("u500_error", PseudoGUIPosition.TOP, $"Nie udało się wskrzesić gracza | Gracza {targetsRagdoll.NetworkInfo.Nickname} nie ma na serwerze", 5);
                return false;
            }

            if (_resurrected.Contains(target))
            {
                player.SetGUI("u500_error", PseudoGUIPosition.TOP, "Nie udało się wskrzesić gracza | Gracz może zostać wskrzeszony raz na życie", 5);
                return false;
            }

            if (targetsRagdoll.NetworkInfo.RoleType == RoleType.Scp0492 && !_roleBeforeRecall.ContainsKey(target))
            {
                player.SetGUI("u500_error", PseudoGUIPosition.TOP, "Nie udało się wskrzesić gracza | Nie znaleziono roli gracza przed sprzed wskrzeszenia", 5);
                return false;
            }

            _runningResurrections.Add(player);
            this.RunCoroutine(this.ExecuteResurrection(player, target, targetsRagdoll), "ExecuteResurrection", true);
            return true;
        }

        internal static readonly Dictionary<Player, List<Ragdoll>> _resurrectableRagdolls = new();
        private static readonly HashSet<Player> _resurrected = new();
        private static readonly List<Player> _runningResurrections = new();
        private static readonly Dictionary<Player, RoleType> _roleBeforeRecall = new();

        private IEnumerator<float> ExecuteResurrection(Player player, Player target, Ragdoll ragdoll)
        {
            RLogger.Log("RESURRECT", "START", $"Player {player.PlayerToString()} started resurrection of {target.PlayerToString()}");
            target.SetSessionVariable(SessionVarType.RESPAWN_BLOCK, true);
            player.SetSessionVariable(SessionVarType.BLOCK_INVENTORY_INTERACTION, true);
            player.SetGUI("u500", PseudoGUIPosition.MIDDLE, $"Używam <color=yellow>SCP-500</color> na {target.GetDisplayName()}", 9);

            var originalRole = player.Role.Type;
            Vector3 resurrectPosition = player.Position;

            for (int i = 0; i < 10; i++)
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

            if (player.Role.Type != originalRole)
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
            player.RemoveItem(item, false);
            _resurrected.Add(target);
            target.SetSessionVariable(SessionVarType.NO_SPAWN_PROTECT, true);
            target.SetSessionVariable(SessionVarType.ITEM_LESS_CLSSS_CHANGE, true);

            if (ragdoll.NetworkInfo.RoleType == RoleType.Scp0492)
            {
                target.Role.Type = _roleBeforeRecall[target];
                _roleBeforeRecall.Remove(target);
            }
            else
                target.Role.Type = ragdoll.NetworkInfo.RoleType;

            target.IsGodModeEnabled = true;

            void DisableGodMode()
            {
                target.Position = resurrectPosition;
                target.IsGodModeEnabled = false;
            }

            this.CallDelayed(3.5f, DisableGodMode, nameof(DisableGodMode));
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
                Debug.LogError(ex);
            }

            target.SetGUI("u500", PseudoGUIPosition.MIDDLE, $"Zostałeś <color=green>wskrzeszony</color> przez {player.GetDisplayName()}", 5);
            target.EnableEffect<CustomPlayerEffects.Blinded>(10);
            target.EnableEffect<CustomPlayerEffects.Deafened>(15);
            target.EnableEffect<CustomPlayerEffects.Disabled>(20);
            target.EnableEffect<CustomPlayerEffects.Concussed>(15);
            target.EnableEffect<CustomPlayerEffects.Flashed>(5);

            _runningResurrections.Remove(player);
            RLogger.Log("RESURRECT", "FINISH", $"Player {player.PlayerToString()} successfully resurrected {target.PlayerToString()}");
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
            this.CallDelayed(8, () => effect.Intensity = oldIntensity, "Change_Intensity", true);
        }

        private void Player_ChangingRole(Exiled.Events.EventArgs.ChangingRoleEventArgs ev)
        {
            if (!ev.IsAllowed)
                return;

            if (_resurrected.Contains(ev.Player) && ev.NewRole != RoleType.Spectator)
                _resurrected.Remove(ev.Player);

            ev.Player.SetGUI("u500", PseudoGUIPosition.TOP, null);
        }

        private void Player_Died(Exiled.Events.EventArgs.DiedEventArgs ev)
        {
            ev.Target.SetGUI("u500", PseudoGUIPosition.TOP, null);

            if (_resurrected.Contains(ev.Target))
                this.CallDelayed(PluginHandler.Instance.Config.MaxDeathTime + 1, () => _resurrected.Remove(ev.Target), "Resurrected_Remove", true);
        }

        private void Server_RestartingRound()
        {
            _resurrected.Clear();
            _roleBeforeRecall.Clear();
            _runningResurrections.Clear();
            _resurrectableRagdolls.Clear();
        }

        private void Player_ChangingItem(Exiled.Events.EventArgs.ChangingItemEventArgs ev)
        {
            if (ev.NewItem?.Type == ItemType.SCP500)
                this.RunCoroutine(this.Interface(ev.Player), nameof(this.Interface));
        }

        private void Scp049_FinishingRecall(Exiled.Events.EventArgs.FinishingRecallEventArgs ev)
        {
            if (!ev.IsAllowed)
                return;

            Exiled.API.Features.Ragdoll ragdoll = ev.Ragdoll;

            if (ragdoll == null)
            {
                Debug.LogError("Ragdoll was null (1)");
                ragdoll = Exiled.API.Features.Ragdoll.Get(ev.Target).LastOrDefault();

                if (ragdoll == null)
                {
                    Debug.LogError("Ragdoll was null (2)");
                    return;
                }
            }

            _roleBeforeRecall[ev.Target] = ev.Ragdoll.NetworkInfo.RoleType;
        }

        private IEnumerator<float> Interface(Player player)
        {
            yield return Timing.WaitForSeconds(1);

            if (!_resurrectableRagdolls.ContainsKey(player))
                _resurrectableRagdolls.Add(player, new());

            while (player?.CurrentItem?.Type == ItemType.SCP500)
            {
                _resurrectableRagdolls[player].Clear();

                try
                {
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

                        var target = Player.Get(ragdoll.NetworkInfo.OwnerHub);

                        if (!target.IsConnected())
                            continue;

                        if (_resurrected.Contains(target))
                            continue;

                        var distance = Vector3.Distance(player.Position, ragdoll.Base.transform.position);

                        if (distance < PluginHandler.Instance.Config.MaximalDistance)
                            _resurrectableRagdolls[player].Add(ragdoll.Base);
                    }

                    var ragdolls = _resurrectableRagdolls[player];

                    if (ragdolls.Count > 0)
                    {
                        RagdollInfo info;
                        if (ragdolls.Count == 1)
                        {
                            info = ragdolls[0].NetworkInfo;
                            var deathtime = PluginHandler.Instance.Config.MaxDeathTime - Math.Floor(info.ExistenceTime);
                            player.SetGUI(
                            "u500",
                            PseudoGUIPosition.TOP,
                            $"Wpisz '.u500' w konsoli (~) aby <color=green>wskrzesić</color> {info.Nickname} - {info.RoleType} ({deathtime})");
                        }
                        else
                        {
                            List<string> informResurrect = ListPool<string>.Shared.Rent();
                            informResurrect.Add("Wpisz '.u500 [ID]' w konsoli (~) aby <color=green>wskrzesić</color>:");

                            for (int i = 0; i < ragdolls.Count; i++)
                            {
                                info = ragdolls[i].NetworkInfo;
                                var deathtime = PluginHandler.Instance.Config.MaxDeathTime - Math.Floor(info.ExistenceTime);
                                informResurrect.Add($"<br>ID: {i} | {info.Nickname} - {info.RoleType} ({deathtime})");
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
                    this.Log.Error(ex);
                    player.SetGUI("u500", PseudoGUIPosition.TOP, null);
                }

                yield return Timing.WaitForSeconds(1);
            }

            player.SetGUI("u500", PseudoGUIPosition.TOP, null);
        }
    }
}
