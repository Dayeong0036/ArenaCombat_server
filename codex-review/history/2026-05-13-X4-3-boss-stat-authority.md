# X4-3: BossNetworkController3D HP/Alive Authority + NV Sync + ICombatant Routing — 2026-05-13

**Status**: APPLIED. Codex Round 1 APPROVED WITH CHANGES (3 critical + 6 suggestion, all applied).

## Scope

1 파일 EDIT (`BossNetworkController3D.cs`). PNC3D X3-3 merged round 패턴 미러. NetworkVariable + Initialize + sync hook + ICombatant 17 멤버 라우팅 일관 swap.

## Codex Critical Applied

- **C-1 `_bossStatsSO == null` 시 inert 유지**: `InitializeStatManager()` early-return + warn + `networkHP=0` + `networkIsAlive=false`. BossMaxHP fallback (1000f 등) 사용 안 함. stray shell이 "live boss target"이 되는 시나리오 차단.
- **C-2 ICombatant read property는 NV / SO 기반**: 클라이언트의 `StatManager._isAlive=true` default가 누설되지 않도록 8개 property 모두 replicated state 사용 — `MaxHP` ← `_bossStatsSO.BossMaxHP` (`_statMgr.GetMaxHP()` fallback), `CurrentHPPercent` ← `networkHP / MaxHP`, `IsAlive` ← `networkIsAlive.Value`, `IsCasting` ← `IsServer + alive + _statMgr.IsCasting`. 서버는 그대로 StatManager 권위.
- **C-3 모든 mutation method IsServer 가드**: 11개 메서드 모두 `if (!IsServer || _statMgr == null || !networkIsAlive.Value) return;` prologue. StatManager 자체는 서버 권위 enforce 안 함 → 클라이언트 우발 호출 차단.

## Codex Suggestions Applied

- **S-1 `CombatantKind.Boss` 사용 확인**: `Assets/.../Stats/StatManager.cs:39`에 존재. `_statMgr.Initialize(..., CombatantKind.Boss)` 직접 사용.
- **S-2 `BossBaseDefense` 사용 안 함**: `StatManager.Initialize`에 defense 파라미터 없음. 현재 stat model이 `BaseStatsSO` multiplier 기반. defense formula는 후속 boss-specific damage formula 라운드.
- **S-3 NV default 0 / false**: `networkHP = new NetworkVariable<float>(0f, ...)` / `networkIsAlive = new NetworkVariable<bool>(false, ...)`. 서버 Initialize 성공 시만 live로 flip.
- **S-4 FixedUpdate 게이트 강화**: `if (!IsServer || !IsSpawned || _statMgr == null || !networkIsAlive.Value) return;`. 4중 가드.
- **S-5 Boss defeat = warn-once + networkIsAlive=false**: `OnBossDefeated(attackerId)` 호출 → log + NV write까지만. Match-end broadcast은 X4-5/6 BossManager+GSM 라운드.
- **S-6 `networkPosition` defer 확정**: X4-3 = HP/alive 권위만. X4-4에서 NetworkTransform 컴포넌트 vs 명시 NV 결정.

## Edits

- 헤더: X4-2 SCOPE → X4-3 SCOPE 갱신, dormant 계약 보존 5가지 항목 명시 (SetAutoCast / NV default / Initialize null guard / IsServer mutation guard / replicated reads).
- NetworkVariable 2개 추가 (`networkHP` / `networkIsAlive`).
- `_lastAttackerId` private field 추가 (skill kill attribution, PNC3D X3-3 미러).
- `OnNetworkSpawn` override 추가 (IsServer 분기 → `InitializeStatManager`).
- `InitializeStatManager()` 헬퍼 (`_bossStatsSO` null 가드 + StatManager.Initialize + NV prime, returns bool).
- `FixedUpdate()` 추가 (server-only sync hook).
- `OnBossDefeated(ulong)` 추가 (warn-once + networkIsAlive=false).
- `WarnX4Stub` 메시지 문구 갱신 (position control 한정).
- ICombatant 8 read property: inert default → NV/SO 기반.
- ICombatant 11 mutation/query: warn-once no-op → IsServer 가드 + StatManager forward (`ReceiveDamage / ReceiveShieldBreakDamage / RecoverHP / AddShield / ApplyStatus / HasStatus / ApplyBuff / ApplyDebuff / RemoveStatuses / RemoveBuffs / NotifyParryReward`).
- ICombatant 3 position-control: WarnX4Stub 유지 (X4-4 대상).

## Surface Verification

- `NetworkVariable<float> networkHP` + `NetworkVariable<bool> networkIsAlive` ✓ (both server-write everyone-read, default 0/false)
- `InitializeStatManager` 1개 ✓, `_bossStatsSO == null` early return ✓
- `OnNetworkSpawn` override + IsServer 분기 ✓
- `FixedUpdate` 4-guard return ✓
- `OnBossDefeated` 호출 1회 (sync hook alive→dead 검출 시) ✓
- ICombatant IsServer 가드 11회 (TakeDamage / TakeShieldBreakDamage / RecoverHP / AddShield / ApplyStatus / HasStatus / ApplyBuff / ApplyDebuff / RemoveStatuses / RemoveBuffs / NotifyParryReward) ✓ — HasStatus는 read-only이므로 `_statMgr != null && HasStatus` 만 (mutation 아님)
- `_lastAttackerId = attacker is PlayerNetworkController3D pnc ? pnc.OwnerClientId : 0UL` 2회 (TakeDamage + TakeShieldBreakDamage) ✓
- `CombatantKind.Boss` 사용 ✓
- 3 position-control stub (Knockback/Pull/MoveBy) → WarnX4Stub 유지 ✓
- `SetAutoCast(false)` Awake 유지 ✓

## Dormant 계약 5중 안전망 (의도된 중복)

1. **NetworkPrefab 미등록** — 런타임 인스턴스 부재.
2. **`InitializeStatManager` BossStatsSO null guard** — designer 미할당 시 inert.
3. **NV default 0/false** — 서버 Initialize 성공 시만 live.
4. **모든 mutation IsServer + alive 가드** — 클라이언트 / dead 시 우발 호출 차단.
5. **`SetAutoCast(false)`** — auto-cast 차단 (X4-4 FSM 라운드까지 유지).

## Verification (post-apply, expected)

1. Unity recompile <5s. 0 신규 에러 / 0 신규 경고.
2. Boss 인스턴스 씬에 없음 → 런타임 무영향.
3. 기존 PNC3D + skill 경로 영향 없음 (BossNetworkController3D는 ICombatant 구현 추가 외 외부 호출 surface 없음).

## Risks (Acknowledged)

- **`_bossStatsSO` 디자이너 미할당 → 보스 영구 inert**: 의도된 fail-safe. designer가 X4-5 prefab 라운드에서 명시 할당 필요. 미할당 시 warn 로그로 즉시 감지 가능.
- **`networkPosition` 없음**: 의도. Boss 이동은 X4-4 FSM 라운드에서 NetworkTransform vs 명시 NV 결정.
- **Match-end broadcast 미구현**: 의도. `OnBossDefeated`은 networkIsAlive=false + log까지. victory screen / GSM phase transition은 BossManager 라운드.

## Spawned Follow-up

- **X4-4 NEXT**: position control routing (Knockback / Pull / MoveBy → ApplyPositionOffset 헬퍼, PNC3D X3-4 미러) + `networkPosition` NV 결정 + Buildup BossController FSM 포팅 시작.
