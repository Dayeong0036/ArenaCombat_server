using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ArenaCombat.Core.Combat;
using ArenaCombat.Core.Skill;

// ══════════════════════════════════════════════════════════════════
// StatManager — single-combatant stat / damage / status / buff authority.
//
// Per-entity MonoBehaviour component (one instance lives on each combatant
// GameObject alongside the controller). Holds runtime HP / Shield, an
// instantiated copy of the BaseStatsSO (multipliers can mutate without
// affecting the source asset), and tracks active status / buff / debuff
// lifetimes via coroutine handles.
//
// SERVER AUTHORITY CONTRACT:
//   StatManager does NOT enforce server authority itself. All mutating calls
//   (DealDamage, ReceiveDamage, ApplyStatus, ApplyBuff, ApplyDebuff,
//   RecoverHP, AddShield, NotifyParryReward, BeginParryWindow, etc.) MUST be
//   gated by the caller with `if (IsServer)` or equivalent. Calling these
//   from a remote client silently mutates client-local state with no NV sync
//   and corrupts authority. Read accessors (GetHP, HasStatus, etc.) are
//   safe to call from any context.
//
// Buildup origin: Assets/Scripts/Stats/StatManager.cs (X2-4 import, 2026-05-12).
// Public API surface and field names are byte-identical to Buildup; only
// comments and Debug.Log strings are translated to English (X2-2 lesson on
// encoding-driven hazards).
// ══════════════════════════════════════════════════════════════════

namespace ArenaCombat.Core.Stats
{
    // Distinguishes Player from Boss for ResetForTraining branching.
    // Top-level enum inside the namespace (not nested in StatManager) per
    // Buildup origin.
    public enum CombatantKind
    {
        Player,
        Boss,
    }

    public class StatManager : MonoBehaviour
    {
        // ── runtime SO copy (multiplier mutations only) ──────────────
        private BaseStatsSO _runtimeStats;
        private BaseStatsSO _baseStats;

        // ── runtime HP / Shield ──────────────────────────────────────
        private float _maxHP;
        private float _currentHP;
        private float _shieldMax;
        private float _currentShield;
        private float _hpRegenRate;
        private float _parryWindow;
        private float _baseParryWindow;

        private static readonly WaitForSeconds WaitOneSec = new(1f);

        // ── state flags ──────────────────────────────────────────────
        private bool _isAlive    = true;
        private bool _isCasting  = false;
        private bool _isParrying = false;

        [Header("Debug")]
        [SerializeField] private bool _logCombat = true;

        // ── inspector live monitor ───────────────────────────────────
        [Header("HP / Shield (live)")]
        [SerializeField] private float _debugHP;
        [SerializeField] private float _debugMaxHP;
        [SerializeField] private float _debugShield;
        [SerializeField] private float _debugHPPercent;

        [Header("State Flags (live)")]
        [SerializeField] private bool _debugIsAlive;
        [SerializeField] private bool _debugIsCasting;
        [SerializeField] private bool _debugIsParrying;

        [Header("Phase scaling (set by BossNetworkController3D)")]
        private float _phaseDamageScale = 1f;
        public void SetPhaseDamageScale(float scale) => _phaseDamageScale = Mathf.Max(0f, scale);
        public float GetPhaseDamageScale() => _phaseDamageScale;

        [Header("Multipliers (live)")]
        [SerializeField] private float _debugDamageTaken;
        [SerializeField] private float _debugDamageUp;
        [SerializeField] private float _debugMoveControl;
        [SerializeField] private float _debugHealingMult;
        [SerializeField] private float _debugReflectRatio;

        [Header("Active Statuses / Buffs / Debuffs")]
        [SerializeField] private string[] _debugActiveStatuses = System.Array.Empty<string>();
        [SerializeField] private string[] _debugActiveBuffs    = System.Array.Empty<string>();
        [SerializeField] private string[] _debugActiveDebuffs  = System.Array.Empty<string>();

