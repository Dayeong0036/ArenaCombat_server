# B1-5: K/D/A + Atomic Die() Flip + hitTargets Refinement (2026-05-11)

ROADMAP item B1-5 — final B1 sub-cycle. Closes Phase B1.

---

## Outcome

**Status**: APPLIED. Two Codex review rounds, final verdict APPROVED.

**Files modified**:
- `Assets/ArenaCombat/Scripts/Core/Network/PlayerNetworkController3D.cs` — `TakeDamage` signature `void → bool`, removed unused `oldHP` local, `Die()` atomic flip from `CombatManager.Instance.OnPlayerDeath` to `CombatManager3D.Instance.OnPlayerDeath3D`
- `Assets/ArenaCombat/Scripts/Core/Network/CombatManager3D.cs` — full K/D/A infrastructure (config, dicts, struct, OnPlayerDeath3D, ComputeAssisters3D, TrackDamageForAssist3D, accessors, PlayerKilled3DRpc) + resolver damage loop refined to "actually damaged" semantics

**No legacy CombatManager.cs modification.**

**Doc updates**:
- `ROADMAP.md` — B1-5 marked DONE, **B1 PHASE COMPLETE** marker added, Phase B Followups appended with kill-zone scored death note
- B1 closure summary doc written to `codex-review/B1_PHASE_SUMMARY.md`

---

## Review Cycle Summary

### Round 1 — APPROVED WITH CHANGES (2 Critical)

Round 1 proposed full K/D/A infrastructure + atomic Die flip + TakeDamage bool refinement, with 8 questions for Codex.

Codex Critical Issues:
- **CI-B1-5-R1-1** Fatal-hit assist tracking ordering bug. TakeDamage triggers Die→OnPlayerDeath3D synchronously, which clears recentDamage3D[victim]. Round 1 then called TrackDamageForAssist3D AFTER → re-populated cleared log with stale entries that would corrupt next death's assist computation.
- **CI-B1-5-R1-2** Kill-zone path uses Respawn() direct, not Die() → bypasses OnPlayerDeath3D. Round 1 self-kill smoke test would never fire. Either route through Die (behavior change) or scope-out kill-zone.

Codex Suggestions adopted:
- S-5: ComputeAssisters3D skip unregistered attackers (defensive against disconnected players)
- S-6: typo `kililerCounts` → `killerCounts`
- S-1/2/3/4 (TakeDamage bool / Time.time / struct / OnPlayerKill skip): all confirmed

### Round 2 — APPROVED

Round 2 adopted both CIs:
- `if (t.IsAlive) TrackDamageForAssist3D(...)` gate — only non-fatal hits track damage; fatal hits credit via Die→OnPlayerDeath3D(killerId) directly
- **Scope decision** on kill-zone: NOT routed through Die() in B1-5. Documented as direct bounds respawn (not scored). Captured as Phase B Followup if game design wants kill-zone to count later.

Codex final notes (Suggestions for follow-through):
- Remove unused `oldHP` local in TakeDamage while modifying for bool — done
- Document kill-zone negative test in ROADMAP/history — done (Death contract = combat death only)
- Write B1 closure summary — done (`B1_PHASE_SUMMARY.md`)

---

## Final Code Shape

### PlayerNetworkController3D additions

```csharp
// Signature change with caller-relevant bool semantics
public bool TakeDamage(float damage, ulong attackerId, DamageType damageType = DamageType.Physical) {
    if (!IsServer) return false;
    if (!networkIsAlive.Value) return false;
    if (invulnerabilityTimer > 0f || !StatusHelper.CanTakeDamage(networkStatusMask.Value)) return false;
    networkHP.Value = Mathf.Max(0f, networkHP.Value - damage);
    DamageEventRpc(damage, attackerId, (byte)damageType);
    if (networkHP.Value <= 0f) Die(attackerId);
    else if (StatusHelper.CanBeInterrupted(networkStatusMask.Value)) SetStateId(CharacterStateId.Hit);
    return true;
}

// Atomic flip in Die() — one block replacement
if (CombatManager3D.Instance != null) {
    CombatManager3D.Instance.OnPlayerDeath3D(OwnerClientId, killerId);
}
```

### CombatManager3D additions

- Config: `assistWindow3D = 10f`, `assistDamageThreshold = 10f`
- State: `kills3D`, `deaths3D`, `assists3D` (Dictionary<ulong, int>) + `recentDamage3D` (Dictionary<ulong, Dictionary<ulong, DamageAttribution>>)
- `DamageAttribution` private struct (cumulative + lastDamageTime)
- `OnPlayerDeath3D(victimId, killerId)`: death++, kill credit (skipped on self-kill or unregistered killer), assists via ComputeAssisters3D, clear victim log, broadcast PlayerKilled3DRpc
- `ComputeAssisters3D`: window + threshold + exclude killer + skip unregistered
- `TrackDamageForAssist3D(attackerId, victimId, damage)` private helper, called from resolver only on `t.IsAlive`
- `EnsurePlayerSessionBuckets3D` — init all 4 dicts on first touch
- Public accessors `GetKills3D / GetDeaths3D / GetAssists3D / GetKDAString3D`
- `RegisterPlayer3D` calls `EnsurePlayerSessionBuckets3D`
- `OnNetworkDespawn` clears all 4 K/D/A dicts (manager going away → fresh state)
- `UnregisterPlayer3D` keeps K/D/A (session display); only removes attackCooldowns3D
- `[Rpc(SendTo.ClientsAndHost)] PlayerKilled3DRpc(killerId, victimId, ulong[] assisters)`

