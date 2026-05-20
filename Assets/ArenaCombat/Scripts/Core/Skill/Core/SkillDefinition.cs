using System;
using UnityEngine;

// Single-skill metadata + runtime composite tree.
//
// Serialized portion (SO fields): draft UI, boss AI counter-pick, SkillRegistry lookup.
// Runtime portion ([NonSerialized]): SkillStep + SkillCondition delegates assembled by
// SkillLibrary (X2-7) and injected by SkillBinder (X2-7) at game start. Domain reload /
// scene change requires re-injection (delegates are not serializable).

namespace ArenaCombat.Core.Skill
{
    public enum SkillCategoryFlag : byte
    {
        None        = 0,
        Directional = 1,
        AoE         = 2,
        Projectile  = 3,
        Self        = 4,
    }

    [CreateAssetMenu(menuName = "ArenaCombat/SkillDefinition", fileName = "NewSkill")]
    public class SkillDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string SkillId;
        public string DisplayName;
        [TextArea] public string Description;
        public Sprite Icon;

        [Header("Combat")]
        public float      Cooldown;
        [Min(0f)] public float TelegraphDuration;
        public float      Range;
        public TargetType TargetType;

        [Header("AI / Draft")]
        // What this skill IS (Burst / DOT / Shield / ...).
        public SkillRoleTag[] RoleTags;
        // What this skill is good AGAINST — used by boss AI counter-pick.
        public SkillRoleTag[] CounterTags;

        [Header("AI Observation Hints")]
        public float AIHint_ConeOrAoE;
        public SkillCategoryFlag AIHint_Category;

        // Runtime composite tree — not serializable; SkillLibrary builds + SkillBinder injects.
        [NonSerialized] public SkillStep RuntimeStep;

        // Pre-execute condition. Null = always run. Returning false skips execute
        // without consuming cooldown.
        [NonSerialized] public SkillCondition RuntimeCondition;

        public bool IsReady => RuntimeStep != null;
    }
}