        // ── kind + owner ─────────────────────────────────────────────
        private CombatantKind _kind;
        private ICombatant    _owner;

        // ── status change events (for NV bridge) ────────────────────
        public event System.Action<StatusType, float> OnStatusApplied;
        public event System.Action<StatusType> OnStatusRemoved;
        public event System.Action OnStatusBulkCleared;

        // ── buff/debuff change events (for NV bridge) ───────────
        public event System.Action<BuffType, float> OnBuffApplied;
        public event System.Action<BuffType> OnBuffRemoved;
        public event System.Action<DebuffType, float> OnDebuffApplied;
        public event System.Action<DebuffType> OnDebuffRemoved;
        public event System.Action OnBuffDebuffBulkCleared;

        // ── coroutine tracking ───────────────────────────────────────
        private readonly Dictionary<StatusType, Coroutine> _statusCoroutines = new();
        private readonly Dictionary<BuffType,   Coroutine> _buffCoroutines   = new();
        private readonly Dictionary<DebuffType, Coroutine> _debuffCoroutines = new();
        private Coroutine       _parryCoroutine;
        private WaitForSeconds  _parryWait;

        // ══════════════════════════════════════════════════════
        // Initialization
        // ══════════════════════════════════════════════════════

        public void Initialize(
            BaseStatsSO   baseStats,
            float         maxHP,
            float         shieldMax,
            float         hpRegenRate,
            float         parryWindow,
            CombatantKind kind)
        {
            _baseStats       = baseStats;
            _runtimeStats    = baseStats != null ? Instantiate(baseStats) : null;
            _maxHP           = maxHP;
            _currentHP       = maxHP;
            _shieldMax       = shieldMax;
            _currentShield   = 0f;
            _hpRegenRate     = hpRegenRate;
            _parryWindow     = parryWindow;
            _baseParryWindow = parryWindow;
            _kind            = kind;
            _isAlive       = true;
            _isCasting     = false;
            _isParrying    = false;
            _parryWait     = parryWindow > 0f ? new WaitForSeconds(parryWindow) : null;
        }

        // Controller binds itself as the ICombatant — used as `attacker` reference
        // when dealing damage / parry counter / reflect.
        public void BindOwner(ICombatant owner) => _owner = owner;

        // ══════════════════════════════════════════════════════
        // Read — base stats
        // ══════════════════════════════════════════════════════

        public float GetHP()          => _currentHP;
        public float GetMaxHP()       => _maxHP;
        public float GetHPPercent()   => _maxHP > 0f ? _currentHP / _maxHP : 0f;
        public float GetShield()      => _currentShield;
        public float GetShieldMax()   => _shieldMax;
        public float GetHPRegenRate() => _hpRegenRate;
        public float GetParryWindow() => _parryWindow;

        public float GetMoveControl()           => _runtimeStats != null ? _runtimeStats.MoveControlMultiplier      : 1f;
        public float GetReflectRatio()          => _runtimeStats != null ? _runtimeStats.ReflectRatio               : 0f;
        public float GetDamageTakenMultiplier() => _runtimeStats != null ? _runtimeStats.DamageTakenMultiplier      : 1f;
        public float GetHealingMultiplier()     => _runtimeStats != null ? _runtimeStats.HealingReceivedMultiplier  : 1f;
        public float GetDamageUpMultiplier()    => _runtimeStats != null ? _runtimeStats.DamageUpMultiplier         : 1f;

        public CombatantKind Kind  => _kind;
        public ICombatant    Owner => _owner;

        // ══════════════════════════════════════════════════════
        // State flags
        // ══════════════════════════════════════════════════════

        public bool IsAlive    => _isAlive;
        public bool IsCasting  => _isCasting;
        public bool IsParrying => _isParrying;

        public void SetCasting(bool value)  => _isCasting  = value;
        public void SetParrying(bool value) => _isParrying = value;

