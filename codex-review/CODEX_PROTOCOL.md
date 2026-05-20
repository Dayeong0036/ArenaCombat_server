# Codex Review Protocol — Claude → Codex MCP 자동 워크플로우

이 프로젝트의 Codex review 사이클은 **자동화된 MCP 호출**로 진행된다 (X1-6b-2 라운드부터, 2026-05-14~). 사용자 복사붙여넣기 0.

## 표준 호출 패턴 (제 약속)

매 Codex 검증 호출에 다음 4가지 강제:

```
mcp__codex__codex(
  sandbox: "read-only",
  approval-policy: "never",
  cwd: "<project root>",
  config: {"model_reasoning_effort": "high"},
  prompt: "<context + plan + verdict format>"
)
```

### 각 옵션 이유

| 옵션 | 값 | 이유 |
|------|-----|------|
| `sandbox` | `read-only` | Codex가 파일 수정 / 씬 변경 0. 코드 리뷰만 |
| `approval-policy` | `never` | Codex가 shell 명령 호출 0. 검증 컨텍스트에 불필요 |
| `cwd` | project root | 파일 read 범위를 프로젝트로 한정 |
| `config.model_reasoning_effort` | `high` (not xhigh) | 코드 리뷰는 high로 충분, xhigh 대비 비용 절반 |

### 사용자 글로벌 설정 분리

`~/.codex/config.toml`은 사용자 직접 Codex CLI 사용 시 default. 이 프로토콜이 호출 시 위 4개 옵션으로 override.
- 사용자 직접 사용: xhigh / 자기 패턴
- Claude → Codex: 본 프로토콜 (high / read-only / never)

## 보안 격리

| 채널 | Codex 접근 |
|------|-----------|
| 파일 read (cwd 내) | ✅ 의도 |
| 파일 write/modify | ❌ sandbox=read-only |
| Shell 명령 | ❌ approval=never |
| OpenAI API (자기 모델) | ✅ 필수 |
| 외부 네트워크 (browser_use) | ⚠️ Codex 자체 판단 사용 가능 (모델 내장 도구) |
| **mcp-unity (Unity 조작)** | ❌ ~/.codex/config.toml에서 항목 제거됨 (2026-05-14) |
| 다른 MCP 서버 | ❌ Codex 측 등록 0 |

`browser_use`는 read-only sandbox로 막을 수 없음 (모델 내장 도구). Unity 6.3 / NGO 2.x 같은 well-known 영역에서는 거의 호출 안 함. 새로운 API 검증 필요 시 prompt에 "cross-reference X if uncertain" 명시 가능.

## 인증 / 비용

- 인증: ChatGPT Plus 구독 (`paek678@gmail.com`, OpenAI API 키 미사용)
- 비용: ChatGPT Plus 사용량 한도 내. Plus 갱신 끊기면 Codex 호출 실패.
- 갱신 주기: `~/.codex/auth.json`의 `chatgpt_subscription_active_until` 확인

## Verdict 표준 형식 (Codex 응답이 따르는 형식)

```
Verdict: APPROVED / APPROVED WITH CHANGES / REJECTED
Critical Issues: ...
Suggestions: ...
Questions Back To Claude: ...
Notes On The Approach: ...
```

## 라운드 사이클

```
1. Claude가 plan을 codex-review/pending.md에 작성
2. Claude가 mcp__codex__codex 호출 (위 표준 패턴)
3. Codex가 verdict 응답 (threadId + content)
4. Claude가 verdict 분석 → critical/suggestion 모두 적용
5. Claude가 코드/문서 변경 + 아카이브 + pending 리셋
6. (필요시) 사용자에게 재컴파일/검증 요청
```

기존 manual 사이클 (사용자 복사붙여넣기) 폐기. 첫 자동 라운드 = X1-6b-2 (2026-05-14).

## 후속 질문 (codex-reply)

같은 threadId로 추가 질문 가능: `mcp__codex__codex-reply(threadId, prompt)`. 같은 세션 컨텍스트 유지. 라운드 완료 후 보통 새 세션 시작 (threadId 새로 받음).

## 폴백

Codex MCP 끊기거나 Plus 구독 만료 시 — 기존 manual 사이클 (pending.md 복사 → 외부 GPT/Claude 별도 세션 → verdict 채팅에 붙여넣기) 사용 가능.
