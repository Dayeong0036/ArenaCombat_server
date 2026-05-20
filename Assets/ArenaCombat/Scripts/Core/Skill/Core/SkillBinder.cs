using UnityEngine;

// One-shot bootstrap that injects SkillLibrary recipes into SkillDefinition.RuntimeStep.
// Call SkillBinder.BindAll(registry) once at game start.
//
// SkillDefinition.RuntimeStep is [NonSerialized], so domain reload / scene change
// requires re-injection. X3 wiring decides the exact call site.

namespace ArenaCombat.Core.Skill
{
    public static class SkillBinder
    {
        // ── Full bind pass ──────────────────────────────────────
        public static void BindAll(SkillRegistry registry)
        {
            if (registry == null)
            {
                Debug.LogWarning("[SkillBinder] SkillRegistry is null");
                return;
            }

            int bound = 0;

            // ── Player common ───────────────────────────────────
            bound += Bind(registry, "ExecutionSpike",     SkillLibrary.ExecutionSpike());
            bound += Bind(registry, "CrushingBarrage",    SkillLibrary.CrushingBarrage());
            bound += Bind(registry, "ErosionField",       SkillLibrary.ErosionField());
            bound += Bind(registry, "HuntingMark",        SkillLibrary.HuntingMark());
            bound += Bind(registry, "SurvivalPulse",      SkillLibrary.SurvivalPulse(),      SkillLibrary.SurvivalPulseCondition());
            bound += Bind(registry, "FortressArmor",      SkillLibrary.FortressArmor());
            bound += Bind(registry, "SealChain",          SkillLibrary.SealChain());
            bound += Bind(registry, "CollapseRoar",       SkillLibrary.CollapseRoar());
            bound += Bind(registry, "BarrierBreaker",     SkillLibrary.BarrierBreaker());
            bound += Bind(registry, "OverchargeMode",     SkillLibrary.OverchargeMode(),     SkillLibrary.OverchargeModeCondition());
            bound += Bind(registry, "PiercingShot",       SkillLibrary.PiercingShot());
            bound += Bind(registry, "RuptureMagazine",    SkillLibrary.RuptureMagazine());

            // ── Player only ─────────────────────────────────────
            bound += Bind(registry, "CounterStance",      SkillLibrary.CounterStance());
            bound += Bind(registry, "ParryEnhance",       SkillLibrary.ParryEnhance());
            bound += Bind(registry, "CounterSlash",       SkillLibrary.CounterSlash());
            bound += Bind(registry, "WireHook",           SkillLibrary.WireHook());
            bound += Bind(registry, "RopeShockwave",      SkillLibrary.RopeShockwave());
            bound += Bind(registry, "CollapseStrike",     SkillLibrary.CollapseStrike());

            // ── Boss common ─────────────────────────────────────
            bound += Bind(registry, "ExecutionSpike_Boss",    SkillLibrary.ExecutionSpike_Boss());
            bound += Bind(registry, "CrushingBarrage_Boss",   SkillLibrary.CrushingBarrage_Boss());
            bound += Bind(registry, "ErosionField_Boss",      SkillLibrary.ErosionField_Boss());
            bound += Bind(registry, "SurvivalPulse_Boss",     SkillLibrary.SurvivalPulse_Boss(),     SkillLibrary.SurvivalPulseCondition_Boss());
            bound += Bind(registry, "FortressArmor_Boss",     SkillLibrary.FortressArmor_Boss());
            bound += Bind(registry, "CollapseRoar_Boss",      SkillLibrary.CollapseRoar_Boss());
            bound += Bind(registry, "OverchargeMode_Boss",    SkillLibrary.OverchargeMode_Boss(),    SkillLibrary.OverchargeModeCondition_Boss());
            bound += Bind(registry, "MarkWave_Boss",          SkillLibrary.MarkWave_Boss());
            bound += Bind(registry, "SealChain_Boss",         SkillLibrary.SealChain_Boss());
            bound += Bind(registry, "BarrierBreaker_Boss",    SkillLibrary.BarrierBreaker_Boss());
            bound += Bind(registry, "RuptureMagazine_Boss",   SkillLibrary.RuptureMagazine_Boss());

            Debug.Log($"[SkillBinder] {bound} skills bound");
        }

        // ── Single bind ─────────────────────────────────────────
        // Returns 1 on success, 0 on skip. SO missing or step == null => 0.
        private static int Bind(SkillRegistry registry, string skillId, SkillStep step,
                                SkillCondition condition = null)
        {
            var def = registry.Get(skillId);
            if (def == null) return 0;   // SO not yet created — ignore

            def.RuntimeStep      = step;
            def.RuntimeCondition = condition;
            return step != null ? 1 : 0;
        }
    }
}
