using static ArenaCombat.Core.Skill.SkillComponents;

// Skill recipe code. Composes SkillStep / SkillCondition trees that get injected
// into SkillDefinition.RuntimeStep / RuntimeCondition by SkillBinder.
//
// Usage:
//   skillDef.RuntimeStep = SkillLibrary.ErosionField_Boss();
//
// Notes:
//   * LaunchProjectile : design spec (range, speed) -> code signature (speed, range)
//   * homing flag      : in design spec, not in code signature — ignored
//   * Ratio (%) params : ApplyVulnerability / AntiHeal / Slow etc. use 0..1 decimal (5 -> 0.05f)
//   * Integer % params : ApplyDamageUp / DefenseDown etc. pass as-is (15 -> 15f)
//   * "X% of MaxHP"    : computed at runtime from ctx.Caster.MaxHP

namespace ArenaCombat.Core.Skill
{
    public static class SkillLibrary
    {
        // ══════════════════════════════════════════════════════════════
        // ───────────────  Player common (12)  ───────────────
        // ══════════════════════════════════════════════════════════════

        // ExecutionSpike — BAL-1 T2: cone 30→32, wide retry 제거, dmg 125→100.
        public static SkillStep ExecutionSpike() =>
            ctx =>
            {
                DealDirectionalHit(100f, 15f, 32f).Invoke(ctx);
                TriggerOnHit(
                    onHit: hit =>
                    {
                        ApplyVulnerability(2f, 0.05f).Invoke(hit);
                        ExecuteBelowHP(30f, 20f).Invoke(hit);
                    }
                ).Invoke(ctx);
            };

        // CrushingBarrage — BAL-1 T2: cone 35→38, wide retry 제거, dmg 34→28 (per hit, ×4).
        public static SkillStep CrushingBarrage() =>
            ctx =>
            {
                DealDirectionalHit(0f, 13.6f, 38f).Invoke(ctx);
                TriggerOnHit(
                    onHit: hit =>
                    {
                        DealMultiHitDamage(28f, 4).Invoke(hit);
                        DealShieldBreakDamage(45f, 1.2f).Invoke(hit);
                    }
                ).Invoke(ctx);
            };

        // ErosionField (player) — BAL-1 T2: projectile 19→28 m/s, inner AoE 7.6→9.
        public static SkillStep ErosionField() =>
            LaunchProjectile(28f, 42f, false,
                impact =>
                {
                    SpawnPersistentArea(4f, 9f, AreaShape.Circle, 1f,
                        tick =>
                        {
                            ApplyDamageOverTime(1f, 7f).Invoke(tick);
                            ApplyAntiHeal(1f, 0.20f).Invoke(tick);
                        }
                    ).Invoke(impact);

                    SpawnPersistentArea(4f, 20f, AreaShape.Circle, 1f,
                        tick => ApplyDamageOverTime(1f, 7f).Invoke(tick)
                    ).Invoke(impact);
                }
            );

        // HuntingMark — BAL-1 T2: projectile 22→35 m/s, dmg 68→60.
        public static SkillStep HuntingMark() =>
            LaunchProjectile(35f, 48f, false,
                hit =>
                {
                    DealDamage(60f).Invoke(hit);
                    ApplyDebuff(4f, DebuffType.Mark, 1f).Invoke(hit);
                }
            );

        // SurvivalPulse — extracted condition for SkillBinder coupling.
        public static SkillCondition SurvivalPulseCondition() =>
            ctx => ctx.Caster != null && ctx.Caster.CurrentHPPercent < 0.3f;

        // SurvivalPulse — low HP: recover + regen + cleanse.
        public static SkillStep SurvivalPulse() =>
            TriggerOnCondition("LowHP",
                cond => cond.Caster != null && cond.Caster.CurrentHPPercent < 0.3f,
                onTrue: ctx =>
                {
                    float maxHp = ctx.Caster?.MaxHP ?? 0f;
                    RecoverHP(maxHp * 0.06f).Invoke(ctx);
                    ApplyHPRegen(3f, maxHp * 0.012f).Invoke(ctx);
                    CleanseStatus(CleanseType.All, 1).Invoke(ctx);
                }
            );

        // FortressArmor — BAL-1 T2: cone 35→38, wide retry 제거, dmg 90→75.
        public static SkillStep FortressArmor() =>
            ctx =>
            {
                DealDirectionalHit(75f, 14.4f, 38f).Invoke(ctx);
                TriggerOnHit(
                    onHit: hit =>
                    {
                        float maxHp = hit.Caster?.MaxHP ?? 0f;
                        GainShield(maxHp * 0.06f).Invoke(hit);
                    }
                ).Invoke(ctx);
            };

        // SealChain — BAL-1 T2: projectile 20→30 m/s, dmg 60→50.
        public static SkillStep SealChain() =>
            LaunchProjectile(30f, 48f, false,
                hit =>
                {
                    DealDamage(50f).Invoke(hit);
                    ApplyHitStun(0.08f).Invoke(hit);
                    ApplySilence(0.5f).Invoke(hit);
                }
            );

