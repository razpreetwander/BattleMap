using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleMaps.CollisionSystem
{
    public enum CollisionCategory
    {
        None = 0,
        Head = 1,
        Trail = 2,
        Zone = 3,
        Trigger = 4,
        Obstacle = 5,
        ArenaBorder = 6
    }

    public enum EntityKind
    {
        Player = 0,
        Bot = 1,
        Train = 2,
        PowerField = 3,
        Pad = 4,
        PumpTower = 5
    }

    public readonly struct Edge
    {
        public readonly int OwnerId;
        public readonly int SegmentIndex;
        public readonly Vector2 A;
        public readonly Vector2 B;

        public Edge(int ownerId, int segmentIndex, Vector2 a, Vector2 b)
        {
            OwnerId = ownerId;
            SegmentIndex = segmentIndex;
            A = a;
            B = b;
        }
    }

    public readonly struct TrailEnterHit
    {
        public readonly int HitterHeadId;
        public readonly int TrailOwnerId;
        public readonly int SegmentIndex;
        public readonly Vector2 HitPoint;

        public TrailEnterHit(int hitterHeadId, int trailOwnerId, int segmentIndex, Vector2 hitPoint)
        {
            HitterHeadId = hitterHeadId;
            TrailOwnerId = trailOwnerId;
            SegmentIndex = segmentIndex;
            HitPoint = hitPoint;
        }
    }

    public readonly struct TriggerContact
    {
        public readonly int HeadId;
        public readonly int TriggerId;
        public readonly Vector2 Position;

        public TriggerContact(int headId, int triggerId, Vector2 pos)
        {
            HeadId = headId;
            TriggerId = triggerId;
            Position = pos;
        }
    }

    public struct HeadPose
    {
        public int OwnerId;
        public Vector2 Position;
        public Vector2 Velocity;
        public float BodyRadius;
        public float TrailHitRadius;
        public bool SimulationEnabled;
        public bool IgnoreOwnTrail;
        public bool IgnoreAllTrails;
        public bool IgnoreShieldShells;
        public bool IsAirborneOrUnderground;
    }

    /// <summary>
    /// Spatial hash grid accelerating trail segment overlap queries.
    /// Reconstructed from BattleMaps.CollisionSystem.EdgeGridWorld (index 3079) & EdgeStorage (index 3080).
    /// </summary>
    public sealed class EdgeGridWorld
    {
        private readonly float _cellSize;
        private readonly float _invCellSize;
        private readonly Dictionary<long, List<int>> _cells = new(256);
        private readonly List<Edge> _edges = new(512);
        private readonly Dictionary<int, List<int>> _ownerEdgeIndices = new(32);

        public EdgeGridWorld(float cellSize = 4f)
        {
            _cellSize = Mathf.Max(0.5f, cellSize);
            _invCellSize = 1f / _cellSize;
        }

        private static long CellKey(int cx, int cy) => ((long)cx << 32) | (uint)cy;

        public void AppendSegment(int ownerId, Vector2 a, Vector2 b)
        {
            if (!_ownerEdgeIndices.TryGetValue(ownerId, out var ownerList))
            {
                ownerList = new List<int>(64);
                _ownerEdgeIndices[ownerId] = ownerList;
            }

            int edgeIdx = _edges.Count;
            var edge = new Edge(ownerId, ownerList.Count, a, b);
            _edges.Add(edge);
            ownerList.Add(edgeIdx);

            int minX = Mathf.FloorToInt(Mathf.Min(a.x, b.x) * _invCellSize);
            int maxX = Mathf.FloorToInt(Mathf.Max(a.x, b.x) * _invCellSize);
            int minY = Mathf.FloorToInt(Mathf.Min(a.y, b.y) * _invCellSize);
            int maxY = Mathf.FloorToInt(Mathf.Max(a.y, b.y) * _invCellSize);

            for (int cx = minX; cx <= maxX; cx++)
            {
                for (int cy = minY; cy <= maxY; cy++)
                {
                    long key = CellKey(cx, cy);
                    if (!_cells.TryGetValue(key, out var bucket))
                    {
                        bucket = new List<int>(8);
                        _cells[key] = bucket;
                    }
                    bucket.Add(edgeIdx);
                }
            }
        }

        public void ResetOwner(int ownerId)
        {
            if (!_ownerEdgeIndices.TryGetValue(ownerId, out var list) || list.Count == 0)
                return;

            // Mark edges dead by setting OwnerId = -1; compact if needed
            for (int i = 0; i < list.Count; i++)
            {
                int idx = list[i];
                if (idx >= 0 && idx < _edges.Count)
                    _edges[idx] = new Edge(-1, -1, Vector2.zero, Vector2.zero);
            }
            list.Clear();

            // Periodic compaction when dead ratio is high
            if (_edges.Count > 4096)
                Compact();
        }

        public void Clear()
        {
            _cells.Clear();
            _edges.Clear();
            _ownerEdgeIndices.Clear();
        }

        public int GetOwnerSegmentCount(int ownerId)
            => _ownerEdgeIndices.TryGetValue(ownerId, out var list) ? list.Count : 0;

        public void GetOwnerTrailEdges(int ownerId, List<Edge> output)
        {
            output.Clear();
            if (!_ownerEdgeIndices.TryGetValue(ownerId, out var list)) return;
            for (int i = 0; i < list.Count; i++)
            {
                int idx = list[i];
                if (idx >= 0 && idx < _edges.Count && _edges[idx].OwnerId == ownerId)
                    output.Add(_edges[idx]);
            }
        }

        public bool QueryTrailOverlapCircleFirst(
            Vector2 center,
            float radius,
            int queryingOwnerId,
            bool ignoreOwnTrail,
            int selfTailGraceSegments,
            out TrailEnterHit hit)
        {
            hit = default;
            float rSq = radius * radius;
            int minX = Mathf.FloorToInt((center.x - radius) * _invCellSize);
            int maxX = Mathf.FloorToInt((center.x + radius) * _invCellSize);
            int minY = Mathf.FloorToInt((center.y - radius) * _invCellSize);
            int maxY = Mathf.FloorToInt((center.y + radius) * _invCellSize);

            int selfCount = GetOwnerSegmentCount(queryingOwnerId);

            for (int cx = minX; cx <= maxX; cx++)
            {
                for (int cy = minY; cy <= maxY; cy++)
                {
                    if (!_cells.TryGetValue(CellKey(cx, cy), out var bucket)) continue;
                    for (int i = 0; i < bucket.Count; i++)
                    {
                        int idx = bucket[i];
                        if (idx >= _edges.Count) continue;
                        var e = _edges[idx];
                        if (e.OwnerId < 0) continue;

                        if (e.OwnerId == queryingOwnerId)
                        {
                            if (ignoreOwnTrail) continue;
                            // Do not self-hit the most recent segments directly behind the player head
                            if (e.SegmentIndex >= selfCount - selfTailGraceSegments) continue;
                        }

                        Vector2 closest = ClosestPointOnSegment(e.A, e.B, center);
                        if ((center - closest).sqrMagnitude <= rSq)
                        {
                            hit = new TrailEnterHit(queryingOwnerId, e.OwnerId, e.SegmentIndex, closest);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private void Compact()
        {
            var alive = new List<Edge>(_edges.Count / 2);
            for (int i = 0; i < _edges.Count; i++)
            {
                if (_edges[i].OwnerId >= 0) alive.Add(_edges[i]);
            }
            _cells.Clear();
            _edges.Clear();
            _ownerEdgeIndices.Clear();
            for (int i = 0; i < alive.Count; i++)
                AppendSegment(alive[i].OwnerId, alive[i].A, alive[i].B);
        }

        public static Vector2 ClosestPointOnSegment(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 1e-8f) return a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            return a + ab * t;
        }
    }

    /// <summary>
    /// Circular or Polygonal arena border reconstructed from BattleMaps.CollisionSystem.ArenaBorder (index 3068).
    /// </summary>
    public sealed class ArenaBorder
    {
        public bool IsCircular { get; private set; } = true;
        public float Radius { get; private set; } = 32f;
        public List<Vector2> Polyline { get; private set; } = new();

        public void SetCircular(float radius, int segments = 64)
        {
            IsCircular = true;
            Radius = radius;
            Polyline = BattleMaps.Clipper.ZoneUtils.GetPointsOnCircle(Vector2.zero, radius, segments);
        }

        public void SetBorderPolyline(IReadOnlyList<Vector2> points)
        {
            IsCircular = false;
            Polyline = new List<Vector2>(points);
            float maxR = 1f;
            for (int i = 0; i < Polyline.Count; i++)
                maxR = Mathf.Max(maxR, Polyline[i].magnitude);
            Radius = maxR;
        }

        public bool IsOutside(Vector2 pos, float margin = 0f)
        {
            if (IsCircular)
                return pos.sqrMagnitude > (Radius - margin) * (Radius - margin);
            return !BattleMaps.Clipper.ZoneUtils.ContainsPoint(Polyline, pos);
        }

        public float TotalArenaArea()
            => IsCircular ? Mathf.PI * Radius * Radius : BattleMaps.Clipper.ZoneUtils.CalculatePolygonArea(Polyline);

        public Vector2 ClampInside(Vector2 pos, float margin = 0.2f)
        {
            if (IsCircular)
            {
                float maxR = Mathf.Max(0.5f, Radius - margin);
                if (pos.sqrMagnitude > maxR * maxR)
                    return pos.normalized * maxR;
                return pos;
            }
            if (!IsOutside(pos)) return pos;
            BattleMaps.Clipper.ZoneUtils.ClosestPointOnPolygonWithNormal(Polyline, pos, out Vector2 closest, out _, out _);
            return closest;
        }
    }
}
