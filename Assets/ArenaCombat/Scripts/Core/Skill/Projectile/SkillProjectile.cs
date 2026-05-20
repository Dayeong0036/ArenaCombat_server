using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using ArenaCombat.Core.Combat;

// Skill projectile prefab component.
// Pooled via ProjectilePool. Launch -> OverlapSphere hit detection in FixedUpdate
// -> onHit(SkillStep) -> return to pool. Pierce flag controls single-hit vs through.
//
// SERVER AUTHORITY (X3-5a):
//   NetworkBehaviour. Hit detection gated by IsServer (replaces X2-7 client-side
//   stub). ProjectilePool NGO spawn/despawn lifecycle handled in X3-5b — until
//   then pool still uses Instantiate/SetActive (X2-7 pattern); client visibility
//   limited until X3-5b lands.
//
// DESIGNER SETUP (X3-5b will document fully):
//   - Prefab must have NetworkObject component
//   - Prefab must be registered in NetworkManager.NetworkConfig.NetworkPrefabs
//   - Optional NetworkTransform for smooth client-side interpolation
//
// Buildup origin: Assets/Scripts/Skill/Projectile/SkillProjectile.cs

namespace ArenaCombat.Core.Skill
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    public class SkillProjectile : NetworkBehaviour, IProjectile
    {
        [SerializeField] private Color _color = Color.red;
        [SerializeField] private float _detectionRadius = 1.0f;
        [SerializeField] private LayerMask _targetMask = -1;

        private const int OverlapBufferSize = 16;
        private static readonly Collider[] _overlapBuffer = new Collider[OverlapBufferSize];

        private Rigidbody   _rb;
        private Collider    _col;
        private Renderer    _renderer;
        private ProjectilePool _pool;

        private SkillStep   _onHit;
        private SkillContext _ctx;
        private bool        _pierce;
        private float       _range;
        private Vector3     _spawnPos;
        private bool        _active;
        private readonly System.Collections.Generic.HashSet<ICombatant> _hitTargets = new();

        private void Awake()
        {
            _rb           = GetComponent<Rigidbody>();
            _col          = GetComponent<Collider>();
            _renderer     = GetComponentInChildren<Renderer>();
            _rb.useGravity = false;
            _col.isTrigger = true;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer && _rb != null)
                _rb.isKinematic = true;
        }

        public void SetPool(ProjectilePool pool) => _pool = pool;

        // ── IProjectile ──────────────────────────────────────────

        public void Launch(Vector3 direction, float speed, float range)
        {
            _range    = range;
            _spawnPos = transform.position;
            _active   = true;
            _rb.linearVelocity = direction.normalized * speed;
        }

        public void SetHitCallback(SkillStep onHit, SkillContext ctx, bool pierce)
        {
            _onHit  = onHit;
            _ctx    = ctx;
            _pierce = pierce;
        }

        // ── Hit detection (FixedUpdate, sync with physics tick) ──
        private void FixedUpdate()
        {
            if (!_active) return;

            if (Vector3.Distance(_spawnPos, transform.position) >= _range)
            {
                ReturnToPool();
                return;
            }

            if (!ShouldRunHitDetection()) return;

            int count = Physics.OverlapSphereNonAlloc(transform.position, _detectionRadius, _overlapBuffer, _targetMask);

            for (int i = 0; i < count; i++)
            {
                var target = _overlapBuffer[i].GetComponentInParent<ICombatant>();
                if (target == null) continue;
                if (_ctx != null && ReferenceEquals(target, _ctx.Caster)) continue;
                if (!_hitTargets.Add(target)) continue;

                ApplyHit(target);

                if (!_pierce) { ReturnToPool(); return; }
            }
        }

        // ── Authority gate (X3-5a: server-only hit detection) ──
        private bool ShouldRunHitDetection() => IsServer;

        // ── Hit handler — X3 wire RPC broadcast for VFX cue here ──
        private void ApplyHit(ICombatant target)
        {
            if (_ctx != null)
            {
                _ctx.PrimaryTarget = target;
                _ctx.HitLanded     = true;
                _ctx.CastPosition  = transform.position;
                _ctx.RefreshSnapshot();
                _ctx.OnHitRecorded?.Invoke();
            }

            _onHit?.Invoke(_ctx);
        }

        // ── IPoolable ────────────────────────────────────────────

        public void OnSpawn()
        {
            _active            = false;
            _onHit             = null;
            _ctx               = null;
            _pierce            = false;
            _rb.linearVelocity = Vector3.zero;
            _hitTargets.Clear();
            if (_renderer != null) _renderer.material.color = _color;
        }

        public void OnDespawn()
        {
            _active            = false;
            _rb.linearVelocity = Vector3.zero;
            _hitTargets.Clear();
        }

        private void ReturnToPool()
        {
            _active = false;
            _pool?.Return(this);
        }
    }
}