        // ══════════════════════════════════════════════════════
        // Per-frame tick — called from controller's Update path (server only).
        // ══════════════════════════════════════════════════════
        public void Tick(float dt)
        {
            if (!_isAlive) return;

            if (_hpRegenRate > 0f && _currentHP < _maxHP)
                _currentHP = Mathf.Min(_maxHP, _currentHP + _hpRegenRate * dt);

#if UNITY_EDITOR
            _debugRefreshTimer -= dt;
            if (_debugRefreshTimer <= 0f)
            {
                _debugRefreshTimer = 0.25f;
                RefreshDebugInspector();
            }
#endif
        }

#if UNITY_EDITOR
        private float _debugRefreshTimer;
#endif

        private void RefreshDebugInspector()
        {
            _debugHP         = _currentHP;
            _debugMaxHP      = _maxHP;
            _debugShield     = _currentShield;
            _debugHPPercent  = _maxHP > 0f ? _currentHP / _maxHP : 0f;

            _debugIsAlive    = _isAlive;
            _debugIsCasting  = _isCasting;
            _debugIsParrying = _isParrying;

            _debugDamageTaken  = GetDamageTakenMultiplier();
            _debugDamageUp     = GetDamageUpMultiplier();
            _debugMoveControl  = GetMoveControl();
            _debugHealingMult  = GetHealingMultiplier();
            _debugReflectRatio = GetReflectRatio();

            int statusCount = _statusCoroutines.Count;
            if (_debugActiveStatuses == null || _debugActiveStatuses.Length != statusCount)
                _debugActiveStatuses = new string[statusCount];
            int si = 0;
            foreach (var kv in _statusCoroutines) _debugActiveStatuses[si++] = kv.Key.ToString();

            int buffCount = _buffCoroutines.Count;
            if (_debugActiveBuffs == null || _debugActiveBuffs.Length != buffCount)
                _debugActiveBuffs = new string[buffCount];
            int bi = 0;
            foreach (var kv in _buffCoroutines) _debugActiveBuffs[bi++] = kv.Key.ToString();

            int debuffCount = _debuffCoroutines.Count;
            if (_debugActiveDebuffs == null || _debugActiveDebuffs.Length != debuffCount)
                _debugActiveDebuffs = new string[debuffCount];
            int di = 0;
            foreach (var kv in _debuffCoroutines) _debugActiveDebuffs[di++] = kv.Key.ToString();
        }

        // ══════════════════════════════════════════════════════
        // Attack — caller side (this combatant's DamageUp applied, then routed to target).
        // ══════════════════════════════════════════════════════

        public void DealDamage(ICombatant target, float amount)
        {
            if (target == null || !_isAlive) return;
            float adjusted = amount * GetDamageUpMultiplier() * _phaseDamageScale;
            target.TakeDamage(adjusted, _owner);
        }

        public void DealShieldBreakDamage(ICombatant target, float amount, float multiplier)
        {
            if (target == null || !_isAlive) return;
            float adjusted = amount * GetDamageUpMultiplier() * _phaseDamageScale;
            target.TakeShieldBreakDamage(adjusted, multiplier, _owner);
        }

        // ══════════════════════════════════════════════════════
        // Damage receive — body of ICombatant.TakeDamage (controller delegates here).
        // Order: dead-check -> parry -> reflect -> DamageTakenMultiplier -> Shield -> HP
        // ══════════════════════════════════════════════════════

        public void ReceiveDamage(float amount, ICombatant attacker)
        {
            if (!_isAlive) return;

            string myName = _owner?.Transform.name ?? name;
            string atkName = attacker?.Transform.name ?? "?";

            if (_isParrying && attacker != null)
            {
                if (_logCombat) Debug.Log($"[Combat] <b>PARRY!</b> {myName} reflected {amount:F0} to {atkName}");
                attacker.TakeDamage(amount, _owner);
                return;
            }

            if (HasStatus(StatusType.Reflecting) && attacker != null)
            {
                float reflected = amount * GetReflectRatio();
                if (_logCombat) Debug.Log($"[Combat] Reflect: {myName} -> {atkName} {reflected:F0} reflected damage");
                attacker.TakeDamage(reflected, _owner);
            }

            float prevHP = _currentHP;
            float prevShield = _currentShield;
            float finalDamage = amount * GetDamageTakenMultiplier();
            ApplyDamageToHPAndShield(finalDamage);

            if (_logCombat)
                Debug.Log($"[Combat] <b>{myName}</b> took damage: {amount:F0} x {GetDamageTakenMultiplier():F2} = {finalDamage:F0}" +
                          $" | HP: {prevHP:F0}->{_currentHP:F0}/{_maxHP:F0} | Shield: {prevShield:F0}->{_currentShield:F0}" +
                          $" | from: {atkName}" +
                          (_currentHP <= 0f ? " | <color=red>DEAD!</color>" : ""));
        }

