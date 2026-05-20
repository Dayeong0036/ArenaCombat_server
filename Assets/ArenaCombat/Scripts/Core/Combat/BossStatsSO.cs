using UnityEngine;

namespace ArenaCombat.Core.Combat
{

[CreateAssetMenu(fileName = "BossStatsSO", menuName = "Scriptable Objects/BossStatsSO")]
public class BossStatsSO : BaseStatsSO
{
    public float BossMaxHP = 1000f;
    public float BossCurrentHP = 1000f;
    public float BossBaseDamage = 50f;
    public float BossBaseDefense = 20f;

    [Tooltip("Phase transition HP ratios (e.g. 0.75, 0.5, 0.25)")]
    public float[] BossPhaseThresholds;

    public float BossTelegraphTimeMultiplier = 1f;
    public float BossAggroSensitivity = 1f;
}

}
