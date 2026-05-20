using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.Netcode;
using ArenaCombat.Core.Network;
using ArenaCombat.Core.Skill;
using ArenaCombat.Core.Stats;
using ArenaCombat.Core.State;
using ArenaCombat.Core.Combat;

// Inference-only boss ML-Agent (BAL-1 T1B: movement-only).
// Drops an ONNX model into BehaviorParameters → ML drives boss movement.
// Skill selection is handled by server-side SkillManager auto-cast
// (BossAdaptiveWeights + variant slotWeights) — NOT by ML. SkillManager
// telegraph path remains intact; movement is gated on BNC.IsBusy during windup.
//
// SERVER AUTHORITY: OnActionReceived gates all mutations with IsServer check.
// Movement routes through BNC3D.ApplyMLPosition (MovePosition + networkPosition NV).
//
// Observation: 35 (BossObservationCollector.Phase3Size)
//   + 5 extra (P1/P2 casting, P1/P2 speed, burst damage)
//   = 40 total
//
// Actions: 1 discrete branch (BAL-1 T1B)
//   B0 = 4 (idle / forward / left / right)

namespace ArenaCombat.Core.AI
{
    [RequireComponent(typeof(BossObservationCollector))]
    public class BossInferenceAgent : Agent
    {
        private const int ExtraObsCount = 5;
        public const int TotalObsSize = BossObservationCollector.Phase3Size + ExtraObsCount;

        [Header("Movement")]
        // BAL-1 T1A: 8.4 m/s (Player 14 × 60%). 페이즈별 변경은 SetMoveSpeed.
        [SerializeField] private float _moveSpeed = 8.4f;
        [SerializeField] private float _rotationSpeed = 540f;

        // BAL-1 T1A: 페이즈별 속도 갱신 외부 API. BossNetworkController3D.OnPhaseChanged 호출.
        public void SetMoveSpeed(float speed)
        {
            _moveSpeed = Mathf.Max(0f, speed);
        }

        [Header("References")]
        [SerializeField] private BossNetworkController3D _bnc;
        [SerializeField] private BossObservationCollector _collector;
        [SerializeField] private SkillManager _skillManager;
        [SerializeField] private SkillExecutor _skillExecutor;
        [SerializeField] private StatManager _statManager;
        [SerializeField] private StateManager _stateManager;

        private bool IsServer =>
            NetworkManager.Singleton != null ? NetworkManager.Singleton.IsServer : true;

        public override void Initialize()
        {
            if (_bnc == null) _bnc = GetComponent<BossNetworkController3D>();
            if (_collector == null) _collector = GetComponent<BossObservationCollector>();
            if (_skillManager == null) _skillManager = GetComponent<SkillManager>();
            if (_skillExecutor == null) _skillExecutor = GetComponent<SkillExecutor>();
            if (_statManager == null) _statManager = GetComponent<StatManager>();
            if (_stateManager == null) _stateManager = GetComponent<StateManager>();

            // BAL-1 T1B: ML은 이동만 담당. 스킬은 서버 SkillManager auto-cast (BossAdaptiveWeights
            // + variant slotWeights 가중치)가 처리. 텔레그래프도 SkillManager 정상 경로 사용.
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            if (_collector == null)
            {
                sensor.AddObservation(new float[TotalObsSize]);
                return;
            }

            _collector.CollectPhase3(sensor);
            CollectExtraObs(sensor);
        }

        private void CollectExtraObs(VectorSensor sensor)
        {
            bool p1Alive = _collector.P1 != null;
            bool p2Alive = _collector.P2 != null;
            StatManager p1Stat = null;
            StatManager p2Stat = null;

            if (p1Alive)
            {
                p1Stat = _collector.P1.GetComponent<StatManager>();
                p1Alive = p1Stat != null && p1Stat.IsAlive;
            }
            if (p2Alive)
            {
                p2Stat = _collector.P2.GetComponent<StatManager>();
                p2Alive = p2Stat != null && p2Stat.IsAlive;
            }

            sensor.AddObservation(p1Alive && p1Stat.IsCasting ? 1f : 0f);
            sensor.AddObservation(p2Alive && p2Stat.IsCasting ? 1f : 0f);
            // BAL-1 T1A: Player 14 m/s → /16f (여유), burst damage max ~95 → /80f
            sensor.AddObservation(p1Alive ? Mathf.Clamp01(_collector.P1AvgSpeed / 16f) : 0f);
            sensor.AddObservation(p2Alive ? Mathf.Clamp01(_collector.P2AvgSpeed / 16f) : 0f);
            sensor.AddObservation(Mathf.Clamp01(_collector.RecentBurstDamage / 80f));
        }

        // BAL-1 T1B: 이동 전용. Branch 0 = 0=idle 1=forward 2=left 3=right.
        // 스킬은 서버 SkillManager auto-cast가 처리. Branch 1은 사용 안 함 (BehaviorParameters 1-branch).
        public override void OnActionReceived(ActionBuffers actionBuffers)
        {
            if (!IsServer) return;
            if (_statManager == null || !_statManager.IsAlive) return;

            int moveAction = actionBuffers.DiscreteActions[0];
            ApplyMovement(moveAction);
        }

        private void ApplyMovement(int moveAction)
        {
            if (_bnc == null) return;

            // BAL-1 T1B: SkillManager 텔레그래프 중 보스 이동 정지. hit 위치 예측 가능 + readable.
            if (_bnc.IsBusy)
            {
                if (_stateManager != null) _stateManager.NotifyMovementInput(false);
                return;
            }

            switch (moveAction)
            {
                case 1:
                    Vector3 target = transform.position + transform.forward * (_moveSpeed * Time.deltaTime);
                    _bnc.ApplyMLPosition(target);
                    break;
                case 2:
                    transform.Rotate(0f, -_rotationSpeed * Time.deltaTime, 0f);
                    break;
                case 3:
                    transform.Rotate(0f, _rotationSpeed * Time.deltaTime, 0f);
                    break;
            }

            if (_stateManager != null)
                _stateManager.NotifyMovementInput(moveAction == 1);
        }

        // BAL-1 T1B: 스킬 실행 제거 (서버 auto-cast가 처리). WriteDiscreteActionMask, Heuristic도
        // 1-branch movement-only로 축소.

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var d = actionsOut.DiscreteActions;
            d[0] = 0;

            if (Input.GetKey(KeyCode.UpArrow)) d[0] = 1;
            else if (Input.GetKey(KeyCode.LeftArrow)) d[0] = 2;
            else if (Input.GetKey(KeyCode.RightArrow)) d[0] = 3;
        }
    }
}