        public void ReceiveShieldBreakDamage(float amount, float multiplier, ICombatant attacker)
        {
            if (!_isAlive) return;

            float prevHP = _currentHP;
            float prevShield = _currentShield;
            float shieldDamage = amount * multiplier;
            float overflow     = Mathf.Max(0f, shieldDamage - _currentShield);
            _currentShield     = Mathf.Max(0f, _currentShield - shieldDamage);

            if (overflow > 0f)
                SetHP(_currentHP - overflow * GetDamageTakenMultiplier());

            if (_logCombat)
            {
                string myName = _owner?.Transform.name ?? name;
                Debug.Log($"[Combat] <b>{myName}</b> shield-break: {amount:F0}x{multiplier:F1}={shieldDamage:F0}" +
                          $" | Shield: {prevShield:F0}->{_currentShield:F0} | HP: {prevHP:F0}->{_currentHP:F0}" +
                          (_currentHP <= 0f ? " | <color=red>DEAD!</color>" : ""));
            }
        }

        // Shield drains first; overflow rolls into HP (negative shield carries the deficit).
        private void ApplyDamageToHPAndShield(float finalDamage)
        {
            if (_currentShield > 0f)
            {
                _currentShield -= finalDamage;
                if (_currentShield < 0f)
                {
                    SetHP(_currentHP + _currentShield); // _currentShield is negative -> subtracts from HP
                    _currentShield = 0f;
                }
            }
            else
            {
                SetHP(_currentHP - finalDamage);
            }
        }

        // ══════════════════════════════════════════════════════
        // Parry — Begin / End + auto-end coroutine
        // ══════════════════════════════════════════════════════

        public void BeginParryWindow()
        {
            if (!_isAlive || _parryWait == null) return;

            if (_parryCoroutine != null)
                StopCoroutine(_parryCoroutine);

            _isParrying      = true;
            _parryCoroutine  = StartCoroutine(ParryWindowRoutine());
        }

        public void EndParryWindow()
        {
            if (_parryCoroutine != null)
            {
                StopCoroutine(_parryCoroutine);
                _parryCoroutine = null;
            }
            _isParrying = false;
        }

        private IEnumerator ParryWindowRoutine()
        {
            yield return _parryWait;
            _isParrying     = false;
            _parryCoroutine = null;
        }

        // ══════════════════════════════════════════════════════
        // Parry reward — invoked by skill layer (ApplyParryReward component)
        // Counter / HitStun routed through ICombatant; Invulnerable / Buff applied to self.
        // ══════════════════════════════════════════════════════
        public void NotifyParryReward(ParryRewardType type, float value, float duration, ICombatant attacker)
        {
            switch (type)
            {
                case ParryRewardType.Counter:
                    attacker?.TakeDamage(value, _owner);
                    break;

                case ParryRewardType.HitStun:
                    attacker?.ApplyStatus(StatusType.HitStun, duration);
                    break;

                case ParryRewardType.Invulnerable:
                    ApplyStatus(StatusType.Invulnerable, duration, 0f);
                    break;

                case ParryRewardType.Buff:
                    ApplyBuff(BuffType.DamageUp, duration, value);
                    break;
            }
        }

