# PHASE B1 — 3D COMBAT JUDGMENT PIPELINE (CLOSURE SUMMARY)

**Phase**: B1 (5 sub-cycles)
**Duration**: 2026-05-11 (single day, ~12 Codex rounds total)
**Status**: ✅ COMPLETE

This document consolidates the architectural decisions, code surface, and design contracts established across B1-1 through B1-5. Future B2/B3/B4 work should read this BEFORE diving into individual cycle history files.

---

## What B1 Delivered

A complete, server-authoritative 3D combat judgment pipeline:
- Player input (J/K/L) → owner intent RPC → server queue → `CombatManager3D` resolver → Physics.OverlapBox hit detection → `PlayerNetworkController3D.TakeDamage` damage application → K/D/A bookkeeping → result/death RPCs broadcast.
- Parry mechanic with binary success + attacker stun.
- Game-side cooldown enforcement separate from rate limiting.
- Atomic ownership transition: 3D combat side fully on `CombatManager3D`; legacy `CombatManager.cs` untouched.

---

## Files Created/Modified Across B1

### NEW
- `Assets/ArenaCombat/Scripts/Core/Combat/AttackData3D.cs` — ScriptableObject for per-attack data (B1-2)
- `Assets/ArenaCombat/Scripts/Core/Network/CombatManager3D.cs` — 3D combat hub (B1-2 → B1-5 progressive)

### MODIFIED
- `Assets/ArenaCombat/Scripts/Core/Network/PlayerNetworkController3D.cs` — input subscription, attack/parry intent + RPC chain, queue extension, parry timer, parry stun, registration, Die() flip, TakeDamage bool (B1-3 → B1-5)

### UNTOUCHED (per architectural decision D1)
- `Assets/ArenaCombat/Scripts/Core/Network/CombatManager.cs` — legacy 2D combat survives intact for the legacy 2D path; Phase D1 will remove cleanly without affecting 3D code.

---

## Locked Architectural Decisions (B1-1)

| ID | Decision | Rationale |
|---|---|---|
| **D1** | NEW `CombatManager3D.cs`; legacy `CombatManager.cs` UNTOUCHED. Two singletons coexist with independent Awake() lifecycles. | Phase D1 (legacy 2D removal) becomes trivial — delete `CombatManager.cs` without touching `CombatManager3D`. Avoids the legacy-2D-modification ban. |
| **D2** | `Physics.OverlapBox` instant snapshot at attack moment with full filtering: LayerMask + self exclusion + team exclusion + dead exclusion + `QueryTriggerInteraction.Ignore` + HashSet dedup + registry validation. | Simplest sufficient mechanism for snappy melee. Active-frame semantics deferred. Registry validation (CI-B1-4-R1-2) closes the "stale PNC3D on layer takes damage" gap. |
| **D3** | `AttackData3D` ScriptableObject; `[SerializeField] List<AttackData3D> attackTable` Inspector-assigned; `Lookup(AttackType) → AttackData3D?`; lazy cache built on `OnNetworkSpawn`. | Anticipates B2 (skill data) and B3 (boss patterns) reuse pattern. NO `Resources.Load` dependency. Caller treats null as request rejection (no fallback defaults). |
| **D4** | Single root collider per player + LayerMask filter. No per-body-part hurtboxes. | Top-down 2P co-op vs boss doesn't need per-limb specificity. |
| **D5** | Parry: `parryWindowTimer` owned by `PlayerNetworkController3D` (mirrors `ropeCooldownTimer`). Binary success — no direction/angle. CombatManager3D resolver queries `target.IsParrying` during hit resolution. | Matches existing per-player transient state pattern (rope/invulnerability timers). Direction/angle is animation-polish concern, deferred. |
| **D6** | Extend existing `QueuedActionType` enum + `QueuedServerAction` struct (Attack + Parry cases, AttackKind field). Per-player queue stays in PlayerNetworkController3D. CombatManager3D is resolver only. | Single per-player queue keeps ordering coherent (rope/perk/attack/parry interleave correctly). CombatManager3D doesn't get a competing queue. |
| **D7** | `actionPriority` ordering for same `clientTick`: **Parry=0, Rope=1, Attack=2, PerkTrigger=3**. WITHIN-PLAYER ONLY. | Same-player parry-then-attack resolves parry first. Cross-player ordering NOT guaranteed (different objects, separate queues, Unity processing order). Documented in code as B1-4 NOTE. |

---

## Sub-cycle Summary

### B1-1 — Architecture (3 Codex rounds, no code)
- 7 architectural decisions locked (above table).
- Sub-item split locked: B1-2 through B1-5.
- Forward suggestions captured for B1-3 (parry timer in UpdateServerTimers + clear sites) and B1-4 (`AttackData3D.appliedStatus` deferred until status duration model exists).