        // CollapseRoar — BAL-1 T2: AoE 10.6→12, wide retry 제거, dmg 95→80.
        public static SkillStep CollapseRoar() =>
            ctx =>
            {
                ApplyInArea(12f, AreaShape.Circle,
                    inner =>
                    {
                        DealDamage(80f).Invoke(inner);
                        ApplyDefenseDown(2f, 5f).Invoke(inner);
                    }
                ).Invoke(ctx);
            };

        // BarrierBreaker — BAL-1 T2: projectile 19→30 m/s, dmg 105→88.
        public static SkillStep BarrierBreaker() =>
            LaunchProjectile(30f, 42f, false,
                hit =>
                {
                    DealShieldBreakDamage(60f, 1.4f).Invoke(hit);
                    ApplyDefenseDown(3f, 6f).Invoke(hit);
                    DealDamage(88f).Invoke(hit);
                }
            );

        // OverchargeMode — extracted condition.
        public static SkillCondition OverchargeModeCondition() =>
            ctx => ctx.Caster != null && ctx.Caster.CurrentHPPercent < 0.5f;

        // OverchargeMode — low HP: attack up + self-defense-down.
        public static SkillStep OverchargeMode() =>
            TriggerOnCondition("CombatPhaseOrLowHP",
                cond => cond.Caster != null && cond.Caster.CurrentHPPercent < 0.5f,
                onTrue: ctx =>
                {
                    ApplyDamageUp(6f, 15f).Invoke(ctx);
                    ApplyDebuff(6f, DebuffType.SelfDefenseDown, 10f).Invoke(ctx);
                }
            );

        // PiercingShot — BAL-1 T2: projectile 25→40 m/s, dmg 140→110.
        public static SkillStep PiercingShot() =>
            LaunchProjectile(40f, 60f, true,
                hit =>
                {
                    ApplyDefenseDown(3f, 8f).Invoke(hit);
                    DealDamage(110f).Invoke(hit);
                }
            );

        // RuptureMagazine — BAL-1 T2: projectile 20→30 m/s, dmg 115→95.
        public static SkillStep RuptureMagazine() =>
            LaunchProjectile(30f, 48f, false,
                hit =>
                {
                    ApplyVulnerability(3f, 0.10f).Invoke(hit);
                    DealDamage(95f).Invoke(hit);
                }
            );

        // ══════════════════════════════════════════════════════════════
        // ───────────────  Player only (6)  ───────────────
        // ══════════════════════════════════════════════════════════════

        // CounterStance — UNIMPLEMENTED.
        // Reason: StatManager.BeginParryWindow() caller (parry input binding) absent;
        //         IsParrying stays false; CheckParry returns HitLanded=false. Restore
        //         after parry input system lands.
        public static SkillStep CounterStance() => null;

        // ParryEnhance — parry window up + reward up buffs.
        public static SkillStep ParryEnhance() =>
            ctx =>
            {
                ApplyBuff(5f, BuffType.ParryWindowUp, 15f).Invoke(ctx);
                ApplyBuff(5f, BuffType.ParryRewardUp, 10f).Invoke(ctx);
            };

        // CounterSlash — UNIMPLEMENTED.
        // Reason: same as CounterStance — parry input binding absent.
        public static SkillStep CounterSlash() => null;

        // WireHook — UNIMPLEMENTED.
        // Reason: rope landing event system absent. MoveSelf works, but post-landing
        //         branching unavailable. Restore after rope event system lands.
        public static SkillStep WireHook() => null;

        // RopeShockwave — UNIMPLEMENTED.
        // Reason: same as WireHook — rope landing event absent.
        public static SkillStep RopeShockwave() => null;

        // CollapseStrike — UNIMPLEMENTED.
        // Reason: ParrySuccessRecent (recent parry success history) tracking absent;
        //         parry input binding also absent.
        public static SkillStep CollapseStrike() => null;

        // ══════════════════════════════════════════════════════════════
        // ───────────────  Boss common (11)  ───────────────
        // ══════════════════════════════════════════════════════════════

        // ExecutionSpike (boss) — BAL-1 T2: dmg 118→76.
        public static SkillStep ExecutionSpike_Boss() =>
            ctx =>
            {
                DealDirectionalHit(76f, 18f, 38f).Invoke(ctx);
                TriggerOnHit(
                    onHit: hit =>
                    {
                        ApplyVulnerability(2.5f, 0.06f).Invoke(hit);
                        ExecuteBelowHP(30f, 26f).Invoke(hit);
                    }
                ).Invoke(ctx);
            };

        // CrushingBarrage (boss) — BAL-1 T2: dmg 32→20 (per hit, ×4).
        public static SkillStep CrushingBarrage_Boss() =>
            ctx =>
            {
                DealDirectionalHit(0f, 15f, 40f).Invoke(ctx);
                TriggerOnHit(
                    onHit: hit =>
                    {
                        DealMultiHitDamage(20f, 4).Invoke(hit);
                        DealShieldBreakDamage(55f, 1.35f).Invoke(hit);
                    }
                ).Invoke(ctx);
            };

