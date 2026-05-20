# A4: MapBounds3D + Cleanup (2026-05-11)

ROADMAP item A4 — three medium-priority cleanup items:
- A4-1: `Vector3.zero` sentinel removed from rope chain (`bool hasAnchorHint` propagation)
- A4-2: Unused `ASSIST_WINDOW` constant deleted
- A4-3: Redundant `if (!networkIsRoping.Value)` wrapper removed

---

## Outcome

**Status**: APPLIED. Two Codex review rounds, final verdict APPROVED.

**Files changed**:
- `Assets/ArenaCombat/Scripts/Core/MapBounds3D.cs` — `TryResolveRopeTarget` signature + sentinel block
- `Assets/ArenaCombat/Scripts/Core/Network/PlayerNetworkController3D.cs` — `QueuedServerAction` struct, `SubmitRopeIntent`, `RequestRopeRpc`, `ExecuteQueuedRopeAction` call site, `TryResolveRopeCandidateTarget` signature + body, line 647 if wrapper
- `Assets/ArenaCombat/Scripts/Core/Network/CombatManager.cs` — `ASSIST_WINDOW` deletion
- `Assets/ArenaCombat/3DSceneScript/Scripts/RopeAction.cs:141` — caller passes `hasAnchorHint: true`

**Doc updates after apply**:
- `ROADMAP.md` — A4 marked DONE with sub-bullets, assist-window note for Phase B
- `PROJECT_STRUCTURE.md` §5.2 known-bug section updated (all three items resolved)
- Memory `project_known_bugs.md` updated

**Verification grep** after apply (`rg -n "anchorHint != Vector3\.zero|candidate == Vector3\.zero|ASSIST_WINDOW|if \(!networkIsRoping\.Value\)"`):
- All four patterns: **0 matches**

---

## Review Cycle Summary

### Round 1 — APPROVED WITH CHANGES (2 Critical)

Round 1 proposed:
- A4-1 Option A: add `bool hasAnchorHint` to `MapBounds3D.TryResolveRopeTarget` only, with caller computing `hasAnchorHint = anchorHint != Vector3.zero`
- A4-2 Option B: keep `ASSIST_WINDOW` constant + add TODO comment
- A4-3: remove redundant if wrapper (no options)

Codex critical issues:
- CI-A4-R1-1: Round 1 Option A diff just moved the sentinel check from callee to caller. Real fix requires propagating `bool hasAnchorHint` through the entire chain (SubmitRopeIntent, RequestRopeRpc, QueuedServerAction, TryResolveRopeCandidateTarget, MapBounds3D.TryResolveRopeTarget, RopeAction caller, fallback path).
- CI-A4-R1-2: A4-2 must be DELETE not keep+TODO. ROADMAP wording "미사용 상수 제거" requires actual removal. Design intent belongs in ROADMAP / PROJECT_STRUCTURE notes.

Codex questions:
- Q-A4-R1-1: Is RopeAction.cs the only external caller of SubmitRopeIntent?
- Q-A4-R1-2: Switch A4-2 to delete?

### Round 2 — APPROVED

Round 2 adopted both CIs:
- A4-1: full propagation through 7+ sites in 4 files
- A4-2: delete

Verified that `RopeAction.cs:141` is the only external caller of `SubmitRopeIntent` (one grep result).

Codex final notes:
- Method, no defaults, single caller — all green
- Perk `TargetHint` should NOT be added in this cycle (perk system is largely incomplete; protocol expansion premature)
- Recommended grep verification after apply (executed, all 0 matches)

---

## Key Code Changes

### A4-1: `bool hasAnchorHint` propagation

`QueuedServerAction` struct (PlayerNetworkController3D.cs:187):
```csharp
public Vector3 AnchorHint;
public bool HasAnchorHint;   // ← added
public Vector3 Direction;
```

`SubmitRopeIntent` (PlayerNetworkController3D.cs:513):
```csharp
public void SubmitRopeIntent(Vector3 anchorHint, Vector3 direction, bool hasAnchorHint)
```

