using UnityEngine;
using ArenaCombat.Core.Skill;

namespace ArenaCombat.Core.Network
{
    public class BossTelegraphDisplay : MonoBehaviour
    {
        [Header("Telegraph VFX Prefabs")]
        [SerializeField] private GameObject _areaTelegraphPrefab;
        [SerializeField] private GameObject _directionalTelegraphPrefab;
        [SerializeField] private GameObject _singleTargetTelegraphPrefab;

        [Header("Phase VFX")]
        [SerializeField] private GameObject _phaseTransitionPrefab;
        [SerializeField] private float _phaseVfxLifetime = 3f;

        [Header("Settings")]
        [SerializeField] private float _areaScale = 3f;
        [SerializeField] private float _heightOffset = 0.1f;

        private GameObject _activeTelegraph;

        public void Show(Vector3 position, Vector3 direction, float duration, TargetType targetType)
        {
            Hide();

            GameObject prefab;
            switch (targetType)
            {
                case TargetType.Area:
                    prefab = _areaTelegraphPrefab;
                    break;
                case TargetType.Direction:
                    prefab = _directionalTelegraphPrefab;
                    break;
                case TargetType.Single:
                    prefab = _singleTargetTelegraphPrefab;
                    break;
                case TargetType.Self:
                    return;
                default:
                    Debug.LogWarning($"[BossTelegraphDisplay] Unknown TargetType {targetType}, skipping VFX.");
                    return;
            }

            if (prefab == null) return;

            var pos = position + Vector3.up * _heightOffset;
            var rot = direction.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(direction, Vector3.up)
                : Quaternion.identity;

            _activeTelegraph = Instantiate(prefab, pos, rot);

            if (targetType == TargetType.Area)
                _activeTelegraph.transform.localScale = Vector3.one * _areaScale;

            Destroy(_activeTelegraph, duration);
        }

        public void Hide()
        {
            if (_activeTelegraph != null)
            {
                Destroy(_activeTelegraph);
                _activeTelegraph = null;
            }
        }

        public void ShowPhaseTransition(BossPhase newPhase)
        {
            if (_phaseTransitionPrefab == null) return;
            var vfx = Instantiate(_phaseTransitionPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, _phaseVfxLifetime);
        }

        private void OnDestroy()
        {
            Hide();
        }
    }
}
