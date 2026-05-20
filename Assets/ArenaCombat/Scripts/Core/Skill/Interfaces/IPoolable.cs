// Minimal pool lifecycle hooks. Implemented by SkillProjectile / SkillArea.

namespace ArenaCombat.Core.Skill
{
    public interface IPoolable
    {
        void OnSpawn();
        void OnDespawn();
    }
}
