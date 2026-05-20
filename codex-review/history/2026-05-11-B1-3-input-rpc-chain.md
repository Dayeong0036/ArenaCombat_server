# B1-3: Input → Server RPC → Queue → CombatManager3D Stub Wiring (2026-05-11)

ROADMAP item B1-3 — largest single sub-cycle so far. Wires 3D attack/parry from PlayerInputHandler events through PlayerNetworkController3D (Submit/Request RPC + queue) into CombatManager3D resolver stubs. Establishes the server contract that B1-4 hit logic builds on.

---

## Outcome

**Status**: APPLIED. Two Codex review rounds, final verdict APPROVED.

**Files modified**:
- `Assets/ArenaCombat/Scripts/Core/Network/PlayerNetworkController3D.cs` — 11 distinct regions
- `Assets/ArenaCombat/Scripts/Core/Network/CombatManager3D.cs` — 2 resolver stubs + 2 result RPCs

**No files created.** No legacy `CombatManager.cs` modification.

**Doc updates**:
- `ROADMAP.md` — B1-3 marked DONE with full sub-bullet list, B1-4 entry expanded with cooldown enforcement / cross-player parry / attack-recovery timer notes

---

## Review Cycle Summary

### Round 1 — APPROVED WITH CHANGES (3 Critical)

Round 1 proposed full B1-3 wiring with:
- `RequestAttackRpc` no AttackType validation
- `ExecuteQueuedParryAction` opens parry timer BEFORE manager call
- Priority constants implied cross-player ordering guarantee
- Reject RPC direction unspecified (defaulted to broadcast like perk)
- Field name `AttackType` (clashed with type name)
- `AttackResultRpc` no `hitTargets` array (would force B1-4 signature change)

Codex Critical Issues:
- **CI-B1-3-R1-1**: `RequestAttackRpc` missing `Enum.IsDefined` for AttackType — security hole
- **CI-B1-3-R1-2**: ExecuteQueued resolver failure has no rollback. Parry timer especially: opens BEFORE manager call → if manager fails, parry stays on
- **CI-B1-3-R1-3**: Priority `Parry=0...PerkTrigger=3` only orders WITHIN one player's queue. Same-tick cross-player parry-vs-attack ordering depends on Unity object processing — not deterministic. Documented guarantee was wrong.

Codex Suggestions (all adopted):
- S-1: Reject RPC = `[Rpc(SendTo.Owner)]`
- S-2: Field rename `AttackType` → `AttackKind`
- S-3: Add `ulong[] hitTargets` to `AttackResultRpc` from B1-3 (empty array)
- S-4: `CharacterStateId.Attacking` exists at `NetworkConstants.cs:48` — set on accept
- S-5: Bundle B1-3 in one cycle
- S-6: All RPC calls positional (no mixed named/positional)

Codex Question:
- Q-1: Cross-player parry — B1-4 plan?

### Round 2 — APPROVED

Round 2 adopted all 3 CIs:
- `Enum.IsDefined(typeof(AttackType), attackType)` added to `RequestAttackRpc`
- `ExecuteQueued*Action` checks `TryProcess*` return value, sends reject RPC on false
- Parry timer opens AFTER `TryProcessParry3D` returns true (rollback by not opening)
- `CombatManager3D.Instance == null` rejects parry too
- `B1-4 NOTE` comment in `ExecuteQueuedAttackAction` documenting cross-player parry caveat
- Same-tick cross-player parry **scope-down committed**: only "previously-opened parry" reliably defends. Global combat ordering would be a separate roadmap item if playtesting reveals it's needed.

All 6 Suggestions adopted.

Round 2 minor questions answered:
- `SetStateId(Attacking)` exit: don't add hack now; B1-4 attack recovery timer handles
- `Enum.IsDefined` cost: acceptable on rate-limited RPC entry
- `AttackData3D.Cooldown` enforcement → added to B1-4 scope (per Codex S-B1-3-R2-3)

---

## Final Code Shape

### PlayerNetworkController3D.cs (11 regions)

1. **Enum extension**: `Attack = 2, Parry = 3` added to `QueuedActionType`
2. **Struct field**: `public AttackType AttackKind;` (note: type=`AttackType`, field=`AttackKind` per Codex S-2)
3. **Parry config**: `[SerializeField] parryWindowDuration = 0.3f`, `parryCooldown = 0.6f` in new `[Header("=== Parry Settings (3D) ===")]` section
4. **Parry state**: `parryWindowTimer`, `parryCooldownTimer` server-side fields + `IsParrying => parryWindowTimer > 0f` accessor
5. **Input subscription**: `OnLightAttack/OnHeavyAttack/OnParry` added to `SubscribeInputEvents` + `UnsubscribeInputEvents`. Handler methods: `HandleLightAttack/HandleHeavyAttack/HandleParry` (single-line expression methods)
6. **Public APIs**: `SubmitAttackIntent(AttackType)` + `SubmitParryIntent()` mirror perk pattern (Owner+Spawned guard, CardDraft guard, localTick++, RequestRpc)
7. **RPCs**: `RequestAttackRpc` (with `Enum.IsDefined` validation, rate limit, tick order, enqueue) + `RequestParryRpc` (same shape minus type validation)
8. **Reject RPCs**: `[Rpc(SendTo.Owner)] AttackRejectedFromOwnerRpc(AttackType, string)` + `ParryRejectedFromOwnerRpc(string)` — owner-only feedback per S-1
9. **Queue dispatcher**: switch updated with `Attack`/`Parry` cases. Priority switch updated to `Parry=0, Rope=1, Attack=2, PerkTrigger=3` (with comment about within-player only)
10. **ExecuteQueued handlers**:
    - `ExecuteQueuedAttackAction`: full validation (CardDraft, alive, CanAct, !IsRoping, CombatManager3D null guard) → `TryProcessAttack3D` → `if (!accepted) reject + return` → `SetStateId(Attacking)`. Includes `B1-4 NOTE` comment about cross-player parry ordering.
    - `ExecuteQueuedParryAction`: same validation pattern → `TryProcessParry3D` → `if (!accepted) reject + return` → **THEN** open `parryWindowTimer` + `parryCooldownTimer` (Codex CI-B1-3-R1-2 fix)
