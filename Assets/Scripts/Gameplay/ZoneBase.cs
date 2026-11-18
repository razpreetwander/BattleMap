using System.Collections.Generic;
using BattleMaps.Clipper;
using BattleMaps.CollisionSystem;
using UnityEngine;

namespace BattleMaps.Gameplay
{
    /// <summary>
    /// Reconstructed from BattleMaps.Gameplay.ZoneBase (IL2CPP index 5114, 115 methods).
    /// Owns a player's 2D polygon territory, shoelace area, 3D raised top+wall mesh,
    /// capture inflate/sweep animation, and CollisionWorld sync.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ZoneBase : MonoBehaviour, IZone
    {
        public int OwnerId { get; private set; }
        public int TeamId { get; private set; } = -1;
        public float Area { get; private set; }
        public float CapturedPercent01 { get; private set; }
        public Rect Bounds { get; private set; }
        public Vector2 Centroid { get; private set; }

        private readonly List<Vector2> _points = new(64);
        public IReadOnlyList<Vector2> Points => _points;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _zoneMesh;
        private Material _topMaterial;
        private Material _wallMaterial;
        private CollisionWorld _collisionWorld;

        // Visual settings matching ZoneBase inflate & sweep feel
        public float ZoneHeight = 0.14f;
        private float _inflateTimer;
        private float _inflateDuration = 0.22f;
        private bool _isPlayingInflate;
        private Color _baseColor;
        private Color _shadowColor;

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _zoneMesh = new Mesh { name = "ZoneMesh" };
            _meshFilter.sharedMesh = _zoneMesh;
        }

        public void Init(int ownerId, int teamId, Color zoneColor, Color shadowColor, CollisionWorld collisionWorld)
        {
            OwnerId = ownerId;
            TeamId = teamId;
            _collisionWorld = collisionWorld;
            _baseColor = zoneColor;
            _shadowColor = shadowColor;

            EnsureMaterials();
            SetZoneColor(zoneColor);
            SetShadowColor(shadowColor);
            gameObject.SetActive(true);
        }

        private void EnsureMaterials()
        {
            if (_topMaterial == null)
            {
                Shader topShader = Shader.Find("BattleMaps/ZonePaintedSlab") ?? Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");
                Shader wallShader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");
                _topMaterial = new Material(topShader) { name = $"ZoneTop_{OwnerId}" };
                _wallMaterial = new Material(wallShader) { name = $"ZoneWall_{OwnerId}" };
                if (_topMaterial.HasProperty("_Glossiness")) _topMaterial.SetFloat("_Glossiness", 0.15f);
                if (_wallMaterial.HasProperty("_Glossiness")) _wallMaterial.SetFloat("_Glossiness", 0.1f);
                _meshRenderer.sharedMaterials = new[] { _topMaterial, _wallMaterial };
            }
        }

        public void SetZoneColor(Color color)
        {
            _baseColor = color;
            EnsureMaterials();
            if (_topMaterial != null) _topMaterial.color = color;
        }

        public void SetShadowColor(Color color)
        {
            _shadowColor = color;
            EnsureMaterials();
            if (_wallMaterial != null) _wallMaterial.color = color;
        }

        public void SetPolygonPoints(IReadOnlyList<Vector2> newPoints, float arenaTotalArea, bool animateGain = false)
        {
            _points.Clear();
            if (newPoints != null)
            {
                var clean = Clipper2CSharpLib.EnsureCCW(Clipper2CSharpLib.CleanPolygon(newPoints, 1e-4f));
                _points.AddRange(clean);
            }

            CalculateAreaWithShoelace(arenaTotalArea);
            RefreshZoneBounds();
            TryFindZoneCenter();
            GenerateZoneMesh();
            TryFeedCollisionWorld();

            if (animateGain) PlayGainSweep();
        }

        public float CalculateAreaWithShoelace(float arenaTotalArea = 1f)
        {
            Area = ZoneUtils.CalculatePolygonArea(_points);
            CapturedPercent01 = ZoneUtils.GetZonePercentage(Area, arenaTotalArea);
            return Area;
        }

        public void RefreshZoneBounds()
        {
            Bounds = ZoneUtils.CalculatePolygonBounds(_points);
        }

        public bool TryFindZoneCenter()
        {
            if (ZoneUtils.TryCalculatePolygonCentroid(_points, out Vector2 c))
            {
                Centroid = c;
                return true;
            }
            return false;
        }

        public bool IsPointInside(Vector2 point)
        {
            if (_points.Count < 3) return false;
            return ZoneUtils.IsPointInsideFast(_points, Bounds, point);
        }

        public void GenerateZoneMesh()
        {
            if (_meshFilter == null) Awake();
            ZoneMeshBuilder.BuildZoneMesh(_points, ZoneHeight, 0f, 0.25f, _zoneMesh);
            _meshFilter.sharedMesh = _zoneMesh;
        }

        public void TryFeedCollisionWorld()
        {
            if (_collisionWorld == null) return;
            if (_points.Count >= 3)
                _collisionWorld.RebuildZone(OwnerId, _points);
            else
                _collisionWorld.RemoveZone(OwnerId);
        }

        public void TryUnregisterFromCollisionWorld()
        {
            _collisionWorld?.RemoveZone(OwnerId);
        }

        public void PlayIntroductionInflateAnimation()
        {
            _inflateTimer = 0f;
            _inflateDuration = 0.35f;
            _isPlayingInflate = true;
            transform.localScale = new Vector3(1f, 0.05f, 1f);
        }

        public void PlayGainSweep()
        {
            _inflateTimer = 0f;
            _inflateDuration = 0.18f;
            _isPlayingInflate = true;
        }

        private void Update()
        {
            if (!_isPlayingInflate) return;
            _inflateTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_inflateTimer / Mathf.Max(0.01f, _inflateDuration));
            // Elastic pop along Y height matching BattleMaps zone capture bounce
            float popY = 1f + Mathf.Sin(t * Mathf.PI) * 0.45f;
            transform.localScale = new Vector3(1f, popY, 1f);

            if (_topMaterial != null)
            {
                float flash = Mathf.Sin(t * Mathf.PI) * 0.22f;
                if (_topMaterial.HasProperty("_SweepAmount"))
                    _topMaterial.SetFloat("_SweepAmount", Mathf.Sin(t * Mathf.PI) * 0.75f);
                _topMaterial.color = Color.Lerp(_baseColor, Color.white, flash);
            }

            if (t >= 1f)
            {
                _isPlayingInflate = false;
                transform.localScale = Vector3.one;
                if (_topMaterial != null)
                {
                    if (_topMaterial.HasProperty("_SweepAmount"))
                        _topMaterial.SetFloat("_SweepAmount", 0f);
                    _topMaterial.color = _baseColor;
                }
            }
        }

        public ZoneInfo ToZoneInfo() => new ZoneInfo
        {
            OwnerId = OwnerId,
            TeamId = TeamId,
            Area = Area,
            Percent01 = CapturedPercent01,
            Centroid = Centroid,
            Bounds = Bounds
        };

        public void ResetForReuse()
        {
            TryUnregisterFromCollisionWorld();
            _points.Clear();
            Area = 0f;
            CapturedPercent01 = 0f;
            if (_zoneMesh != null) _zoneMesh.Clear();
            _isPlayingInflate = false;
            transform.localScale = Vector3.one;
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            TryUnregisterFromCollisionWorld();
            if (_zoneMesh != null) Destroy(_zoneMesh);
            if (_topMaterial != null) Destroy(_topMaterial);
            if (_wallMaterial != null) Destroy(_wallMaterial);
        }
    }
}
