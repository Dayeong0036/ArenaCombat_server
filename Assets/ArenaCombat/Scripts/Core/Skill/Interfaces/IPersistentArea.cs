using UnityEngine;

// Persistent AoE contract. Pooled (IPoolable) + initializable with shape /
// duration / tickInterval. SkillArea implements this. SpawnPersistentArea
// (#31 SkillComponent, X2-9) calls Manager.Spawn -> Pool.Get -> Initialize.

namespace ArenaCombat.Core.Skill
{
    public interface IPersistentArea : IPoolable
    {
        void Initialize(Vector3 forward, float radius, AreaShape shape, float angleDeg,
                        float duration, float tickInterval, SkillStep tickEffect, SkillContext ctx);
    }
}
