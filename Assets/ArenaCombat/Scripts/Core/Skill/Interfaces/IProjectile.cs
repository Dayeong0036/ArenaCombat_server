using UnityEngine;

// Skill projectile contract. Pooled (IPoolable) + launchable + hit-callback-driven.
//
// SkillProjectile implements this. ProjectilePool returns / accepts SkillProjectile
// instances. LaunchProjectile (#32 SkillComponent) calls Launch + SetHitCallback.

namespace ArenaCombat.Core.Skill
{
    public interface IProjectile : IPoolable
    {
        void Launch(Vector3 direction, float speed, float range);
        void SetHitCallback(SkillStep onHit, SkillContext ctx, bool pierce);
    }
}
