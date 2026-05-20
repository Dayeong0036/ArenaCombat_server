using System.Collections.Generic;
using UnityEngine;
using ArenaCombat.Core.Combat;

// Shared runtime data for one skill cast.
//
// Lifetime: per-cast `new` allocation (no pooling, per SKILL_SYSTEM_DESIGN.md §11.2).
// Caller (SkillExecutor / SkillManager, X2-6 / X2-12) populates Caster /
// PrimaryTarget / CastPosition / CastDirection before SkillExecutor.Execute(),
// then the composite tree mutates this object as it runs.
//
// All access is server-only (host-authoritative — see SKILL_SYSTEM_DESIGN.md §6).

namespace ArenaCombat.Core.Skill
{
    public class SkillContext
    {
        // ── Caster / target ────────────────────────────────────
        public ICombatant       Caster;
        public ICombatant       PrimaryTarget;
        public List<ICombatant> TargetList = new();

        // ── Position / direction ───────────────────────────────
        // CastPosition  : skill anchor (caster pos, projectile impact, etc. — wrappers update)
        // CastDirection : aim direction (player input or AI-computed)
        public Vector3 CastPosition;
        public Vector3 CastDirection;

        // ── Hit result ─────────────────────────────────────────
        // Set by DealDirectionalHit / CheckParry / LaunchProjectile.
        // Read by TriggerOnHit for branching.
        public bool HitLanded;

        // ── Hit-record dedup ───────────────────────────────────
        // ApplyInArea / DealDirectionalHit / SkillProjectile.ApplyHit invoke
        // OnHitRecorded; HitRecorded=true blocks repeat invocations within the cast.
        public System.Action OnHitRecorded;
        public bool HitRecorded;

        // ── Target snapshot (refreshed on RefreshSnapshot()) ──
        public float TargetHpPercent;  // 0..1
        public float TargetDistance;   // Caster <-> PrimaryTarget (m)
        public bool  TargetCasting;    // PrimaryTarget.IsCasting at snapshot

        // ── Parry input timestamp ─────────────────────────────
        // Set by player parry-trigger pipeline (X3 wiring).
        // CheckParry component compares against ParryWindow.
        public float ParryInputTime;

        // ── Caster damage scale (phase scaling, buffs) ─────────
        public float DamageScale = 1f;

        // ── Server clock at execute time ──────────────────────
        public float CurrentTime;

        // ── Execution log (debug) ─────────────────────────────
        private readonly List<string> _log = new();

        public void AddLog(string msg) =>
            _log.Add($"[{CurrentTime:F2}] {msg}");

        public IReadOnlyList<string> GetLog() => _log;

        // ── Snapshot refresh — call when PrimaryTarget changes ─
        public void RefreshSnapshot()
        {
            CurrentTime = Time.time;

            if (PrimaryTarget == null) return;

            TargetHpPercent = PrimaryTarget.CurrentHPPercent;
            TargetCasting   = PrimaryTarget.IsCasting;
            TargetDistance  = Caster != null
                ? Vector3.Distance(Caster.Transform.position, PrimaryTarget.Transform.position)
                : 0f;
        }
    }
}
