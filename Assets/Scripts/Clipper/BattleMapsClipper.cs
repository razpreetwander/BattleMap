using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleMaps.Clipper
{
    /// <summary>
    /// Integer-scaled 2D point matching Clipper2 Point64 used by BattleMapsClipper2Lib.dll & QuantumClipper.
    /// </summary>
    [Serializable]
    public struct Point64 : IEquatable<Point64>
    {
        public const double Scale = 1000.0;
        public const double InvScale = 1.0 / Scale;

        public long X;
        public long Y;

        public Point64(long x, long y)
        {
            X = x;
            Y = y;
        }

        public static Point64 FromVector2(Vector2 v) =>
            new Point64((long)Math.Round(v.x * Scale), (long)Math.Round(v.y * Scale));

        public Vector2 ToVector2() => new Vector2((float)(X * InvScale), (float)(Y * InvScale));

        public bool Equals(Point64 other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Point64 p && Equals(p);
        public override int GetHashCode() => unchecked((int)(X * 73856093L ^ Y * 19349663L));
        public static bool operator ==(Point64 a, Point64 b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Point64 a, Point64 b) => !(a == b);
    }

    public enum ClipOp
    {
        Union,
        Difference,
        Intersection
    }

    /// <summary>
    /// Reusable polygon buffer matching BattleMaps.Clipper.NestedBuffer (index 22739).
    /// </summary>
    public sealed class NestedBuffer
    {
        public readonly List<List<Vector2>> Paths = new(8);

        public void Clear()
        {
            for (int i = 0; i < Paths.Count; i++)
                Paths[i].Clear();
            Paths.Clear();
        }

        public List<Vector2> AddPath(int capacity = 32)
        {
            var list = new List<Vector2>(capacity);
            Paths.Add(list);
            return list;
        }
    }

    /// <summary>
    /// Pool matching BattleMaps.Clipper.Paths64Pool (index 22742).
    /// </summary>
    public static class Paths64Pool
    {
        private static readonly Stack<NestedBuffer> Pool = new(16);

        public static NestedBuffer Rent()
        {
            if (Pool.Count > 0)
            {
                var buf = Pool.Pop();
                buf.Clear();
                return buf;
            }
            return new NestedBuffer();
        }

        public static void Return(NestedBuffer buffer)
        {
            if (buffer == null) return;
            buffer.Clear();
            if (Pool.Count < 32) Pool.Push(buffer);
        }
    }

    /// <summary>
    /// Interface matching BattleMaps.Clipper.IClipper2Lib (index 22738, 7 methods).
    /// </summary>
    public interface IClipper2Lib
    {
        void UnionNonAlloc(IReadOnlyList<Vector2> subject, IReadOnlyList<Vector2> clip, NestedBuffer result);
        void DifferenceNonAlloc(IReadOnlyList<Vector2> subject, IReadOnlyList<Vector2> clip, NestedBuffer result);
        void IntersectionNonAlloc(IReadOnlyList<Vector2> subject, IReadOnlyList<Vector2> clip, NestedBuffer result);
        void InflatePathNonAlloc(IReadOnlyList<Vector2> path, float delta, NestedBuffer result);
        bool IsOuterPath(IReadOnlyList<Vector2> path);
        bool IsOverlapping(IReadOnlyList<Vector2> a, IReadOnlyList<Vector2> b);
        float GetSurfaceArea(IReadOnlyList<Vector2> path);
    }

    /// <summary>
    /// Clean-room planar-arrangement polygon boolean engine reconstructed to fulfill
    /// BattleMaps.Clipper.BattleMapsClipper (index 22740, 14 methods) & QuantumClipper (index 13812)
    /// without requiring native C++ Clipper2 DLLs.
    ///
    /// Algorithm:
    /// 1. Collect all segments from Subject and Clip polygons.
    /// 2. Compute all pairwise segment intersections and split edges into atomic sub-segments.
    /// 3. Build a planar half-edge graph and extract every bounded minimal face via leftmost-turn walks.
    /// 4. Classify each face interior point against Subject and Clip using even-odd winding.
    /// 5. Keep faces matching the requested boolean op (Union / Difference / Intersection).
    /// 6. Cancel opposite half-edges shared by two kept faces; stitch surviving boundary half-edges into clean contours.
    /// </summary>
    public sealed class Clipper2CSharpLib : IClipper2Lib
    {
        private const double SnapEps = 1e-4;
        private const double QuantizeScale = 10000.0;

        private struct Seg
        {
            public Vector2 A;
            public Vector2 B;
        }

        private readonly struct NodeKey : IEquatable<NodeKey>
        {
            public readonly long X;
            public readonly long Y;

            public NodeKey(double x, double y)
            {
                X = (long)Math.Round(x * QuantizeScale);
                Y = (long)Math.Round(y * QuantizeScale);
            }

            public Vector2 ToVector2() => new Vector2((float)(X / QuantizeScale), (float)(Y / QuantizeScale));
            public bool Equals(NodeKey other) => X == other.X && Y == other.Y;
            public override bool Equals(object obj) => obj is NodeKey k && Equals(k);
            public override int GetHashCode() => unchecked((int)(X * 73856093L ^ Y * 19349663L));
        }

        private readonly struct DirectedEdgeKey : IEquatable<DirectedEdgeKey>
        {
            public readonly int U;
            public readonly int V;
            public DirectedEdgeKey(int u, int v) { U = u; V = v; }
            public bool Equals(DirectedEdgeKey o) => U == o.U && V == o.V;
            public override bool Equals(object obj) => obj is DirectedEdgeKey o && Equals(o);
            public override int GetHashCode() => unchecked(U * 397 ^ V);
        }

        private struct HalfEdge
        {
            public int From;
            public int To;
            public double Angle;
            public bool Visited;
        }

        public void UnionNonAlloc(IReadOnlyList<Vector2> subject, IReadOnlyList<Vector2> clip, NestedBuffer result)
            => ExecuteBoolean(subject, clip, ClipOp.Union, result);

        public void DifferenceNonAlloc(IReadOnlyList<Vector2> subject, IReadOnlyList<Vector2> clip, NestedBuffer result)
            => ExecuteBoolean(subject, clip, ClipOp.Difference, result);

        public void IntersectionNonAlloc(IReadOnlyList<Vector2> subject, IReadOnlyList<Vector2> clip, NestedBuffer result)
            => ExecuteBoolean(subject, clip, ClipOp.Intersection, result);

        public void InflatePathNonAlloc(IReadOnlyList<Vector2> path, float delta, NestedBuffer result)
        {
            result.Clear();
            if (path == null || path.Count < 3) return;

            var clean = EnsureCCW(CleanPolygon(path));
            if (clean.Count < 3) return;

            var inflated = result.AddPath(clean.Count);
            int n = clean.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = clean[(i - 1 + n) % n];
                Vector2 curr = clean[i];
                Vector2 next = clean[(i + 1) % n];

                Vector2 e1 = (curr - prev).normalized;
                Vector2 e2 = (next - curr).normalized;
                // Outward normals for CCW polygon point right: (e.y, -e.x)
                Vector2 n1 = new Vector2(e1.y, -e1.x);
                Vector2 n2 = new Vector2(e2.y, -e2.x);
                Vector2 miter = n1 + n2;
                float lenSq = miter.sqrMagnitude;
                if (lenSq < 1e-6f)
                {
                    inflated.Add(curr + n1 * delta);
                }
                else
                {
                    miter /= Mathf.Sqrt(lenSq);
                    float dot = Mathf.Max(0.25f, Vector2.Dot(miter, n1));
                    float miterLen = Mathf.Clamp(delta / dot, -Mathf.Abs(delta) * 2.5f, Mathf.Abs(delta) * 2.5f);
                    inflated.Add(curr + miter * miterLen);
                }
            }
        }

        public bool IsOuterPath(IReadOnlyList<Vector2> path) => SignedArea(path) > 0f;

        public float GetSurfaceArea(IReadOnlyList<Vector2> path) => Mathf.Abs(SignedArea(path));

        public bool IsOverlapping(IReadOnlyList<Vector2> a, IReadOnlyList<Vector2> b)
        {
            if (a == null || b == null || a.Count < 3 || b.Count < 3) return false;
            var ba = ComputeBounds(a);
            var bb = ComputeBounds(b);
            if (ba.xMax < bb.xMin || bb.xMax < ba.xMin || ba.yMax < bb.yMin || bb.yMax < ba.yMin)
                return false;

            if (PointInPolygon(a[0], b) || PointInPolygon(b[0], a))
                return true;

            for (int i = 0; i < a.Count; i++)
            {
                Vector2 a1 = a[i], a2 = a[(i + 1) % a.Count];
                for (int j = 0; j < b.Count; j++)
                {
                    Vector2 b1 = b[j], b2 = b[(j + 1) % b.Count];
                    if (SegmentsIntersect(a1, a2, b1, b2, out _, out _))
                        return true;
                }
            }
            return false;
        }

        public void ExecuteBoolean(IReadOnlyList<Vector2> rawSubject, IReadOnlyList<Vector2> rawClip, ClipOp op, NestedBuffer result)
        {
            result.Clear();
            var subject = EnsureCCW(CleanPolygon(rawSubject));
            var clip = EnsureCCW(CleanPolygon(rawClip));

            bool hasSubj = subject.Count >= 3 && GetSurfaceArea(subject) > 1e-5f;
            bool hasClip = clip.Count >= 3 && GetSurfaceArea(clip) > 1e-5f;

            if (!hasSubj && !hasClip) return;
            if (!hasClip)
            {
                if (op == ClipOp.Union || op == ClipOp.Difference)
                    AddCopy(result, subject);
                return;
            }
            if (!hasSubj)
            {
                if (op == ClipOp.Union)
                    AddCopy(result, clip);
                return;
            }

            // Fast-path disjoint AABBs
            Rect bA = ComputeBounds(subject);
            Rect bB = ComputeBounds(clip);
            bool disjoint = bA.xMax < bB.xMin - 1e-3f || bB.xMax < bA.xMin - 1e-3f ||
                            bA.yMax < bB.yMin - 1e-3f || bB.yMax < bA.yMin - 1e-3f;
            if (disjoint)
            {
                if (op == ClipOp.Union) { AddCopy(result, subject); AddCopy(result, clip); }
                else if (op == ClipOp.Difference) { AddCopy(result, subject); }
                return;
            }

            // Step 1: Collect segments
            var segments = new List<Seg>(subject.Count + clip.Count);
            AppendPolygonSegments(subject, segments);
            AppendPolygonSegments(clip, segments);

            // Step 2: Split every segment at every intersection parameter t in (0, 1)
            var splitParams = new List<double>[segments.Count];
            for (int i = 0; i < segments.Count; i++)
                splitParams[i] = new List<double>(4) { 0.0, 1.0 };

            for (int i = 0; i < segments.Count; i++)
            {
                var s1 = segments[i];
                for (int j = i + 1; j < segments.Count; j++)
                {
                    var s2 = segments[j];
                    if (SegmentsIntersectPrecise(s1.A, s1.B, s2.A, s2.B, out double t, out double u))
                    {
                        if (t > 1e-6 && t < 1.0 - 1e-6) splitParams[i].Add(t);
                        if (u > 1e-6 && u < 1.0 - 1e-6) splitParams[j].Add(u);
                    }
                    else
                    {
                        // Handle collinear overlaps by projecting endpoints onto the other segment
                        TryAddCollinearSplit(s1.A, s1.B, s2.A, splitParams[i]);
                        TryAddCollinearSplit(s1.A, s1.B, s2.B, splitParams[i]);
                        TryAddCollinearSplit(s2.A, s2.B, s1.A, splitParams[j]);
                        TryAddCollinearSplit(s2.A, s2.B, s1.B, splitParams[j]);
                    }
                }
            }

            // Step 3: Build deduplicated planar vertex & undirected edge graph
            var nodeIndex = new Dictionary<NodeKey, int>(segments.Count * 2);
            var nodes = new List<Vector2>(segments.Count * 2);
            var undirectedEdges = new HashSet<DirectedEdgeKey>();

            int GetOrAddNode(double x, double y)
            {
                var key = new NodeKey(x, y);
                if (!nodeIndex.TryGetValue(key, out int idx))
                {
                    idx = nodes.Count;
                    nodeIndex[key] = idx;
                    nodes.Add(key.ToVector2());
                }
                return idx;
            }

            for (int i = 0; i < segments.Count; i++)
            {
                var ts = splitParams[i];
                ts.Sort();
                var s = segments[i];
                double dx = s.B.x - s.A.x;
                double dy = s.B.y - s.A.y;

                int prevIdx = GetOrAddNode(s.A.x, s.A.y);
                double prevT = 0.0;
                for (int k = 1; k < ts.Count; k++)
                {
                    double t = ts[k];
                    if (t - prevT < 1e-6) continue;
                    int nextIdx = GetOrAddNode(s.A.x + dx * t, s.A.y + dy * t);
                    if (nextIdx != prevIdx)
                    {
                        int lo = prevIdx < nextIdx ? prevIdx : nextIdx;
                        int hi = prevIdx < nextIdx ? nextIdx : prevIdx;
                        undirectedEdges.Add(new DirectedEdgeKey(lo, hi));
                        prevIdx = nextIdx;
                    }
                    prevT = t;
                }
            }

            if (undirectedEdges.Count < 3)
            {
                FallbackBoolean(subject, clip, op, result);
                return;
            }

            // Step 4: Create twin half-edges & adjacency sorted by outgoing angle
            var halfEdges = new List<HalfEdge>(undirectedEdges.Count * 2);
            var outgoing = new List<int>[nodes.Count];
            for (int i = 0; i < nodes.Count; i++) outgoing[i] = new List<int>(4);

            foreach (var e in undirectedEdges)
            {
                Vector2 pU = nodes[e.U];
                Vector2 pV = nodes[e.V];
                int id1 = halfEdges.Count;
                halfEdges.Add(new HalfEdge { From = e.U, To = e.V, Angle = Math.Atan2(pV.y - pU.y, pV.x - pU.x) });
                outgoing[e.U].Add(id1);

                int id2 = halfEdges.Count;
                halfEdges.Add(new HalfEdge { From = e.V, To = e.U, Angle = Math.Atan2(pU.y - pV.y, pU.x - pV.x) });
                outgoing[e.V].Add(id2);
            }

            for (int i = 0; i < nodes.Count; i++)
                outgoing[i].Sort((a, b) => halfEdges[a].Angle.CompareTo(halfEdges[b].Angle));

            // Map (u, v) -> halfEdge index for twin lookup
            var heLookup = new Dictionary<DirectedEdgeKey, int>(halfEdges.Count);
            for (int i = 0; i < halfEdges.Count; i++)
                heLookup[new DirectedEdgeKey(halfEdges[i].From, halfEdges[i].To)] = i;

            // Step 5: Trace minimal CCW faces using next-clockwise-from-reverse turn (leftmost turn in CCW interior)
            var keptDirectedEdges = new HashSet<DirectedEdgeKey>();
            var faceVerts = new List<Vector2>(32);
            var faceNodeIds = new List<int>(32);

            for (int startHe = 0; startHe < halfEdges.Count; startHe++)
            {
                if (halfEdges[startHe].Visited) continue;

                faceVerts.Clear();
                faceNodeIds.Clear();

                int curr = startHe;
                bool closed = false;
                int guard = halfEdges.Count + 2;

                while (guard-- > 0)
                {
                    var he = halfEdges[curr];
                    if (he.Visited) break;
                    he.Visited = true;
                    halfEdges[curr] = he;

                    faceNodeIds.Add(he.From);
                    faceVerts.Add(nodes[he.From]);

                    // Find twin (To -> From) in outgoing[he.To]
                    int twin = heLookup[new DirectedEdgeKey(he.To, he.From)];
                    var adj = outgoing[he.To];
                    int twinPos = adj.IndexOf(twin);
                    if (twinPos < 0) break;

                    // Previous index in CCW-sorted outgoing edges gives tightest left turn around interior
                    int nextHe = adj[(twinPos - 1 + adj.Count) % adj.Count];
                    if (nextHe == startHe)
                    {
                        closed = true;
                        break;
                    }
                    curr = nextHe;
                }

                if (!closed || faceVerts.Count < 3) continue;

                float area = SignedArea(faceVerts);
                // Only bounded CCW faces have strictly positive signed area (outer infinite face is negative)
                if (area <= 1e-5f) continue;

                Vector2 samplePt = FindInteriorSamplePoint(faceVerts);
                bool inSubj = PointInPolygon(samplePt, subject);
                bool inClip = PointInPolygon(samplePt, clip);

                bool keep = op switch
                {
                    ClipOp.Union => inSubj || inClip,
                    ClipOp.Difference => inSubj && !inClip,
                    ClipOp.Intersection => inSubj && inClip,
                    _ => false
                };

                if (!keep) continue;

                // Add directed edges; cancel opposite twins to dissolve shared internal borders!
                for (int i = 0; i < faceNodeIds.Count; i++)
                {
                    int u = faceNodeIds[i];
                    int v = faceNodeIds[(i + 1) % faceNodeIds.Count];
                    var rev = new DirectedEdgeKey(v, u);
                    if (!keptDirectedEdges.Remove(rev))
                        keptDirectedEdges.Add(new DirectedEdgeKey(u, v));
                }
            }

            if (keptDirectedEdges.Count < 3) return;

            // Step 6: Stitch surviving directed boundary edges into simple contours
            var nextMap = new Dictionary<int, List<int>>(keptDirectedEdges.Count);
            foreach (var e in keptDirectedEdges)
            {
                if (!nextMap.TryGetValue(e.U, out var list))
                {
                    list = new List<int>(2);
                    nextMap[e.U] = list;
                }
                list.Add(e.V);
            }

            var remaining = new HashSet<DirectedEdgeKey>(keptDirectedEdges);
            while (remaining.Count > 0)
            {
                DirectedEdgeKey first = default;
                foreach (var e in remaining) { first = e; break; }

                var loop = new List<Vector2>(32);
                int startNode = first.U;
                int currNode = first.U;
                int nextNode = first.V;
                remaining.Remove(first);
                loop.Add(nodes[currNode]);

                int safety = keptDirectedEdges.Count + 4;
                while (nextNode != startNode && safety-- > 0)
                {
                    loop.Add(nodes[nextNode]);
                    Vector2 inDir = (nodes[nextNode] - nodes[currNode]).normalized;
                    currNode = nextNode;

                    if (!nextMap.TryGetValue(currNode, out var candidates)) break;

                    // Pick unused outgoing edge making the tightest right/left consistent turn
                    int chosen = -1;
                    double bestScore = double.NegativeInfinity;
                    for (int c = 0; c < candidates.Count; c++)
                    {
                        int cand = candidates[c];
                        if (!remaining.Contains(new DirectedEdgeKey(currNode, cand))) continue;
                        Vector2 outDir = (nodes[cand] - nodes[currNode]).normalized;
                        double cross = inDir.x * outDir.y - inDir.y * outDir.x;
                        double dot = inDir.x * outDir.x + inDir.y * outDir.y;
                        double angle = Math.Atan2(cross, dot);
                        if (angle > bestScore)
                        {
                            bestScore = angle;
                            chosen = cand;
                        }
                    }

                    if (chosen < 0) break;
                    remaining.Remove(new DirectedEdgeKey(currNode, chosen));
                    nextNode = chosen;
                }

                var simplified = SimplifyCollinear(loop, 1e-3f);
                if (simplified.Count >= 3 && GetSurfaceArea(simplified) > 1e-4f)
                {
                    var dst = result.AddPath(simplified.Count);
                    dst.AddRange(simplified);
                }
            }
        }

        private void FallbackBoolean(List<Vector2> subject, List<Vector2> clip, ClipOp op, NestedBuffer result)
        {
            bool sInC = PointInPolygon(subject[0], clip);
            bool cInS = PointInPolygon(clip[0], subject);
            if (op == ClipOp.Union)
            {
                if (sInC) AddCopy(result, clip);
                else if (cInS) AddCopy(result, subject);
                else { AddCopy(result, subject); AddCopy(result, clip); }
            }
            else if (op == ClipOp.Difference)
            {
                if (!sInC) AddCopy(result, subject);
            }
            else if (op == ClipOp.Intersection)
            {
                if (sInC) AddCopy(result, subject);
                else if (cInS) AddCopy(result, clip);
            }
        }

        private static void AddCopy(NestedBuffer dst, List<Vector2> src)
        {
            var p = dst.AddPath(src.Count);
            p.AddRange(src);
        }

        private static void AppendPolygonSegments(List<Vector2> poly, List<Seg> segs)
        {
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % poly.Count];
                if ((b - a).sqrMagnitude > 1e-8f)
                    segs.Add(new Seg { A = a, B = b });
            }
        }

        private static void TryAddCollinearSplit(Vector2 a, Vector2 b, Vector2 p, List<double> list)
        {
            double abx = b.x - a.x, aby = b.y - a.y;
            double lenSq = abx * abx + aby * aby;
            if (lenSq < 1e-10) return;
            double apx = p.x - a.x, apy = p.y - a.y;
            double cross = Math.Abs(abx * apy - aby * apx);
            if (cross > SnapEps * Math.Sqrt(lenSq)) return;
            double t = (apx * abx + apy * aby) / lenSq;
            if (t > 1e-5 && t < 1.0 - 1e-5)
                list.Add(t);
        }

        public static bool SegmentsIntersectPrecise(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out double t, out double u)
        {
            t = u = 0;
            double rX = b.x - a.x, rY = b.y - a.y;
            double sX = d.x - c.x, sY = d.y - c.y;
            double denom = rX * sY - rY * sX;
            if (Math.Abs(denom) < 1e-10) return false;
            double qpX = c.x - a.x, qpY = c.y - a.y;
            t = (qpX * sY - qpY * sX) / denom;
            u = (qpX * rY - qpY * rX) / denom;
            return t >= -1e-6 && t <= 1.0 + 1e-6 && u >= -1e-6 && u <= 1.0 + 1e-6;
        }

        public static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out float t, out float u)
        {
            bool hit = SegmentsIntersectPrecise(a, b, c, d, out double dt, out double du);
            t = (float)dt;
            u = (float)du;
            return hit;
        }

        public static Vector2 FindInteriorSamplePoint(IReadOnlyList<Vector2> poly)
        {
            int n = poly.Count;
            // Centroid check first
            Vector2 c = Vector2.zero;
            for (int i = 0; i < n; i++) c += poly[i];
            c /= n;
            if (PointInPolygon(c, poly)) return c;

            // Ear midpoint guaranteed inside any simple polygon
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = poly[(i - 1 + n) % n];
                Vector2 curr = poly[i];
                Vector2 next = poly[(i + 1) % n];
                float cross = (curr.x - prev.x) * (next.y - curr.y) - (curr.y - prev.y) * (next.x - curr.x);
                if (cross > 1e-5f)
                {
                    Vector2 triCenter = (prev + curr + next) / 3f;
                    if (PointInPolygon(triCenter, poly)) return triCenter;
                }
            }

            // Fallback offset slightly inward from edge 0
            Vector2 e = (poly[1] - poly[0]).normalized;
            Vector2 inward = new Vector2(-e.y, e.x);
            return (poly[0] + poly[1]) * 0.5f + inward * 0.01f;
        }

        public static bool PointInPolygon(Vector2 pt, IReadOnlyList<Vector2> poly)
        {
            bool inside = false;
            int n = poly.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 pi = poly[i];
                Vector2 pj = poly[j];
                if (((pi.y > pt.y) != (pj.y > pt.y)) &&
                    (pt.x < (pj.x - pi.x) * (pt.y - pi.y) / ((pj.y - pi.y) + 1e-12f) + pi.x))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        public static float SignedArea(IReadOnlyList<Vector2> poly)
        {
            if (poly == null || poly.Count < 3) return 0f;
            double sum = 0.0;
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % n];
                sum += (double)a.x * b.y - (double)b.x * a.y;
            }
            return (float)(sum * 0.5);
        }

        public static List<Vector2> CleanPolygon(IReadOnlyList<Vector2> src, float minDistSq = 1e-6f)
        {
            var res = new List<Vector2>(src?.Count ?? 0);
            if (src == null) return res;
            for (int i = 0; i < src.Count; i++)
            {
                if (res.Count == 0 || (src[i] - res[res.Count - 1]).sqrMagnitude > minDistSq)
                    res.Add(src[i]);
            }
            while (res.Count > 1 && (res[res.Count - 1] - res[0]).sqrMagnitude <= minDistSq)
                res.RemoveAt(res.Count - 1);
            return res;
        }

        public static List<Vector2> EnsureCCW(List<Vector2> poly)
        {
            if (poly.Count >= 3 && SignedArea(poly) < 0f)
                poly.Reverse();
            return poly;
        }

        public static List<Vector2> SimplifyCollinear(List<Vector2> poly, float eps = 1e-3f)
        {
            if (poly == null || poly.Count < 4) return poly ?? new List<Vector2>();
            var outList = new List<Vector2>(poly.Count);
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = poly[(i - 1 + n) % n];
                Vector2 curr = poly[i];
                Vector2 next = poly[(i + 1) % n];
                Vector2 v1 = curr - prev;
                Vector2 v2 = next - curr;
                if (v1.sqrMagnitude < eps * eps) continue;
                float cross = Mathf.Abs(v1.x * v2.y - v1.y * v2.x);
                if (cross > eps * (v1.magnitude + v2.magnitude) * 0.05f || Vector2.Dot(v1, v2) < 0f)
                    outList.Add(curr);
            }
            return outList.Count >= 3 ? outList : poly;
        }

        public static Rect ComputeBounds(IReadOnlyList<Vector2> poly)
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < poly.Count; i++)
            {
                var p = poly[i];
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }
    }

    /// <summary>
    /// Static facade matching BattleMaps.Clipper.BattleMapsClipper (index 22740, 14 methods).
    /// </summary>
    public static class BattleMapsClipper
    {
        private static readonly IClipper2Lib Backend = new Clipper2CSharpLib();

        public static NestedBuffer GetNestedBuffer() => Paths64Pool.Rent();
        public static void Return(NestedBuffer buffer) => Paths64Pool.Return(buffer);

        public static void UnionNonAlloc(IReadOnlyList<Vector2> subject, IReadOnlyList<Vector2> clip, NestedBuffer result)
            => Backend.UnionNonAlloc(subject, clip, result);

        public static void DifferenceNonAlloc(IReadOnlyList<Vector2> subject, IReadOnlyList<Vector2> clip, NestedBuffer result)
            => Backend.DifferenceNonAlloc(subject, clip, result);

        public static void IntersectionNonAlloc(IReadOnlyList<Vector2> subject, IReadOnlyList<Vector2> clip, NestedBuffer result)
            => Backend.IntersectionNonAlloc(subject, clip, result);

        public static void InflatePathNonAlloc(IReadOnlyList<Vector2> path, float delta, NestedBuffer result)
            => Backend.InflatePathNonAlloc(path, delta, result);

        public static void ClipNonAlloc(IReadOnlyList<Vector2> subject, IReadOnlyList<Vector2> clip, ClipOp op, NestedBuffer result)
        {
            switch (op)
            {
                case ClipOp.Union: UnionNonAlloc(subject, clip, result); break;
                case ClipOp.Difference: DifferenceNonAlloc(subject, clip, result); break;
                case ClipOp.Intersection: IntersectionNonAlloc(subject, clip, result); break;
            }
        }

        public static bool IsOuterPath(IReadOnlyList<Vector2> path) => Backend.IsOuterPath(path);
        public static bool IsOverlapping(IReadOnlyList<Vector2> a, IReadOnlyList<Vector2> b) => Backend.IsOverlapping(a, b);
        public static float GetSurfaceArea(IReadOnlyList<Vector2> path) => Backend.GetSurfaceArea(path);
    }
}
