using UnityEngine;
using ArenaCombat.Core.Skill;

// Card draft asset. References a SkillDefinition that gets equipped to the
// player's SkillManager when this card is selected. skillObjectName is a
// legacy fallback for pre-SkillDefinition card SOs (transform-find unlock).

namespace ArenaCombat.Core.Card
{
    [CreateAssetMenu(fileName = "AbilityCard", menuName = "ArenaCombat/AbilityCard")]
    public class AbilityCard : ScriptableObject
    {
        [Header("Card info")]
        public string cardName;
        public Sprite cardIcon;
        [TextArea] public string description;

        [Header("Skill link")]
        public SkillDefinition skillDefinition;

        [Header("Legacy (backward compat)")]
        public string skillObjectName;
    }
}
