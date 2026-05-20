# X3-4: Position Control Routing — 2026-05-13

**Status**: APPLIED. Codex Round 1 APPROVED WITH CHANGES (1 critical + 4 suggestion, all applied).

## Edits (PNC3D only, ~35 lines net)

1. **3 ICombatant stub replacement**: Knockback / Pull / MoveBy → `ApplyPositionOffset(direction, distance)` private helper.
2. **ApplyPositionOffset helper**:
   - Direction zero-check (early return)
   - `target = transform.position + direction.normalized * distance`
   - `MapBounds3D.Instance?.ResolveServerPosition(target, lastValidatedServerPosition)` clamp
   - `rb.MovePosition(target)` + `lastValidatedServerPosition = target`
   - **`networkPosition.Value = target` immediate NV mirror** (Codex C-1)
3. **WarnX3Stub helper + _x3StubWarned HashSet removed** (Codex S-3, no remaining stub callers).

## Codex Critical Applied

- **C-1 NV mirror inside helper**: position control contract is `MovePosition + networkPosition.Value` sync, not just physics. Without immediate mirror, FixedUpdate sync would catch on next tick → 1-tick desync vs movement path. Helper now sets `networkPosition.Value = target` directly, matching rope action's pattern.

## Codex Suggestions Applied

- **S-1 Instant first, smooth later**: applied. Duration param accepted but ignored. Coroutine-based smooth motion deferred to X3-N.
- **S-2 MoveType.Rope deferred**: TODO comment, no special routing yet. Plain displacement same as Dash.
- **S-3 WarnX3Stub removal**: applied (no callers).
- **S-4 ApplyPositionOffset private instance helper**: applied (uses rb / lastValidatedServerPosition / networkPosition instance fields).

## Surface Verification

- `WarnX3Stub` references: 0 ✓
- `ApplyPositionOffset` references: 4 (helper definition + 3 stub call sites — Knockback / Pull / MoveBy) ✓

## Behavior

- Knockback: instant pushback along caster→target direction × distance.
- Pull: instant move toward `towardPosition` × distance. Duration ignored.
- MoveBy: instant displacement along arbitrary direction × distance. All 4 MoveType values identical for now.
- All operations server-only IsServer-gated. MapBounds clamp prevents out-of-arena push.

## Spawned Follow-ups

- **X3-5 NEXT**: SkillProjectile / SkillArea MonoBehaviour → NetworkBehaviour conversion + IsServer gates.
- **X3-N**: coroutine-based smooth motion for duration param (Pull / MoveBy).
- **X3-N**: MoveType branching — Rope routes through existing rope queue; Jump adds vertical physics; Dash/Charge differentiate speed/curve.

## User Verification

Unity recompile expected. No Inspector change (3 ICombatant explicit impls don't add fields). No MCP verification needed.
