# X1-2: ShaderGraph_Dissolve + Dark Ghosts FREE Import (2026-05-11)

ROADMAP item Phase X1-2. Second Buildup asset import, **first batch with C# scripts**. Two folders bundled — both pre-verified for class/namespace conflicts.

---

## Outcome

**Status**: APPLIED. One Codex review round, APPROVED WITH CHANGES (1 Critical adopted).

**Operations** (sequential PowerShell):
```powershell
Copy-Item -LiteralPath "C:\Users\paek6\Downloads\Buildup\Buildup\Assets\ShaderGraph_Dissolve" `
          -Destination "C:\Users\paek6\ArenaCombat6\Assets\ShaderGraph_Dissolve" -Recurse
Copy-Item -LiteralPath "C:\Users\paek6\Downloads\Buildup\Buildup\Assets\Dark Ghosts FREE" `
          -Destination "C:\Users\paek6\ArenaCombat6\Assets\Dark Ghosts FREE" -Recurse
```

**Created**:
- `Assets/ShaderGraph_Dissolve/` — 35 non-meta + 43 meta (URP/, Utility/Scripts/, readme.pdf)
- `Assets/Dark Ghosts FREE/` — 36 non-meta + 59 meta (Animations/, Material/, Meshes/, Prefabs/, Scenes/, Scripts/, Texture/)

Total: 71 non-meta + 102 meta files, 24.1MB.

**Patched** (post-import per Codex CI-X1-2-R1-1):
- `Assets/ShaderGraph_Dissolve/Utility/Scripts/DissolveOffest.cs` lines 1-3 → added `using UnityEngine.InputSystem;`
- Same file line 29 → `Input.GetKeyDown(KeyCode.I)` → `Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame`
- Comment annotation: `X1-2 (Codex CI-X1-2-R1-1)`

**Verification grep** post-patch: zero `Input.GetKey/GetAxis/GetMouseButton/mousePosition/GetButton/anyKey` in either imported folder.

**Doc updates**:
- ROADMAP X1-2 → DONE, X1-3 (QuarterView player mesh) → NEXT.

---

## Review Cycle Summary

### Round 1 — APPROVED WITH CHANGES (1 Critical, all suggestions adopted)

Critical Issue:
- **CI-X1-2-R1-1**: `DissolveOffest.cs:29` used Old Input API (`Input.GetKeyDown(KeyCode.I)`). Project policy is New Input System only (Active Input Handling = 1). Old API throws at runtime. Fix: `using UnityEngine.InputSystem;` + `Keyboard.current.iKey.wasPressedThisFrame`. Adopted.

Suggestions (all confirmed/adopted):
- S-1: Two folders bundle OK with higher verification given C# content — done
- S-2: Top-level placement + .meta preservation — confirmed
- S-3: Sequential Copy-Item + per-destination pre-flight — done
- S-4: File counts match (35/43, 36/59) — verified post-copy
- S-5: `anim_clip_offset.cs` NRE risk noted but not blocker (defensive code at boss prefab usage point)

---

## Class / Namespace Conflict Pre-Check (verified before import)

| Buildup class | Our project search result | Status |
|---|---|---|
| `class DissolveChilds` | 0 hits | OK (DissolveExample namespace) |
| `class DissolveOffest` | 0 hits | OK (DissolveExample namespace) |
| `class Follow` | 0 hits (FollowMouseInstant exists, different name) | OK (DissolveExample namespace) |
| `class Rotator` | 0 hits | OK (DissolveExample namespace) |
| `class RotatorDissolveDir` | 0 hits | OK (DissolveExample namespace) |
| `class anim_clip_offset` | 0 hits | OK (namespace_animclip_offset) |

All 6 imported scripts in dedicated namespaces — zero collision risk.

---

## Final Patch Diff (DissolveOffest.cs)

```csharp
// BEFORE (line 1-3 + line 29)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// ...
            if (Input.GetKeyDown(KeyCode.I))

// AFTER
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;   // X1-2: ArenaCombat6 uses New Input System (Active Input Handling=1). Old UnityEngine.Input throws at runtime.
// ...
            // X1-2 (Codex CI-X1-2-R1-1): Old Input.GetKeyDown(KeyCode.I) replaced with New Input System.
            if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame)
```

---

## User-Side Verification (pending — user confirms in Unity)

1. Unity Editor focus → auto-import (~30s-2min for 24MB).
2. Console:
   - **Acceptable**: shader compile / URP fallback warnings. 6 utility scripts compile silently.
   - **Unacceptable**: any C# compile error. (Patch eliminated `Input.X` runtime risk.)
3. Project window: `Assets/ShaderGraph_Dissolve/` + `Assets/Dark Ghosts FREE/` visible.
4. Optional: drag a Dark Ghosts ghost prefab into a test scene → confirm renders. Drag a dissolve material onto a test mesh → confirm shader works.
5. 3DScene + SampleScene regression check.
6. Pre-existing warnings (CS0108 in MasterStylizedProjectiles, CS0618 obsolete RpcAttribute.RequireOwnership in CombatManager/GameStateManager, CS0067 unused OnTeamScoreChanged) should NOT increase.

---

## Spawned Follow-ups (none mandatory)

- `anim_clip_offset.cs` defensive null-Animator check — only relevant when boss prefab actually attaches it. Defer to first usage.
- URP shader visual check — defer to actual usage (per X1-1 S-5 pattern).

---

## Lessons

- **First C# import precedent set**: Old Input API is automatic-flag for any Buildup script. Pre-import grep `Input\.` on candidate scripts can catch this before Codex review.
- **Sequential PowerShell Copy-Item works fine for filesystem ops** — no parallelism complexity.
- **Codex catching `Input.GetKeyDown` even in a small utility script** is value-add — easy to miss in casual review of "small import batch".
- **Namespace-wrapped third-party scripts** are safe co-imports when our project uses default namespace. Conflict surface is much smaller than expected.
- **Patch comment trail** (`X1-2 (Codex CI-X1-2-R1-1)`) leaves clear historical reference for future maintainers.
