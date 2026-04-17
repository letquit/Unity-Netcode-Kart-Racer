using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kart
{
    public class Circuit : MonoBehaviour
    {
        [Header("Path Points")]
        public Transform[] waypoints;
        public Transform[] spawnPoints;

        [Header("Preview")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private bool loop = true;

        [Header("Point / Polyline Style")]
        [SerializeField] private float pointRadius = 0.5f;
        [SerializeField] private Color pointColor = new Color(1f, 0.9f, 0.1f, 1f);
        [SerializeField] private Color polylineColor = new Color(0.2f, 1f, 1f, 0.85f);

        [Header("Curve Preview (Catmull-Rom)")]
        [SerializeField] private bool drawCurve = true;
        [SerializeField, Range(4, 60)] private int samplesPerSegment = 16;
        [SerializeField] private Color curveColor = new Color(0.2f, 1f, 0.2f, 1f);

        [Header("Track Width Visualization")]
        [SerializeField] private bool drawTrackWidth = true;
        [Tooltip("赛道总宽度（米）")]
        [SerializeField] private float trackWidth = 8f;
        [Tooltip("是否画左右边界连接线")]
        [SerializeField] private bool drawBoundaryLines = true;
        [Tooltip("是否画每个点的横截面宽度线")]
        [SerializeField] private bool drawCrossSections = true;
        [SerializeField] private Color leftBoundaryColor = new Color(1f, 0.35f, 0.35f, 0.95f);
        [SerializeField] private Color rightBoundaryColor = new Color(0.35f, 0.6f, 1f, 0.95f);
        [SerializeField] private Color crossSectionColor = new Color(1f, 1f, 1f, 0.35f);

        [Header("Inner Racing Line Preview")]
        [SerializeField] private bool drawInnerLine = true;
        [Tooltip("偏向内侧的比例（相对于半宽） 0=中线, 1=贴近内边界")]
        [SerializeField, Range(0f, 1f)] private float innerBias = 0.35f;
        [Tooltip("转弯角度小于该值视为近似直线，不做明显内偏")]
        [SerializeField, Range(0f, 45f)] private float straightAngleDeadZone = 6f;
        [SerializeField] private Color innerLineColor = new Color(1f, 0.1f, 1f, 1f);

        [Header("Suspicious Segment Detection")]
        [SerializeField] private bool drawSuspicious = true;
        [Tooltip("相邻 waypoint 超过该距离视为“过稀”（橙色）")]
        [SerializeField] private float maxSegmentLength = 10f;
        [Tooltip("拐角超过该角度视为“过急”（红色）")]
        [SerializeField, Range(5f, 170f)] private float maxCornerAngle = 45f;
        [Tooltip("是否显示每个点处的角度数值")]
        [SerializeField] private bool showCornerAngleLabel = true;
        [SerializeField] private Color longSegmentColor = new Color(1f, 0.55f, 0f, 1f);
        [SerializeField] private Color sharpCornerColor = new Color(1f, 0.2f, 0.2f, 1f);

        [Header("Labels")]
        [SerializeField] private bool showIndexLabel = true;
        [SerializeField] private bool showWidthLabel = false;
        [SerializeField] private Vector3 labelOffset = new Vector3(0f, 0.8f, 0f);

        private void OnDrawGizmos()
        {
            if (!drawGizmos || waypoints == null || waypoints.Length == 0) return;

            DrawWaypointSpheres();
            DrawPolyline();

            if (drawCurve && waypoints.Length >= (loop ? 3 : 4))
                DrawCatmullRomCurve();

            if (drawTrackWidth && waypoints.Length >= 2)
                DrawTrackWidthAndInnerLine();

            if (drawSuspicious)
                DrawSuspiciousSegmentsAndCorners();

            DrawLabels();
        }

        private void DrawWaypointSpheres()
        {
            Gizmos.color = pointColor;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                Gizmos.DrawSphere(waypoints[i].position, pointRadius);
            }
        }

        private void DrawPolyline()
        {
            Gizmos.color = polylineColor;
            int count = waypoints.Length;
            int last = loop ? count : count - 1;

            for (int i = 0; i < last; i++)
            {
                Transform a = waypoints[i];
                Transform b = waypoints[(i + 1) % count];
                if (a == null || b == null) continue;
                Gizmos.DrawLine(a.position, b.position);
            }
        }

        private void DrawTrackWidthAndInnerLine()
        {
            int count = waypoints.Length;
            float half = Mathf.Max(0.01f, trackWidth * 0.5f);

            // 预计算每个 waypoint 的左右边界点 + 内偏点
            Vector3[] leftPts = new Vector3[count];
            Vector3[] rightPts = new Vector3[count];
            Vector3[] innerPts = new Vector3[count];
            bool[] valid = new bool[count];

            for (int i = 0; i < count; i++)
            {
                if (!TryGetCenterAndRight(i, out Vector3 c, out Vector3 rightDir)) continue;
                valid[i] = true;

                leftPts[i] = c - rightDir * half;
                rightPts[i] = c + rightDir * half;

                // 计算该点转弯符号：>0 左弯，<0 右弯
                float signedTurn = GetSignedTurnAt(i); // -180..180
                float absTurn = Mathf.Abs(signedTurn);

                // 小角度视为直线，减少内偏
                float t = Mathf.InverseLerp(straightAngleDeadZone, maxCornerAngle, absTurn);
                t = Mathf.Clamp01(t);

                // 左弯 => 内侧在左(负rightDir)；右弯 => 内侧在右(正rightDir)
                float side = signedTurn >= 0f ? -1f : 1f;
                float offset = half * innerBias * t * side;

                innerPts[i] = c + rightDir * offset;
            }

            // 横截面
            if (drawCrossSections)
            {
                Gizmos.color = crossSectionColor;
                for (int i = 0; i < count; i++)
                {
                    if (!valid[i]) continue;
                    Gizmos.DrawLine(leftPts[i], rightPts[i]);
                }
            }

            // 左右边界连接
            if (drawBoundaryLines)
            {
                int segCount = loop ? count : count - 1;

                Gizmos.color = leftBoundaryColor;
                for (int i = 0; i < segCount; i++)
                {
                    int j = (i + 1) % count;
                    if (!valid[i] || !valid[j]) continue;
                    Gizmos.DrawLine(leftPts[i], leftPts[j]);
                }

                Gizmos.color = rightBoundaryColor;
                for (int i = 0; i < segCount; i++)
                {
                    int j = (i + 1) % count;
                    if (!valid[i] || !valid[j]) continue;
                    Gizmos.DrawLine(rightPts[i], rightPts[j]);
                }
            }

            // 中线偏内推荐线（连线）
            if (drawInnerLine)
            {
                Gizmos.color = innerLineColor;
                int segCount = loop ? count : count - 1;
                for (int i = 0; i < segCount; i++)
                {
                    int j = (i + 1) % count;
                    if (!valid[i] || !valid[j]) continue;
                    Gizmos.DrawLine(innerPts[i], innerPts[j]);
                }
            }

#if UNITY_EDITOR
            if (showWidthLabel)
            {
                Handles.color = Color.white;
                for (int i = 0; i < count; i++)
                {
                    if (!valid[i]) continue;
                    Handles.Label((leftPts[i] + rightPts[i]) * 0.5f + Vector3.up * 0.25f, $"W {trackWidth:F1}m");
                }
            }
#endif
        }

        private bool TryGetCenterAndRight(int i, out Vector3 center, out Vector3 rightDir)
        {
            center = Vector3.zero;
            rightDir = Vector3.right;

            int count = waypoints.Length;
            if (count == 0 || i < 0 || i >= count) return false;
            if (waypoints[i] == null) return false;

            center = waypoints[i].position;

            // 用邻点估计切线，再与up叉乘得到右方向
            if (!TryGetPrevCurrNext(i, out Vector3 prev, out Vector3 curr, out Vector3 next))
                return false;

            Vector3 tangent = (next - prev);
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 1e-6f) tangent = waypoints[i].forward;

            tangent.Normalize();
            rightDir = Vector3.Cross(Vector3.up, tangent).normalized;
            return rightDir.sqrMagnitude > 0.0001f;
        }

        private float GetSignedTurnAt(int i)
        {
            if (!TryGetPrevCurrNext(i, out Vector3 prev, out Vector3 curr, out Vector3 next))
                return 0f;

            Vector3 inDir = (curr - prev);
            Vector3 outDir = (next - curr);
            inDir.y = 0f; outDir.y = 0f;

            if (inDir.sqrMagnitude < 1e-6f || outDir.sqrMagnitude < 1e-6f) return 0f;

            return Vector3.SignedAngle(inDir.normalized, outDir.normalized, Vector3.up);
        }

        private bool TryGetPrevCurrNext(int i, out Vector3 prev, out Vector3 curr, out Vector3 next)
        {
            prev = curr = next = Vector3.zero;
            int count = waypoints.Length;
            if (count < 2) return false;
            if (i < 0 || i >= count) return false;
            if (waypoints[i] == null) return false;

            int iPrev, iNext;

            if (loop)
            {
                iPrev = (i - 1 + count) % count;
                iNext = (i + 1) % count;
            }
            else
            {
                iPrev = Mathf.Max(i - 1, 0);
                iNext = Mathf.Min(i + 1, count - 1);
                if (iPrev == iNext) return false;
            }

            if (waypoints[iPrev] == null || waypoints[iNext] == null) return false;

            prev = waypoints[iPrev].position;
            curr = waypoints[i].position;
            next = waypoints[iNext].position;
            return true;
        }

        private void DrawCatmullRomCurve()
        {
            Gizmos.color = curveColor;
            int count = waypoints.Length;
            int segmentCount = loop ? count : count - 1;

            for (int i = 0; i < segmentCount; i++)
            {
                if (!TryGetCatmullPoints(i, out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3))
                    continue;

                Vector3 prev = p1;
                for (int s = 1; s <= samplesPerSegment; s++)
                {
                    float t = s / (float)samplesPerSegment;
                    Vector3 cur = CatmullRom(p0, p1, p2, p3, t);
                    Gizmos.DrawLine(prev, cur);
                    prev = cur;
                }
            }
        }

        private void DrawSuspiciousSegmentsAndCorners()
        {
            int count = waypoints.Length;
            if (count < 2) return;

            int segCount = loop ? count : count - 1;
            for (int i = 0; i < segCount; i++)
            {
                Transform a = waypoints[i];
                Transform b = waypoints[(i + 1) % count];
                if (a == null || b == null) continue;

                float len = Vector3.Distance(a.position, b.position);
                if (len > maxSegmentLength)
                {
                    Gizmos.color = longSegmentColor;
                    Gizmos.DrawLine(a.position, b.position);
#if UNITY_EDITOR
                    Handles.color = longSegmentColor;
                    Handles.Label((a.position + b.position) * 0.5f + Vector3.up * 0.35f, $"Long {len:F1}m");
#endif
                }
            }

            int start = loop ? 0 : 1;
            int endExclusive = loop ? count : count - 1;

            for (int i = start; i < endExclusive; i++)
            {
                if (!TryGetCornerPoints(i, out Vector3 prev, out Vector3 curr, out Vector3 next))
                    continue;

                Vector3 inDir = (curr - prev).normalized;
                Vector3 outDir = (next - curr).normalized;
                float cornerAngle = Vector3.Angle(inDir, outDir);

                if (showCornerAngleLabel)
                {
#if UNITY_EDITOR
                    Handles.color = Color.white;
                    Handles.Label(curr + Vector3.up * 0.6f, $"{cornerAngle:F0}°");
#endif
                }

                if (cornerAngle > maxCornerAngle)
                {
                    Gizmos.color = sharpCornerColor;
                    Gizmos.DrawLine(prev, curr);
                    Gizmos.DrawLine(curr, next);

                    float markLen = Mathf.Min(Vector3.Distance(prev, curr), Vector3.Distance(curr, next)) * 0.35f;
                    Vector3 p1 = curr - inDir * markLen;
                    Vector3 p2 = curr + outDir * markLen;
                    Gizmos.DrawLine(curr, p1);
                    Gizmos.DrawLine(curr, p2);

#if UNITY_EDITOR
                    Handles.color = sharpCornerColor;
                    Handles.Label(curr + Vector3.up * 1.0f, $"Sharp {cornerAngle:F0}°");
#endif
                }
            }
        }

        private bool TryGetCornerPoints(int i, out Vector3 prev, out Vector3 curr, out Vector3 next)
        {
            prev = curr = next = Vector3.zero;
            int count = waypoints.Length;

            int iPrev, iCurr, iNext;
            if (loop)
            {
                iPrev = (i - 1 + count) % count;
                iCurr = i;
                iNext = (i + 1) % count;
            }
            else
            {
                if (i <= 0 || i >= count - 1) return false;
                iPrev = i - 1; iCurr = i; iNext = i + 1;
            }

            Transform tPrev = waypoints[iPrev];
            Transform tCurr = waypoints[iCurr];
            Transform tNext = waypoints[iNext];
            if (tPrev == null || tCurr == null || tNext == null) return false;

            prev = tPrev.position; curr = tCurr.position; next = tNext.position;
            return true;
        }

        private bool TryGetCatmullPoints(int i, out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3)
        {
            p0 = p1 = p2 = p3 = Vector3.zero;
            int count = waypoints.Length;

            if (loop)
            {
                Transform t0 = waypoints[(i - 1 + count) % count];
                Transform t1 = waypoints[i % count];
                Transform t2 = waypoints[(i + 1) % count];
                Transform t3 = waypoints[(i + 2) % count];
                if (t0 == null || t1 == null || t2 == null || t3 == null) return false;

                p0 = t0.position; p1 = t1.position; p2 = t2.position; p3 = t3.position;
                return true;
            }
            else
            {
                int i0 = Mathf.Max(i - 1, 0);
                int i1 = i;
                int i2 = Mathf.Min(i + 1, count - 1);
                int i3 = Mathf.Min(i + 2, count - 1);

                Transform t0 = waypoints[i0];
                Transform t1 = waypoints[i1];
                Transform t2 = waypoints[i2];
                Transform t3 = waypoints[i3];
                if (t0 == null || t1 == null || t2 == null || t3 == null) return false;

                p0 = t0.position; p1 = t1.position; p2 = t2.position; p3 = t3.position;
                return true;
            }
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        private void DrawLabels()
        {
#if UNITY_EDITOR
            if (!showIndexLabel) return;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                Handles.color = Color.white;
                Handles.Label(waypoints[i].position + labelOffset, $"WP {i}");
            }
#endif
        }
    }
}