        // ErosionField (boss) — BAL-1 T2: DoT 7→8/s.
        public static SkillStep ErosionField_Boss() =>
            ctx =>
            {
                SpawnPersistentArea(4f, 8f, AreaShape.Circle, 1f,
                    tick =>
                    {
                        ApplyDamageOverTime(1f, 8f).Invoke(tick);
                        ApplyAntiHeal(1f, 0.20f).Invoke(tick);
                    }
                ).Invoke(ctx);

                SpawnPersistentArea(4f, 20f, AreaShape.Circle, 1f,
                    tick => ApplyDamageOverTime(1f, 8f).Invoke(tick)
                ).Invoke(ctx);
            };

        // SurvivalPulse (boss) — extracted condition.
        public static SkillCondition SurvivalPulseCondition_Boss() =>
            ctx => ctx.Caster != null && ctx.Caster.CurrentHPPercent < 0.3f;

        // SurvivalPulse (boss).
        public static SkillStep SurvivalPulse_Boss() =>
            TriggerOnCondition("LowHP",
                cond => cond.Caster != null && cond.Caster.CurrentHPPercent < 0.3f,
                onTrue: ctx =>
                {
                    float maxHp = ctx.Caster?.MaxHP ?? 0f;
                    RecoverHP(maxHp * 0.05f).Invoke(ctx);
                    ApplyHPRegen(3f, maxHp * 0.010f).Invoke(ctx);
                    CleanseStatus(CleanseType.All, 1).Invoke(ctx);
                }
            );

        // FortressArmor (boss) — BAL-1 T2: dmg 82→65.
        public static SkillStep FortressArmor_Boss() =>
            ctx =>
            {
                DealDirectionalHit(65f, 16f, 35f).Invoke(ctx);
                TriggerOnHit(
                    onHit: hit =>
                    {
                        float maxHp = hit.Caster?.MaxHP ?? 0f;
                        GainShield(maxHp * 0.08f).Invoke(hit);
                    }
                ).Invoke(ctx);
            };

        // CollapseRoar (boss) — BAL-1 T2: dmg 88→55 (×2 hits).
        public static SkillStep CollapseRoar_Boss() =>
            ctx =>
            {
                ApplyInArea(12f, AreaShape.Circle,
                    inner =>
                    {
                        DealDamage(55f).Invoke(inner);
                        DealDamage(55f).Invoke(inner);
                        ApplyHitStun(0.16f).Invoke(inner);
                        ApplyDefenseDown(3f, 6f).Invoke(inner);
                    }
                ).Invoke(ctx);
            };

        // OverchargeMode (boss) — extracted condition.
        public static SkillCondition OverchargeModeCondition_Boss() =>
            ctx => ctx.Caster != null && ctx.Caster.CurrentHPPercent < 0.5f;

        // OverchargeMode (boss).
        public static SkillStep OverchargeMode_Boss() =>
            TriggerOnCondition("CombatPhaseOrLowHP",
                cond => cond.Caster != null && cond.Caster.CurrentHPPercent < 0.5f,
                onTrue: ctx =>
                {
                    ApplyDamageUp(6f, 12f).Invoke(ctx);
                    ApplyDebuff(6f, DebuffType.SelfDefenseDown, 12f).Invoke(ctx);
                }
            );

        // MarkWave (boss) — BAL-1 T2: dmg 70→50.
        public static SkillStep MarkWave_Boss() =>
            ApplyInArea(22f, AreaShape.Cone,
                inner =>
                {
                    DealDamage(50f).Invoke(inner);
                    ApplyDebuff(4f, DebuffType.Mark, 1f).Invoke(inner);
                },
                angleDeg: 65f
            );

        // SealChain (boss) — UNIMPLEMENTED.
        // Reason: spec "4-direction ranged pattern" needs multi-direction projectile
        //         wrapper, not yet available. Single-projectile reduction would lose
        //         design intent (forced evasion). Restore after wrapper lands.
        public static SkillStep SealChain_Boss() => null;

        // BarrierBreaker (boss) — BAL-1 T2: dmg 90→62.
        public static SkillStep BarrierBreaker_Boss() =>
            LaunchProjectile(19f, 40f, true,
                hit =>
                {
                    DealDamage(62f).Invoke(hit);
                    DealShieldBreakDamage(70f, 1.5f).Invoke(hit);
                    ApplyDefenseDown(4f, 8f).Invoke(hit);
                }
            );

        // RuptureMagazine (boss) — UNIMPLEMENTED.
        // Reason: spec "8-direction explosion pattern" needs multi-direction wrapper.
        //         Single-projectile reduction would lose design intent.
        public static SkillStep RuptureMagazine_Boss() => null;
    }
}
