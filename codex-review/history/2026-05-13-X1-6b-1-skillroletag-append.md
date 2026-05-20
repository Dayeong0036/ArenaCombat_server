# X1-6b-1: SkillRoleTag enum Append-Only 누락값 보충 — 2026-05-13

**Status**: APPLIED. Codex Round 1 APPROVED WITH CHANGES (1 critical + 4 suggestion, all applied).

## Scope

`SkillRoleTag.cs` enum 9 → 29 (append-only 20개) + SKILL_SYSTEM_DESIGN.md 동시 갱신. 기타 파일 변경 0.

## Codex Critical Applied

- **C-1 파일 경로 정정**: 계획서 `Assets/ArenaCombat/Scripts/Core/Skill/SkillRoleTag.cs` → 실제 `Assets/ArenaCombat/Scripts/Core/Skill/Core/SkillRoleTag.cs` (SkillContext / SkillDefinition / SkillRegistry / SkillTypes와 동일 폴더). 잘못된 경로 신규 파일 생성 시 namespace 중복 정의 컴파일 에러 회피.

## Codex Suggestions Applied

- **S-1 SKILL_SYSTEM_DESIGN.md 동시 갱신**: §10b "Tag type closed" 항목 9개 enum 명시 → reserved index ranges (0..8 X2-5 originals / 9..28 X1-6b-1 append / 29+ future) 갱신. 설계 문서 stale 회피.
- **S-2 X1-6b-2 .asset 변환은 string name 기반 매핑 권장**: 본 라운드는 enum 추가만 하고, X1-6b-2 변환 로직에서 `"AntiHeal" → SkillRoleTag.AntiHeal` 같은 이름 기반 매핑 사용. 최종 YAML은 ordinal 직렬화이지만 변환 단계는 가독성 + 정확성 위해 name-based.
- **S-3 의미 분리 결정 OK**: Cleanse/Heal, AOE/Zone, Survival/Heal, SelfBuff/Buff 모두 별개 추가. Counter-pick / draft scoring 정보 손실 회피.
- **S-4 Reserved index ranges 헤더**: SkillRoleTag.cs 파일 헤더에 "Reserved index ranges: 0..8 X2-5 / 9..28 X1-6b-1 / 29+ future" 명시. append-only 정책 강화.

## Edits

- `Assets/ArenaCombat/Scripts/Core/Skill/Core/SkillRoleTag.cs`:
  - 헤더에 "Reserved index ranges" 블록 추가
  - 기존 9 enum 값 인덱스 코멘트 명시 (0..8)
  - 신규 20 enum 값 카테고리별 그룹 + 인덱스 코멘트 (9..28)
- `Assets/ArenaCombat/Scripts/Core/Network/SKILL_SYSTEM_DESIGN.md`:
  - §10b "Tag type closed" 갱신 — reserved index ranges + 전체 29개 값 명시

## 신규 enum 값 (20개, 인덱스 9..28)

**Damage type (3)**: AOE (9), MultiHit (10), Pierce (11)
**Attack range (2)**: Melee (12), Ranged (13)
**Stat modulation (5)**: DamageUp (14), DefUp (15), DefDown (16), SelfBuff (17), Vulnerable (18)
**Defense (1)**: ShieldBreak (19)
**Healing/regen (3)**: Cleanse (20), Regen (21), AntiHeal (22)
**Crowd control (3)**: CC (23), Silence (24), Buff (25)
**Misc (3)**: Execute (26), Stealth (27), Survival (28)

## 사용처 영향 분석

기존 enum 사용처:
- `SkillRegistry.GetByRoleTag(SkillRoleTag tag)` — `==` 비교, 인덱스 변경 무관 ✓
- `SkillRegistry.GetCounterCandidates(SkillRoleTag[] playerRoleTags)` — `Array.Exists` 비교 ✓
- `SkillRegistry.ScoreCounter(SkillDefinition skill, SkillRoleTag[] playerTags)` — `Count(... pt => pt == ct)` ✓
- `SkillDefinition.RoleTags / CounterTags` — `SkillRoleTag[]` 필드 ✓

기존 9개 enum 정수값 0..8 불변 → 모든 사용처 영향 0.

## 사용자 작업 (본 라운드 후) — 0건

코드 1줄 변경 + Markdown 갱신만. Inspector 작업 / 디자이너 결정 / 씬 변경 모두 없음. **재컴파일 1회 필요**.

## Verification (post-apply, expected)

1. Unity recompile <3s. **0 신규 에러 / 0 신규 경고**.
2. `Add Component > BossNetworkController3D` Inspector dropdown 변화 없음 (SkillRoleTag dropdown은 SkillDefinition.asset에서만 의미 있음).
3. **(선택)** Project > Create > ArenaCombat > SkillDefinition으로 임시 .asset 생성 후 RoleTags / CounterTags dropdown 클릭 → 29개 옵션 표시 확인 가능.

## Risks (Acknowledged)

- **20개 추가가 과한가 / 부족한가**: Buildup 12 PlayerSkill audit 기반이라 정확. Boss skill 12개 .asset도 별도 audit 시 추가 enum 필요할 수 있음 (X1-6b 후속에서 보충 가능, append-only).
- **`Buff` vs `SelfBuff` 의미 중복**: 의도. Counter-pick에서 둘 구분하면 정확도 향상.
- **`enum` 인덱스 9가 다른 enum과 충돌**: 다른 namespace, 무관 (BossPhase / StatusType / 등 모두 별개).

## Spawned Follow-up

- **X1-6b-2 NEXT**: Buildup 12 PlayerSkill .asset 정식 import + RoleTags string→enum 변환 (name-based, Codex S-2). `Resources/Skills/PlayerSkills/` 배치.
- **X1-6b-3**: SkillRegistry._pool YAML에 12 GUID 등록.
- **X1-6b-4**: AbilityCard 6개 ↔ SkillDefinition 매핑.

## Parallel Workstream

X3 smoke test와 파일 0겹침. X4-1..6 / X4-5a / X1-6c 모든 라운드와 파일 0겹침.
