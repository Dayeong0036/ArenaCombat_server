using UnityEngine;
using ArenaCombat.Core.Network;

namespace ArenaCombat.Core.Combat
{
    /// <summary>
    /// Per-attack data for 3D combat resolution.
    /// Inspector-authored ScriptableObject; assigned to CombatManager3D.attackTable.
    /// </summary>
    [CreateAssetMenu(fileName = "AttackData3D_New", menuName = "ArenaCombat/Combat/AttackData3D", order = 100)]
    public class AttackData3D : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private AttackType attackType = AttackType.Light;
        [SerializeField] private DamageType damageType = DamageType.Physical;

        [Header("Damage")]
        [SerializeField, Min(0f)] private float damage = 10f;

        [Header("Hitbox (used in B1-4 hit resolution)")]
        [Tooltip("Distance from attacker origin to hitbox center along facing direction.")]
        [SerializeField, Min(0f)] private float range = 2f;

        [Tooltip("Half-extents for Physics.OverlapBox. Full size = halfExtents * 2.")]
        [SerializeField] private Vector3 hitboxHalfExtents = new Vector3(1f, 0.75f, 0.75f);

        [Header("Cooldown")]
        [SerializeField, Min(0f)] private float cooldown = 0.3f;

        [Header("Status (B1-4 honors StatusMask.None only — duration model deferred)")]
        [Tooltip("Status to apply to hit targets. B1-4 will only honor StatusMask.None until status duration model exists.")]
        [SerializeField] private StatusMask appliedStatus = StatusMask.None;

        public AttackType AttackType => attackType;
        public DamageType DamageType => damageType;
        public float Damage => damage;
        public float Range => range;
        public Vector3 HitboxHalfExtents => hitboxHalfExtents;
        public float Cooldown => cooldown;
        public StatusMask AppliedStatus => appliedStatus;
    }
}
