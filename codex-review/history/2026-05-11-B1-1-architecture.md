# B1-1: 3D Combat Pipeline Architecture (2026-05-11)

ROADMAP item B1-1 — architecture-only round. Locks in design decisions for the 5-sub-item B1 split. **No code change.** First architecture-only Codex cycle on this project.

---

## Outcome

**Status**: APPROVED. Three Codex review rounds. No code changed.

**Sub-items locked**:
- B1-2: CombatManager3D skeleton + AttackData3D ScriptableObject
- B1-3: Input subscription + Submit/Request RPC + queue extension + parry timer + ExecuteQueued + resolver stubs
- B1-4: TryProcessAttack3D full impl (Physics.OverlapBox + filtering + parry check + damage)
- B1-5: K/D/A in CombatManager3D + atomic Die() call replacement

**Documents updated**:
- ROADMAP.md — B1 section expanded with locked sub-items + forward suggestions for B1-3 / B1-4

**No code files changed.**

---

## Locked Architectural Decisions

| Decision | Outcome |
|---|---|
| **D1 Code home** | NEW `CombatManager3D.cs`. CombatManager.cs UNTOUCHED. Two singletons coexist with independent Awake() lifecycles. |
| **D2 Hit detection** | `Physics.OverlapBox` instant snapshot. B1-4 must include LayerMask + self/team/dead exclusion + QueryTriggerInteraction.Ignore + HashSet dedup. |
| **D3 Data model** | ScriptableObject `AttackData3D`. `[SerializeField] List<AttackData3D> attackTable` Inspector-assigned. NO Resources.Load. |
| **D4 Hurtbox** | Single root collider + LayerMask filter. No per-body-part. |
| **D5 Parry scope** | Owner = `PlayerNetworkController3D`. `parryWindowTimer` field (mirrors `ropeCooldownTimer`). Binary success. No direction/angle. |
| **D6 Queue integration** | Extend `QueuedActionType` (Attack/Parry) + `QueuedServerAction.AttackType` field. Per-player queue stays in PlayerNetworkController3D. CombatManager3D is resolver only. |
| **D7 actionPriority** | Same `clientTick`: Parry=0, Rope=1, Attack=2, PerkTrigger=3. Parry-first prevents same-tick attack-vs-parry ambiguity. |

---

## Review Cycle Summary

### Round 1 — APPROVED WITH CHANGES (2 Critical)

Round 1 proposed:
- New CombatManager3D, but K/D/A stays in CombatManager
- Attack/parry as latest-intent
- 5 sub-items B1-1 through B1-5

Codex Critical Issues:
- **CI-B1-R1-1** "K/D/A in CombatManager" defeats Option B's D1-separation purpose. CombatManager3D must own everything 3D.
- **CI-B1-R1-2** Attack/parry latest-intent drops inputs. Must use queue (rope/perk pattern).

### Round 2 — APPROVED WITH CHANGES (1 Critical)

Round 2 adopted both Round 1 CIs:
- CombatManager3D owns 3D K/D/A entirely. CombatManager.cs untouched.
- Queue used for attack/parry, integrated into existing QueuedServerAction.

Codex Critical Issue:
- **CI-B1-R2-1** Existing `Die()` at PlayerNetworkController3D.cs:1040-1043 already calls `CombatManager.Instance.OnPlayerDeath`. B1-5 must REPLACE (not augment) this call. Otherwise 3D deaths still leak into legacy CombatManager.

Codex Suggestions:
- S-1 Parry timer ownership = PlayerNetworkController3D (matches rope ownership pattern)
- S-2 actionPriority ordering = Parry < Rope < Attack < PerkTrigger
- S-3 B1-2 must include scene requirement note

### Round 3 — APPROVED

Round 3 adopted CI + all suggestions:
- B1-5 scope explicitly specifies replacement of the legacy call
- Decision 5 finalized parry timer on PlayerNetworkController3D
- Decision 7 added actionPriority constants
- B1-2 deliverable note added

Codex Suggestions (forward, for later sub-items):
- **S-B1-R3-1**: B1-3 — `parryWindowTimer` decrement should live in `UpdateServerTimers`. Clear on Respawn / Die / status reset.
- **S-B1-R3-2**: B1-4 — `AttackData3D.appliedStatus` apply requires status duration model that doesn't exist yet. In B1, only allow `StatusMask.None` or mark applied as deferred.

These are noted in ROADMAP B1-3 / B1-4 entries for the relevant sub-cycles.

---

## Files Anticipated to Change (across all B1 sub-items)

- **NEW**: `Assets/ArenaCombat/Scripts/Core/Network/CombatManager3D.cs`
- **NEW**: `Assets/ArenaCombat/Scripts/Core/Combat/AttackData3D.cs`
- **NEW** (manual, post-B1-2): `.asset` files for AttackData3D instances
- **MODIFY**: `Assets/ArenaCombat/Scripts/Core/Network/PlayerNetworkController3D.cs` (input subscription, Submit/Request RPCs, queue extension, ExecuteQueued handlers, parry timer, Die() replacement in B1-5)
- **NO CHANGE**: `Assets/ArenaCombat/Scripts/Core/Network/CombatManager.cs`

---

## Key Insight

This was the first **architecture-only** Codex cycle. No code touched, only design proposal. Three rounds were appropriate because:
- Round 1 surfaced the K/D/A ownership contradiction and the queue/latest-intent error in one pass
- Round 2 surfaced the existing Die() bridge that contradicted the new ownership decision
- Round 3 wrapped clean with no new issues

Lesson for future architecture cycles: when proposing scope changes (like "X owns Y"), grep for existing X-Y interactions in code before proposing — would have caught the Die() bridge in Round 1 instead of Round 2.

---

## What Comes Next

B1-2 begins immediately after this archive. New pending.md focuses on:
- `CombatManager3D.cs` skeleton (singleton, NetworkBehaviour lifecycle, registration APIs, attack table)
- `AttackData3D.cs` ScriptableObject class definition
- No hit logic, no RPCs, no input wiring (those are B1-3/B1-4)

User-side: B1-2 will produce a real code diff. Compile + smoke test required after apply.
