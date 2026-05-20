using UnityEngine;
using ArenaCombat.Core.Combat;

// 37 skill parts.
// Consumers (X2-10 SkillLibrary) write: `using static ArenaCombat.Core.Skill.SkillComponents;`
// then reference parts by name (e.g., DealDamage(50f)).
//
// SkillStep      return : execution part (Primitive / Wrapper)
// SkillCondition return : condition part — passed as `condition` to TriggerOnCondition (#33)
//
// ══════════════════════════════════════════════════════════════════
//  #   Function                Category         Target
// ──────────────────────────────────────────────────────────────────
//  1   DealDamage              Combat           target
//  2   DealMultiHitDamage      Combat           target
//  3   ApplyDamageOverTime     Combat           target
//  4   RecoverHP               Survival         caster
//  5   ApplyHPRegen            Survival         caster
//  6   GainShield              Survival         caster
//  7   ReflectDamage           Survival         caster
//  8   ApplyInvulnerability    Survival         caster
//  9   ApplyStun               Status           target
// 10   ApplyHitStun            Status           target
// 11   ApplySlow               Status           target
// 12   ApplyRoot               Status           target
// 13   ApplyVulnerability      Status           target
// 14   ApplyDamageUp           Buff/Debuff      caster
// 15   ApplyDamageDown         Buff/Debuff      target
// 16   ApplyDefenseUp          Buff/Debuff      caster
// 17   ApplyDefenseDown        Buff/Debuff      target
// 18   ApplyKnockback          Position         target
// 19   PullTarget              Position         target
// 20   ApplyInArea             Area wrapper     all in radius
// 21   ApplyBuff               Buff/Debuff      caster
// 22   ApplyDebuff             Buff/Debuff      target
// 23   CheckParry              Core (parry)     caster
// 24   ApplyParryReward        Core (parry)     caster
// 25   ApplySilence            Status           target
// 26   CleanseStatus           Cleanse          caster
// 27   ApplyAntiHeal           Status           target
// 28   DealShieldBreakDamage   Defense          target
// 29   ExecuteBelowHP          Execute          target
// 30   DispelBuff              Cleanse          target
// 31   SpawnPersistentArea     Area wrapper     ctx.CastPosition
// 32   LaunchProjectile        Projectile       ctx.CastDirection
// 33   TriggerOnCondition      Control flow     —
// 34   TriggerOnHit            Control flow     —
// 35   DealDirectionalHit      Combat           front cone
// 36   MoveSelf                Position         caster
// 37   CheckTargetDistance     Condition        —
// ══════════════════════════════════════════════════════════════════

namespace ArenaCombat.Core.Skill
{
    public static class SkillComponents
    {
        // ══════════════════════════════════════════════════════════════
        // Combat
        // ══════════════════════════════════════════════════════════════

        // ── 1. Single damage ─────────────────────────────────────────
        public static SkillStep DealDamage(float amount) =>
            ctx =>
            {
                ctx.PrimaryTarget?.TakeDamage(amount * ctx.DamageScale, ctx.Caster);
                ctx.AddLog($"DealDamage({amount})");
            };

        // ── 2. Multi-hit ─────────────────────────────────────────────
        public static SkillStep DealMultiHitDamage(float amount, int hits) =>
            ctx =>
            {
                if (ctx.PrimaryTarget == null) return;
                float scaled = amount * ctx.DamageScale;
                for (int i = 0; i < hits; i++)
                    ctx.PrimaryTarget.TakeDamage(scaled, ctx.Caster);
                ctx.AddLog($"DealMultiHitDamage({amount}x{hits})");
            };

        // ── 3. Damage over time ──────────────────────────────────────
        public static SkillStep ApplyDamageOverTime(float duration, float dps) =>
            ctx =>
            {
                ctx.PrimaryTarget?.ApplyStatus(StatusType.DamageOverTime, duration, dps * ctx.DamageScale);
                ctx.AddLog($"ApplyDamageOverTime({duration}s,{dps}/s)");
            };