### B1-2 — CombatManager3D + AttackData3D Skeleton (2 rounds)
- 2 NEW files. Pure skeleton — no hit logic, no RPCs, no caller wiring.
- `[RequireComponent(typeof(NetworkObject))]` on CombatManager3D (S-B1-2-R1-1).
- Forward note for B1-3: registration wiring needs both Register AND Unregister symmetry (CI-B1-2-R1-2).

### B1-3 — Input Subscription + RPC Chain + Queue Extension + Stubs (2 rounds)
- Largest single sub-cycle (~210 lines).
- Wired `PlayerInputHandler.OnLightAttack/OnHeavyAttack/OnParry` → `Submit*Intent` → `Request*Rpc` → queue → `ExecuteQueued*Action` → `CombatManager3D.TryProcess*` stubs → result RPCs.
- Critical fixes from review: AttackType `Enum.IsDefined` validation (CI-1), resolver-failure rollback / parry timer opens AFTER success (CI-2), within-player priority NOT cross-player guarantee (CI-3 scope-down).
- Reject RPC contract established: gameplay reject = `[Rpc(SendTo.Owner)]`, resolved combat events = broadcast.
- Field name `AttackKind` (not `AttackType`) to avoid type-name clash.

### B1-4 — Physics.OverlapBox Hit Logic + Cooldown + Parry Handling (3 rounds)
- Full TryProcessAttack3D implementation. Replaces B1-3 stubs with real damage flow.
- Critical fixes: `parryStunDuration <= 0f` early return guard (CI-1), registry validation in filter loop (CI-2), `attackCooldowns3D.Clear()` on despawn (R2-CI-1).
- Suggestions absorbed: `[RequireComponent]` warning if `playerLayer.value == 0` at spawn, `maxHitTargets` cap applied to RPC payload only (not damage), reject duplication removed (early reject = owner-only).
- Parry semantics: any-parrier-blocks-all (2D mirror), attacker stunned via new `parryStunTimer` (one-off pattern, mirrors `invulnerabilityTimer`).
- New Phase B Followup spawned: Team assignment (friendly fire prevention before B3).

### B1-5 — K/D/A + Atomic Die Flip + hitTargets Refinement (2 rounds)
- Final B1 sub-cycle.
- `TakeDamage` signature `void → bool` (only 1 caller affected).
- `Die()` atomic flip from legacy `CombatManager.OnPlayerDeath` to `CombatManager3D.OnPlayerDeath3D`. Legacy bridge fully cut.
- K/D/A infrastructure: per-player counters, `DamageAttribution` struct (cumulative + lastDamageTime), `assistWindow3D = 10f`, `assistDamageThreshold = 10f`.
- Critical fixes: `if (t.IsAlive) TrackDamageForAssist3D` gate prevents fatal-hit ordering corruption (CI-1), kill-zone scope-down to direct respawn (CI-2 — captured as Phase B Followup).
- New Phase B Followup spawned: kill-zone scored death conversion.

---

## Phase B Followups (Captured during B1, NOT closed)

| # | Item | Source | Priority |
|---|------|--------|----------|
| 1 | **Team assignment / friendly fire** — `PlayerNetworkController3D.SetTeam` not called from session/lobby flow | B1-4 (Codex S-B1-4-R2-3) | **HIGH — required before B3** |
| 2 | **Kill-zone scored death** — convert kill-zone Respawn to Die(OwnerClientId) if design wants kill-zone to count | B1-5 (Codex CI-B1-5-R1-2) | OPT-IN (game design call) |
| 3 | **`appliedStatus` warning 1x gate** — designer-set non-None spam | B1-4 (Codex S-B1-4-R3-3) | LOW polish |
| 4 | **Source-tagged status duration model** — generalize one-off timers (invulnerability, parryStun, future) | B1-4/B1-5 | Phase B+ |

---

## Code Surface Snapshot (After B1)

### `CombatManager3D` (single class, all 3D combat surface)
- **Lifecycle**: `Awake` (singleton), `OnNetworkSpawn` (lookup build + layer warning), `OnNetworkDespawn` (clear all state).
- **Registry**: `players3D` dict + `Register/Unregister/GetPlayer3D` API.
- **Hit detection**: `playerLayer` LayerMask + `attackVerticalOffset` + `maxHitTargets` cap.
- **Cooldowns**: `attackCooldowns3D` per-attacker per-AttackType; `IsAttackOnCooldown` / `SetAttackCooldown` helpers.
- **K/D/A**: `kills3D` / `deaths3D` / `assists3D` / `recentDamage3D` (with `DamageAttribution` struct); `OnPlayerDeath3D` / `ComputeAssisters3D` / `TrackDamageForAssist3D`; public `GetKills3D` / `GetDeaths3D` / `GetAssists3D` / `GetKDAString3D` accessors.
- **Resolvers**: `TryProcessAttack3D` (full Physics.OverlapBox + filter chain + parry check + damage + cooldown + RPC), `TryProcessParry3D` (registry confirm + ParryStartedRpc).
- **Result RPCs (broadcast)**: `AttackResultRpc(attackerId, attackType, accepted, hitCount, hitTargets, detail)`, `ParryStartedRpc(defenderId)`, `ParrySuccessRpc(defenderId, attackerId)`, `PlayerKilled3DRpc(killerId, victimId, assisters)`.

