using System.Collections;
using System.Collections.Generic;
using Mistaken.API.Shield;
using UnityEngine;

namespace Mistaken.BetterSCP.SCP500;

internal sealed class Scp500Shield : Shield
{
    protected override float MaxShield => maxShield;

    protected override float ShieldRechargeRate => 500f;

    protected override float ShieldEffectivnes => 1f;

    protected override float TimeUntilShieldRecharge => 0f;

    protected override float ShieldDropRateOnOverflow => 200f;

    protected override void Start()
    {
        base.Start();
        StartCoroutine(Disable());

        foreach (var item in gameObject.GetComponents<Shield>())
        {
            if (item != this && item.enabled)
            {
                item.enabled = false;
                disabledShields.Add(item);
            }
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        foreach (var item in gameObject.GetComponents<Shield>())
        {
            if (disabledShields.Contains(item))
                item.enabled = true;
        }

        disabledShields.Clear();
    }

    private readonly List<Shield> disabledShields = new();
    private int maxShield = 1000;

    private IEnumerator Disable()
    {
        int i = 0;

        while ((Process.CurrentAmount < 500 || i < 25) && i < 40)
        {
            i++;
            yield return new WaitForSeconds(.1f);
        }

        maxShield = 0;

        while (Process.CurrentAmount > 0)
            yield return new WaitForSeconds(.1f);

        Destroy(this);
    }
}
