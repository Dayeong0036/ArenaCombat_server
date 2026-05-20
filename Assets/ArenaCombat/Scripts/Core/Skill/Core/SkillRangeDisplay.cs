using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Debug-only singleton that visualizes skill ranges.
//   1) Runtime: SkillArea-style prefab + material.color (Game view)
//   2) Gizmo: OnDrawGizmos wireframe (Scene view)
// When both representations match, the area config is correct.
//
// Pure debug visualization. No game logic. No ML observation surface.
// Buildup origin: Assets/Scripts/Skill/Core/SkillRangeDisplay.cs

namespace ArenaCombat.Core.Skill
{
    public class SkillRangeDisplay : MonoBehaviour
    {
        public static SkillRangeDisplay Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private bool  _showRange = true;
        [SerializeField] private bool  _logRange  = true;
        [SerializeField] private float _duration  = 2.0f;
        [SerializeField] private float _visualScale = 1.0f;

        [Header("Prefab")]
        [SerializeField] private GameObject _indicatorPrefab;
        [SerializeField] private int _poolSize = 8;

        [Header("Runtime colors (Game view)")]
        [SerializeField] private Color _hitColor  = new Color(1f, 0.1f, 0.1f, 0.6f);
        [SerializeField] private Color _missColor = new Color(1f, 1f, 0f, 0.5f);
        [SerializeField] private Color _projColor = new Color(0.2f, 0.5f, 1f, 0.6f);
        [SerializeField] private Color _areaColor = new Color(0.8f, 0.2f, 1f, 0.5f);

        [Header("Gizmo colors (Scene view)")]
        [SerializeField] private Color _gizmoHitColor  = new Color(0f, 1f, 0f, 0.9f);
        [SerializeField] private Color _gizmoMissColor = new Color(0f, 1f, 1f, 0.9f);
        [SerializeField] private Color _gizmoProjColor = new Color(1f, 1f, 0f, 0.9f);
        [SerializeField] private Color _gizmoAreaColor = new Color(1f, 0.5f, 0f, 0.9f);
        [SerializeField] private bool  _showGizmos = true;
        [SerializeField] private int   _gizmoSegments = 32;

        private readonly Queue<GameObject> _pool = new();

        // ── Gizmo record ────────────────────────────────────────
        private enum GizmoShape { Circle, Cone, Line, Area }

        private struct GizmoRecord
        {
            public Vector3    Center;
            public Vector3    Forward;
            public float      Radius;
            public float      AngleDeg;
            public GizmoShape Shape;
            public float      Timestamp;
            public bool       Hit;
        }

        private readonly List<GizmoRecord> _gizmoRecords = new();

        // ── Initialization ──────────────────────────────────────
        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            if (_indicatorPrefab == null)
            {
                Debug.LogWarning("[SkillRangeDisplay] Indicator Prefab not assigned — range display disabled");
                return;
            }

            for (int i = 0; i < _poolSize; i++)
                _pool.Enqueue(CreateInstance());

            Debug.Log($"[SkillRangeDisplay] Init complete (pool size {_poolSize})");
        }

        // ══════════════════════════════════════════════════════════
        // Runtime display (Game view)
        // ══════════════════════════════════════════════════════════

        public void ShowCircle(Vector3 center, float radius, bool hit)
        {
            RecordGizmo(center, Vector3.forward, radius, 360f, GizmoShape.Circle, hit);

            if (!_showRange || _indicatorPrefab == null) return;
            if (_logRange) Debug.Log($"[RangeDisplay] Circle center={center} r={radius} hit={hit}");

            var go = SpawnAt(center);
            SetScale(go, radius * 2f * _visualScale, radius * 2f * _visualScale);
            go.transform.rotation = Quaternion.identity;
            StartCoroutine(FadeAndReturn(go, hit ? _hitColor : _missColor));
        }

        public void ShowCone(Vector3 center, Vector3 forward, float radius, float angleDeg, bool hit)
        {
            RecordGizmo(center, forward, radius, angleDeg, GizmoShape.Cone, hit);

            if (!_showRange || _indicatorPrefab == null) return;
            if (_logRange) Debug.Log($"[RangeDisplay] Cone center={center} r={radius} angle={angleDeg} hit={hit}");

            float ratio = angleDeg / 360f;
            var go = SpawnAt(center);
            SetScale(go, radius * 2f * ratio * _visualScale, radius * 2f * _visualScale);
            if (forward != Vector3.zero)
                go.transform.rotation = Quaternion.LookRotation(forward);
            StartCoroutine(FadeAndReturn(go, hit ? _hitColor : _missColor));
        }

        public void ShowLine(Vector3 origin, Vector3 forward, float length)
        {
            RecordGizmo(origin, forward, length, 0f, GizmoShape.Line, true);

            if (!_showRange || _indicatorPrefab == null) return;
            if (_logRange) Debug.Log($"[RangeDisplay] Line origin={origin} len={length}");

            var go = SpawnAt(origin + forward.normalized * length * 0.5f);
            SetScale(go, 1.5f * _visualScale, length * _visualScale);
            if (forward != Vector3.zero)
                go.transform.rotation = Quaternion.LookRotation(forward);
            StartCoroutine(FadeAndReturn(go, _projColor));
        }

