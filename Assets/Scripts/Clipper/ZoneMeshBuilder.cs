using System.Collections.Generic;
using UnityEngine;

namespace BattleMaps.Clipper
{
    /// <summary>
    /// Procedural Earcut triangulator + 3D raised territory slab builder
    /// matching ZoneBase.GenerateZoneMesh / BuildZoneCreationWalls / RecalculateUVs / EnsureSaggedShadowMesh.
    /// Produces crisp 3D BattleMaps painted territories (top cap + extruded side skirt) in XZ plane!
    /// </summary>
    public static class ZoneMeshBuilder
    {
        public static Mesh BuildZoneMesh(
            IReadOnlyList<Vector2> polygon,
            float topY = 0.12f,
            float bottomY = 0f,
            float uvScale = 0.25f,
            Mesh targetMesh = null)
        {
            var mesh = targetMesh != null ? targetMesh : new Mesh();
            mesh.Clear();

            if (polygon == null || polygon.Count < 3)
                return mesh;

            var clean = Clipper2CSharpLib.EnsureCCW(Clipper2CSharpLib.CleanPolygon(polygon, 1e-4f));
            if (clean.Count < 3) return mesh;

            var topTris = EarcutTriangulate(clean);
            if (topTris.Count < 3) return mesh;

            int n = clean.Count;
            // Top cap vertices: n, Side skirt vertices: n * 4 (flat-shaded crisp walls)
            int totalVerts = n + n * 4;
            var verts = new Vector3[totalVerts];
            var normals = new Vector3[totalVerts];
            var uvs = new Vector2[totalVerts];

            // Top face (XZ plane, Y = topY)
            for (int i = 0; i < n; i++)
            {
                Vector2 p = clean[i];
                verts[i] = new Vector3(p.x, topY, p.y);
                normals[i] = Vector3.up;
                uvs[i] = p * uvScale;
            }

            // Flip triangle winding for Unity XZ plane (so normal faces +Y)
            var topIndices = new int[topTris.Count];
            for (int i = 0; i < topTris.Count; i += 3)
            {
                topIndices[i] = topTris[i];
                topIndices[i + 1] = topTris[i + 2];
                topIndices[i + 2] = topTris[i + 1];
            }

            // Side skirt walls (submesh 1 so side wall can have darker shadow tint!)
            var wallIndices = new int[n * 6];
            int vCursor = n;
            int iCursor = 0;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = clean[i];
                Vector2 b = clean[(i + 1) % n];
                Vector2 edge = (b - a).normalized;
                Vector3 outNorm = new Vector3(edge.y, 0f, -edge.x);

                int v0 = vCursor++;
                int v1 = vCursor++;
                int v2 = vCursor++;
                int v3 = vCursor++;

                verts[v0] = new Vector3(a.x, topY, a.y);
                verts[v1] = new Vector3(b.x, topY, b.y);
                verts[v2] = new Vector3(b.x, bottomY, b.y);
                verts[v3] = new Vector3(a.x, bottomY, a.y);

                normals[v0] = normals[v1] = normals[v2] = normals[v3] = outNorm;
                uvs[v0] = new Vector2(0, 1);
                uvs[v1] = new Vector2(1, 1);
                uvs[v2] = new Vector2(1, 0);
                uvs[v3] = new Vector2(0, 0);

                wallIndices[iCursor++] = v0;
                wallIndices[iCursor++] = v1;
                wallIndices[iCursor++] = v2;
                wallIndices[iCursor++] = v0;
                wallIndices[iCursor++] = v2;
                wallIndices[iCursor++] = v3;
            }

            mesh.indexFormat = totalVerts > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(topIndices, 0);
            mesh.SetTriangles(wallIndices, 1);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Ear-clipping triangulator for arbitrary simple concave CCW polygons.
        /// </summary>
        public static List<int> EarcutTriangulate(IReadOnlyList<Vector2> poly)
        {
            int n = poly.Count;
            var tris = new List<int>(Mathf.Max(0, (n - 2) * 3));
            if (n < 3) return tris;

            var indices = new List<int>(n);
            for (int i = 0; i < n; i++) indices.Add(i);

            int maxIterations = n * n;
            int iter = 0;

            while (indices.Count > 3 && iter++ < maxIterations)
            {
                bool earFound = false;
                int count = indices.Count;

                for (int i = 0; i < count; i++)
                {
                    int iPrev = indices[(i - 1 + count) % count];
                    int iCurr = indices[i];
                    int iNext = indices[(i + 1) % count];

                    Vector2 a = poly[iPrev];
                    Vector2 b = poly[iCurr];
                    Vector2 c = poly[iNext];

                    // Must be strictly convex (CCW)
                    if (Cross(a, b, c) <= 1e-6f) continue;

                    // No other vertex may lie inside triangle (a, b, c)
                    bool containsOther = false;
                    for (int j = 0; j < count; j++)
                    {
                        int idx = indices[j];
                        if (idx == iPrev || idx == iCurr || idx == iNext) continue;
                        if (PointInTriangle(poly[idx], a, b, c))
                        {
                            containsOther = true;
                            break;
                        }
                    }

                    if (!containsOther)
                    {
                        tris.Add(iPrev);
                        tris.Add(iCurr);
                        tris.Add(iNext);
                        indices.RemoveAt(i);
                        earFound = true;
                        break;
                    }
                }

                // Degenerate vertex fallback: clip shallowest valid corner to guarantee termination
                if (!earFound)
                {
                    tris.Add(indices[0]);
                    tris.Add(indices[1]);
                    tris.Add(indices[2]);
                    indices.RemoveAt(1);
                }
            }

            if (indices.Count == 3)
            {
                tris.Add(indices[0]);
                tris.Add(indices[1]);
                tris.Add(indices[2]);
            }
            return tris;
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c)
            => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float c1 = Cross(a, b, p);
            float c2 = Cross(b, c, p);
            float c3 = Cross(c, a, p);
            return c1 >= -1e-6f && c2 >= -1e-6f && c3 >= -1e-6f;
        }
    }
}