        // ── 35. Directional hit — all ICombatant in front cone, records ctx.HitLanded ──
        public static SkillStep DealDirectionalHit(float damage, float range, float angleDeg, int layerMask = -1) =>
            ctx =>
            {
                if (ctx.Caster == null) return;

                Vector3 origin  = ctx.Caster.Transform.position;
                Vector3 forward = ctx.CastDirection != Vector3.zero
                    ? ctx.CastDirection.normalized
                    : ctx.Caster.Transform.forward;

                var  saved     = ctx.PrimaryTarget;
                bool anyHit    = false;
                var  processed = new System.Collections.Generic.HashSet<ICombatant>();

                foreach (var col in Physics.OverlapSphere(origin, range, layerMask))
                {
                    var target = col.GetComponentInParent<ICombatant>();
                    if (target == null || ReferenceEquals(target, ctx.Caster)) continue;
                    if (!processed.Add(target)) continue;
                    if (Vector3.Angle(forward, col.transform.position - origin) > angleDeg * 0.5f) continue;

                    ctx.PrimaryTarget = target;
                    ctx.HitLanded     = true;
                    ctx.RefreshSnapshot();
                    target.TakeDamage(damage * ctx.DamageScale, ctx.Caster);
                    anyHit = true;
                }

                if (!anyHit) { ctx.PrimaryTarget = saved; ctx.HitLanded = false; }
                else         ctx.OnHitRecorded?.Invoke();
                SkillRangeDisplay.Instance?.ShowCone(origin, forward, range, angleDeg, anyHit);
                ctx.AddLog($"DealDirectionalHit(dmg:{damage},r:{range},a:{angleDeg})=>{(anyHit ? "HIT" : "MISS")}");
            };

        // ══════════════════════════════════════════════════════════════
        // Survival
        // ══════════════════════════════════════════════════════════════

        // ── 4. Recover HP ────────────────────────────────────────────
        public static SkillStep RecoverHP(float amount) =>
            ctx =>
            {
                ctx.Caster?.RecoverHP(amount);
                ctx.AddLog($"RecoverHP({amount})");
            };

        // ── 5. HP regen over time ────────────────────────────────────
        public static SkillStep ApplyHPRegen(float duration, float perSecond) =>
            ctx =>
            {
                ctx.Caster?.ApplyStatus(StatusType.HPRegen, duration, perSecond);
                ctx.AddLog($"ApplyHPRegen({duration}s,{perSecond}/s)");
            };

        // ── 6. Gain shield ───────────────────────────────────────────
        public static SkillStep GainShield(float amount) =>
            ctx =>
            {
                ctx.Caster?.AddShield(amount);
                ctx.AddLog($"GainShield({amount})");
            };

        // ── 7. Damage reflect ────────────────────────────────────────
        // Applies Reflecting status; PlayerController.TakeDamage handles reflection routing.
        public static SkillStep ReflectDamage(float duration, float ratio) =>
            ctx =>
            {
                ctx.Caster?.ApplyStatus(StatusType.Reflecting, duration, ratio);
                ctx.AddLog($"ReflectDamage({duration}s,{ratio * 100f:F0}%)");
            };

        // ── 8. Invulnerable ──────────────────────────────────────────
        public static SkillStep ApplyInvulnerability(float duration) =>
            ctx =>
            {
                ctx.Caster?.ApplyStatus(StatusType.Invulnerable, duration);
                ctx.AddLog($"ApplyInvulnerability({duration}s)");
            };

        // ══════════════════════════════════════════════════════════════
        // Status effects
        // ══════════════════════════════════════════════════════════════

        // ── 9. Stun ──────────────────────────────────────────────────
        public static SkillStep ApplyStun(float duration) =>
            ctx =>
            {
                ctx.PrimaryTarget?.ApplyStatus(StatusType.Stunned, duration);
                ctx.AddLog($"ApplyStun({duration}s)");
            };

        // ── 10. HitStun ──────────────────────────────────────────────
        public static SkillStep ApplyHitStun(float duration) =>
            ctx =>
            {
                ctx.PrimaryTarget?.ApplyStatus(StatusType.HitStun, duration);
                ctx.AddLog($"ApplyHitStun({duration}s)");
            };

