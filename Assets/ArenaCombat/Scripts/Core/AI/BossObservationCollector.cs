using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents.Sensors;
using ArenaCombat.Core.Network;
using ArenaCombat.Core.Skill;
using ArenaCombat.Core.Stats;

namespace ArenaCombat.Core.AI
{
    public class BossObservationCollector : MonoBehaviour
    {
        public const int BossSkillSlots = 5;
        public const int Phase3Size = 35;

        [Header("Boss References")]
        [SerializeField] private BossNetworkController3D _bnc;
        [SerializeField] private StatManager _bossStatManager;
        [SerializeField] private SkillExecutor _skillExecutor;
        [SerializeField] private SkillManager _skillManager;

        [Header("Observation Limits")]
        [SerializeField] private float _maxDistance = 55f;
        [SerializeField] private float _maxCooldown = 30f;
        // BAL-1 T1A: Player 14 m/s + burst damage 최대 ~95 (R3+R4) 반영
#pragma warning disable CS0414
        [SerializeField] private float _maxBurstDmg = 120f;
        [SerializeField] private float _maxSpeed = 16f;
#pragma warning restore CS0414
        [SerializeField] private int _maxBossPhase = 4;

        private GameObject _p1;
        private GameObject _p2;
        private StatManager _p1StatManager;
        private StatManager _p2StatManager;

        private float _prevBossHp;
        private readonly Queue<(float time, float damage)> _damageLog = new();
        private float _recentDamageSum;

        private Vector3 _prevP1Pos;
        private Vector3 _prevP2Pos;
        private float _p1AvgSpeed;
        private float _p2AvgSpeed;
        private bool _initialized;

        private const float SpeedSmooth = 0.15f;

        public float P1AvgSpeed => _p1AvgSpeed;
        public float P2AvgSpeed => _p2AvgSpeed;
        public float RecentBurstDamage => _recentDamageSum;
        public GameObject P1 => _p1;
        public GameObject P2 => _p2;

        private void Awake()
        {
            if (_bnc == null) _bnc = GetComponent<BossNetworkController3D>();
            if (_bossStatManager == null) _bossStatManager = GetComponent<StatManager>();
            if (_skillExecutor == null) _skillExecutor = GetComponent<SkillExecutor>();
            if (_skillManager == null) _skillManager = GetComponent<SkillManager>();
        }

        private void Start()
        {
            RefreshPlayerCache();
        }

        public void RefreshPlayerCache()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            _p1 = gm.Player1;
            _p2 = gm.Player2;

            if (_p1 != null)
            {
                _p1StatManager = _p1.GetComponent<StatManager>();
                _prevP1Pos = _p1.transform.position;
            }
            if (_p2 != null)
            {
                _p2StatManager = _p2.GetComponent<StatManager>();
                _prevP2Pos = _p2.transform.position;
            }

