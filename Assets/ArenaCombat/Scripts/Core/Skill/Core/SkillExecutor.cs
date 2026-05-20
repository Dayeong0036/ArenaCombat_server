using System.Collections.Generic;
using UnityEngine;

// ══════════════════════════════════════════════════════════════════
// SkillExecutor — per-combatant cooldown tracking + composite tree
// dispatch + attempt/hit history.
//
// Per-entity MonoBehaviour component. PlayerController / BossController /
// SkillManager hold a reference and call Execute(skill, ctx) when a slot
// is selected for casting.
//
// SERVER AUTHORITY CONTRACT:
//   SkillExecutor does NOT enforce server authority itself. Execute() mutates
//   internal cooldown / attempt / hit dicts and invokes RuntimeStep delegates
//   that call into StatManager etc. (server-only operations). Callers MUST
//   gate Execute / ResetCooldown / ResetAll with `if (IsServer)` (or
//   equivalent host-only condition). Read accessors (CanUse, GetRemainingCooldown,
//   GetHitRate, GetUseCount, GetLastNSkillIds, TotalHitCount, TotalUseCount)
//   are safe from any context.
//
// ML OBSERVATION SURFACE (do not rename / remove):
//   GetRemainingCooldown / GetHitRate / GetUseCount / GetLastNSkillIds /
//   TotalHitCount / TotalUseCount are read by BossObservationCollector
//   (Buildup ML-Agents pipeline, planned for X4-N integration). Renaming
//   these or changing internal dict field names breaks the trained policy's
//   expected observation layout. See SKILL_SYSTEM_DESIGN.md §10a for the
//   full ML preservation policy.
//
// Buildup origin: Assets/Scripts/Skill/Core/SkillExecutor.cs (X2-6 import).
// ══════════════════════════════════════════════════════════════════

namespace ArenaCombat.Core.Skill
{
    public class SkillExecutor : MonoBehaviour
    {
        [Header("Settings")]
        private float _cooldownScale = 1f;
        public float CooldownScale
        {
            get => _cooldownScale;
            set => _cooldownScale = Mathf.Max(0.1f, value);
        }

        public event System.Action<SkillDefinition, SkillContext> OnExecuted;

        [Header("Debug")]
        [SerializeField] private bool _logExecution = true;

        // SkillId -> last use timestamp (Time.time on host)
        private readonly Dictionary<string, float> _lastUseTimes = new();

        // ── Hit / attempt history (BossObservationCollector reads this) ──
        private readonly Dictionary<string, int> _attemptCounts = new();
        private readonly Dictionary<string, int> _hitCounts     = new();
        private readonly List<string>            _skillHistory  = new();

        // ══════════════════════════════════════════════════════════
        // Cooldown query
        // ══════════════════════════════════════════════════════════
        public bool CanUse(SkillDefinition skill)
        {
            if (skill == null || !skill.IsReady) return false;
            return Time.time - GetLastUseTime(skill) >= skill.Cooldown * _cooldownScale;
        }

        public float GetRemainingCooldown(SkillDefinition skill)
        {
            if (skill == null) return 0f;
            return Mathf.Max(0f, skill.Cooldown * _cooldownScale - (Time.time - GetLastUseTime(skill)));
        }

        // ══════════════════════════════════════════════════════════
        // Execute — central dispatch.
        // Returns true iff cast actually fired (CanUse passed + RuntimeCondition allowed).
        // Returns false on cooldown / RuntimeStep null / RuntimeCondition denied.
        // ══════════════════════════════════════════════════════════
        public bool Execute(SkillDefinition skill, SkillContext ctx)
        {
            if (!CanUse(skill)) return false;

            ctx.CurrentTime = Time.time;
            ctx.RefreshSnapshot();

            if (skill.RuntimeCondition != null && !skill.RuntimeCondition(ctx))
                return false;

            _lastUseTimes[skill.SkillId] = Time.time;
            RecordAttempt(skill.SkillId);
            ctx.HitRecorded = false;
            string capturedId = skill.SkillId;
            ctx.OnHitRecorded = () =>
            {
                if (!ctx.HitRecorded)
                {
                    ctx.HitRecorded = true;
                    RecordHit(capturedId);
                }
            };
            skill.RuntimeStep.Invoke(ctx);
            OnExecuted?.Invoke(skill, ctx);

            if (_logExecution)
            {
                string target = ctx.PrimaryTarget != null
                    ? $"{ctx.PrimaryTarget.Transform.name}(HP:{ctx.PrimaryTarget.CurrentHPPercent * 100f:F0}%)"
                    : "none";
                string caster = ctx.Caster?.Transform.name ?? "?";
                Debug.Log($"[Skill] {caster} -> <b>{skill.DisplayName}</b>({skill.SkillId}) | target: {target} | CD: {skill.Cooldown}s\n" +
                          $"  trace: {string.Join(" -> ", ctx.GetLog())}");
            }

            return true;
        }

        // ══════════════════════════════════════════════════════════
        // Cooldown forced reset (parry rewards, perks)
        // ══════════════════════════════════════════════════════════
        public void ResetCooldown(string skillId) =>
            _lastUseTimes.Remove(skillId);

        public void MarkUsedNow(string skillId) =>
            _lastUseTimes[skillId] = Time.time;

        // ══════════════════════════════════════════════════════════
        // Hit / attempt recording — internal
        // ══════════════════════════════════════════════════════════
        private void RecordAttempt(string skillId)
        {
            _attemptCounts[skillId] = GetUseCount(skillId) + 1;
            _skillHistory.Add(skillId);
        }

        private void RecordHit(string skillId)
        {
            _hitCounts.TryGetValue(skillId, out int h);
            _hitCounts[skillId] = h + 1;
            TotalHitCount++;
        }

        public int TotalHitCount { get; private set; }

        public void ResetAll()
        {
            _lastUseTimes.Clear();
            _attemptCounts.Clear();
            _hitCounts.Clear();
            _skillHistory.Clear();
            TotalHitCount = 0;
        }

        // ══════════════════════════════════════════════════════════
        // Hit / attempt query — external (ML observation)
        // ══════════════════════════════════════════════════════════
        public float GetHitRate(string skillId)
        {
            int attempts = GetUseCount(skillId);
            if (attempts == 0) return 0f;
            _hitCounts.TryGetValue(skillId, out int hits);
            return (float)hits / attempts;
        }

        public int GetUseCount(string skillId) =>
            _attemptCounts.TryGetValue(skillId, out int a) ? a : 0;

        public string[] GetLastNSkillIds(int count)
        {
            var result = new string[count];
            int start  = Mathf.Max(0, _skillHistory.Count - count);
            int len    = Mathf.Min(count, _skillHistory.Count);
            for (int i = 0; i < len; i++)
                result[count - len + i] = _skillHistory[start + i];
            return result;
        }

        public int TotalUseCount => _skillHistory.Count;

        // ══════════════════════════════════════════════════════════
        // Internal
        // ══════════════════════════════════════════════════════════
        private float GetLastUseTime(SkillDefinition skill) =>
            _lastUseTimes.TryGetValue(skill.SkillId, out float t) ? t : float.NegativeInfinity;
    }
}