### Resolver damage loop (TryProcessAttack3D modification)

```csharp
// Build "actually damaged" list using TakeDamage's new bool return
List<PlayerNetworkController3D> actuallyDamaged = new List<PlayerNetworkController3D>();
foreach (var t in eligibleHits) {
    if (t.TakeDamage(data.Damage, attackerId, data.DamageType)) {
        actuallyDamaged.Add(t);
        // CI-B1-5-R1-1: only track non-fatal damage. Fatal hit's Die→OnPlayerDeath3D
        // already cleared recentDamage3D[t]; re-tracking would re-populate with stale data.
        if (t.IsAlive) {
            TrackDamageForAssist3D(attackerId, t.OwnerClientId, data.Damage);
        }
    }
}

// hitTargets payload from actuallyDamaged (not eligibleHits) — refinement per S-B1-4-R3-1
int hitCount = actuallyDamaged.Count;
// ... rest of cap + RPC payload ...
```

---

## Death Contract After B1-5

**Locked behavior**:
- HP-zero combat death (via TakeDamage) → Die() → OnPlayerDeath3D → K/D/A updated, PlayerKilled3DRpc broadcast
- Kill-zone fall → Respawn() direct → no K/D/A change, no PlayerKilled3D broadcast
- Self-damage death (e.g., future self-damage skill) → Die() with killerId == OwnerClientId → death++, no kill credit (handled in OnPlayerDeath3D)
- Killer disconnects mid-combat then victim dies → death++ + assists computed, kill credit skipped (players3D registry check)

This contract is load-bearing for Phase B Followups (kill-zone scored death conversion is OPT-IN, not retroactive).

---

## Spawned Phase B Followups

1. **Kill-zone scored death** (NEW, per CI-B1-5-R1-2): convert FixedUpdate kill-zone branch from `Respawn()` to `Die(OwnerClientId)` if game design wants kill-zone to count toward Death/KDA. Side effect: respawn timer wait that direct Respawn skips.
2. **Team assignment** (carried from B1-4): Lobby slot → TeamId mapping. Required before B3.
3. **appliedStatus warning first-time-only gate** (carried from B1-4): polish.
4. **Source-tagged status duration model** (carried from B1-4): generalizes invulnerabilityTimer + parryStunTimer + future stun.

---

## Lessons

- **Synchronous death cascade**: TakeDamage → Die → OnPlayerDeath3D happens synchronously inside the `t.TakeDamage(...)` call. Easy to miss when reasoning "I'll do X right after the call". Code-locality bias: assume callee may have side effects up the stack.
- **`if (t.IsAlive)` after damage call**: simple post-call gate is the right shape for "do thing X only when target is still standing after my modification". Use this pattern any time a method might trigger a death cascade.
- **Scope decisions count as B1 deliverables**: kill-zone choice (B1-5 doesn't expand to it) is part of the design surface this cycle defines, not just a TODO. Documenting scope-out is as important as documenting scope-in.
- **Atomic flips with single-line replacement** are clean when the legacy call is one statement. Compare to A2 where `rb.position` had multiple sites — that needed local-var refactor first. B1-5's `CombatManager → CombatManager3D` flip is just a name swap on one block.
- **B1 closure summary** is worth writing even though no future Codex round will read it. Future-Claude-or-Codex reading this in B2/B3 will appreciate the consolidated decision trail vs. archaeologizing 5 history files.

---

## What Comes Next

User-side verification (B1-5 specific):
1. Compile in Unity.
2. Same scene setup as B1-4.
3. **Kill flow**: HP-zero death → `[CombatManager3D] PlayerKilled killer=N victim=M` log. **NO** `[CombatManager] Kill: ...` legacy log (atomic flip verification).
4. **K/D/A**: queryable via `CombatManager3D.Instance.GetKDAString3D(clientId)`.
5. **hitTargets refinement**: invulnerable target (e.g., post-respawn invuln) hit by attack → `hits=0` (was `hits=1` in B1-4 eligible-only).
6. **Kill-zone negative test**: walk into kill zone → respawn → K/D/A unchanged, no PlayerKilled3D log.
7. **Assist scenario**: 2-attacker fatal flow (when implementable in test harness).

After verification, **Phase B1 is closed**. User picks next priority:
- A2-followup runtime test (close stabilization 100%)
- Phase B Followups (Team assignment recommended before B3)
- B2 (ISkillAction composite tree)