        // ── 11. Slow ─ ratio: 0..1 (0.3 = 30% reduction) ─────────────
        public static SkillStep ApplySlow(float duration, float ratio) =>
            ctx =>
            {
                ctx.PrimaryTarget?.ApplyStatus(StatusType.Slowed, duration, ratio);
                ctx.AddLog($"ApplySlow({duration}s,{ratio * 100f:F0}%)");
            };

        // ── 12. Root ─────────────────────────────────────────────────
        public static SkillStep ApplyRoot(float duration) =>
            ctx =>
            {
                ctx.PrimaryTarget?.ApplyStatus(StatusType.Rooted, duration);
                ctx.AddLog($"ApplyRoot({duration}s)");
            };

        // ── 13. Vulnerable ─ ratio: extra damage taken (0.2 = +20%) ──
        public static SkillStep ApplyVulnerability(float duration, float ratio) =>
            ctx =>
            {
                ctx.PrimaryTarget?.ApplyStatus(StatusType.Vulnerable, duration, ratio);
                ctx.AddLog($"ApplyVulnerability({duration}s,+{ratio * 100f:F0}%)");
            };

        // ── 25. Silence ──────────────────────────────────────────────
        public static SkillStep ApplySilence(float duration) =>
            ctx =>
            {
                ctx.PrimaryTarget?.ApplyStatus(StatusType.Silence, duration);
                ctx.AddLog($"ApplySilence({duration}s)");
            };

        // ── 27. AntiHeal ─ ratio: 0..1 (0.5 = 50% healing reduction) ─
        public static SkillStep ApplyAntiHeal(float duration, float ratio) =>
            ctx =>
            {
                ctx.PrimaryTarget?.ApplyStatus(StatusType.AntiHeal, duration, ratio);
                ctx.AddLog($"ApplyAntiHeal({duration}s,{ratio * 100f:F0}%)");
            };

        // ══════════════════════════════════════════════════════════════
        // Buff / Debuff
        // ══════════════════════════════════════════════════════════════

        // ── 14. Damage up ────────────────────────────────────────────
        public static SkillStep ApplyDamageUp(float duration, float ratio) =>
            ctx =>
            {
                ctx.Caster?.ApplyBuff(BuffType.DamageUp, duration, ratio);
                ctx.AddLog($"ApplyDamageUp({duration}s,+{ratio}%)");
            };

        // ── 15. Damage down ──────────────────────────────────────────
        public static SkillStep ApplyDamageDown(float duration, float ratio) =>
            ctx =>
            {
                ctx.PrimaryTarget?.ApplyDebuff(DebuffType.DamageDown, duration, ratio);
                ctx.AddLog($"ApplyDamageDown({duration}s,{ratio}%)");
            };

        // ── 16. Defense up ───────────────────────────────────────────
        public static SkillStep ApplyDefenseUp(float duration, float ratio) =>
            ctx =>
            {
                ctx.Caster?.ApplyBuff(BuffType.DefenseUp, duration, ratio);
                ctx.AddLog($"ApplyDefenseUp({duration}s,+{ratio}%)");
            };

        // ── 17. Defense down ─────────────────────────────────────────
        public static SkillStep ApplyDefenseDown(float duration, float ratio) =>
            ctx =>
            {
                ctx.PrimaryTarget?.ApplyDebuff(DebuffType.DefenseDown, duration, ratio);
                ctx.AddLog($"ApplyDefenseDown({duration}s,{ratio}%)");
            };

        // ── 21. ApplyBuff (generic) ──────────────────────────────────
        public static SkillStep ApplyBuff(float duration, BuffType type, float value) =>
            ctx =>
            {
                ctx.Caster?.ApplyBuff(type, duration, value);
                ctx.AddLog($"ApplyBuff({type},{duration}s,{value})");
            };