### `PlayerNetworkController3D` (B1 additions)
- **Properties**: `IsParrying` (B1-3), `PlayerCollider` (B1-4).
- **Parry config**: `parryWindowDuration`, `parryCooldown`, `parryStunDuration` (Min(0) — 0 disables stun).
- **Server-side timers**: `parryWindowTimer`, `parryCooldownTimer`, `parryStunTimer` — all decrement in `UpdateServerTimers`, all cleared in `Respawn()` and `Die()`.
- **Public API**: `SubmitAttackIntent(AttackType)`, `SubmitParryIntent()`, `ApplyParryStun()`.
- **RPCs**: `RequestAttackRpc` (with `Enum.IsDefined`), `RequestParryRpc`, `[Rpc(SendTo.Owner)] AttackRejectedFromOwnerRpc` / `ParryRejectedFromOwnerRpc`.
- **Queue extension**: `QueuedActionType.Attack/Parry`, `QueuedServerAction.AttackKind`, `ExecuteQueuedAttackAction`/`ExecuteQueuedParryAction`.
- **Lifecycle**: `OnNetworkSpawn` server registers with both legacy CombatManager AND CombatManager3D (transition state); `OnNetworkDespawn` unregisters from both.
- **Damage**: `TakeDamage` returns bool (post-B1-5).
- **Death**: `Die()` calls `CombatManager3D.OnPlayerDeath3D` exclusively (post-B1-5 atomic flip).

### `AttackData3D` ScriptableObject
- Fields: `AttackType`, `DamageType`, `damage`, `range`, `hitboxHalfExtents` (Physics.OverlapBox API name), `cooldown`, `appliedStatus` (B1 honors `None` only).
- Authored via `Assets > Create > ArenaCombat > Combat > AttackData3D`.

---

## Death Contract (Important for B3)

After B1-5, K/D/A counters move only on **HP-zero combat death** (TakeDamage damage cascade through Die):
- ✅ Combat damage → HP=0 → Die → OnPlayerDeath3D → death++ + kill credit
- ✅ Self-damage death (future skill) → Die with killerId == OwnerClientId → death++, no kill credit
- ✅ Killer disconnects mid-combat → death++ + assists computed; kill credit skipped
- ❌ Kill-zone fall (current): Respawn direct → no K/D/A change (Phase B Followup #2 if changed)

This contract is load-bearing: B3 boss work assuming "every death = scored death" would break for kill-zone scenario. Either fix Phase B Followup #2 first OR design boss arena without kill zones.

---

## Lessons Distilled From B1

1. **Architecture-first sub-cycle is worth it for big phases.** B1-1 (3 rounds, no code) caught design issues that would have been very expensive to fix mid-implementation.
2. **Codex catches cross-cutting issues code review alone misses.** Examples: cross-player parry ordering (CI-B1-3-R1-3), kill-zone bypassing Die (CI-B1-5-R1-2), fatal-hit assist ordering (CI-B1-5-R1-1) — all required reasoning across multiple files / multiple time-points.
3. **"Min(0f)" doesn't mean "should be > 0".** `parryStunDuration` Min(0) accepted 0 → permanent stun. Defensive: `if (X <= 0f) return;` early guard for "feature disabled at this value" semantics.
4. **`if (t.IsAlive)` after a damage call is a useful idiom** for "do thing X only when target survives my modification". Comes up whenever a method may trigger a death cascade.
5. **Atomic flips are clean when legacy is one block.** A2 needed local-var refactor first because rb.position had multiple sites; B1-5 just swapped CombatManager → CombatManager3D in one block.
6. **Reject RPC direction matters.** Owner-only for gameplay rule rejects (cooldown, rate limit, missing data) prevents broadcast spam to non-requesters. Resolved combat events stay broadcast.
7. **Wire format / ScriptableObject field stability ships from day 1.** B1-3 added `ulong[] hitTargets` (always empty) to AttackResultRpc so B1-4 didn't need a signature change. Saved a Codex round.
8. **Document scope-outs as deliverables.** Kill-zone-not-scored is a B1-5 design decision, not a TODO. The contract section above captures this so B3 work doesn't accidentally assume otherwise.

---

## Recommended Next Step

**Phase B Followup #1 (Team assignment) before B2 or B3.** Friendly fire is currently active in B1-5 smoke test. B3 boss work would need to spawn the boss as a different team than players; that's the natural moment to wire team assignment in lobby/spawn flow.

If user prefers feature-forward, B2 (ISkillAction composite tree) is also an option — it doesn't require team assignment to test single-player skill flows.

A2-followup runtime test (rope/bounds re-push) is independent and can happen any time the user runs Unity in playtest.