        // ══════════════════════════════════════════════════════
        // Status / Buff / Debuff queries
        // ══════════════════════════════════════════════════════
        public bool HasStatus(StatusType type) => _statusCoroutines.ContainsKey(type);
        public bool HasBuff  (BuffType   type) => _buffCoroutines.ContainsKey(type);
        public bool HasDebuff(DebuffType type) => _debuffCoroutines.ContainsKey(type);

        // ══════════════════════════════════════════════════════
        // Recovery / shield
        // ══════════════════════════════════════════════════════

        public void RecoverHP(float amount)
        {
            if (!_isAlive) return;
            float prev = _currentHP;
            float finalAmount = amount * GetHealingMultiplier();
            SetHP(_currentHP + finalAmount);
            if (_logCombat)
                Debug.Log($"[Combat] <b>{_owner?.Transform.name ?? name}</b> recover: {finalAmount:F0} | HP: {prev:F0}->{_currentHP:F0}/{_maxHP:F0}");
        }

        public void AddShield(float amount)
        {
            if (!_isAlive) return;
            _currentShield = Mathf.Min(_shieldMax, _currentShield + amount);
        }

        // ══════════════════════════════════════════════════════
        // Status — Apply / Remove
        // ══════════════════════════════════════════════════════

        public void ApplyStatus(StatusType type, float duration, float value = 0f)
        {
            if (!_isAlive || _runtimeStats == null) return;
            if (_logCombat)
                Debug.Log($"[Combat] <b>{_owner?.Transform.name ?? name}</b> apply status: {type} ({duration:F1}s, val:{value:F2})");
            float effective = CalcStatusDuration(type, duration);
            if (_statusCoroutines.TryGetValue(type, out var existing) && existing != null)
                StopCoroutine(existing);
            _statusCoroutines[type] = StartCoroutine(StatusRoutine(type, effective, value));
            OnStatusApplied?.Invoke(type, effective);
        }

        public void RemoveStatuses(CleanseType type, int count)
        {
            if (_runtimeStats == null) return;
            int removed = 0;
            var toRemove = new List<StatusType>();
            foreach (var kv in _statusCoroutines)
            {
                if (count > 0 && removed >= count) break;
                if (type == CleanseType.All
                    || (type == CleanseType.DamageOverTime && kv.Key == StatusType.DamageOverTime)
                    || (type == CleanseType.Debuff         && IsDebuffStatus(kv.Key)))
                {
                    toRemove.Add(kv.Key);
                    if (kv.Value != null) StopCoroutine(kv.Value);
                    removed++;
                }
            }
            foreach (var key in toRemove)
            {
                RevertStatus(key);
                _statusCoroutines.Remove(key);
                OnStatusRemoved?.Invoke(key);
            }
        }

        private float CalcStatusDuration(StatusType type, float duration)
        {
            if (_runtimeStats == null) return duration;
            if (type == StatusType.Stunned || type == StatusType.HitStun)
                return duration * _runtimeStats.StunDurationMultiplier * (1f - _runtimeStats.HitStunResistance);
            if (IsDebuffStatus(type))
                return duration * (1f - _runtimeStats.DebuffDurationResistance);
            return duration;
        }

