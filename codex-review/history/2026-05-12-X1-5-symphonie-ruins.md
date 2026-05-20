# X1-5: Symphonie / Ruins Environment Pack Import (2026-05-12)

ROADMAP item Phase X1-5. Largest single import to date (108MB), but tiny file count (14 non-meta). Zero scripts. **Survey-classification corrected**: originally tagged as "audio" but actually a 3D Ruins environment pack (boss arena dressing).

---

## Outcome

**Status**: APPLIED. One Codex review round, APPROVED first pass.

**Operation**:
```powershell
Copy-Item -LiteralPath "C:\Users\paek6\Downloads\Buildup\Buildup\Assets\Symphonie" `
          -Destination "C:\Users\paek6\ArenaCombat6\Assets\Symphonie" -Recurse
```

**Created**: `Assets/Symphonie/Ruins/` with subfolders Build_IN/, Demo/, HDRP/, URP/, Model/, Shader/, Texture/.

**File counts (verified post-copy)**:
- Non-meta: 14
- Meta: 29
- C#: 0
- asmdef: 0
- Custom shaders (.shader): 0
- ShaderGraph (.shadergraph): **1** (`Ruins_URP.shadergraph` URP-targeted)

108MB size breakdown (verified):
- 52.6MB `T_archway_pillar02_N.png` (Normal map, 4K)
- 33.03MB `T_archway_pillar02_D.png` (Diffuse, 4K)
- 18.25MB `T_archway_pillar02_M.png` (Metallic-Smoothness, 4K)
- 3.4MB `T_archway_pillar02_O.png` (Ambient Occlusion)
- <0.5MB everything else (mesh + 3 materials + 3 prefab variants + shadergraph + demo scene + README)

**Doc updates**:
- ROADMAP X1-5 → DONE with **corrected description** (Ruins environment, not audio) + URP-only usage note + 4K texture optimization deferred.

---

## Review Cycle Summary

### Round 1 — APPROVED first pass

Codex performed independent local verification before approving:
- Source exists, destination doesn't
- File counts match exactly (14 + 29)
- Zero scripts / asmdef / .shader
- 1 .shadergraph (URP variant)
- 108MB breakdown matched proposal exactly

Suggestions adopted:
- S-1: Description corrected (audio → Ruins env) in ROADMAP entry
- S-2: Verification language tightened ("C# compile warnings/errors should not increase" vs broad "warnings")
- S-3: 4K texture downsize deferred to Chapter1 setup time
- S-4: URP-only usage note (`Assets/Symphonie/Ruins/URP/Prefabs/` only) — captured in ROADMAP entry

---

## Multi-RP Variant Strategy

Pack ships 3 render-pipeline variants of same content:
- `URP/` — material + prefab using `Ruins_URP.shadergraph` (our pipeline)
- `HDRP/` — HDRP-specific material + prefab (renders pink in URP)
- `Build_IN/` — built-in pipeline material + prefab (renders pink in URP)

**Decision**: only use URP/ variant. HDRP/Build_IN variants exist but never dragged into our scenes. No conflict, just unused content (3 prefab GUIDs occupied harmlessly).

---

## Verification State (Server-side)

- Destination folder created: ✅
- Subfolder structure preserved: ✅ (7 subfolders + README.md)
- File counts match prediction exactly: ✅ (14 + 29)
- Zero .cs/.asmdef/.shader: ✅
- One URP-targeted .shadergraph: ✅
- 108MB transferred correctly: ✅

---

## User-Side Verification (pending — user confirms in Unity)

1. Unity Editor focus → auto-import. **Expect 3-6min wait** due to 4K texture compression × 4.
2. Console:
   - **Acceptable**: ShaderGraph compile messages, URP fallback / HDRP material pink warnings.
   - **Unacceptable**: any C# compile warning/error increase (zero scripts imported → impossible).
3. Project window: `Assets/Symphonie/Ruins/` visible with all subfolders.
4. Optional smoke: drag `URP/Prefabs/archway_pillar02.prefab` → confirm renders. (HDRP/Build_IN prefabs intentionally not dragged.)
5. 3DScene + SampleScene regression check.
6. Pre-existing C# warnings (CS0108/CS0618/CS0067/CS0414) should NOT increase.

---

## Spawned Follow-ups

1. **4K texture downsize / Streaming Mipmaps** — decide at Chapter1 placement time, not now. Tagged for X5 (Chapter1 activation).
2. **HDRP/Build_IN variant cleanup** — optional later. Delete unused prefabs/materials if disk space matters. Currently negligible.

---

## Strategy Decision Needed Before X1-6

After X1-5, all zero-script and low-script imports done. Remaining X1:
- **X1-6**: Buildup game data (Stats SOs, Materials, Settings, Prefabs from `Player&Boss/`, `ScriptableObjects/`, `Material/`, `AbilityCard/`, `Prefabs/`). **NEW RISK**: SOs reference Buildup `.cs` (e.g., `PlayerStatsSO.asset` references `PlayerStatsSO.cs` GUID). Without .cs, SOs show "missing script" warning.
- **X1-7**: Chapter1.unity scene — many script references, same warning class on larger scale.

**Options to discuss with user before X1-6**:
- **A. Import as-is**: accept "missing script" warnings as expected transient state. X2 (script import) eventually resolves them.
- **B. X2 first (port scripts before importing SOs/scene)**: cleanest end state but reorders the Phase X plan.
- **C. Sub-batch X1-6**: import dependency-light parts first (Material/, Settings/, Texture-only Prefabs) and defer Stats SOs to X2.

User decides at "X1-6 진행" time.

---

## Lessons

- **Survey-time classification can be wrong**: Symphonie was labeled "audio" in initial Buildup survey based on directory name pattern guess. Actual inspection revealed Ruins env pack. Lesson: when actually starting an import cycle, always inspect contents, don't trust initial bulk-categorization.
- **Large file size ≠ large risk**: 108MB with 14 files (mostly 4K textures) is lower risk than 25MB with 100+ scripts. File count + content type matters more than disk size.
- **Multi-RP packs** are common — URP/HDRP/Build_IN side-by-side. Always identify which variant matches our pipeline and document "use only X/ folder" in ROADMAP.
- **Strategy pivots between sub-cycles**: X1-1~X1-5 were all "zero or low-script" pattern. X1-6/X1-7 break that pattern — need explicit decision before proceeding. Don't auto-extend a working pattern beyond its safe domain.
