using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleMaps.World.Controller
{
    [Serializable]
    public struct CountryMapDefinition
    {
        public string CountryId;
        public string DisplayName;
        public string Continent;
        public Vector2 MapPinNormalized; // (0..1, 0..1) inside continent visual map panel
        public Color FlagPrimary;
        public Color FlagSecondary;
        public Vector2[] Silhouette; // Normalized polygon vertices in [-1, 1]
    }

    /// <summary>
    /// Reconstructed from BattleMaps.World.Controller.WorldCountryGeometryResolver (IL2CPP 6 methods).
    /// Provides playable country-shaped PolygonArena silhouettes & continent map pin coordinates for all 25 World countries!
    /// </summary>
    public static class WorldCountryGeometryResolver
    {
        private static readonly Dictionary<string, CountryMapDefinition> Registry = new();
        private static bool _initialized;

        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            // --- EUROPE ---
            Register("europe_france", "France", "Europe", new Vector2(0.38f, 0.48f), new Color(0.14f, 0.34f, 0.82f), new Color(0.92f, 0.24f, 0.28f),
                new[] { v(-0.2f,0.95f), v(0.45f,0.75f), v(0.88f,0.35f), v(0.75f,-0.25f), v(0.85f,-0.72f), v(0.20f,-0.92f), v(-0.45f,-0.80f), v(-0.55f,-0.20f), v(-0.95f,0.28f), v(-0.55f,0.68f) });
            Register("europe_germany", "Germany", "Europe", new Vector2(0.54f, 0.62f), new Color(0.18f, 0.18f, 0.20f), new Color(0.96f, 0.78f, 0.14f),
                new[] { v(-0.25f,0.96f), v(0.32f,0.90f), v(0.72f,0.62f), v(0.65f,0.05f), v(0.82f,-0.45f), v(0.45f,-0.92f), v(-0.35f,-0.88f), v(-0.78f,-0.42f), v(-0.72f,0.30f), v(-0.52f,0.78f) });
            Register("europe_italy", "Italy", "Europe", new Vector2(0.58f, 0.28f), new Color(0.12f, 0.66f, 0.34f), new Color(0.90f, 0.22f, 0.26f),
                new[] { v(-0.68f,0.92f), v(0.05f,0.95f), v(0.28f,0.58f), v(0.12f,0.22f), v(0.62f,-0.28f), v(0.90f,-0.62f), v(0.65f,-0.95f), v(0.28f,-0.82f), v(0.20f,-0.50f), v(-0.30f,0.12f), v(-0.78f,0.48f) });
            Register("europe_spain", "Spain", "Europe", new Vector2(0.22f, 0.24f), new Color(0.88f, 0.18f, 0.22f), new Color(0.98f, 0.78f, 0.12f),
                new[] { v(-0.88f,0.72f), v(0.18f,0.82f), v(0.92f,0.55f), v(0.72f,-0.08f), v(0.42f,-0.75f), v(-0.28f,-0.88f), v(-0.85f,-0.58f), v(-0.72f,0.12f) });
            Register("europe_united_kingdom", "United Kingdom", "Europe", new Vector2(0.26f, 0.74f), new Color(0.14f, 0.28f, 0.76f), new Color(0.88f, 0.18f, 0.26f),
                new[] { v(-0.22f,0.96f), v(0.32f,0.88f), v(0.45f,0.40f), v(0.62f,-0.15f), v(0.82f,-0.62f), v(0.28f,-0.92f), v(-0.68f,-0.88f), v(-0.38f,-0.32f), v(-0.55f,0.22f), v(-0.48f,0.72f) });

            // --- ASIA & OCEANIA ---
            Register("asia_&_oceania_india", "India", "Asia & Oceania", new Vector2(0.24f, 0.56f), new Color(1.0f, 0.58f, 0.14f), new Color(0.12f, 0.66f, 0.30f),
                new[] { v(-0.18f,0.98f), v(0.24f,0.84f), v(0.46f,0.46f), v(0.92f,0.42f), v(0.82f,0.10f), v(0.52f,0.02f), v(0.34f,-0.38f), v(0.05f,-0.96f), v(-0.22f,-0.90f), v(-0.44f,-0.32f), v(-0.82f,0.08f), v(-0.72f,0.44f), v(-0.42f,0.68f) });
            Register("asia_&_oceania_japan", "Japan", "Asia & Oceania", new Vector2(0.78f, 0.74f), new Color(0.94f, 0.94f, 0.96f), new Color(0.88f, 0.16f, 0.24f),
                new[] { v(0.42f,0.96f), v(0.82f,0.78f), v(0.68f,0.32f), v(0.42f,-0.18f), v(-0.08f,-0.58f), v(-0.58f,-0.92f), v(-0.85f,-0.70f), v(-0.45f,-0.25f), v(0.05f,0.15f), v(0.25f,0.58f) });
            Register("asia_&_oceania_australia", "Australia", "Asia & Oceania", new Vector2(0.72f, 0.24f), new Color(0.12f, 0.30f, 0.78f), new Color(0.94f, 0.68f, 0.16f),
                new[] { v(-0.78f,0.35f), v(-0.22f,0.78f), v(0.32f,0.88f), v(0.88f,0.48f), v(0.92f,-0.25f), v(0.58f,-0.82f), v(0.02f,-0.62f), v(-0.62f,-0.72f), v(-0.92f,-0.22f) });
            Register("asia_&_oceania_south_korea", "South Korea", "Asia & Oceania", new Vector2(0.64f, 0.72f), new Color(0.16f, 0.36f, 0.86f), new Color(0.88f, 0.22f, 0.28f),
                new[] { v(-0.35f,0.92f), v(0.42f,0.88f), v(0.65f,0.32f), v(0.62f,-0.38f), v(0.28f,-0.92f), v(-0.42f,-0.85f), v(-0.58f,-0.20f), v(-0.52f,0.48f) });
            Register("asia_&_oceania_thailand", "Thailand", "Asia & Oceania", new Vector2(0.46f, 0.44f), new Color(0.84f, 0.18f, 0.24f), new Color(0.16f, 0.28f, 0.72f),
                new[] { v(-0.42f,0.94f), v(0.35f,0.88f), v(0.75f,0.42f), v(0.68f,-0.02f), v(0.18f,-0.22f), v(0.08f,-0.94f), v(-0.32f,-0.90f), v(-0.30f,-0.15f), v(-0.62f,0.35f) });

            // --- NORTH AMERICA ---
            Register("north_america_usa", "USA", "North America", new Vector2(0.48f, 0.56f), new Color(0.16f, 0.30f, 0.76f), new Color(0.88f, 0.20f, 0.26f),
                new[] { v(-0.94f,0.62f), v(-0.18f,0.72f), v(0.52f,0.68f), v(0.95f,0.52f), v(0.82f,-0.10f), v(0.62f,-0.75f), v(0.25f,-0.48f), v(-0.12f,-0.82f), v(-0.52f,-0.48f), v(-0.92f,-0.28f) });
            Register("north_america_canada", "Canada", "North America", new Vector2(0.52f, 0.80f), new Color(0.90f, 0.16f, 0.22f), new Color(0.96f, 0.96f, 0.98f),
                new[] { v(-0.95f,0.62f), v(-0.40f,0.92f), v(0.28f,0.95f), v(0.92f,0.55f), v(0.85f,-0.22f), v(0.32f,-0.65f), v(-0.25f,-0.52f), v(-0.88f,-0.42f) });
            Register("north_america_mexico", "Mexico", "North America", new Vector2(0.42f, 0.32f), new Color(0.10f, 0.62f, 0.34f), new Color(0.88f, 0.20f, 0.26f),
                new[] { v(-0.90f,0.78f), v(-0.15f,0.62f), v(0.28f,0.22f), v(0.92f,-0.12f), v(0.85f,-0.62f), v(0.28f,-0.78f), v(-0.28f,-0.42f), v(-0.72f,0.12f) });
            Register("north_america_cuba", "Cuba", "North America", new Vector2(0.72f, 0.30f), new Color(0.14f, 0.34f, 0.84f), new Color(0.88f, 0.20f, 0.26f),
                new[] { v(-0.96f,0.18f), v(-0.32f,0.48f), v(0.48f,0.32f), v(0.96f,-0.15f), v(0.62f,-0.52f), v(-0.18f,-0.25f), v(-0.82f,-0.32f) });
            Register("north_america_costa_rica", "Costa Rica", "North America", new Vector2(0.56f, 0.16f), new Color(0.14f, 0.30f, 0.78f), new Color(0.88f, 0.22f, 0.28f),
                new[] { v(-0.72f,0.78f), v(0.15f,0.68f), v(0.82f,0.08f), v(0.65f,-0.78f), v(0.05f,-0.55f), v(-0.62f,-0.12f) });

            // --- SOUTH AMERICA ---
            Register("south_america_brazil", "Brazil", "South America", new Vector2(0.64f, 0.56f), new Color(0.12f, 0.68f, 0.32f), new Color(0.98f, 0.82f, 0.14f),
                new[] { v(-0.48f,0.88f), v(0.22f,0.92f), v(0.92f,0.35f), v(0.82f,-0.25f), v(0.28f,-0.92f), v(-0.25f,-0.80f), v(-0.55f,-0.28f), v(-0.92f,0.18f) });
            Register("south_america_argentina", "Argentina", "South America", new Vector2(0.46f, 0.22f), new Color(0.36f, 0.74f, 0.96f), new Color(0.98f, 0.84f, 0.24f),
                new[] { v(-0.32f,0.96f), v(0.45f,0.82f), v(0.62f,0.22f), v(0.32f,-0.38f), v(0.08f,-0.96f), v(-0.32f,-0.92f), v(-0.48f,-0.18f), v(-0.52f,0.52f) });
            Register("south_america_peru", "Peru", "South America", new Vector2(0.26f, 0.54f), new Color(0.88f, 0.18f, 0.24f), new Color(0.96f, 0.96f, 0.98f),
                new[] { v(-0.65f,0.88f), v(0.22f,0.92f), v(0.62f,0.28f), v(0.72f,-0.45f), v(0.22f,-0.92f), v(-0.35f,-0.52f), v(-0.78f,0.18f) });
            Register("south_america_chile", "Chile", "South America", new Vector2(0.30f, 0.22f), new Color(0.14f, 0.34f, 0.82f), new Color(0.88f, 0.20f, 0.26f),
                new[] { v(-0.25f,0.98f), v(0.28f,0.94f), v(0.32f,0.12f), v(0.25f,-0.65f), v(0.12f,-0.98f), v(-0.28f,-0.92f), v(-0.35f,-0.12f), v(-0.32f,0.52f) });
            Register("south_america_colombia", "Colombia", "South America", new Vector2(0.32f, 0.80f), new Color(0.98f, 0.80f, 0.14f), new Color(0.14f, 0.34f, 0.82f),
                new[] { v(-0.18f,0.96f), v(0.55f,0.72f), v(0.82f,0.08f), v(0.58f,-0.75f), v(-0.08f,-0.92f), v(-0.68f,-0.45f), v(-0.72f,0.32f) });

            // --- AFRICA ---
            Register("africa_egypt", "Egypt", "Africa", new Vector2(0.66f, 0.78f), new Color(0.86f, 0.18f, 0.24f), new Color(0.94f, 0.78f, 0.16f),
                new[] { v(-0.82f,0.78f), v(0.42f,0.82f), v(0.88f,0.68f), v(0.72f,0.08f), v(0.90f,-0.75f), v(-0.82f,-0.75f) });
            Register("africa_kenya", "Kenya", "Africa", new Vector2(0.72f, 0.44f), new Color(0.14f, 0.16f, 0.18f), new Color(0.14f, 0.64f, 0.32f),
                new[] { v(-0.42f,0.92f), v(0.48f,0.82f), v(0.78f,0.22f), v(0.68f,-0.52f), v(0.18f,-0.94f), v(-0.48f,-0.62f), v(-0.72f,0.18f) });
            Register("africa_morocco", "Morocco", "Africa", new Vector2(0.24f, 0.82f), new Color(0.84f, 0.16f, 0.22f), new Color(0.12f, 0.62f, 0.32f),
                new[] { v(0.08f,0.94f), v(0.82f,0.78f), v(0.72f,0.18f), v(0.15f,-0.32f), v(-0.42f,-0.88f), v(-0.88f,-0.72f), v(-0.52f,0.08f), v(-0.22f,0.58f) });
            Register("africa_south_africa", "South Africa", "Africa", new Vector2(0.56f, 0.18f), new Color(0.12f, 0.64f, 0.34f), new Color(0.96f, 0.76f, 0.14f),
                new[] { v(-0.68f,0.62f), v(0.22f,0.88f), v(0.88f,0.52f), v(0.72f,-0.25f), v(0.18f,-0.78f), v(-0.52f,-0.85f), v(-0.88f,-0.32f) });
            Register("africa_nigeria", "Nigeria", "Africa", new Vector2(0.38f, 0.56f), new Color(0.10f, 0.64f, 0.34f), new Color(0.96f, 0.96f, 0.98f),
                new[] { v(-0.85f,0.72f), v(0.28f,0.82f), v(0.90f,0.68f), v(0.78f,-0.15f), v(0.32f,-0.82f), v(-0.25f,-0.85f), v(-0.82f,-0.52f) });
        }

        private static Vector2 v(float x, float y) => new Vector2(x, y);

        private static void Register(string id, string name, string continent, Vector2 pin, Color c1, Color c2, Vector2[] shape)
        {
            Registry[id] = new CountryMapDefinition
            {
                CountryId = id,
                DisplayName = name,
                Continent = continent,
                MapPinNormalized = pin,
                FlagPrimary = c1,
                FlagSecondary = c2,
                Silhouette = shape
            };
        }

        public static bool TryGetDefinition(string countryId, out CountryMapDefinition def)
        {
            EnsureInitialized();
            if (!string.IsNullOrEmpty(countryId) && Registry.TryGetValue(countryId, out def))
                return true;
            def = default;
            return false;
        }

        public static List<CountryMapDefinition> GetByContinent(string continent)
        {
            EnsureInitialized();
            var list = new List<CountryMapDefinition>();
            foreach (var kv in Registry)
                if (kv.Value.Continent == continent) list.Add(kv.Value);
            return list;
        }

        /// <summary>
        /// Builds a smooth, scaled CCW playable polygon for the given World Country.
        /// </summary>
        public static List<Vector2> BuildCountryArenaPolygon(string countryId, float scaleRadius = 34f)
        {
            EnsureInitialized();
            if (!TryGetDefinition(countryId, out var def) || def.Silhouette == null || def.Silhouette.Length < 3)
                return BattleMaps.Clipper.ZoneUtils.GetPointsOnCircle(Vector2.zero, scaleRadius, 48);

            // Subdivide & smooth edges with Chaikin corner refinement so country borders feel organic & smooth
            var pts = new List<Vector2>(def.Silhouette.Length);
            for (int i = 0; i < def.Silhouette.Length; i++)
                pts.Add(def.Silhouette[i] * scaleRadius);

            pts = ChaikinSmooth(pts, iterations: 2);
            return BattleMaps.Clipper.Clipper2CSharpLib.EnsureCCW(pts);
        }

        public static List<Vector2> ChaikinSmooth(List<Vector2> input, int iterations = 2)
        {
            var current = input;
            for (int it = 0; it < iterations; it++)
            {
                var next = new List<Vector2>(current.Count * 2);
                int n = current.Count;
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = current[i];
                    Vector2 b = current[(i + 1) % n];
                    next.Add(Vector2.Lerp(a, b, 0.25f));
                    next.Add(Vector2.Lerp(a, b, 0.75f));
                }
                current = next;
            }
            return current;
        }
    }
}