        // ── 22. ApplyDebuff (generic) ────────────────────────────────
        public static SkillStep ApplyDebuff(float duration, DebuffType type, float value) =>
            ctx =>
            {
                ctx.PrimaryTarget?.ApplyDebuff(type, duration, value);
                ctx.AddLog($"ApplyDebuff({type},{duration}s,{value})");
            };

        // ══════════════════════════════════════════════════════════════
        // Position control
        // ══════════════════════════════════════════════════════════════

        // ── 18. Knockback ─ pushes target along caster->target direction ──
        public static SkillStep ApplyKnockback(float distance) =>
            ctx =>
            {
                if (ctx.PrimaryTarget == null || ctx.Caster == null) return;
                Vector3 dir = (ctx.PrimaryTarget.Transform.position - ctx.Caster.Transform.position).normalized;
                ctx.PrimaryTarget.Knockback(dir, distance);
                ctx.AddLog($"ApplyKnockback({distance}m)");
            };

        // ── 19. Pull ─ drags target toward caster ────────────────────
        public static SkillStep PullTarget(float distance, float duration) =>
            ctx =>
            {
                if (ctx.PrimaryTarget == null || ctx.Caster == null) return;
                ctx.PrimaryTarget.Pull(ctx.Caster.Transform.position, distance, duration);
                ctx.AddLog($"PullTarget({distance}m,{duration}s)");
            };

        // ── 36. MoveSelf ─ caster moves along ctx.CastDirection ──────
        public static SkillStep MoveSelf(float distance, float duration, MoveType moveType) =>
            ctx =>
            {
                if (ctx.Caster == null) return;
                ctx.Caster.MoveBy(ctx.CastDirection, distance, duration, moveType);
                ctx.AddLog($"MoveSelf({moveType},{distance}m,{duration}s)");
            };

        // ══════════════════════════════════════════════════════════════
        // Defense / Execute / Cleanse
        // ══════════════════════════════════════════════════════════════

        // ── 28. ShieldBreak damage — shield-priority + overflow to HP ─
        public static SkillStep DealShieldBreakDamage(float amount, float multiplier) =>
            ctx =>
            {
                ctx.PrimaryTarget?.TakeShieldBreakDamage(amount * ctx.DamageScale, multiplier, ctx.Caster);
                ctx.AddLog($"DealShieldBreakDamage({amount},x{multiplier})");
            };

        // ── 29. ExecuteBelowHP — bonus damage when target HP% < threshold ──
        public static SkillStep ExecuteBelowHP(float hpThreshold, float bonusDamage) =>
            ctx =>
            {
                if (ctx.PrimaryTarget == null) return;
                if (ctx.TargetHpPercent * 100f < hpThreshold)
                {
                    ctx.PrimaryTarget.TakeDamage(bonusDamage * ctx.DamageScale, ctx.Caster);
                    ctx.AddLog($"ExecuteBelowHP(<{hpThreshold}%,+{bonusDamage})");
                }
            };

        // ── 26. Cleanse caster statuses (count=0 = all) ──────────────
        public static SkillStep CleanseStatus(CleanseType type, int count) =>
            ctx =>
            {
                ctx.Caster?.RemoveStatuses(type, count);
                ctx.AddLog($"CleanseStatus({type},{count})");
            };

        // ── 30. Dispel target buffs (count=0 = all) ──────────────────
        public static SkillStep DispelBuff(DispelType type, int count) =>
            ctx =>
            {
                ctx.PrimaryTarget?.RemoveBuffs(type, count);
                ctx.AddLog($"DispelBuff({type},{count})");
            };

        // ══════════════════════════════════════════════════════════════
        // Core — Parry
        // ══════════════════════════════════════════════════════════════

        // ── 23. CheckParry ───────────────────────────────────────────
        // Sets ctx.HitLanded = true when caster.IsParrying AND parry input
        // was within ParryWindow (ctx.ParryInputTime recorded by player input).
        public static SkillStep CheckParry() =>
            ctx =>
            {
                if (ctx.Caster == null) { ctx.HitLanded = false; return; }
                bool inWindow = (Time.time - ctx.ParryInputTime) <= ctx.Caster.ParryWindow;
                ctx.HitLanded = ctx.Caster.IsParrying && inWindow;
                ctx.AddLog($"CheckParry(window:{ctx.Caster.ParryWindow}s)=>{ctx.HitLanded}");
            };

