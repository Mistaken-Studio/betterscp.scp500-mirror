// -----------------------------------------------------------------------
// <copyright file="SCP500Shield.cs" company="Mistaken">
// Copyright (c) Mistaken. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using Mistaken.API.Shield;
using UnityEngine;

namespace Mistaken.BetterSCP.SCP500
{
    internal sealed class SCP500Shield : Shield
    {
        protected override float MaxShield => this.maxShield;

        protected override float ShieldRechargeRate => 500f;

        protected override float ShieldEffectivnes => 1f;

        protected override float TimeUntilShieldRecharge => 0f;

        protected override float ShieldDropRateOnOverflow => 200f;

        protected override void Start()
        {
            base.Start();
            this.StartCoroutine(this.Disable());

            foreach (var item in this.gameObject.GetComponents<Shield>())
            {
                if (item != this && item.enabled)
                {
                    item.enabled = false;
                    this.disabledShields.Add(item);
                }
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            foreach (var item in this.gameObject.GetComponents<Shield>())
            {
                if (this.disabledShields.Contains(item))
                    item.enabled = true;
            }

            this.disabledShields.Clear();
        }

        private readonly List<Shield> disabledShields = new();
        private int maxShield = 1000;

        private IEnumerator Disable()
        {
            int i = 0;

            while ((this.Process.CurrentAmount < 500 || i < 25) && i < 40)
            {
                i++;
                yield return new WaitForSeconds(.1f);
            }

            this.maxShield = 0;

            while (this.Process.CurrentAmount > 0)
                yield return new WaitForSeconds(.1f);

            Destroy(this);
        }
    }
}
