using System;
using UnityEngine;
using ArenaCombat.Core.Combat;
using ArenaCombat.Core.Skill;
using ArenaCombat.Core.Stats;

// ══════════════════════════════════════════════════════════════════
// StateManager — single-combatant FSM cache.
//
// Per-entity MonoBehaviour component sitting alongside StatManager
// (RequireComponent). Reads StatManager timers + ICombatant flags every
// Tick to derive the canonical CombatantState. Holds NO authoritative
// state itself — everything is derived. All transitions funnel through
// the single ChangeState() gate so OnStateChanged / Entered / Exited
// events fire reliably.
//
// Resolution priority (strongest to weakest):
//   Dead > Stunned > HitStun > Parrying > Casting > Moving > Idle
//
// Skill cast allowed: state in { Idle, Moving } && !Status(Silence).
//
// SERVER AUTHORITY CONTRACT:
//   Mirrors StatManager — does not enforce server authority itself. Tick
//   should only be called from server code paths. Notify* methods mutate
//   StatManager state and must be gated by callers accordingly.
//
// Buildup origin: Assets/Scripts/State/StateManager.cs (X2-4 import).
// ══════════════════════════════════════════════════════════════════

namespace ArenaCombat.Core.State
{
    [RequireComponent(typeof(StatManager))]
    public class StateManager : MonoBehaviour
    {
        [Header("Debug (inspector readout)")]
        [SerializeField] private CombatantState _debugCurrent;
        [SerializeField] private CombatantState _debugPrevious;
        [SerializeField] private float          _debugTimeInState;

        [Header("Transition logging")]
        [SerializeField] private bool _logTransitions = false;

        // ── references ──────────────────────────────────────────
        private StatManager _stat;
        private ICombatant  _owner;   // for IsAlive / IsCasting / IsParrying queries

        // ── state cache (computed values only) ─────────────────
        public CombatantState CurrentState  { get; private set; } = CombatantState.Idle;
        public CombatantState PreviousState { get; private set; } = CombatantState.Idle;
        public float          TimeInState   { get; private set; } = 0f;

        // ── external Notify-receiver flag ──────────────────────
        private bool _movementInputActive;

        // ── derived capability flags ───────────────────────────
        public bool CanAct   => CurrentState != CombatantState.Dead
                             && CurrentState != CombatantState.Stunned
                             && CurrentState != CombatantState.HitStun;

        public bool CanMove  => (CurrentState == CombatantState.Idle || CurrentState == CombatantState.Moving)
                             && !_stat.HasStatus(StatusType.Rooted);

        // Skill cast — Idle / Moving only, blocked by Silence.
        public bool CanCast  => (CurrentState == CombatantState.Idle || CurrentState == CombatantState.Moving)
                             && !_stat.HasStatus(StatusType.Silence);

        public bool CanParry => (CurrentState == CombatantState.Idle || CurrentState == CombatantState.Moving)
                             && !_stat.HasStatus(StatusType.Silence);

        // ── events ──────────────────────────────────────────────
        public event Action<CombatantState, CombatantState> OnStateChanged;  // (prev, next)
        public event Action<CombatantState> OnStateEntered;
        public event Action<CombatantState> OnStateExited;

        // ══════════════════════════════════════════════════════
        // Initialization
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _stat = GetComponent<StatManager>();
        }

        // Controller binds itself as ICombatant.
        public void BindOwner(ICombatant owner) => _owner = owner;

        // ══════════════════════════════════════════════════════
        // External Notify API
        // ══════════════════════════════════════════════════════

        public void NotifyMovementInput(bool isMoving) => _movementInputActive = isMoving;

        // Cast begin / end — called by SkillManager or controller.
        // Toggles StatManager.SetCasting in lockstep so IsCasting stays consistent.
        public void NotifyCastStart()
        {
            _stat.SetCasting(true);
        }

        public void NotifyCastEnd()
        {
            _stat.SetCasting(false);
        }

        public void NotifyParryStart()
        {
            _stat.BeginParryWindow();
        }

        public void NotifyParryEnd()
        {
            _stat.EndParryWindow();
        }

        // ══════════════════════════════════════════════════════
        // Tick — called from owner controller's Update path.
        // ══════════════════════════════════════════════════════

        public void Tick(float dt)
        {
            TimeInState += dt;
            var computed = ComputeState();
            if (computed != CurrentState)
                ChangeState(computed);

            _debugCurrent     = CurrentState;
            _debugPrevious    = PreviousState;
            _debugTimeInState = TimeInState;
        }

        // Single-pass priority table — hierarchy applied automatically.
        private CombatantState ComputeState()
        {
            if (_owner == null) return CombatantState.Idle;

            if (!_owner.IsAlive)                            return CombatantState.Dead;
            if (_stat.HasStatus(StatusType.Stunned))        return CombatantState.Stunned;
            if (_stat.HasStatus(StatusType.HitStun))        return CombatantState.HitStun;
            if (_owner.IsParrying)                          return CombatantState.Parrying;
            if (_owner.IsCasting)                           return CombatantState.Casting;
            if (_movementInputActive)                       return CombatantState.Moving;
            return CombatantState.Idle;
        }

        // ══════════════════════════════════════════════════════
        // ChangeState — single gate for all transitions.
        // ══════════════════════════════════════════════════════

        private void ChangeState(CombatantState next)
        {
            if (next == CurrentState) return;

            var prev = CurrentState;
            OnStateExited?.Invoke(prev);
            HandleForceInterrupts(prev, next);

            PreviousState = prev;
            CurrentState  = next;
            TimeInState   = 0f;

            OnStateEntered?.Invoke(next);
            OnStateChanged?.Invoke(prev, next);

            if (_logTransitions)
                Debug.Log($"[StateManager:{name}] {prev} -> {next}");
        }

        // Side effects when a higher-priority state overrides a lower one.
        private void HandleForceInterrupts(CombatantState prev, CombatantState next)
        {
            // Force-end Casting / Parrying when a superior state (Stunned / HitStun / Dead) takes over.
            bool forcedBySuperior =
                next == CombatantState.Dead ||
                next == CombatantState.Stunned ||
                next == CombatantState.HitStun;

            if (forcedBySuperior)
            {
                if (prev == CombatantState.Casting)   _stat.SetCasting(false);
                if (prev == CombatantState.Parrying)  _stat.EndParryWindow();
            }
        }

        // ══════════════════════════════════════════════════════
        // Reset (ML episode / scene reload)
        // ══════════════════════════════════════════════════════
        public void ForceReset()
        {
            _movementInputActive = false;
            PreviousState = CurrentState;
            CurrentState  = CombatantState.Idle;
            TimeInState   = 0f;
        }
    }
}
