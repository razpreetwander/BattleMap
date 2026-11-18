using System.Collections.Generic;
using UnityEngine;

namespace BattleMaps.Clipper
{
    /// <summary>
    /// Territory geometry utilities reconstructed from Quantum.Paths.Zones.Utils.ZoneUtils (index 13817, 59 methods).
    /// Handles loop closure (CW vs CCW), area calculations, arena clamping, boundary snapping, and surviving island selection.
    /// </summary>
    public static class ZoneUtils
    {
        public static bool AreZonePointsValid(IReadOnlyList<Vector2> points)
            => points != null && points.Count >= 3 && CalculatePolygonArea(points) > 0.05f;

        public static float CalculatePolygonArea(IReadOnlyList<Vector2> points)
            => Mathf.Abs(Clipper2CSharpLib.SignedArea(points));

        public static Rect CalculatePolygonBounds(IReadOnlyList<Vector2> points)
            => points == null || points.Count == 0 ? new Rect() : Clipper2CSharpLib.ComputeBounds(points);

        public static bool TryCalculatePolygonCentroid(IReadOnlyList<Vector2> points, out Vector2 centroid)
        {
            centroid = Vector2.zero;
            if (points == null || points.Count < 3) return false;
            double area2 = 0, cx = 0, cy = 0;
            int n = points.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % n];
                double cross = (double)a.x * b.y - (double)b.x * a.y;
                area2 += cross;
                cx += (a.x + b.x) * cross;
                cy += (a.y + b.y) * cross;
            }
            if (System.Math.Abs(area2) < 1e-6)
            {
                for (int i = 0; i < n; i++) centroid += points[i];
                centroid /= n;
                return true;
            }
            double factor = 1.0 / (3.0 * area2);
            centroid = new Vector2((float)(cx * factor), (float)(cy * factor));
            return true;
        }

        public static bool TryFindInteriorPoint(IReadOnlyList<Vector2> points, out Vector2 interior)
        {
            interior = Vector2.zero;
            if (points == null || points.Count < 3) return false;
            interior = Clipper2CSharpLib.FindInteriorSamplePoint(points);
            return true;
        }

        public static bool ContainsPoint(IReadOnlyList<Vector2> polygon, Vector2 point)
            => polygon != null && polygon.Count >= 3 && Clipper2CSharpLib.PointInPolygon(point, polygon);

        public static bool IsPointInsideFast(IReadOnlyList<Vector2> polygon, Rect bounds, Vector2 point)
        {
            if (!bounds.Contains(point)) return false;
            return ContainsPoint(polygon, point);
        }

        public static List<Vector2> GetPointsOnCircle(Vector2 center, float radius, int segments = 24)
        {
            var pts = new List<Vector2>(segments);
            float step = Mathf.PI * 2f / segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * step;
                pts.Add(new Vector2(center.x + Mathf.Cos(a) * radius, center.y + Mathf.Sin(a) * radius));
            }
            return pts;
        }

        public static float GetZonePercentage(float zoneArea, float arenaTotalArea)
            => arenaTotalArea <= 1e-4f ? 0f : Mathf.Clamp01(zoneArea / arenaTotalArea);

        public static void ClampZonePointsToArena(List<Vector2> points, float arenaRadius)
        {
            if (points == null) return;
            float maxR = arenaRadius - 0.05f;
            float maxRSq = maxR * maxR;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i];
                if (p.sqrMagnitude > maxRSq)
                    points[i] = p.normalized * maxR;
            }
        }

        /// <summary>
        /// Clamps zone points cleanly against either a circular or custom polygon arena border (World / Chapters).
        /// </summary>
        public static void ClampZonePointsToPolygonArena(List<Vector2> points, IReadOnlyList<Vector2> arenaPolygon)
        {
            if (points == null || arenaPolygon == null || arenaPolygon.Count < 3) return;
            for (int i = 0; i < points.Count; i++)
            {
                if (!ContainsPoint(arenaPolygon, points[i]))
                {
                    ClosestPointOnPolygonWithNormal(arenaPolygon, points[i], out Vector2 closest, out _, out _);
                    points[i] = closest;
                }
            }
        }

        public static Vector2 ClosestPointOnEdge(Vector2 a, Vector2 b, Vector2 p, out float t)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 1e-8f) { t = 0f; return a; }
            t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            return a + ab * t;
        }

        public static int ClosestPointOnPolygonWithNormal(
            IReadOnlyList<Vector2> polygon,
            Vector2 point,
            out Vector2 closestPoint,
            out Vector2 outwardNormal,
            out float distSq)
        {
            closestPoint = point;
            outwardNormal = Vector2.up;
            distSq = float.MaxValue;
            int bestEdge = -1;
            if (polygon == null || polygon.Count < 2) return -1;

            int n = polygon.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % n];
                Vector2 cand = ClosestPointOnEdge(a, b, point, out _);
                float d2 = (point - cand).sqrMagnitude;
                if (d2 < distSq)
                {
                    distSq = d2;
                    closestPoint = cand;
                    Vector2 dir = (b - a).normalized;
                    outwardNormal = new Vector2(dir.y, -dir.x);
                    bestEdge = i;
                }
            }
            return bestEdge;
        }

        public static int FindNearestZonePointIndex(IReadOnlyList<Vector2> zone, Vector2 pos)
        {
            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < zone.Count; i++)
            {
                float d = (zone[i] - pos).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        /// <summary>
        /// Builds the two candidate closed polygons when a player's trail exits the zone at exitPoint
        /// and re-enters at enterPoint (matching ZoneUtils.GetCreatedAreaClockwise / CounterClockwise).
        /// </summary>
        public static List<Vector2> BuildBestTrailClosurePolygon(
            IReadOnlyList<Vector2> zone,
            IReadOnlyList<Vector2> trail)
        {
            if (zone == null || zone.Count < 3 || trail == null || trail.Count < 2)
                return null;

            Vector2 exitPt = trail[0];
            Vector2 enterPt = trail[trail.Count - 1];

            int exitEdge = ClosestPointOnPolygonWithNormal(zone, exitPt, out Vector2 exitSnap, out _, out _);
            int enterEdge = ClosestPointOnPolygonWithNormal(zone, enterPt, out Vector2 enterSnap, out _, out _);
            if (exitEdge < 0 || enterEdge < 0) return null;

            // Candidate 1: follow trail forward, then trace zone CCW from enterEdge+1 back to exitEdge
            var polyA = BuildArcClosure(zone, trail, exitEdge, exitSnap, enterEdge, enterSnap, forward: true);
            // Candidate 2: follow trail forward, then trace zone CW from enterEdge back to exitEdge+1
            var polyB = BuildArcClosure(zone, trail, exitEdge, exitSnap, enterEdge, enterSnap, forward: false);

            // Pick the union candidate that creates the larger valid captured territory cleanly,
            // or the smaller added lobe when encircling empty space (the smaller closed loop is the newly added lobe!).
            float areaA = CalculatePolygonArea(polyA);
            float areaB = CalculatePolygonArea(polyB);

            // In BattleMaps, when you draw a loop around an area outside your zone:
            // - One boundary walk encloses ONLY the new lobe (smaller area).
            // - The other boundary walk encloses the ENTIRE existing zone + the new lobe (larger area).
            // Unioning the larger closure with the existing zone naturally captures BOTH the lobe and any encircled holes!
            // However, if the trail wraps >50% around the zone, checking which lobe avoids swallowing the whole outside is key:
            // Compare area of (poly - zone) or pick the one whose boundary doesn't overlap inverted.
            // Simply unioning the smaller polygon with the zone ALWAYS adds the intended captured pocket cleanly
            // unless the player encircled an island completely (in which case the larger one covers the encircled pocket too).
            var chosen = areaA <= areaB ? polyA : polyB;
            if (CalculatePolygonArea(chosen) < 0.05f)
                chosen = areaA > areaB ? polyA : polyB;

            return Clipper2CSharpLib.EnsureCCW(Clipper2CSharpLib.CleanPolygon(chosen, 1e-4f));
        }

        private static List<Vector2> BuildArcClosure(
            IReadOnlyList<Vector2> zone,
            IReadOnlyList<Vector2> trail,
            int exitEdge,
            Vector2 exitSnap,
            int enterEdge,
            Vector2 enterSnap,
            bool forward)
        {
            int n = zone.Count;
            var poly = new List<Vector2>(trail.Count + n + 4);
            poly.Add(exitSnap);
            for (int i = 0; i < trail.Count; i++) poly.Add(trail[i]);
            poly.Add(enterSnap);

            if (forward)
            {
                int idx = (enterEdge + 1) % n;
                int target = (exitEdge + 1) % n;
                int guard = n + 2;
                while (idx != target && guard-- > 0)
                {
                    poly.Add(zone[idx]);
                    idx = (idx + 1) % n;
                }
            }
            else
            {
                int idx = enterEdge;
                int target = exitEdge;
                int guard = n + 2;
                while (idx != target && guard-- > 0)
                {
                    poly.Add(zone[idx]);
                    idx = (idx - 1 + n) % n;
                }
            }
            return poly;
        }

        /// <summary>
        /// Matching ZoneUtils.SelectSurvivingZonePiece / SelectSolutionContainingPoint.
        /// When an enemy zone is cut into multiple pieces, keeps the piece containing the owner
        /// (or the largest outer piece if the owner is outside on a trail).
        /// </summary>
        public static List<Vector2> SelectSurvivingZonePiece(
            IReadOnlyList<List<Vector2>> pieces,
            Vector2 ownerPos,
            Vector2? trailAnchor = null)
        {
            if (pieces == null || pieces.Count == 0) return null;

            // Filter to outer (CCW / positive area) polygons first
            List<Vector2> bestByArea = null;
            float bestArea = -1f;

            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                if (p == null || p.Count < 3) continue;
                if (!BattleMapsClipper.IsOuterPath(p)) continue;

                if (ContainsPoint(p, ownerPos))
                    return p;

                float area = CalculatePolygonArea(p);
                if (area > bestArea)
                {
                    bestArea = area;
                    bestByArea = p;
                }
            }

            if (trailAnchor.HasValue)
            {
                List<Vector2> nearest = null;
                float minDist = float.MaxValue;
                for (int i = 0; i < pieces.Count; i++)
                {
                    var p = pieces[i];
                    if (p == null || p.Count < 3 || !BattleMapsClipper.IsOuterPath(p)) continue;
                    ClosestPointOnPolygonWithNormal(p, trailAnchor.Value, out _, out _, out float d2);
                    if (d2 < minDist) { minDist = d2; nearest = p; }
                }
                if (nearest != null) return nearest;
            }

            return bestByArea;
        }
    }
}
