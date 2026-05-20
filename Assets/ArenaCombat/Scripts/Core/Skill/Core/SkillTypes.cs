// Base type definitions used across the skill system.
// SkillStep      : execution unit (Primitive / Wrapper unified under this delegate).
// SkillCondition : condition evaluator — used by TriggerOnCondition (#33) as the condition argument.

namespace ArenaCombat.Core.Skill
{
    // X2-5: activated alongside SkillContext arrival.
    public delegate void SkillStep(SkillContext ctx);
    public delegate bool SkillCondition(SkillContext ctx);

    // Area shape for AoE skills.
    public enum AreaShape
    {
        Circle,
        Cone,
        Line,
    }

    // Target selection mode.
    public enum TargetType
    {
        Single,
        Area,
        Self,
        Direction,
    }

    // Movement skill kind.
    public enum MoveType
    {
        Dash,
        Charge,
        Jump,
        Rope,
    }

    // Parry reward variants.
    public enum ParryRewardType
    {
        Counter,      // counter damage applied to attacker
        HitStun,      // hit-stun applied to attacker
        Invulnerable, // invulnerability granted to caster
        Buff,         // DamageUp buff granted to caster
    }

    // Status effects.
    public enum StatusType
    {
        Stunned,        // no movement, no actions
        HitStun,        // short stun (movement restricted)
        Slowed,         // reduced movement speed
        Rooted,         // no movement (actions allowed)
        Vulnerable,     // increased incoming damage
        Silence,        // skills disabled
        Invulnerable,   // damage nullified
        Reflecting,     // incoming damage reflected to attacker
        HPRegen,        // HP regen per second
        DamageOverTime, // damage per second (DoT)
        AntiHeal,       // reduced healing received
        Marked,         // vulnerability mark (bonus damage taken)
    }

    // Buff kinds.
    public enum BuffType
    {
        DamageUp,      // attack power up
        DefenseUp,     // defense up
        ParryWindowUp, // parry window duration up
        ParryRewardUp, // parry reward magnitude up
    }

    // Debuff kinds.
    public enum DebuffType
    {
        DamageDown,      // attack power down
        DefenseDown,     // defense down
        SelfDefenseDown, // self defense down (self-inflicted)
        Mark,            // vulnerability mark
    }

    // Cleanse scope.
    public enum CleanseType
    {
        All,            // all statuses
        Debuff,         // debuffs only
        DamageOverTime, // DoTs only
    }

    // Dispel scope.
    public enum DispelType
    {
        All,         // all buffs
        DefenseBuff, // defense buffs only
        OffenseBuff, // offense buffs only
    }
}