            if (_bossStatManager != null)
                _prevBossHp = _bossStatManager.GetHP();

            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized) return;
            TrackBurstDamage();
            TrackMovementSpeed();
        }

        private void TrackBurstDamage()
        {
            if (_bossStatManager == null) return;

            float currentHp = _bossStatManager.GetHP();
            float delta = _prevBossHp - currentHp;
            if (delta > 0f)
            {
                _damageLog.Enqueue((Time.time, delta));
                _recentDamageSum += delta;
            }
            _prevBossHp = currentHp;

            while (_damageLog.Count > 0 && Time.time - _damageLog.Peek().time > 1f)
                _recentDamageSum -= _damageLog.Dequeue().damage;

            if (_recentDamageSum < 0f) _recentDamageSum = 0f;
        }

        private void TrackMovementSpeed()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.001f);
            if (_p1 != null)
            {
                float s = Vector3.Distance(_p1.transform.position, _prevP1Pos) / dt;
                _p1AvgSpeed = Mathf.Lerp(_p1AvgSpeed, s, SpeedSmooth);
                _prevP1Pos = _p1.transform.position;
            }
            if (_p2 != null)
            {
                float s = Vector3.Distance(_p2.transform.position, _prevP2Pos) / dt;
                _p2AvgSpeed = Mathf.Lerp(_p2AvgSpeed, s, SpeedSmooth);
                _prevP2Pos = _p2.transform.position;
            }
        }

        // Phase3Size = 35 observations total:
        //   Phase1 (#0-5):   P1 dir/dist + boss forward + dot = 6
        //   Phase2 (#6-10):  P2 dir/dist + P1-P2 dist + dot = 5
        //   Phase3 (#11-34): HP×3 + CD×5 + phase + range×5 + (cd_max,tt)×5 = 24
        public void CollectPhase3(VectorSensor sensor)
        {
            CollectPhase1(sensor);
            CollectPhase2(sensor);
            CollectPhase3Stats(sensor);
        }

        private void CollectPhase1(VectorSensor sensor)
        {
            if (_p1 == null)
            {
                sensor.AddObservation(new float[6]);
                return;
            }

            Vector3 bossPos = transform.position;
            Vector3 fwd = transform.forward;
            Vector3 toP1 = _p1.transform.position - bossPos;
            float distP1 = toP1.magnitude;
            Vector3 dirP1 = distP1 > 0.001f ? toP1.normalized : Vector3.forward;

            sensor.AddObservation(dirP1.x);
            sensor.AddObservation(dirP1.z);
            sensor.AddObservation(Mathf.Clamp01(distP1 / _maxDistance));
            sensor.AddObservation(fwd.x);
            sensor.AddObservation(fwd.z);
            sensor.AddObservation(Vector3.Dot(fwd, dirP1));
        }

        private void CollectPhase2(VectorSensor sensor)
        {
            if (_p1 == null || _p2 == null)
            {
                sensor.AddObservation(new float[5]);
                return;
            }

            Vector3 bossPos = transform.position;
            Vector3 fwd = transform.forward;
            Vector3 toP2 = _p2.transform.position - bossPos;
            float distP2 = toP2.magnitude;
            Vector3 dirP2 = distP2 > 0.001f ? toP2.normalized : Vector3.forward;
            float distPP = Vector3.Distance(_p1.transform.position, _p2.transform.position);

            sensor.AddObservation(dirP2.x);
            sensor.AddObservation(dirP2.z);
            sensor.AddObservation(Mathf.Clamp01(distP2 / _maxDistance));
            sensor.AddObservation(Mathf.Clamp01(distPP / _maxDistance));
            sensor.AddObservation(Vector3.Dot(fwd, dirP2));
        }

        private void CollectPhase3Stats(VectorSensor sensor)
        {
            float bossHp = _bossStatManager != null ? _bossStatManager.GetHPPercent() : 0f;
            float p1Hp = _p1StatManager != null ? _p1StatManager.GetHPPercent() : 0f;
            float p2Hp = _p2StatManager != null ? _p2StatManager.GetHPPercent() : 0f;

            sensor.AddObservation(bossHp);
            sensor.AddObservation(p1Hp);
            sensor.AddObservation(p2Hp);

            var slots = _skillManager != null ? _skillManager.Slots : null;
            for (int i = 0; i < BossSkillSlots; i++)
            {
                SkillDefinition skill = (slots != null && i < slots.Count) ? slots[i] : null;
                if (_skillExecutor != null && skill != null)
                {
                    float remaining = _skillExecutor.GetRemainingCooldown(skill);
                    float total = skill.Cooldown;
                    sensor.AddObservation(total > 0f ? Mathf.Clamp01(remaining / total) : 0f);
                }
                else
                {
                    sensor.AddObservation(0f);
                }
            }

            int phase = _bnc != null ? (int)_bnc.CurrentPhase : 0;
            int maxPhase = _maxBossPhase > 0 ? _maxBossPhase : 1;
            sensor.AddObservation(Mathf.Clamp01((float)phase / maxPhase));

            float maxCd = _maxCooldown > 0f ? _maxCooldown : 1f;
            int targetTypeCount = System.Enum.GetValues(typeof(TargetType)).Length;
            float ttDivisor = Mathf.Max(targetTypeCount - 1, 1);

            for (int i = 0; i < BossSkillSlots; i++)
            {
                SkillDefinition skill = (slots != null && i < slots.Count) ? slots[i] : null;
                float range = skill != null ? skill.Range : 0f;
                sensor.AddObservation(Mathf.Clamp01(range / _maxDistance));
            }

            for (int i = 0; i < BossSkillSlots; i++)
            {
                SkillDefinition skill = (slots != null && i < slots.Count) ? slots[i] : null;
                if (skill != null)
                {
                    sensor.AddObservation(Mathf.Clamp01(skill.Cooldown / maxCd));
                    sensor.AddObservation((float)skill.TargetType / ttDivisor);
                }
                else
                {
                    sensor.AddObservation(0f);
                    sensor.AddObservation(0f);
                }
            }
        }

        public float GetDistanceToP1()
        {
            if (_p1 == null) return _maxDistance;
            return Vector3.Distance(transform.position, _p1.transform.position);
        }

        public float GetDistanceToP2()
        {
            if (_p2 == null) return _maxDistance;
            return Vector3.Distance(transform.position, _p2.transform.position);
        }
    }
}
