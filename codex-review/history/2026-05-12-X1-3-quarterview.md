# X1-3: QuarterView 3D Action BE5 Import (2026-05-12)

ROADMAP item Phase X1-3. Player character mesh pack (Asset Store). Single folder, 4.1MB. Codex caught Unity 6 reflection compat risk in Asset Store ReadMe Editor script.

---

## Outcome

**Status**: APPLIED. One Codex review round, APPROVED WITH CHANGES (1 Critical adopted).

**Operation**:
```powershell
Copy-Item -LiteralPath "C:\Users\paek6\Downloads\Buildup\Buildup\Assets\QuarterView 3D Action BE5" `
          -Destination "C:\Users\paek6\ArenaCombat6\Assets\QuarterView 3D Action BE5" -Recurse
```

**Created**: `Assets/QuarterView 3D Action BE5/` — 130 non-meta + 141 meta files = 271 total, 4.1MB.

Subfolders: App Icon.png + Demo/ + Materials/ + Models/ + Particles/ + Prefabs/ + ReadMe/ + Readme.asset + Sprites/ + Textures/.

C# files: 2 (`ReadmeBE5.cs` ScriptableObject base + `ReadmeEditorBE5.cs` CustomEditor). asmdef: 0.

**Patched** (post-import per Codex CI-X1-3-R1-1):
- `Assets/QuarterView 3D Action BE5/ReadMe/Scripts/Editor/ReadmeEditorBE5.cs` lines 29-33 → commented out `LoadLayout()` call to prevent Unity 6 reflection error at import time. `loadedLayout = true` marker preserved so the auto-load attempt runs at most once per session.
- `LoadLayout()` static method definition (line 37-43) kept intact for transparency about original Asset Store behavior.

**Verification grep** post-patch: `LoadLayout()` only referenced in comments (lines 32, 35) — actual call disabled.

**Doc updates**:
- ROADMAP X1-3 → DONE, X1-4 (Hovl Studio 71MB VFX) → NEXT.

---

## Review Cycle Summary

### Round 1 — APPROVED WITH CHANGES (1 Critical, all suggestions confirmed)

Critical Issue:
- **CI-X1-3-R1-1**: `ReadmeEditorBE5.cs` runs via `[InitializeOnLoad]` and calls `LoadLayout()` which uses reflection on `UnityEditor.WindowLayout.LoadWindowLayout` to load `Assets/TutorialInfo/Layout.wlt`. If file missing OR signature changed in Unity 6, Console exception at import. Fix: comment out `LoadLayout()` call, keep `loadedLayout = true` marker. Adopted.

Suggestions (all confirmed):
- S-1: Top-level placement matches X1-1/X1-2 — confirmed
- S-2: .meta preservation correct — confirmed
- S-3: Default-namespace BE5 OK — confirmed (BE5 suffix unique)
- S-4: All verification (Old Input 0, asmdef 0, file counts) passed

---

## Class / Namespace Conflict Pre-Check

| Buildup class | Our project search | Status |
|---|---|---|
| `class ReadmeBE5` | 0 hits | OK (default namespace, BE5 suffix unique) |
| `class ReadmeEditorBE5` | 0 hits | OK (Editor-only assembly, BE5 suffix unique) |

**Old Input API scan**: 0 occurrences in any QuarterView script.

---

## Final Patch Diff (ReadmeEditorBE5.cs)

```csharp
// BEFORE (lines 29-33)
if (readme && !readme.loadedLayout)
{
    LoadLayout();
    readme.loadedLayout = true;
}

// AFTER
if (readme && !readme.loadedLayout)
{
    // X1-3 (Codex CI-X1-3-R1-1): Asset Store layout auto-load disabled for Unity 6 import stability.
    // Original LoadLayout() uses reflection on UnityEditor.WindowLayout.LoadWindowLayout which may
    // throw at import time (signature changes / missing Layout.wlt). Marker still set so this
    // branch runs only once per session.
    // LoadLayout();
    readme.loadedLayout = true;
}
```

`LoadLayout()` static method definition (line 37-43) preserved unmodified for code-history transparency.

---

## User-Side Verification (pending — user confirms in Unity)

1. Unity Editor focus → auto-import (~30s-2min for 4.1MB).
2. Console:
   - **Acceptable**: 2 utility scripts compile silently. Asset Store ReadMe widget may pop up in Inspector (one-time, dismiss). NO `LoadWindowLayout` reflection exception (patched).
   - **Unacceptable**: any C# compile error.
3. Project window: `Assets/QuarterView 3D Action BE5/` visible.
4. Optional smoke: drag a character prefab from `Prefabs/` into a test scene → confirm renders + animator works.
5. 3DScene + SampleScene regression check.
6. Pre-existing warnings (CS0108/CS0618/CS0067) should NOT increase.

---

## Spawned Follow-ups

None. Patch is contained to QuarterView's own Editor script; no impact on other code.

---

## Lessons

- **Asset Store ReadMe scripts can have `[InitializeOnLoad]` side effects** — even passive-looking utility scripts can do reflection work at import time. Pre-import `grep "InitializeOnLoad"` on candidate Editor scripts catches this category.
- **Codex caught reflection-on-internal-API risk** that easy-to-miss in code review of "Asset Store packs are usually safe" assumption. Specific to Unity 6 where internal API signatures shifted.
- **Comment-out vs delete** — kept `LoadLayout()` definition + commented-out call lets future Unity-6-aware port restore it cleanly. Deletion would lose that history.
- **Patch comment trail** (`X1-3 (Codex CI-X1-3-R1-1)`) consistent with X1-2 pattern.
- **Pre-flight + count verification + grep + commented-out historical reference** is now the established X1 import pattern.
