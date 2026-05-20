# Codex Review Workflow

Claude의 모든 코드 변경은 Codex 검증을 거친 뒤 적용된다.

## 디렉토리 구조

```
codex-review/
├── README.md          # 이 파일
├── codex-session-prompt.md  # Codex 세션 셋업 프롬프트
├── pending.md         # 현재 검증 대기 중인 제안 (Claude가 작성)
├── feedback.md        # Codex 응답 (사용자가 저장하거나 채팅에 붙여넣기)
└── history/           # 과거 review 아카이브 (timestamp-topic.md)
```

## 사이클

```
[Claude]
  1. 변경 제안을 pending.md 에 기록 (+ 채팅에 요약 출력)
  2. 사용자에게 "Codex 검증 요청" 알림
  3. 멈춤. 코드 수정 안 함.

[User]
  4. pending.md 내용을 Codex 세션에 전달
  5. Codex 응답을:
     - feedback.md 에 저장   ← Claude가 자동으로 읽음
     - 또는 채팅에 직접 붙여넣기
     - 또는 "그냥 진행해" 한 줄

[Claude]
  6. 피드백 반영 → 필요하면 pending.md 갱신해서 재제출
  7. 승인되면 실행
  8. 완료 후 history/<YYYY-MM-DD>-<topic>.md 로 아카이브
  9. pending.md, feedback.md 비움
```

## pending.md 표준 섹션

매 사이클 다음 항목을 채워서 작성:

- **Topic** — 한 줄 요약
- **Roadmap link** — ROADMAP.md의 어느 항목인지
- **Goal** — 무엇을 달성하려는지
- **Files to touch** — 정확한 경로 목록
- **Approach** — 채택한 방식 + 고려한 대안
- **Diff sketch** — 핵심 변경 의도 (전체 diff는 아님, 의도만)
- **Risks / unknowns** — 깰 수 있는 것, 가정한 것
- **Questions for Codex** — 명시적으로 검증받고 싶은 포인트

## 적용 범위

**Codex 검증 필요:**
- 모든 `.cs` 파일 수정 (생성/편집/삭제)
- `.asmdef`, `manifest.json`, `ProjectSettings/*.asset` 등 빌드/패키지 영향 파일

**Codex 검증 생략 가능:**
- 문서 (`.md`) 갱신 (ROADMAP.md, NETWORK_ARCHITECTURE.md, PROJECT_STRUCTURE.md 포함)
- 메모리 파일 (`.claude/projects/.../memory/*`)
- `.claude/settings*.json` (사용자가 별도 안전 prompt로 검토)
- 사용자가 한 줄 단위로 정확히 지시한 수정

## 회피 금지

- "사소한 fix니까 그냥 적용" 식의 우회 금지
- 한 번에 여러 파일을 묶어서 검증 통과 후 다른 파일도 슬쩍 추가 금지
- pending.md 작성 없이 채팅으로만 plan을 흘려보내고 코드 수정 금지

## 위반 시 사용자 액션

검증 없이 코드가 바뀐 게 발견되면:
- "Codex 거쳤어?" 한 마디로 호출
- Claude는 즉시 변경을 되돌리거나, 사후 pending.md 작성 후 재검증