`RequestRopeRpc` ([Rpc(SendTo.Server)], line 860):
```csharp
private void RequestRopeRpc(Vector3 anchorHint, Vector3 direction, bool hasAnchorHint, int clientTick)
```

`TryResolveRopeCandidateTarget` (line 774):
```csharp
private bool TryResolveRopeCandidateTarget(
    Vector3 origin,
    Vector3 anchorHint,
    bool hasAnchorHint,
    Vector3 direction,
    out Vector3 candidateTarget,
    out string detail)
```

Inside this method, the fallback path (no MapBounds3D) sentinel was replaced:
```csharp
// Before: return anchorHint != Vector3.zero;
// After:
if (!hasAnchorHint)
{
    candidateTarget = Vector3.zero;
    detail = "InvalidAnchorHint";
    return false;
}
candidateTarget = anchorHint;
detail = "Resolved";
return true;
```

`MapBounds3D.TryResolveRopeTarget` (MapBounds3D.cs:173):
```csharp
public bool TryResolveRopeTarget(
    Vector3 origin,
    Vector3 anchorHint,
    bool hasAnchorHint,
    Vector3 direction,
    float maxDistance,
    LayerMask anchorMask,
    out Vector3 resolvedTarget,
    out string detail)
```

Sentinel `if (candidate == Vector3.zero)` replaced with `if (!hasAnchorHint)`.

`RopeAction.cs:141` (caller):
```csharp
networkController3D.SubmitRopeIntent(hitPoint, direction.normalized, hasAnchorHint: true);
```

### A4-2: Delete `ASSIST_WINDOW`

`CombatManager.cs:54` — single-line removal. Surrounding `recentDamage` dictionary preserved for future assist tracking implementation.

### A4-3: Remove redundant if wrapper

`PlayerNetworkController3D.cs` ProcessServerMovement else branch:
```csharp
// Before
else
{
    targetYaw = serverLookYaw;
    if (!networkIsRoping.Value)
    {
        SetStateId(CharacterStateId.Idle);
    }
}

// After
else
{
    targetYaw = serverLookYaw;
    SetStateId(CharacterStateId.Idle);
}
```

Safe because the early return at line 622 guarantees `networkIsRoping.Value == false` past that point.

---

## RPC Wire Format Note

`RequestRopeRpc` adds a `bool` parameter, which changes the NGO 2.x serialized payload (parameters are serialized in declaration order). Old clients cannot talk to new servers and vice versa. Acceptable for current non-deployed state. Flag if external compatibility ever becomes a requirement.

---

## Spawned Follow-ups (none mandatory)

1. **Perk TargetHint sentinel pattern**: `QueuedServerAction.TargetHint` (perk path) still uses `Vector3` directly without `HasTargetHint` flag. Codex explicitly recommended NOT including this in A4 — perk system is largely incomplete (Phase B work). Watch for sentinel-pattern reintroduction when perk path is implemented.

2. **Phase B reintroduction of `ASSIST_WINDOW`**: when assist tracking is actually wired up (Phase B combat work), re-add `private const float ASSIST_WINDOW = 10f;` next to the `recentDamage` dict. Original design intent was 10-second damage attribution window for assist credit.

---

## Lessons

- Codex Round 1 critical (CI-A4-R1-1) caught a fake fix — `bool hasAnchorHint = anchorHint != Vector3.zero` looks like a fix but is just sentinel relocation. Without explicit caller assertion, sentinel detection still happens somewhere.
- Round 1 also flagged ROADMAP-wording inconsistency (CI-A4-R1-2): "미사용 상수 제거" entry can't be marked DONE if the constant remains.
- File-level transcription accuracy matters: my Round 2 pending.md mistyped `grappleRange` (actual: `ropeMaxDistance`) and `ropeAnchorMask` (actual: `ropeAnchorLayer`). Edit failed once because of this; recovered by re-reading the file. Per `feedback_accuracy_first.md`, transcribe from current file state, not from memory.
- Verification grep is cheap and high-value; running it post-apply caught nothing this cycle (all clean), but the negative result is itself valuable evidence.