        // ── 24. ApplyParryReward ─────────────────────────────────────
        // ctx.PrimaryTarget = the attacker that was parried. Counter / HitStun rewards
        // route through the attacker.
        public static SkillStep ApplyParryReward(ParryRewardType type, float value, float duration) =>
            ctx =>
            {
                ctx.Caster?.NotifyParryReward(type, value, duration, ctx.PrimaryTarget);
                ctx.AddLog($"ApplyParryReward({type},v:{value},d:{duration}s)");
            };

        // ══════════════════════════════════════════════════════════════
        // Area wrappers
        // ══════════════════════════════════════════════════════════════

        // ── 20. ApplyInArea ──────────────────────────────────────────
        // ctx.CastPosition centered radius sweep. Applies `inner` to each ICombatant in range.
        // angleDeg : Cone only — full cone angle (360 = circle equivalent).
        // lineWidth: Line only — forward strip half-width (m).
        public static SkillStep ApplyInArea(float radius, AreaShape shape, SkillStep inner,
                                            float angleDeg = 360f, float lineWidth = 0.5f, int layerMask = -1) =>
            ctx =>
            {
                var     saved     = ctx.PrimaryTarget;
                bool    anyHit    = false;
                var     processed = new System.Collections.Generic.HashSet<ICombatant>();
                Vector3 forward   = ctx.CastDirection != Vector3.zero
                    ? ctx.CastDirection.normalized
                    : Vector3.forward;

                foreach (var col in Physics.OverlapSphere(ctx.CastPosition, radius, layerMask))
                {
                    var target = col.GetComponentInParent<ICombatant>();
                    if (target == null || ReferenceEquals(target, ctx.Caster)) continue;
                    if (!processed.Add(target)) continue;

                    Vector3 toTarget = col.transform.position - ctx.CastPosition;

                    if (shape == AreaShape.Cone)
                    {
                        if (Vector3.Angle(forward, toTarget) > angleDeg * 0.5f) continue;
                    }
                    else if (shape == AreaShape.Line)
                    {
                        float forwardDot = Vector3.Dot(forward, toTarget);
                        if (forwardDot < 0f) continue;
                        Vector3 side = toTarget - forward * forwardDot;
                        if (side.magnitude > lineWidth) continue;
                    }

                    ctx.PrimaryTarget = target;
                    ctx.RefreshSnapshot();
                    inner?.Invoke(ctx);
                    anyHit = true;
                }

                ctx.HitLanded     = anyHit;
                if (anyHit) ctx.OnHitRecorded?.Invoke();
                ctx.PrimaryTarget = saved;
                switch (shape)
                {
                    case AreaShape.Cone:
                        SkillRangeDisplay.Instance?.ShowCone(ctx.CastPosition, forward, radius, angleDeg, anyHit);
                        break;
                    case AreaShape.Line:
                        SkillRangeDisplay.Instance?.ShowLine(ctx.CastPosition, forward, radius);
                        break;
                    default:
                        SkillRangeDisplay.Instance?.ShowCircle(ctx.CastPosition, radius, anyHit);
                        break;
                }
                ctx.AddLog($"ApplyInArea(r:{radius},{shape},angle:{angleDeg},width:{lineWidth})=>{(anyHit ? "HIT" : "MISS")}");
            };

