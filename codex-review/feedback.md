# Codex Feedback — B5-1: Mouse Look Rotation Fix

## Verdict
APPROVED WITH CHANGES

## Critical Issues (must fix before applying)
1. **중괄호 오류**: Update() 제안 코드에 `}` 하나 초과 → 컴파일 안됨
2. **Owner prediction에 서버 거부 상태 미반영**: stun/root/rope/card draft 중 owner만 회전하고 서버는 거부 → 드리프트 발생. 해당 상태에서는 `InterpolateRotation()` fallback 필요.
3. **lookYaw 정규화 없음**: `InputValidator` float clamp 정책에 맞춰 `[-180, 180]` 또는 `[0, 360)` 정규화 helper 필요. 서버 진입점 + owner prediction 양쪽에 적용.
4. **RequestMoveRpc에 RpcParams 검증 없음**: sender vs OwnerClientId 검증 추가 권장 (기존 문제이나 이 변경이 yaw 신뢰 범위를 넓히므로).

## Suggestions
- `NormalizeYaw()` helper로 통일
- 테스트: 2P 이동+조준, 원격 시점 확인, stun/rope/draft 중 드리프트 확인

## Questions
- Stunned/Rooted/Frozen/Roping/CardDraft 중 조준만 회전 가능해야 하는가?
  → 현재: 서버가 이 상태에서 yaw 갱신 거부. 일단 서버 동작 유지하고 owner prediction만 같은 조건으로 guard.
