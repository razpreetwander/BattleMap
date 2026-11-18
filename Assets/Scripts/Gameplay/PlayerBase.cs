using System;
using System.Collections.Generic;
using BattleMaps.Clipper;
using BattleMaps.CollisionSystem;
using BattleMaps.Signals;
using UnityEngine;

namespace BattleMaps.Gameplay
{
    /// <summary>
    /// Reconstructed from BattleMaps.Gameplay.PlayerBase (IL2CPP index 5102, 167 methods) & Player (index 149, 306 methods).
    /// Handles smooth 360-degree movement, trail generation, zone status transitions,
    /// speed modifiers, jump/tunnel elevation, status effects, and procedural cube+crown avatar rendering.
    /// </summary>
    public class PlayerBase : MonoBehaviour
    {
        public int PlayerId { get; private set; }
        public string DisplayName { get; private set; }
        public bool IsLocalPlayer { get; private set; }
        public int TeamId { get; private set; } = -1;
        public bool IsAlive { get; private set; }
        public bool IsInvincible { get; private set; }
        public bool IsAirborne { get; set; }
        public bool IsUnderground { get; set; }
        public bool IsShielded { get; set; }
        public bool HasRockTail { get; set; }
        public bool IgnoreOwnTrail { get; set; }

        public ZoneBase OwnedZone { get; private set; }
        public ZoneStatus CurrentZoneStatus { get; private set; } = ZoneStatus.InOwnZone;

        public Vector2 Position2D => new Vector2(transform.position.x, transform.position.z);
        public Vector2 Heading { get; private set; } = Vector2.up;
        public Vector2 DesiredHeading { get; set; } = Vector2.up;

        // Movement & Physics tuning (clean-room constants)
        [Header("Movement Tuning")]
        public float BaseSpeed = 7.2f;
        public float TurnSpeedDegPerSec = 360f;
        public float TrailSampleDistance = 0.42f;
        public float BodyRadius = 0.52f;
        public float TrailWidth = 0.65f;

        // Dynamic modifiers from kills, pads, powers, terrain
        public float PowerSpeedMultiplier { get; set; } = 1f;
        public float PadSpeedMultiplier { get; set; } = 1f;
        public float TerrainSpeedMultiplier { get; set; } = 1f;
        public float KillBoostTimer { get; private set; }
        public float FreezeTimer { get; private set; }
        public float SlowTimer { get; private set; }
        public float InvincibilityTimer { get; private set; }
        public float VerticalOffset { get; set; }

        // Combat & Progression stats
        public int Kills { get; private set; }
        public int LoopsClosed { get; private set; }
        public int CoinsCollected { get; private set; }
        public float XpGained { get; private set; }
        public bool IsKing { get; private set; }

        // Active trail points (empty while safely inside own zone)
        private readonly List<Vector2> _trailPoints = new(128);
        public IReadOnlyList<Vector2> TrailPoints => _trailPoints;
        public bool HasActiveTrail => _trailPoints.Count > 1;
        public float ActiveTrailLength { get; private set; }

        // Colors & Visuals
        public Color ZoneColor { get; private set; }
        public Color ShadowColor { get; private set; }
        public Color TrailColor { get; private set; }

        private Transform _visualRoot;
        private GameObject _crownObj;
        private LineRenderer _trailRenderer;
        private Material _bodyMaterial;
        private Material _trailMaterial;
        private CollisionWorld _collisionWorld;
        private ZoneManager _zoneManager;

        // Events matching PlayerBase/Quantum signals
        public event Action<PlayerBase> Died;
        public event Action<PlayerBase, float> LoopClosed;
        public event Action<PlayerBase, ZoneStatus, ZoneStatus> ZoneStatusChanged;

        public void Init(SpawnPlayerModel model, ZoneBase zone, CollisionWorld collisionWorld, ZoneManager zoneManager)
        {
            PlayerId = model.PlayerId;
            DisplayName = string.IsNullOrEmpty(model.DisplayName) ? $"Player {model.PlayerId}" : model.DisplayName;
            IsLocalPlayer = model.IsLocalPlayer;
            TeamId = model.TeamId;
            ZoneColor = model.PrimaryColor;
            ShadowColor = model.ShadowColor;
            TrailColor = model.TrailColor;
            OwnedZone = zone;
            _collisionWorld = collisionWorld;
            _zoneManager = zoneManager;

            Kills = 0;
            LoopsClosed = 0;
            CoinsCollected = 0;
            XpGained = 0f;
            ActiveTrailLength = 0f;
            VerticalOffset = 0f;
            IsAirborne = false;
            IsUnderground = false;
            IsAlive = true;
            CurrentZoneStatus = ZoneStatus.InOwnZone;
            _trailPoints.Clear();

            float startAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            Heading = new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle));
            DesiredHeading = Heading;

