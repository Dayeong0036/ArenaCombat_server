using ArenaCombat.Core;
using ArenaCombat.Core.AI.BT;
using ArenaCombat.Core.Network;
using ArenaCombat.Core.Combat;
using UnityEngine;

namespace ArenaCombat.Core.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerNetworkController3D))]
    public class BTPlayerAgent : MonoBehaviour
    {
        [Header("Personality")]
        [SerializeField, Range(0f, 1f)] float _meleeAggression = 0.5f;
        [SerializeField, Range(0f, 1f)] float _parryTendency = 0.3f;
        [SerializeField, Range(0f, 1f)] float _survivalCaution = 0.3f;
        [SerializeField] float _meleeRange = 3f;
        [SerializeField] float _fleeHPThreshold = 0.3f;

        [Header("Tick")]
        [SerializeField] float _tickInterval = 0.2f;

        PlayerNetworkController3D _pnc;
        BTNode _root;
        float _nextTick;
        ICombatant _self;

        bool IsServer => Unity.Netcode.NetworkManager.Singleton != null
                       && Unity.Netcode.NetworkManager.Singleton.IsServer;

        void Awake()
        {
            _pnc = GetComponent<PlayerNetworkController3D>();
            _self = GetComponent<ICombatant>();
            if (_pnc != null && !_pnc.IsBTControlled)
                Debug.LogWarning($"[BTPlayerAgent] PNC3D.IsBTControlled is false on {gameObject.name} — owner input may conflict with BT");
        }

        void OnEnable()
        {
            _root = BuildTree();
        }

        void FixedUpdate()
        {
            if (!IsServer || _root == null) return;
            if (!_self.IsAlive) return;
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + _tickInterval;
            _root.Tick();
        }

        ICombatant FindBoss()
        {
            if (GameManager.Instance == null) return null;
            var bosses = GameManager.Instance.Bosses;
            if (bosses == null || bosses.Count == 0) return null;
            return bosses[0].GetComponent<ICombatant>();
        }

        float BossDistance()
        {
            var boss = FindBoss();
            if (boss == null) return float.MaxValue;
            return Vector3.Distance(transform.position, boss.Transform.position);
        }

        float LookAtBoss()
        {
            var boss = FindBoss();
            if (boss == null) return transform.eulerAngles.y;
            Vector3 dir = boss.Transform.position - transform.position;
            return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        BTNode BuildTree() => new BTSelector(
            new BTSequence(
                new BTCondition(() => _self.CurrentHPPercent < _fleeHPThreshold),
                new BTAction(() =>
                {
                    var boss = FindBoss();
                    if (boss == null) return BTStatus.Failure;
                    Vector3 away = (transform.position - boss.Transform.position).normalized;
                    _pnc.ServerSetMoveIntent(new Vector2(away.x, away.z), LookAtBoss());
                    return BTStatus.Success;
                })
            ),
            new BTSequence(
                new BTCondition(() => BossDistance() < _meleeRange * 1.5f
                                   && Random.value < _parryTendency),
                new BTAction(() =>
                {
                    _pnc.ServerSubmitParry();
                    return BTStatus.Success;
                })
            ),
            new BTSequence(
                new BTCondition(() => BossDistance() < _meleeRange
                                   && Random.value < _meleeAggression),
                new BTAction(() =>
                {
                    _pnc.ServerSubmitAttack(AttackType.Light);
                    _pnc.ServerSetMoveIntent(Vector2.zero, LookAtBoss());
                    return BTStatus.Success;
                })
            ),
            new BTAction(() =>
            {
                var boss = FindBoss();
                if (boss == null) return BTStatus.Failure;
                Vector3 toward = (boss.Transform.position - transform.position).normalized;
                _pnc.ServerSetMoveIntent(new Vector2(toward.x, toward.z), LookAtBoss());
                return BTStatus.Success;
            })
        );
    }
}
