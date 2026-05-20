# X4-5b: Boss prefab creation + scene wiring + NetworkPrefab registration

## Files changed/created
- `Assets/ArenaCombat/Prefabs/Boss/Boss.prefab` (NEW) — hand-written YAML
- `Assets/ArenaCombat/Resources/Stats/BossStatsSO.asset` (EDIT) — added BossPhaseThresholds
- `Assets/DefaultNetworkPrefabs.asset` (EDIT) — added Boss prefab entry
- `Assets/Scenes/3DScene.unity` (EDIT via MCP) — added BossManager + BossSpawnPoint GameObjects

## Boss.prefab components (9 total)
| fileID | Component | Script GUID | Notes |
|--------|-----------|-------------|-------|
| 1000001 | GameObject | (built-in !u!1) | Root, name="Boss" |
| 1000002 | Transform | (built-in !u!4) | origin |
| 1000003 | NetworkObject | d5a57f767e5e46a458fc5d3c628d0cbb | NGO spawn/despawn lifecycle |
| 1000004 | Rigidbody | (built-in !u!54) | mass=100, useGravity=0, isKinematic=0, interpolation=1, constraints=80 (FreezeRotX+Z) |
| 1000005 | BoxCollider | (built-in !u!65) | size=(2,3,2), center=(0,1.5,0), isTrigger=0 |
| 1000006 | BossNetworkController3D | 0bc3625afac9487db2939f933bd06f27 | `_bossStatsSO` wired to BossStatsSO.asset |
| 1000007 | StatManager | cc3c21c8a39261443a34cc98fd927089 | |
| 1000008 | StateManager | 17b9658f930c142478c4b8a264f616c7 | |
| 1000009 | SkillExecutor | f847c085b853a8f48bcb400b32c5ddc7 | |
| 1000010 | SkillManager | 024d296822d4c824e91509a4df32638d | |

## BossStatsSO.asset changes
- Added `BossPhaseThresholds: [0.75, 0.5, 0.25]` (was empty `[]`)
- Matches BossNetworkController3D.HandlePhase expectations (descending HP-ratio, MaxPhaseThresholds=3)

## DefaultNetworkPrefabs.asset
- Appended Boss entry: `{fileID: 1000001, guid: 3d6a053bfb577c742bc7c671cb3a7e7b, type: 3}`
- Now 5 prefabs total (was 4)

## 3DScene changes (MCP)
- **BossManager** GameObject — BossManager component with `_bossPrefab` → Boss.prefab, `_bossSpawnPoint` → BossSpawnPoint Transform
- **BossSpawnPoint** GameObject — empty Transform at (0, 1, 8)

## Verification checklist
- [ ] Boss.prefab imports without errors in Unity (all 9 components resolve)
- [ ] BossNetworkController3D._bossStatsSO is non-null (points to BossStatsSO.asset)
- [ ] BossStatsSO.BossPhaseThresholds has 3 entries [0.75, 0.5, 0.25]
- [ ] DefaultNetworkPrefabs.asset lists Boss prefab (5th entry)
- [ ] 3DScene has BossManager GO with BossManager component
- [ ] BossManager._bossPrefab references Boss.prefab
- [ ] BossManager._bossSpawnPoint references BossSpawnPoint Transform
- [ ] No compile errors introduced