        public void ShowArea(Vector3 center, float radius)
        {
            RecordGizmo(center, Vector3.forward, radius, 360f, GizmoShape.Area, true);

            if (!_showRange || _indicatorPrefab == null) return;
            if (_logRange) Debug.Log($"[RangeDisplay] Area center={center} r={radius}");

            var go = SpawnAt(center);
            SetScale(go, radius * 2f * _visualScale, radius * 2f * _visualScale);
            go.transform.rotation = Quaternion.identity;
            StartCoroutine(FadeAndReturn(go, _areaColor));
        }

        // ══════════════════════════════════════════════════════════
        // Gizmo record + draw (Scene view)
        // ══════════════════════════════════════════════════════════

        private void RecordGizmo(Vector3 center, Vector3 forward, float radius,
                                 float angleDeg, GizmoShape shape, bool hit)
        {
            _gizmoRecords.Add(new GizmoRecord
            {
                Center    = center,
                Forward   = forward.normalized,
                Radius    = radius,
                AngleDeg  = angleDeg,
                Shape     = shape,
                Timestamp = Time.time,
                Hit       = hit,
            });
        }

        private void OnDrawGizmos()
        {
            if (!_showGizmos || _gizmoRecords.Count == 0) return;

            float now = Time.time;
            for (int i = _gizmoRecords.Count - 1; i >= 0; i--)
            {
                var rec = _gizmoRecords[i];
                float age = now - rec.Timestamp;
                if (age > _duration) { _gizmoRecords.RemoveAt(i); continue; }

                float alpha = age < _duration * 0.5f ? 1f : 1f - (age - _duration * 0.5f) / (_duration * 0.5f);

                Color baseColor;
                switch (rec.Shape)
                {
                    case GizmoShape.Line: baseColor = _gizmoProjColor; break;
                    case GizmoShape.Area: baseColor = _gizmoAreaColor; break;
                    default:              baseColor = rec.Hit ? _gizmoHitColor : _gizmoMissColor; break;
                }
                Gizmos.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);

                Vector3 center = new Vector3(rec.Center.x, 1.2f, rec.Center.z);

                switch (rec.Shape)
                {
                    case GizmoShape.Circle:
                    case GizmoShape.Area:
                        DrawWireCircle(center, rec.Radius);
                        break;
                    case GizmoShape.Cone:
                        DrawWireCone(center, rec.Forward, rec.Radius, rec.AngleDeg);
                        break;
                    case GizmoShape.Line:
                        DrawWireLine(center, rec.Forward, rec.Radius);
                        break;
                }
            }
        }

        private void DrawWireCircle(Vector3 center, float radius)
        {
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= _gizmoSegments; i++)
            {
                float angle = (float)i / _gizmoSegments * 360f * Mathf.Deg2Rad;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        private void DrawWireCone(Vector3 center, Vector3 forward, float radius, float angleDeg)
        {
            float halfAngle = angleDeg * 0.5f;
            Quaternion leftRot  = Quaternion.Euler(0f, -halfAngle, 0f);
            Quaternion rightRot = Quaternion.Euler(0f,  halfAngle, 0f);

            Vector3 leftDir  = leftRot  * forward;
            Vector3 rightDir = rightRot * forward;

            Vector3 leftEnd  = center + leftDir  * radius;
            Vector3 rightEnd = center + rightDir * radius;

            Gizmos.DrawLine(center, leftEnd);
            Gizmos.DrawLine(center, rightEnd);

            int arcSegments = Mathf.Max(4, Mathf.RoundToInt(_gizmoSegments * angleDeg / 360f));
            Vector3 prev = leftEnd;
            for (int i = 1; i <= arcSegments; i++)
            {
                float t = (float)i / arcSegments;
                float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward;
                Vector3 next = center + dir * radius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        private void DrawWireLine(Vector3 origin, Vector3 forward, float length)
        {
            float halfWidth = 0.75f;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized * halfWidth;
            Vector3 endPoint = origin + forward * length;

            Gizmos.DrawLine(origin + right, endPoint + right);
            Gizmos.DrawLine(origin - right, endPoint - right);
            Gizmos.DrawLine(origin + right, origin - right);
            Gizmos.DrawLine(endPoint + right, endPoint - right);
        }

        // ══════════════════════════════════════════════════════════
        // Runtime internal
        // ══════════════════════════════════════════════════════════

        private GameObject SpawnAt(Vector3 center)
        {
            var go = _pool.Count > 0 ? _pool.Dequeue() : CreateInstance();
            go.SetActive(true);
            go.transform.position = new Vector3(center.x, 1.15f, center.z);
            return go;
        }

        private static void SetScale(GameObject go, float x, float z)
        {
            Transform visual = go.transform.childCount > 0 ? go.transform.GetChild(0) : go.transform;
            visual.localScale = new Vector3(x, 0.05f, z);
        }

        private IEnumerator FadeAndReturn(GameObject go, Color color)
        {
            var rend = go.GetComponentInChildren<Renderer>();
            if (rend == null) { ReturnToPool(go); yield break; }

            rend.material.color = color;

            float half = _duration * 0.5f;
            yield return new WaitForSeconds(half);

            float elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Lerp(color.a, 0f, elapsed / half);
                rend.material.color = new Color(color.r, color.g, color.b, a);
                yield return null;
            }

            ReturnToPool(go);
        }

        private void ReturnToPool(GameObject go)
        {
            go.SetActive(false);
            _pool.Enqueue(go);
        }

        private GameObject CreateInstance()
        {
            var go = Instantiate(_indicatorPrefab, transform);
            go.name = "[RangeIndicator]";
            go.SetActive(false);

            var col = go.GetComponentInChildren<Collider>();
            if (col != null) Destroy(col);

            return go;
        }
    }
}
