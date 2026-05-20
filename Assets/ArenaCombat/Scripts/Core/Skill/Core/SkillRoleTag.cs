// Tag describing a skill's role / niche.
//
// Used by:
//   - SkillDefinition.RoleTags    : what this skill IS (Burst / DOT / ...)
//   - SkillDefinition.CounterTags : what this skill is good AGAINST
//   - SkillRegistry.GetByRoleTag / GetCounterCandidates : draft + boss AI matching
//
// APPEND-ONLY: enum values are safe to add at the end. Removing or reordering
// breaks .asset references (Unity serializes enum by ordinal index).
//
// Reserved index ranges:
//   0..8   X2-5 originals (do not reorder)
//   9..28  X1-6b-1 reserved (Buildup 12 PlayerSkill audit append)
//   29+    Future rounds (Boss skill audit / new skills) — append only.

namespace ArenaCombat.Core.Skill
{
    public enum SkillRoleTag
    {
        // ── X2-5 originals (0..8) ──────────────────────────────
        Burst,    // 0 — high single-hit damage
        DOT,      // 1 — damage over time
        Shield,   // 2 — defensive / shield grant
        Parry,    // 3 — parry-related (window extension, parry-trigger payload)
        Zone,     // 4 — persistent area / lingering AoE
        Counter,  // 5 — effective against casting / channeling enemies
        Heal,     // 6 — HP restoration
        Mobility, // 7 — knockback / pull / displacement
        Mark,     // 8 — vulnerability mark / debuff stack

        // ── X1-6b-1 append (Buildup 12 PlayerSkill audit, 9..28) ──
        // Damage type
        AOE,         // 9  — instant area-of-effect (distinct from Zone = persistent)
        MultiHit,    // 10 — multi-strike sequence
        Pierce,      // 11 — armor / defense pierce
        // Attack range
        Melee,       // 12 — short range
        Ranged,      // 13 — long range
        // Stat modulation
        DamageUp,    // 14 — damage output increase
        DefUp,       // 15 — defense increase
        DefDown,     // 16 — defense decrease (debuff on target)
        SelfBuff,    // 17 — buff applied to caster (distinct from Buff = on target)
        Vulnerable,  // 18 — vulnerability mark (damage taken increase)
        // Defense
        ShieldBreak, // 19 — anti-shield damage path
        // Healing / regen
        Cleanse,     // 20 — status removal (distinct from Heal = HP restoration)
        Regen,       // 21 — sustained HP regeneration
        AntiHeal,    // 22 — healing reduction debuff
        // Crowd control
        CC,          // 23 — generic crowd control
        Silence,     // 24 — skill / cast prevention
        Buff,        // 25 — beneficial effect on target (distinct from SelfBuff)
        // Misc
        Execute,     // 26 — high damage versus low-HP targets
        Stealth,     // 27 — invisibility / detection avoidance
        Survival,    // 28 — low-HP survival trigger / revive (e.g. SurvivalPulse)

        // ── X4-7 boss skill tags (29+) ─────────────────────────
        Boss,        // 29 — skill usable by boss entities (SkillRegistry filter key)
    }
}
