using System.Collections;
using UnityEngine;
using Unity.Netcode;
using ArenaCombat.Core.Combat;

// Persistent AoE prefab component. Prefab structure:
//   SkillArea (this script)
//   |__ Visual (MeshRenderer + MeshFilter — Cylinder or Plane recommended)
//
// Pulled from PersistentAreaPool, Initialize starts the tick loop: every
// tickInterval, apply tickEffect (SkillStep) to ICombatant in range. After
// duration elapses, auto-returns to pool.
//
// SERVER AUTHORITY (X3-5a):
//   NetworkBehaviour. TickArea() gated by IsServer. PersistentAreaPool NGO
//   spawn/despawn lifecycle handled in X3-5b — until then pool still uses
//   Instantiate/SetActive (X2-8 pattern); client visibility limited until X3-5b.
//
// DESIGNER SETUP (X3-5b will document fully):
//   - Prefab must have NetworkObject component
//   - Prefab must be registered in NetworkManager.NetworkConfig.NetworkPrefabs
//
// Buildup origin: Assets/Scripts/Skill/Area/SkillArea.cs

namespace ArenaCombat.Core.Skill
{
    [RequireComponent(typeof(NetworkObject))]
    public class SkillArea : NetworkBehaviour, IPersistentArea
    {
        [SerializeField] private Color _areaColor = new Color(1f, 0.2f, 0.2f, 0.35f); // semi-transparent red

        private Renderer           _renderer;
        private PersistentAreaPool _pool;
        private Coroutine          _routine;

        // Runtime values populated by Initialize
        private float        _radius;
        private float        _angleDeg;
        private AreaShape    _shape;
        private SkillStep    _tickEffect;
        private SkillContext _ctx;
        private Vector3      _forward;

        // ── Initialization (called once per pool spawn) ─────────
        private void Awake()
        {
            // Cache child Renderer (fallback to self).
            _renderer = GetComponentInChildren<Renderer>();
        }

        public void SetPool(PersistentAreaPool pool) => _pool = pool;

        // ── IPersistentArea ──────────────────────────────────────

        public void Initialize(Vector3 forward, float radius, AreaShape shape, float angleDeg,
                               float duration, float tickInterval, SkillStep tickEffect, SkillContext ctx)
        {
            _forward    = forward;
            _radius     = radius;
            _shape      = shape;
            _angleDeg   = angleDeg;
            _tickEffect = tickEffect;
            _ctx        = ctx;

            ApplyVisual(forward, radius, shape);
            if (IsServer)
                ApplyVisualRpc(forward, radius, (int)shape, angleDeg);

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(AreaRoutine(duration, tickInterval));
        }

        // ── Visual apply — scale child Visual transform to express AoE radius ──
        private void ApplyVisual(Vector3 forward, float radius, AreaShape shape)
        {
            if (_renderer != null) _renderer.material.color = _areaColor;

            Transform visual = transform.childCount > 0 ? transform.GetChild(0) : transform;

            switch (shape)
            {
                case AreaShape.Circle:
                    visual.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
                    break;

                case AreaShape.Cone:
                    // Cone uses circle-based scale + rotation toward forward;
                    // actual angle filter is in TickArea.
                    visual.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
                    if (forward != Vector3.zero)
                        transform.rotation = Quaternion.LookRotation(forward);
                    break;

                case AreaShape.Line:
                    // Linear AoE: width 0.5 fixed, length = radius * 2.
                    visual.localScale = new Vector3(0.5f, 0.02f, radius * 2f);
                    if (forward != Vector3.zero)
                        transform.rotation = Quaternion.LookRotation(forward);
                    break;
            }
        }

        [Rpc(SendTo.NotServer)]
        private void ApplyVisualRpc(Vector3 forward, float radius, int shape, float angleDeg)
        {
            _forward  = forward;
            _radius   = radius;
            _shape    = (AreaShape)shape;
            _angleDeg = angleDeg;
            ApplyVisual(forward, radius, (AreaShape)shape);
        }

        // ── Tick loop ────────────────────────────────────────────
        private IEnumerator AreaRoutine(float duration, float tickInterval)
        {
            var wait    = new WaitForSeconds(tickInterval);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                yield return wait;
                elapsed += tickInterval;
                TickArea();
            }

            ReturnToPool();
        }

        private void TickArea()
        {
            if (!IsServer) return;   // X3-5a: server-only AoE tick.
            if (_ctx == null) return;

            ICombatant original    = _ctx.PrimaryTarget;
            bool       originalHit = _ctx.HitLanded;
            var colliders  = Physics.OverlapSphere(transform.position, _radius);
            var processed  = new System.Collections.Generic.HashSet<ICombatant>();

            foreach (var col in colliders)
            {
                var target = col.GetComponentInParent<ICombatant>();
                if (target == null) continue;
                if (ReferenceEquals(target, _ctx.Caster)) continue;
                if (!processed.Add(target)) continue;

                if (_shape == AreaShape.Cone)
                {
                    Vector3 toTarget = col.transform.position - transform.position;
                    if (Vector3.Angle(_forward, toTarget) > _angleDeg * 0.5f) continue;
                }

                _ctx.PrimaryTarget = target;
                _ctx.RefreshSnapshot();
                _tickEffect?.Invoke(_ctx);
            }

            _ctx.PrimaryTarget = original;
            _ctx.HitLanded     = originalHit;
        }

        // ── IPoolable ────────────────────────────────────────────

        public void OnSpawn()
        {
            if (_renderer != null) _renderer.material.color = _areaColor;
        }

        public void OnDespawn()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            _tickEffect = null;
            _ctx        = null;
        }

        private void ReturnToPool() => _pool?.Return(this);
    }
}
