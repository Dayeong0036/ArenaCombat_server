---
round: X4-N
title: "Boss position interpolation — snap → smooth lerp on clients"
date: 2026-05-15
---

# X4-N: Boss client-side position interpolation

## Change

`Assets/ArenaCombat/Scripts/Core/Network/BossNetworkController3D.cs` — replaced instant snap with lerp interpolation.

### Before
```csharp
private void HandlePositionChanged(Vector3 oldPosition, Vector3 newPosition)
{
    transform.position = newPosition;
}
```

### After
```csharp
private const float InterpSpeed = 18f;
private Vector3 _interpTarget;
private bool _interpActive;

private void HandlePositionChanged(Vector3 oldPosition, Vector3 newPosition)
{
    _interpTarget = newPosition;
    _interpActive = true;
}

private void Update()
{
    if (!_interpActive || IsServer) return;
    transform.position = Vector3.Lerp(transform.position, _interpTarget, Time.deltaTime * InterpSpeed);
}
```

## Verification Checklist

1. `IsServer` guard in Update — server uses rb.MovePosition directly, no double-move
2. `_interpActive` flag prevents unnecessary Lerp before first position change
3. OnNetworkSpawn client path still snaps immediately (`transform.position = networkPosition.Value`) before subscribing HandlePositionChanged — no lerp from origin
4. InterpSpeed=18 at 60fps → `deltaTime*18 ≈ 0.3` lerp factor = fast catch-up, no visible lag
5. No impact on server-side FixedUpdate HP sync / position control
6. HandlePositionChanged still only called on clients (subscribe is in `!IsServer` block)
