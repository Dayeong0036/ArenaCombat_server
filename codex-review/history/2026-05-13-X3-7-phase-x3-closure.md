# X3-7: Phase X3 Wiring Closure + Smoke Test Preflight — 2026-05-13

**Status**: APPLIED (doc-only). Codex Round 1 APPROVED WITH CHANGES (3 critical + 3 suggestion, all applied).

## Scope

**No code changes**. Pure doc-only closure round. All X3-1..6 code wiring complete.

## Codex Critical Applied

- **C-1 PHASE X3 COMPLETE deferred**: ROADMAP labeled "WIRING COMPLETE / RUNTIME SMOKE PENDING" pending user Play-mode host + 2P smoke test pass. Will flip to COMPLETE after user verifies 5 verification points.
- **C-2 Stale entries cleaned**: ROADMAP `X3-2 NEXT` → DONE entry. `X3-7 NEXT` → DONE entry. TARGET_ARCHITECTURE.md §10 X3-2 NEXT → full X3-1..7 status block.
- **C-3 Smoke test preflight checklist**: 6 designer setup items added to ROADMAP X3 closure block (before runtime verification can succeed).

## Codex Suggestions Applied

- **S-1 Documented vs verified complete split**: ROADMAP states wiring complete, runtime smoke pending.
- **S-2 CardManager GSM null warning**: noted in ROADMAP verification step #2 — if it fires draft UI stays idle, X3-6.1 patch trigger.
- **S-3 SKILL_SYSTEM_DESIGN.md AutoCastTick wording**: §9 tick rate row updated to reflect actual `SkillManager.Update` per-frame with server + draft gates; §12 "wiring" guidance updated. Future FixedUpdate extraction noted as not committed.

## Edited Files (doc only)

- `ROADMAP.md`: X3 section header + X3-2/X3-7 entries + closure block with preflight (6) and verification (5) checklists
- `TARGET_ARCHITECTURE.md` §10: X3-1..7 entries + "PHASE X3 COMPLETE pending runtime smoke" status
- `SKILL_SYSTEM_DESIGN.md` §9 (tick rate row) + §12 (wiring guidance) — current Update-based reality, deferred FixedUpdate extraction

## Smoke Test Preflight (Codex C-3)

Designer setup confirmed before Play-mode host match:
1. PlayerStatsSO assigned to Player A.prefab PNC3D Inspector
2. CardManager.allCards Inspector populated
3. AbilityCard.skillDefinition refs valid
4. SkillBinder.BindAll runs at game start (GameManager.Start auto-invokes)
5. SkillProjectile + SkillArea prefabs in NetworkManager.NetworkConfig.NetworkPrefabs
6. ProjectilePool / PersistentAreaPool / PersistentAreaManager wired in match scene

## Smoke Test Verification (Codex C-1)

User runs Play-mode host + 2P session and verifies:
1. New compile errors 0
2. CardManager event sub/unsub no NRE (GSM.Instance null warning absence)
3. During draft: PNC3D basic input + SkillManager auto-cast both blocked
4. CardSelectionResolved → both clients' same player slot state matches
5. Pool Spawn → Despawn(false) → re-Spawn cycle no exceptions

Verification pass → flip ROADMAP X3 section to "PHASE X3 COMPLETE", proceed to X4 (BossNetworkController3D + Boss FSM + ML-Agents).

## Phase X3 Wiring Summary

- 7 sub-cycles, 2026-05-12 ~ 2026-05-13
- Touched files: PNC3D + StatManager/StateManager/SkillExecutor/SkillManager attach + SkillProjectile/SkillArea/ProjectilePool/PersistentAreaPool/PersistentAreaManager NGO conversion + SkillComponents.LaunchProjectile null guard + CardManager full refactor + Player A.prefab migration + SkillManager.Update card-draft gate
- Net authority model: single damage flow (StatManager.ReceiveDamage), single death path (Die on sync hook transition), NGO projectile/area lifecycle, GSM-driven card draft
- Deferred to X3-N polish: Knockback/Pull/MoveBy duration coroutine, MoveType.Rope queue routing, StatusType↔StatusMask bridge, NetworkTransform per-prefab decision, smooth animation interpolation

## Phase X3 → X4 Handoff

Next phase: **X4 BossNetworkController3D**:
- Mirror PNC3D pattern (NetworkBehaviour + ICombatant impl + StatManager/StateManager/SkillExecutor/SkillManager attach + BossStatsSO Initialize)
- Boss FSM (B3 design + Buildup BossController reference)
- ML-Agents integration (X2-5 transfer plan: 12 trained .onnx + BossObservationCollector + 5 Agents + curriculum chain)
- Boss skill pool (RoleTag-filtered SkillRegistry subset)

Parallel work: **X1-6 / X1-7** Buildup `.asset` SO + Chapter1.unity scene import.
