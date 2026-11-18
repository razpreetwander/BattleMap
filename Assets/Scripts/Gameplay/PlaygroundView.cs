using BattleMaps.Clipper;
using UnityEngine;

namespace BattleMaps.Gameplay
{
    /// <summary>
    /// Procedural 3D Arena Floor + Subtle BattleMap Grid + Red Border Ring
    /// reconstructed from PlaygroundView (IL2CPP index 156, 77 methods).
    /// </summary>
    public class PlaygroundView : MonoBehaviour
    {
        public float ArenaRadius { get; private set; } = 32f;

        private GameObject _floorObj;
        private LineRenderer _borderRing;
        private Texture2D _gridTexture;

        public void BuildArena(float radius, Color battleMapColor, Color borderColor, System.Collections.Generic.IReadOnlyList<Vector2> customPolygon = null)
        {
            ArenaRadius = radius;
            ClearArena();

            // Circular or Custom Country/Chapter Polygon arena floor mesh (XZ plane at y = -0.01)
            _floorObj = new GameObject("ArenaFloor");
            _floorObj.transform.SetParent(transform, false);
            var mf = _floorObj.AddComponent<MeshFilter>();
            var mr = _floorObj.AddComponent<MeshRenderer>();

            var arenaPts = (customPolygon != null && customPolygon.Count >= 3)
                ? new System.Collections.Generic.List<Vector2>(customPolygon)
                : ZoneUtils.GetPointsOnCircle(Vector2.zero, radius, 72);
            mf.sharedMesh = ZoneMeshBuilder.BuildZoneMesh(arenaPts, -0.01f, -0.6f, 0.35f);

            _gridTexture = CreateBattleMapGridTexture(battleMapColor);
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Texture");
            var topMat = new Material(shader) { mainTexture = _gridTexture, color = Color.white };
            if (topMat.HasProperty("_Glossiness")) topMat.SetFloat("_Glossiness", 0.05f);
            var sideMat = new Material(shader) { color = Color.Lerp(borderColor, Color.black, 0.25f) };
            mr.sharedMaterials = new[] { topMat, sideMat };

            // Crisp outer border ring tracing exact polygon contour
            var ringGo = new GameObject("ArenaBorderRing");
            ringGo.transform.SetParent(transform, false);
            _borderRing = ringGo.AddComponent<LineRenderer>();
            _borderRing.useWorldSpace = true;
            _borderRing.loop = true;
            _borderRing.startWidth = 0.45f;
            _borderRing.endWidth = 0.45f;
            _borderRing.positionCount = arenaPts.Count;
            _borderRing.sharedMaterial = new Material(Shader.Find("Sprites/Default") ?? shader) { color = borderColor };
            _borderRing.startColor = borderColor;
            _borderRing.endColor = borderColor;
            for (int i = 0; i < arenaPts.Count; i++)
                _borderRing.SetPosition(i, new Vector3(arenaPts[i].x, 0.05f, arenaPts[i].y));
        }

        private static Texture2D CreateBattleMapGridTexture(Color bg)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            Color line = Color.Lerp(bg, new Color(0.75f, 0.80f, 0.88f), 0.35f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool onLine = x < 3 || y < 3;
                    pixels[y * size + x] = onLine ? line : bg;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void ClearArena()
        {
            if (_floorObj != null) Destroy(_floorObj);
            if (_borderRing != null) Destroy(_borderRing.gameObject);
            if (_gridTexture != null) Destroy(_gridTexture);
        }

        private void OnDestroy() => ClearArena();
    }
}
