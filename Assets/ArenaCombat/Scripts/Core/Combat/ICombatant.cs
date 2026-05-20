using UnityEngine;
using ArenaCombat.Core.Skill;

namespace ArenaCombat.Core.Combat
{
    /// <summary>
    /// Contract for combat-capable entities (players, bosses). Used by skill execution, damage flow,
    /// status / buff / debuff application, positional control, and parry reward delivery.
    ///
    /// X2-2: replaced the X0-5 minimum stub (7 members) with the full Buildup surface (23 members:
    /// 9 properties + 14 methods). PNC3D becomes first implementer in X3-1 (compile-bridge stub —
    /// read-only property forwards + warn-once no-op for all mutating methods). X3-2/3/4 wire
    /// actual routing to StatManager / position control. BossNetworkController3D in X4.
    /// </summary>
    public interface ICombatant
    {
        // Identity
        Transform  Transform  { get; }
        GameObject GameObject { get; }

        // State (read-only)
        float MaxHP            { get; }
        float CurrentHPPercent { get; } // 0..1
        float Shield           { get; }
        bool  IsAlive          { get; }
        bool  IsCasting        { get; } // charging / channeling / pattern wind-up
        bool  IsParrying       { get; } // currently inside parry input window
        float ParryWindow      { get; } // parry valid duration (PlayerStatsSO.ParryWindow)

        // Damage / heal
        // attacker: optional attacker reference — used for parry counter / hit-stun / reflect routing.
        void TakeDamage(float amount, ICombatant attacker = null);

        // Drains shield first; targets with shield take multiplier-scaled bonus damage.
        void TakeShieldBreakDamage(float amount, float multiplier, ICombatant attacker = null);

        void RecoverHP(float amount);
        void AddShield(float amount);

        // Status effects
        // value semantics depend on StatusType
        // (DamageOverTime -> DPS, Slowed -> reduction ratio, Reflecting -> reflect ratio, etc.)
        void ApplyStatus(StatusType type, float duration, float value = 0f);
        bool HasStatus(StatusType type);

        // Buff / debuff
        void ApplyBuff  (BuffType   type, float duration, float value);
        void ApplyDebuff(DebuffType type, float duration, float value);

        // Cleanse / dispel
        void RemoveStatuses(CleanseType type, int count);
        void RemoveBuffs   (DispelType  type, int count);

        // Positional control
        void Knockback(Vector3 direction, float distance);
        void Pull(Vector3 towardPosition, float distance, float duration);
        void MoveBy(Vector3 direction, float distance, float duration, MoveType moveType);

        // Parry reward delivery
        // Skill layer decides the reward kind after a successful parry and notifies the combatant.
        // attacker: required for Counter / HitStun rewards (effects target the attacker).
        void NotifyParryReward(ParryRewardType rewardType, float value, float duration, ICombatant attacker = null);
    }
}