        private IEnumerator StatusRoutine(StatusType type, float duration, float value)
        {
            ApplyStatusValue(type, value);
            if (type == StatusType.HPRegen || type == StatusType.DamageOverTime)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    yield return WaitOneSec;
                    elapsed += 1f;
                    if (type == StatusType.HPRegen)        RecoverHP(value);
                    if (type == StatusType.DamageOverTime) SetHP(_currentHP - value * GetDamageTakenMultiplier());
                }
            }
            else
            {
                yield return new WaitForSeconds(duration);
            }
            RevertStatus(type);
            _statusCoroutines.Remove(type);
            OnStatusRemoved?.Invoke(type);
        }

        private void ApplyStatusValue(StatusType type, float value)
        {
            switch (type)
            {
                case StatusType.Stunned:
                case StatusType.HitStun:
                case StatusType.Rooted:
                    _runtimeStats.MoveControlMultiplier = 0f; break;
                case StatusType.Slowed:
                    _runtimeStats.MoveControlMultiplier *= Mathf.Max(0f, 1f - value); break;
                case StatusType.Vulnerable:
                    _runtimeStats.DamageTakenMultiplier += value; break;
                case StatusType.Invulnerable:
                    _runtimeStats.DamageTakenMultiplier = 0f; break;
                case StatusType.Reflecting:
                    _runtimeStats.ReflectRatio = value; break;
                case StatusType.AntiHeal:
                    _runtimeStats.HealingReceivedMultiplier *= Mathf.Max(0f, 1f - value); break;
                case StatusType.Marked:
                    _runtimeStats.VulnerabilityBonus += value; break;
                // Silence / HPRegen / DamageOverTime: handled via HasStatus query / tick loop
            }
        }

        private void RevertStatus(StatusType type)
        {
            if (_baseStats == null) return;
            switch (type)
            {
                case StatusType.Stunned:
                case StatusType.HitStun:
                case StatusType.Rooted:
                case StatusType.Slowed:
                    _runtimeStats.MoveControlMultiplier = _baseStats.MoveControlMultiplier; break;
                case StatusType.Vulnerable:
                case StatusType.Invulnerable:
                    _runtimeStats.DamageTakenMultiplier = _baseStats.DamageTakenMultiplier; break;
                case StatusType.Reflecting:
                    _runtimeStats.ReflectRatio = _baseStats.ReflectRatio; break;
                case StatusType.AntiHeal:
                    _runtimeStats.HealingReceivedMultiplier = _baseStats.HealingReceivedMultiplier; break;
                case StatusType.Marked:
                    _runtimeStats.VulnerabilityBonus = _baseStats.VulnerabilityBonus; break;
            }
        }

        // ══════════════════════════════════════════════════════
        // Buff — Apply / Remove
        // ══════════════════════════════════════════════════════

        public void ApplyBuff(BuffType type, float duration, float value)
        {
            if (!_isAlive || _runtimeStats == null) return;
            if (_buffCoroutines.TryGetValue(type, out var existing) && existing != null)
                StopCoroutine(existing);
            _buffCoroutines[type] = StartCoroutine(BuffRoutine(type, duration, value));
            OnBuffApplied?.Invoke(type, duration);
        }

        public void RemoveBuffs(DispelType type, int count)
        {
            if (_runtimeStats == null) return;
            int removed = 0;
            var toRemove = new List<BuffType>();
            foreach (var kv in _buffCoroutines)
            {
                if (count > 0 && removed >= count) break;
                if (type == DispelType.All
                    || (type == DispelType.DefenseBuff && IsDefenseBuff(kv.Key))
                    || (type == DispelType.OffenseBuff && IsOffenseBuff(kv.Key)))
                {
                    toRemove.Add(kv.Key);
                    if (kv.Value != null) StopCoroutine(kv.Value);
                    removed++;
                }
            }
            foreach (var key in toRemove)
            {
                RevertBuff(key);
                _buffCoroutines.Remove(key);
                OnBuffRemoved?.Invoke(key);
            }
        }

        private IEnumerator BuffRoutine(BuffType type, float duration, float value)
        {
            ApplyBuffValue(type, value);
            yield return new WaitForSeconds(duration);
            RevertBuff(type);
            _buffCoroutines.Remove(type);
            OnBuffRemoved?.Invoke(type);
        }

        private void ApplyBuffValue(BuffType type, float value)
        {
            switch (type)
            {
                case BuffType.DamageUp:
                    _runtimeStats.DamageUpMultiplier += value / 100f; break;
                case BuffType.DefenseUp:
                    _runtimeStats.DefenseUpMultiplier += value / 100f; break;
                case BuffType.ParryWindowUp:
                    _parryWindow += _baseParryWindow * (value / 100f);
                    _parryWait = _parryWindow > 0f ? new WaitForSeconds(_parryWindow) : null;
                    break;
                case BuffType.ParryRewardUp:
                    // No dedicated field — queried via HasBuff at ApplyParryReward time.
                    break;
            }
        }

        private void RevertBuff(BuffType type)
        {
            if (_baseStats == null) return;
            switch (type)
            {
                case BuffType.DamageUp:
                    _runtimeStats.DamageUpMultiplier = _baseStats.DamageUpMultiplier; break;
                case BuffType.DefenseUp:
                    _runtimeStats.DefenseUpMultiplier = _baseStats.DefenseUpMultiplier; break;
                case BuffType.ParryWindowUp:
                    _parryWindow = _baseParryWindow;
                    _parryWait = _parryWindow > 0f ? new WaitForSeconds(_parryWindow) : null;
                    break;
            }
        }

        // ══════════════════════════════════════════════════════
        // Debuff — Apply (no separate Remove API; cleansed via RemoveStatuses path or expiration)
        // ══════════════════════════════════════════════════════

        public void ApplyDebuff(DebuffType type, float duration, float value)
        {
            if (!_isAlive || _runtimeStats == null) return;
            float effective = duration * (1f - _runtimeStats.DebuffDurationResistance);
            if (_debuffCoroutines.TryGetValue(type, out var existing) && existing != null)
                StopCoroutine(existing);
            _debuffCoroutines[type] = StartCoroutine(DebuffRoutine(type, effective, value));
            OnDebuffApplied?.Invoke(type, effective);
        }

        private IEnumerator DebuffRoutine(DebuffType type, float duration, float value)
        {
            ApplyDebuffValue(type, value);
            yield return new WaitForSeconds(duration);
            RevertDebuff(type);
            _debuffCoroutines.Remove(type);
            OnDebuffRemoved?.Invoke(type);
        }

        private void ApplyDebuffValue(DebuffType type, float value)
        {
            switch (type)
            {
                case DebuffType.DamageDown:
                case DebuffType.SelfDefenseDown:
                    _runtimeStats.DamageUpMultiplier = Mathf.Max(0f, _runtimeStats.DamageUpMultiplier - value / 100f); break;
                case DebuffType.DefenseDown:
                    _runtimeStats.DamageTakenMultiplier += value / 100f; break;
                case DebuffType.Mark:
                    _runtimeStats.VulnerabilityBonus += value; break;
            }
        }

        private void RevertDebuff(DebuffType type)
        {
            if (_baseStats == null) return;
            switch (type)
            {
                case DebuffType.DamageDown:
                case DebuffType.SelfDefenseDown:
                    _runtimeStats.DamageUpMultiplier = _baseStats.DamageUpMultiplier; break;
                case DebuffType.DefenseDown:
                    _runtimeStats.DamageTakenMultiplier = _baseStats.DamageTakenMultiplier; break;
                case DebuffType.Mark:
                    _runtimeStats.VulnerabilityBonus = _baseStats.VulnerabilityBonus; break;
            }
        }

        // ══════════════════════════════════════════════════════
        // Clear all timed effects (status/buff/debuff). Called on death/respawn.
        // ══════════════════════════════════════════════════════

        public void ClearAllEffects()
        {
            foreach (var kv in _statusCoroutines)
                if (kv.Value != null) StopCoroutine(kv.Value);
            _statusCoroutines.Clear();
            OnStatusBulkCleared?.Invoke();

            foreach (var kv in _buffCoroutines)
                if (kv.Value != null) StopCoroutine(kv.Value);
            _buffCoroutines.Clear();

            foreach (var kv in _debuffCoroutines)
                if (kv.Value != null) StopCoroutine(kv.Value);
            _debuffCoroutines.Clear();
            OnBuffDebuffBulkCleared?.Invoke();

            if (_parryCoroutine != null)
            {
                StopCoroutine(_parryCoroutine);
                _parryCoroutine = null;
            }
            _isParrying = false;

            if (_baseStats != null && _runtimeStats != null)
            {
                _runtimeStats.MoveControlMultiplier    = _baseStats.MoveControlMultiplier;
                _runtimeStats.DamageTakenMultiplier     = _baseStats.DamageTakenMultiplier;
                _runtimeStats.ReflectRatio              = _baseStats.ReflectRatio;
                _runtimeStats.HealingReceivedMultiplier = _baseStats.HealingReceivedMultiplier;
                _runtimeStats.DamageUpMultiplier         = _baseStats.DamageUpMultiplier;
                _runtimeStats.DefenseUpMultiplier        = _baseStats.DefenseUpMultiplier;
                _runtimeStats.VulnerabilityBonus         = _baseStats.VulnerabilityBonus;
            }

            _parryWindow = _baseParryWindow;
            _parryWait = _parryWindow > 0f ? new WaitForSeconds(_parryWindow) : null;
        }

        // ══════════════════════════════════════════════════════
        // Full reset for ML training episodes / scene reload.
        // ══════════════════════════════════════════════════════

        public void ResetForTraining()
        {
            foreach (var kv in _statusCoroutines)
            {
                if (kv.Value != null) StopCoroutine(kv.Value);
            }
            _statusCoroutines.Clear();
            OnStatusBulkCleared?.Invoke();

            foreach (var kv in _buffCoroutines)
                if (kv.Value != null) StopCoroutine(kv.Value);
            _buffCoroutines.Clear();

            foreach (var kv in _debuffCoroutines)
                if (kv.Value != null) StopCoroutine(kv.Value);
            _debuffCoroutines.Clear();
            OnBuffDebuffBulkCleared?.Invoke();

            if (_parryCoroutine != null)
            {
                StopCoroutine(_parryCoroutine);
                _parryCoroutine = null;
            }

            if (_baseStats != null && _runtimeStats != null)
            {
                _runtimeStats.MoveControlMultiplier     = _baseStats.MoveControlMultiplier;
                _runtimeStats.DamageTakenMultiplier      = _baseStats.DamageTakenMultiplier;
                _runtimeStats.ReflectRatio               = _baseStats.ReflectRatio;
                _runtimeStats.HealingReceivedMultiplier  = _baseStats.HealingReceivedMultiplier;
                _runtimeStats.DamageUpMultiplier          = _baseStats.DamageUpMultiplier;
                _runtimeStats.VulnerabilityBonus          = _baseStats.VulnerabilityBonus;

                if (_baseStats is BossStatsSO bossSO)
                {
                    _maxHP = bossSO.BossMaxHP;
                }
                else if (_baseStats is PlayerStatsSO playerSO)
                {
                    _maxHP     = playerSO.MaxHP;
                    _shieldMax = playerSO.ShieldMax;
                }
            }

            _currentHP     = _maxHP;
            _currentShield = 0f;
            _isAlive       = true;
            _isCasting     = false;
            _isParrying    = false;
        }

        // ══════════════════════════════════════════════════════
        // Internal utilities
        // ══════════════════════════════════════════════════════

        protected internal void SetHP(float value)
        {
            _currentHP = Mathf.Clamp(value, 0f, _maxHP);
            if (_currentHP <= 0f) _isAlive = false;
        }

        protected internal void SetShield(float value)
        {
            _currentShield = Mathf.Clamp(value, 0f, _shieldMax);
        }

        // ══════════════════════════════════════════════════════
        // Classification helpers
        // ══════════════════════════════════════════════════════

        private static bool IsDebuffStatus(StatusType type) =>
            type == StatusType.Slowed     || type == StatusType.Rooted     || type == StatusType.Stunned ||
            type == StatusType.HitStun    || type == StatusType.Vulnerable || type == StatusType.Silence ||
            type == StatusType.AntiHeal   || type == StatusType.Marked     || type == StatusType.DamageOverTime;

        private static bool IsDefenseBuff(BuffType type) =>
            type == BuffType.DefenseUp;

        private static bool IsOffenseBuff(BuffType type) =>
            type == BuffType.DamageUp || type == BuffType.ParryWindowUp || type == BuffType.ParryRewardUp;
    }
}