            transform.position = new Vector3(model.SpawnPosition.x, 0.45f, model.SpawnPosition.y);
            BuildProceduralVisuals();
            SetupInvincibilityMode(1.5f);

            _collisionWorld.RegisterHead(PlayerId, model.SpawnPosition, BodyRadius, BodyRadius * 0.85f);
            gameObject.SetActive(true);
            EventBus.Shared.Publish(new PlayerSpawnedSignal(PlayerId, IsLocalPlayer, model.SpawnPosition));
        }

        private void BuildProceduralVisuals()
        {
            if (_visualRoot == null)
            {
                var rootGo = new GameObject("AvatarVisual");
                rootGo.transform.SetParent(transform, false);
                _visualRoot = rootGo.transform;

                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "BodyCube";
                cube.transform.SetParent(_visualRoot, false);
                cube.transform.localScale = new Vector3(0.95f, 0.72f, 0.95f);
                Destroy(cube.GetComponent<Collider>());

                Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");
                _bodyMaterial = new Material(shader);
                cube.GetComponent<MeshRenderer>().sharedMaterial = _bodyMaterial;

                // Eyes so heading direction is immediately readable
                for (int side = -1; side <= 1; side += 2)
                {
                    var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    eye.name = side < 0 ? "EyeL" : "EyeR";
                    eye.transform.SetParent(_visualRoot, false);
                    eye.transform.localScale = Vector3.one * 0.18f;
                    eye.transform.localPosition = new Vector3(side * 0.22f, 0.18f, 0.44f);
                    Destroy(eye.GetComponent<Collider>());
                    var eyeMat = new Material(shader) { color = Color.white };
                    eye.GetComponent<MeshRenderer>().sharedMaterial = eyeMat;
                }

                // King Crown
                _crownObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                _crownObj.name = "KingCrown";
                _crownObj.transform.SetParent(_visualRoot, false);
                _crownObj.transform.localScale = new Vector3(0.45f, 0.2f, 0.45f);
                _crownObj.transform.localPosition = new Vector3(0f, 0.56f, 0f);
                Destroy(_crownObj.GetComponent<Collider>());
                _crownObj.GetComponent<MeshRenderer>().sharedMaterial = new Material(shader)
                {
                    color = new Color(1f, 0.84f, 0.1f)
                };
                _crownObj.SetActive(false);

                // Trail LineRenderer
                var trailGo = new GameObject($"Trail_{PlayerId}");
                trailGo.transform.SetParent(null);
                _trailRenderer = trailGo.AddComponent<LineRenderer>();
                _trailRenderer.useWorldSpace = true;
                _trailRenderer.startWidth = TrailWidth;
                _trailRenderer.endWidth = TrailWidth;
                _trailRenderer.numCapVertices = 4;
                _trailRenderer.numCornerVertices = 4;
                _trailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _trailRenderer.receiveShadows = false;
                _trailMaterial = new Material(Shader.Find("Sprites/Default") ?? shader);
                _trailRenderer.sharedMaterial = _trailMaterial;
                _trailRenderer.positionCount = 0;
            }

            if (_bodyMaterial != null) _bodyMaterial.color = Color.Lerp(ZoneColor, Color.white, 0.15f);
            if (_trailMaterial != null) _trailMaterial.color = TrailColor;
            if (_trailRenderer != null)
            {
                _trailRenderer.startColor = TrailColor;
                _trailRenderer.endColor = TrailColor;
                _trailRenderer.positionCount = 0;
            }

            // Apply 3D procedural outfit attachment
            int outfitIdx = IsLocalPlayer
                ? (BattleMaps.SaveStates.SaveStateController.Instance != null ? BattleMaps.SaveStates.SaveStateController.Instance.Data.SelectedCharacterIndex % 10 : 0)
                : (PlayerId % 10);
            BattleMaps.CharacterOutfits.CharacterOutfitBuilder.ApplyOutfit(_visualRoot, (BattleMaps.CharacterOutfits.SkinOutfitType)outfitIdx, ZoneColor);
        }

        public void SetOutfit(BattleMaps.CharacterOutfits.SkinOutfitType outfit)
        {
            BattleMaps.CharacterOutfits.CharacterOutfitBuilder.ApplyOutfit(_visualRoot, outfit, ZoneColor);
        }

        public void SetKing(bool isKing)
        {
            IsKing = isKing;
            if (_crownObj != null) _crownObj.SetActive(isKing);
        }

        public void SetupInvincibilityMode(float durationSeconds)
        {
            InvincibilityTimer = Mathf.Max(InvincibilityTimer, durationSeconds);
            IsInvincible = InvincibilityTimer > 0f;
        }

        public void ApplyKillSpeedBoost(float duration = 2.0f)
        {
            KillBoostTimer = Mathf.Max(KillBoostTimer, duration);
        }

        public void ApplyFreeze(float duration)
        {
            if (IsInvincible || IsShielded) return;
            FreezeTimer = Mathf.Max(FreezeTimer, duration);
        }

        public void ApplySlow(float duration)
        {
            if (IsInvincible || IsShielded) return;
            SlowTimer = Mathf.Max(SlowTimer, duration);
        }

        public float ComputeCurrentSpeed()
        {
            if (FreezeTimer > 0f) return 0f;
            float killMult = KillBoostTimer > 0f ? 1.25f : 1f;
            float slowMult = SlowTimer > 0f ? 0.55f : 1f;
            return BaseSpeed * PowerSpeedMultiplier * PadSpeedMultiplier * TerrainSpeedMultiplier * killMult * slowMult;
        }

        public void TickMovement(float deltaTime)
        {
            if (!IsAlive) return;

            UpdateTimers(deltaTime);

            // Smooth turn toward DesiredHeading
            if (DesiredHeading.sqrMagnitude > 0.001f)
            {
                Vector2 target = DesiredHeading.normalized;
                float currentAngle = Mathf.Atan2(Heading.y, Heading.x) * Mathf.Rad2Deg;
                float targetAngle = Mathf.Atan2(target.y, target.x) * Mathf.Rad2Deg;
                float nextAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, TurnSpeedDegPerSec * deltaTime);
                float rad = nextAngle * Mathf.Deg2Rad;
                Heading = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            }

            float speed = ComputeCurrentSpeed();
            Vector2 currentPos = Position2D;
            Vector2 nextPos = currentPos + Heading * (speed * deltaTime);

            // Shield/obstacle shell deflection
            if (_collisionWorld.IsBlockedByShell(PlayerId, nextPos, BodyRadius, out Vector2 pushOut))
            {
                nextPos = currentPos + pushOut * (speed * deltaTime);
                Heading = pushOut;
            }

            // Arena border check (EdgeSlingshot or kill)
            if (_collisionWorld.Border.IsOutside(nextPos, BodyRadius * 0.5f))
            {
                if (_zoneManager != null && _zoneManager.BorderKillsPlayers && !IsAirborne && !IsUnderground)
                {
                    Die(-1, "ArenaBorder");
                    return;
                }
                nextPos = _collisionWorld.Border.ClampInside(nextPos, BodyRadius * 0.5f);
                // Deflect smoothly along arena tangent
                Vector2 inward = (-nextPos).normalized;
                Heading = Vector2.Lerp(Heading, inward, 0.35f).normalized;
            }

            float visualY = 0.45f + VerticalOffset;
            transform.position = new Vector3(nextPos.x, visualY, nextPos.y);
            if (_visualRoot != null)
            {
                float yaw = Mathf.Atan2(Heading.x, Heading.y) * Mathf.Rad2Deg;
                _visualRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
                if (IsInvincible)
                {
                    float pulse = 0.9f + Mathf.PingPong(Time.time * 4f, 0.2f);
                    _visualRoot.localScale = Vector3.one * pulse;
                }
                else
                {
                    _visualRoot.localScale = Vector3.one;
                }
            }

            // Sync CollisionWorld
            _collisionWorld.SetHeadVelocity(PlayerId, Heading * speed);
            _collisionWorld.SetHeadFlags(PlayerId, IgnoreOwnTrail || HasRockTail, IsInvincible || HasRockTail, IsAirborne || IsUnderground);
            _collisionWorld.TeleportHead(PlayerId, nextPos);

            // Evaluate Zone Status & Trail
            UpdateZoneStatusAndTrail(nextPos);
        }

        private void UpdateTimers(float dt)
        {
            if (InvincibilityTimer > 0f)
            {
                InvincibilityTimer -= dt;
                if (InvincibilityTimer <= 0f) IsInvincible = false;
            }
            if (KillBoostTimer > 0f) KillBoostTimer -= dt;
            if (FreezeTimer > 0f) FreezeTimer -= dt;
            if (SlowTimer > 0f) SlowTimer -= dt;
        }

        private void UpdateZoneStatusAndTrail(Vector2 pos)
        {
            bool inOwn = OwnedZone != null && OwnedZone.IsPointInside(pos);
            ZoneStatus nextStatus;
            if (inOwn)
            {
                nextStatus = ZoneStatus.InOwnZone;
            }
            else
            {
                int overOwner = _collisionWorld.OverlapPointOwner(pos);
                bool sameTeam = TeamId >= 0 && _zoneManager != null && _zoneManager.IsSameTeam(PlayerId, overOwner);
                nextStatus = (overOwner >= 0 && !sameTeam) ? ZoneStatus.InEnemyZone : ZoneStatus.OutOfZone;
            }

            ZoneStatus prev = CurrentZoneStatus;
            CurrentZoneStatus = nextStatus;
            if (prev != nextStatus)
                ZoneStatusChanged?.Invoke(this, prev, nextStatus);

            if (nextStatus == ZoneStatus.InOwnZone)
            {
                if (_trailPoints.Count >= 2)
                {
                    // Closed a loop! Capture territory!
                    _trailPoints.Add(pos);
                    float captured = _zoneManager != null ? _zoneManager.ProcessTrailCapture(this, _trailPoints) : 0f;
                    LoopsClosed++;
                    ClearTrail();
                    LoopClosed?.Invoke(this, captured);
                }
                else
                {
                    ClearTrail();
                }
            }
            else
            {
                AddCurrentPositionToTrail(pos);
            }
        }

        public void AddCurrentPositionToTrail(Vector2 pos)
        {
            if (_trailPoints.Count == 0)
            {
                // Anchor trail cleanly on zone perimeter if leaving own zone
                if (OwnedZone != null && OwnedZone.Points.Count >= 3)
                {
                    ZoneUtils.ClosestPointOnPolygonWithNormal(OwnedZone.Points, pos, out Vector2 anchor, out _, out _);
                    _trailPoints.Add(anchor);
                }
                _trailPoints.Add(pos);
                if (_trailPoints.Count == 2)
                {
                    ActiveTrailLength += Vector2.Distance(_trailPoints[0], _trailPoints[1]);
                    if (!IsAirborne && !IsUnderground)
                        _collisionWorld.AppendSegment(PlayerId, _trailPoints[0], _trailPoints[1]);
                }
                SyncTrailVisual();
                return;
            }

            Vector2 last = _trailPoints[_trailPoints.Count - 1];
            float dist = Vector2.Distance(last, pos);
            if (dist >= TrailSampleDistance)
            {
                _trailPoints.Add(pos);
                ActiveTrailLength += dist;
                if (!IsAirborne && !IsUnderground)
                    _collisionWorld.AppendSegment(PlayerId, last, pos);
                SyncTrailVisual();
            }
        }

        public void ClearTrail()
        {
            _trailPoints.Clear();
            ActiveTrailLength = 0f;
            _collisionWorld?.ResetOwner(PlayerId);
            if (_trailRenderer != null) _trailRenderer.positionCount = 0;
        }

        private void SyncTrailVisual()
        {
            if (_trailRenderer == null) return;
            _trailRenderer.positionCount = _trailPoints.Count;
            float y = 0.16f + (IsAirborne ? VerticalOffset * 0.5f : 0f);
            for (int i = 0; i < _trailPoints.Count; i++)
            {
                Vector2 p = _trailPoints[i];
                _trailRenderer.SetPosition(i, new Vector3(p.x, y, p.y));
            }
        }

        public void RegisterKill()
        {
            Kills++;
            XpGained += 50f;
            ApplyKillSpeedBoost(2.0f);
        }

        public void AddCoins(int amount) => CoinsCollected += amount;
        public void AddXpGained(float xp) => XpGained += xp;
        public float GetCapturedPercent01() => OwnedZone != null ? OwnedZone.CapturedPercent01 : 0f;

        public void Die(int killerId, string reason)
        {
            if (!IsAlive) return;
            IsAlive = false;
            Vector2 deathPos = Position2D;

            ClearTrail();
            _collisionWorld?.RemoveHead(PlayerId);
            OwnedZone?.ResetForReuse();

            EventBus.Shared.Publish(new PlayerKilledSignal(PlayerId, killerId, reason, deathPos));
            EventBus.Shared.Publish(new FlyingTextSignal(
                IsLocalPlayer ? "ELIMINATED!" : "KILL!",
                transform.position + Vector3.up * 1.2f,
                Color.red,
                1.2f));

            Died?.Invoke(this);
            gameObject.SetActive(false);
        }

        public void OnDispose()
        {
            ClearTrail();
            if (_trailRenderer != null) Destroy(_trailRenderer.gameObject);
        }

        private void OnDestroy()
        {
            if (_trailRenderer != null) Destroy(_trailRenderer.gameObject);
            if (_bodyMaterial != null) Destroy(_bodyMaterial);
            if (_trailMaterial != null) Destroy(_trailMaterial);
        }
    }
}
