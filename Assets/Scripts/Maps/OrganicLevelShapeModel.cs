using System.Collections.Generic;
using BattleMaps.Clipper;
using UnityEngine;

namespace BattleMaps.Gameplay
{
    public enum OrganicShapeKind
    {
        OctagonPlains = 0,
        TurboStar = 1,
        CloverTunnels = 2,
        DumbbellValley = 3,
        StadiumShield = 4,
        GrandCrown = 5
    }

    /// <summary>
    /// Reconstructed from BattleMaps.Gameplay.OrganicLevelShapeModel (IL2CPP index 5122, 9 methods).
    /// Generates custom organic polygon arenas for Chapters & special event maps!
    /// </summary>
    public static class OrganicLevelShapeModel
    {
        public static List<Vector2> BuildChapterArenaPolygon(int chapterIndex, float baseRadius = 32f)
        {
            var kind = (OrganicShapeKind)(Mathf.Abs(chapterIndex) % 6);
            return BuildShape(kind, baseRadius);
        }

        public static List<Vector2> BuildShape(OrganicShapeKind kind, float r)
        {
            var pts = new List<Vector2>(64);
            switch (kind)
            {
                case OrganicShapeKind.OctagonPlains:
                    for (int i = 0; i < 8; i++)
                    {
                        float a = (i / 8f) * Mathf.PI * 2f + Mathf.PI / 8f;
                        pts.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
                    }
                    return BattleMaps.World.Controller.WorldCountryGeometryResolver.ChaikinSmooth(pts, 1);

                case OrganicShapeKind.TurboStar:
                    for (int i = 0; i < 12; i++)
                    {
                        float a = (i / 12f) * Mathf.PI * 2f;
                        float rad = (i % 2 == 0) ? r * 1.12f : r * 0.74f;
                        pts.Add(new Vector2(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad));
                    }
                    return BattleMaps.World.Controller.WorldCountryGeometryResolver.ChaikinSmooth(pts, 2);

                case OrganicShapeKind.CloverTunnels:
                    for (int i = 0; i < 64; i++)
                    {
                        float a = (i / 64f) * Mathf.PI * 2f;
                        float rad = r * (0.86f + 0.24f * Mathf.Cos(4f * a));
                        pts.Add(new Vector2(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad));
                    }
                    return pts;

                case OrganicShapeKind.DumbbellValley:
                    for (int i = 0; i < 64; i++)
                    {
                        float a = (i / 64f) * Mathf.PI * 2f;
                        float x = Mathf.Cos(a) * r * 1.20f;
                        float pinch = 0.58f + 0.42f * Mathf.Abs(Mathf.Cos(a));
                        float y = Mathf.Sin(a) * r * 0.85f * pinch;
                        pts.Add(new Vector2(x, y));
                    }
                    return pts;

                case OrganicShapeKind.StadiumShield:
                    pts.Add(new Vector2(-0.85f * r, 0.85f * r));
                    pts.Add(new Vector2(0.85f * r, 0.85f * r));
                    pts.Add(new Vector2(0.95f * r, 0.05f * r));
                    pts.Add(new Vector2(0.55f * r, -0.68f * r));
                    pts.Add(new Vector2(0f, -1.05f * r));
                    pts.Add(new Vector2(-0.55f * r, -0.68f * r));
                    pts.Add(new Vector2(-0.95f * r, 0.05f * r));
                    return Clipper2CSharpLib.EnsureCCW(BattleMaps.World.Controller.WorldCountryGeometryResolver.ChaikinSmooth(pts, 2));

                case OrganicShapeKind.GrandCrown:
                default:
                    for (int i = 0; i < 64; i++)
                    {
                        float a = (i / 64f) * Mathf.PI * 2f;
                        float rad = r * (0.90f + 0.16f * Mathf.Cos(5f * a) + 0.06f * Mathf.Sin(3f * a));
                        pts.Add(new Vector2(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad));
                    }
                    return pts;
            }
        }
    }
}
