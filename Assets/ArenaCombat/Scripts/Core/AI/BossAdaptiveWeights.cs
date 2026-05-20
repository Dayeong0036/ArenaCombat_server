using UnityEngine;
using ArenaCombat.Core.Skill;

namespace ArenaCombat.Core.AI
{
    [DisallowMultipleComponent]
    public class BossAdaptiveWeights : MonoBehaviour
    {
        public static BossAdaptiveWeights Instance { get; private set; }

        [SerializeField] float _baseWeight = 1f;
        [SerializeField] float _biasMultiplier = 2f;

        static readonly SkillRoleTag[] BiasResponseMap = new SkillRoleTag[]
        {
            SkillRoleTag.Ranged,
            SkillRoleTag.Melee,
            SkillRoleTag.Shield,
            SkillRoleTag.Burst,
            SkillRoleTag.AOE,
            SkillRoleTag.Zone,
            SkillRoleTag.Counter,
            SkillRoleTag.AOE,
            SkillRoleTag.Mark,
        };

        void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(this); return; }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public float ComputeWeight(SkillDefinition skill)
        {
            float weight = _baseWeight;
            if (skill.RoleTags == null || skill.RoleTags.Length == 0)
                return weight;

            if (PlayerBiasTracker.Instance == null)
                return weight;

            float[] avgBias = PlayerBiasTracker.Instance.GetAverageBiases();
            if (avgBias == null) return weight;

            for (int b = 0; b < BiasResponseMap.Length && b < avgBias.Length; b++)
            {
                if (avgBias[b] <= 0f) continue;
                if (System.Array.Exists(skill.RoleTags, t => t == BiasResponseMap[b]))
                    weight += avgBias[b] * _biasMultiplier;
            }

            return weight;
        }
    }
}