11. **Lifecycle**:
    - `UpdateServerTimers`: `parryWindowTimer` and `parryCooldownTimer` decrement alongside `ropeCooldownTimer`
    - `Respawn`: clear both parry timers after `lastValidatedServerPosition = position`
    - `Die`: clear both parry timers after `isRopeMoving = false`
    - `OnNetworkSpawn` server: add `CombatManager3D.Instance.RegisterPlayer3D` (with `Instance != null` guard) alongside legacy CombatManager registration
    - `OnNetworkDespawn` server: add `CombatManager3D.Instance.UnregisterPlayer3D` alongside legacy

### CombatManager3D.cs (2 stubs + 2 RPCs)

```csharp
public bool TryProcessAttack3D(ulong attackerId, AttackType attackType, Vector3 attackerPosition, float attackerYaw, out string detail) {
    // Server-only. Validate attacker registration. Stub emits AttackResultRpc with empty hitTargets.
    AttackResultRpc(attackerId, (byte)attackType, true, 0, System.Array.Empty<ulong>(), "AcceptedStub");
    return true;
}

public bool TryProcessParry3D(ulong defenderId, out string detail) {
    // Server-only. Validate defender registration. Stub emits ParryStartedRpc.
    ParryStartedRpc(defenderId);
    return true;
}

[Rpc(SendTo.ClientsAndHost)]
private void AttackResultRpc(ulong attackerId, byte attackType, bool accepted, int hitCount, ulong[] hitTargets, string detail) { /* log */ }

[Rpc(SendTo.ClientsAndHost)]
private void ParryStartedRpc(ulong defenderId) { /* log */ }
```

---

## Key Architectural Decisions Recorded

1. **Cross-player parry ordering**: scope-down to "previously-opened parry" model. Global combat ordering deferred to potential follow-up.
2. **Reject RPC direction**: owner-only (`SendTo.Owner`). Only accepted gameplay events broadcast.
3. **Resolver failure handling**: ExecuteQueued checks return value, sends reject, early-returns. Parry timer opens AFTER success only (no rollback needed).
4. **AttackType field naming**: `AttackKind` (avoids type-name clash with field-name).
5. **RPC payload stability**: `AttackResultRpc(ulong[] hitTargets)` shipped from B1-3 with empty array to avoid signature churn in B1-4.

---

## Spawned B1-4 Requirements (captured in ROADMAP)

1. Replace `TryProcessAttack3D` stub with Physics.OverlapBox + LayerMask + filtering + dedup + parry check + damage via `TakeDamage`
2. **Game-side cooldown enforcement** using `AttackData3D.Cooldown` (separate from InputValidator rate limit) per Codex S-B1-3-R2-3
3. `appliedStatus` continues to honor only `StatusMask.None` (status duration model deferred)
4. Cross-player parry: hit logic accepts the within-player limitation; defender's parry must be opened in prior fixed-step
5. **Attack recovery timer** to handle `SetStateId(Attacking)` → Idle transition cleanly (S-B1-3-R2-1 noted "almost invisible" overwrite by ProcessServerMovement same fixed step)

---

## Lessons

- **Round 1 sentinel-of-the-week**: `priority constants imply cross-player ordering` was a hidden bug in my proposal. I documented "Parry first" as if it always worked, but the queues are per-player. Codex caught this with cross-cutting reasoning that pure code review (which sees one file at a time) would also catch but only when actually attempted.
- **Resolver-failure rollback** (CI-2) is a class of bug to remember: anytime an action has side effects (like opening a timer), open it AFTER the operation succeeds, not before.
- **`Enum.IsDefined`** as the canonical "validate this enum is in range" check, even on internal RPC handlers — wire-format validation matters.
- **B1-3 size**: ~210 added lines across 2 files. Two Codex rounds appropriate. Bundling was right call (S-5) — splitting would have created intermediate state where PlayerNetworkController3D references stub methods that don't exist yet.
- **Verification grep** post-apply showed PlayerInfoDisplay.cs:142 has `playerController.IsParrying` — that's for legacy `PlayerNetworkController` (2D type). My new `PlayerNetworkController3D.IsParrying` is a different type, no conflict. Grep with type context matters.