        // ── 31. SpawnPersistentArea ──────────────────────────────────
        // Spawns an AoE at ctx.CastPosition; tickInterval ticks apply tickAction.
        // Scene requires PersistentAreaManager + PersistentAreaPool.
        public static SkillStep SpawnPersistentArea(float duration, float radius, AreaShape shape,
                                                    float tickInterval, SkillStep tickAction,
                                                    float angleDeg = 360f) =>
            ctx =>
            {
                if (PersistentAreaManager.Instance == null)
                {
                    Debug.LogWarning("[SpawnPersistentArea] PersistentAreaManager not in scene");
                    return;
                }

                Vector3 pos     = ctx.CastPosition  != Vector3.zero ? ctx.CastPosition            : ctx.Caster?.Transform.position ?? Vector3.zero;
                Vector3 forward = ctx.CastDirection != Vector3.zero ? ctx.CastDirection.normalized : Vector3.forward;

                PersistentAreaManager.Instance.Spawn(pos, forward, radius, shape, angleDeg,
                                                     duration, tickInterval, tickAction, ctx);

                if (shape == AreaShape.Cone)
                    SkillRangeDisplay.Instance?.ShowCone(pos, forward, radius, angleDeg, true);
                else
                    SkillRangeDisplay.Instance?.ShowCircle(pos, radius, true);

                ctx.AddLog($"SpawnPersistentArea(r:{radius},{shape},{angleDeg}deg,{duration}s,tick:{tickInterval}s)");
            };

        // ══════════════════════════════════════════════════════════════
        // Projectile wrapper
        // ══════════════════════════════════════════════════════════════

        // ── 32. LaunchProjectile ─────────────────────────────────────
        // Fires a projectile along ctx.CastDirection; onImpact runs on hit.
        // pierce=true: keep flying through after first hit.
        // Scene requires ProjectilePool.
        public static SkillStep LaunchProjectile(float speed, float range, bool pierce, SkillStep onImpact) =>
            ctx =>
            {
                if (ctx.Caster == null) return;
                if (ProjectilePool.Instance == null)
                {
                    Debug.LogWarning("[LaunchProjectile] ProjectilePool not in scene");
                    return;
                }

                Vector3 origin = ctx.CastPosition  != Vector3.zero ? ctx.CastPosition            : ctx.Caster.Transform.position;
                Vector3 dir    = ctx.CastDirection  != Vector3.zero ? ctx.CastDirection.normalized : ctx.Caster.Transform.forward;

                var proj = ProjectilePool.Instance.Get(origin, Quaternion.LookRotation(dir));
                if (proj == null) return;   // X3-5b: pool returned null (client context / NetworkObject error). Pool already logged.
                proj.SetHitCallback(onImpact, ctx, pierce);
                proj.Launch(dir, speed, range);

                SkillRangeDisplay.Instance?.ShowLine(origin, dir, range);
                ctx.AddLog($"LaunchProjectile(spd:{speed},r:{range},pierce:{pierce})");
            };

        // ══════════════════════════════════════════════════════════════
        // Control flow
        // ══════════════════════════════════════════════════════════════

        // ── 33. TriggerOnCondition — branches on condition result ────
        public static SkillStep TriggerOnCondition(string name, SkillCondition condition,
                                                   SkillStep onTrue, SkillStep onFalse = null) =>
            ctx =>
            {
                bool result = condition?.Invoke(ctx) ?? false;
                ctx.AddLog($"[Cond:{name}]=>{result}");
                (result ? onTrue : onFalse)?.Invoke(ctx);
            };

        // ── 34. TriggerOnHit — branches on ctx.HitLanded ─────────────
        // ctx.HitLanded is set by DealDirectionalHit (#35), CheckParry (#23),
        // LaunchProjectile (#32) onImpact, ApplyInArea (#20).
        public static SkillStep TriggerOnHit(SkillStep onHit = null, SkillStep onMiss = null) =>
            ctx =>
            {
                ctx.AddLog($"[TriggerOnHit] HitLanded={ctx.HitLanded}");
                (ctx.HitLanded ? onHit : onMiss)?.Invoke(ctx);
            };

        // ══════════════════════════════════════════════════════════════
        // Conditions (SkillCondition)
        // ══════════════════════════════════════════════════════════════

        // ── 37. CheckTargetDistance — true when distance in [min, max] ──
        public static SkillCondition CheckTargetDistance(float minDistance, float maxDistance) =>
            ctx => ctx.TargetDistance >= minDistance && ctx.TargetDistance <= maxDistance;
    }
}
