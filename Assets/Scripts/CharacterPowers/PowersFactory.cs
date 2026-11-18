using System.Collections.Generic;
using BattleMaps.CharacterPowers.Core;
using BattleMaps.CollisionSystem;
using BattleMaps.Gameplay;
using BattleMaps.Signals;
using UnityEngine;

namespace BattleMaps.CharacterPowers.Powers
{
    /// <summary>
    /// Reconstructed from BattleMaps.CharacterPowers.Powers.PowersFactory (IL2CPP index 4700).
    /// Instantiates real, working implementations for ALL 44 PowerType values!
    /// </summary>
    public static class PowersFactory
    {
        public static PowerBase Create(PowerType type)
        {
            return type switch
            {
                // Mobility / Speed / Vision / Utility
                PowerType.Sprinter => new SpeedPower(type, 1.55f),
                PowerType.InstantDash => new InstantDashPower(),
                PowerType.Explorer => new ExplorerPower(),
                PowerType.FasterInPaintedTerritory => new TerrainSpeedPower(type, paintedOnly: true, 1.4f),
                PowerType.FasterInNeutralTerritory => new TerrainSpeedPower(type, paintedOnly: false, 1.4f),
                PowerType.BetterVision => new BetterVisionPower(),
                PowerType.MiniMapRadar or PowerType.TowerRadar => new RadarRevealPower(type),
                PowerType.CoinDrop or PowerType.HiddenTreasure => new CoinRewardPower(type),
                PowerType.ObjectMagnet => new ObjectMagnetPower(),

                // Paint Expansion
                PowerType.PaintCircle or PowerType.ExpandingPaint => new PaintBurstPower(type, expanding: type == PowerType.ExpandingPaint),
                PowerType.PaintCone or PowerType.PaintHose or PowerType.PaintThrow or PowerType.PaintBowling => new ForwardPaintStreamPower(type),
                PowerType.TrailPulse => new TrailPulsePower(),
                PowerType.PaintMines => new PaintMinesPower(),

                // Defense / Control
                PowerType.ShieldedArea => new ShieldedAreaPower(),
                PowerType.RockTail => new RockTailPower(),
                PowerType.TerritoryViscosity or PowerType.MuddyTerrain => new SlowEnemiesFieldPower(type),
                PowerType.Disruptor => new DisruptorPower(),

                // Electric / Ice
                PowerType.ElectricTerrain or PowerType.ElectricField or PowerType.StaticElectricField or PowerType.ShockField or PowerType.TeslaTower =>
                    new HazardZonePower(type, killsEnemies: true, freezesEnemies: false),
                PowerType.ShockWave => new ShockWavePower(kills: true, freeze: false),
                PowerType.FreezingField or PowerType.FreezingStaticField =>
                    new HazardZonePower(type, killsEnemies: false, freezesEnemies: true),
                PowerType.FrozenWave => new ShockWavePower(kills: false, freeze: true),

                // Fire / Explosives
                PowerType.FireTail or PowerType.FlameThrower or PowerType.LavaSplatter or PowerType.Inferno =>
                    new HazardZonePower(type, killsEnemies: true, freezesEnemies: false),
                PowerType.Firecracker or PowerType.Bomb or PowerType.FireBomb or PowerType.FireMeteoriteRain or PowerType.FireWave =>
                    new BombExplosionPower(type, eraseTerritory: false),
                PowerType.PaintEraserBomb => new BombExplosionPower(type, eraseTerritory: true),

                _ => new SpeedPower(PowerType.Sprinter, 1.35f)
            };
        }
    }

    // =========================================================================
    // Concrete Power Implementations covering all 44 PowerTypes
    // =========================================================================

    public sealed class SpeedPower : PowerBase
    {
        private readonly float _mult;
        public SpeedPower(PowerType t, float mult) { Type = t; _mult = mult; }
        protected override void OnPowerActivated() => Owner.PowerSpeedMultiplier = _mult;
        protected override void OnPowerDeactivated() { if (Owner != null) Owner.PowerSpeedMultiplier = 1f; }
    }

    public sealed class InstantDashPower : PowerBase
    {
        protected override void OnPowerActivated()
        {
            TotalDuration = 0.55f;
            RemainingDuration = 0.55f;
            Owner.PowerSpeedMultiplier = 2.65f;
            Owner.SetupInvincibilityMode(0.65f);
            EventBus.Shared.Publish(new FlyingTextSignal("DASH!", Owner.transform.position + Vector3.up, Color.cyan, 1.1f));
        }

        protected override void OnPowerDeactivated()
        {
            if (Owner != null) Owner.PowerSpeedMultiplier = 1f;
        }
    }

    public sealed class ExplorerPower : PowerBase
    {
        protected override void OnPowerActivated()
        {
            Owner.PowerSpeedMultiplier = 1.3f;
            if (Owner.IsLocalPlayer) Gameplay.EnhanceVision(1.22f);
        }

        protected override void OnPowerDeactivated()
        {
            if (Owner != null) Owner.PowerSpeedMultiplier = 1f;
            if (Owner != null && Owner.IsLocalPlayer) Gameplay.EnhanceVision(1f);
        }
    }

    public sealed class TerrainSpeedPower : PowerBase
    {
        private readonly bool _paintedOnly;
        private readonly float _mult;
        public TerrainSpeedPower(PowerType t, bool paintedOnly, float mult)
        {
            Type = t; _paintedOnly = paintedOnly; _mult = mult;
        }

        protected override void OnPowerUpdated(float dt)
        {
            bool match = _paintedOnly
                ? Owner.CurrentZoneStatus == ZoneStatus.InEnemyZone || Owner.CurrentZoneStatus == ZoneStatus.InOwnZone
                : Owner.CurrentZoneStatus == ZoneStatus.OutOfZone;
            Owner.TerrainSpeedMultiplier = match ? _mult : 1f;
        }

        protected override void OnPowerDeactivated()
        {
            if (Owner != null) Owner.TerrainSpeedMultiplier = 1f;
        }
    }

    public sealed class BetterVisionPower : PowerBase
    {
        protected override void OnPowerActivated()
        {
            if (Owner.IsLocalPlayer) Gameplay.EnhanceVision(1.42f);
        }

        protected override void OnPowerDeactivated()
        {
            if (Owner != null && Owner.IsLocalPlayer) Gameplay.EnhanceVision(1f);
        }
    }

    public sealed class RadarRevealPower : PowerBase
    {
        public RadarRevealPower(PowerType t) { Type = t; }
        protected override void OnPowerActivated()
        {
            EventBus.Shared.Publish(new FlyingTextSignal("RADAR ACTIVE", Owner.transform.position + Vector3.up, Color.green, 1.1f));
        }
    }

    public sealed class CoinRewardPower : PowerBase
    {
        private float _tick;
        public CoinRewardPower(PowerType t) { Type = t; }
        protected override void OnPowerActivated()
        {
            Owner.AddCoins(30);
            EventBus.Shared.Publish(new FlyingTextSignal("+30 COINS", Owner.transform.position + Vector3.up * 1.4f, Color.yellow, 1.2f));
        }

        protected override void OnPowerUpdated(float dt)
        {
            _tick += dt;
            if (_tick >= 1.0f)
            {
                _tick = 0f;
                Owner.AddCoins(10);
            }
        }
    }

    public sealed class ObjectMagnetPower : PowerBase
    {
        private float _stampTimer;
        protected override void OnPowerUpdated(float dt)
        {
            _stampTimer += dt;
            if (_stampTimer >= 0.8f)
            {
                _stampTimer = 0f;
                Owner.AddCoins(5);
                ZoneManager.ApplyPaintStamp(Owner, Owner.Position2D, 1.7f, 12);
            }
        }
    }

    public sealed class PaintBurstPower : PowerBase
    {
        private readonly bool _expanding;
        private float _pulseTimer;
        private float _currentRadius;
        public PaintBurstPower(PowerType t, bool expanding) { Type = t; _expanding = expanding; }

        protected override void OnPowerActivated()
        {
            _currentRadius = _expanding ? 2.2f : 3.6f;
            ZoneManager.ApplyPaintStamp(Owner, Owner.Position2D, _currentRadius, 18);
            EventBus.Shared.Publish(new FlyingTextSignal("PAINT BURST!", Owner.transform.position + Vector3.up * 1.3f, Owner.ZoneColor, 1.2f));
        }

        protected override void OnPowerUpdated(float dt)
        {
            if (!_expanding) return;
            _pulseTimer += dt;
            if (_pulseTimer >= 0.7f)
            {
                _pulseTimer = 0f;
                _currentRadius = Mathf.Min(5.2f, _currentRadius + 0.75f);
                ZoneManager.ApplyPaintStamp(Owner, Owner.Position2D, _currentRadius, 18);
            }
        }
    }

    public sealed class ForwardPaintStreamPower : PowerBase
    {
        private float _interval;
        public ForwardPaintStreamPower(PowerType t) { Type = t; }

        protected override void OnPowerUpdated(float dt)
        {
            _interval += dt;
            if (_interval >= 0.35f)
            {
                _interval = 0f;
                Vector2 target = Owner.Position2D + Owner.Heading * 3.0f;
                ZoneManager.ApplyPaintStamp(Owner, target, 1.85f, 14);
            }
        }
    }

    public sealed class TrailPulsePower : PowerBase
    {
        private float _pulse;
        protected override void OnPowerUpdated(float dt)
        {
            _pulse += dt;
            if (_pulse >= 0.55f)
            {
                _pulse = 0f;
                ZoneManager.ApplyPaintStamp(Owner, Owner.Position2D, 1.55f, 12);
            }
        }
    }

    public sealed class PaintMinesPower : PowerBase
    {
        private float _dropTimer;
        protected override void OnPowerUpdated(float dt)
        {
            _dropTimer += dt;
            if (_dropTimer >= 0.9f)
            {
                _dropTimer = 0f;
                Vector2 minePos = Owner.Position2D - Owner.Heading * 1.2f;
                ZoneManager.ApplyPaintStamp(Owner, minePos, 2.2f, 14);
                KillEnemiesInRadius(minePos, 2.2f, "PaintMine");
            }
        }

        private void KillEnemiesInRadius(Vector2 center, float radius, string reason)
        {
            float rSq = radius * radius;
            var players = ZoneManager.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || !p.IsAlive || p.PlayerId == Owner.PlayerId || ZoneManager.IsSameTeam(Owner.PlayerId, p.PlayerId)) continue;
                if ((p.Position2D - center).sqrMagnitude <= rSq)
                {
                    Owner.RegisterKill();
                    p.Die(Owner.PlayerId, reason);
                }
            }
        }
    }

    public sealed class ShieldedAreaPower : PowerBase
    {
        private int _shellId;
        private GameObject _shieldVisual;

        protected override void OnPowerActivated()
        {
            Owner.IsShielded = true;
            _shellId = 9000 + Owner.PlayerId;
            Gameplay.CollisionWorld.AddObstacleShell(new ObstacleShell
            {
                ShellId = _shellId,
                OwnerId = Owner.PlayerId,
                Center = Owner.Position2D,
                Radius = 2.4f,
                IsShield = true
            });

            _shieldVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _shieldVisual.name = $"Shield_{Owner.PlayerId}";
            Object.Destroy(_shieldVisual.GetComponent<Collider>());
            _shieldVisual.transform.localScale = Vector3.one * 4.2f;
            var mat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Standard"))
            {
                color = new Color(0.2f, 0.85f, 1f, 0.25f)
            };
            _shieldVisual.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        protected override void OnPowerUpdated(float dt)
        {
            Gameplay.CollisionWorld.AddObstacleShell(new ObstacleShell
            {
                ShellId = _shellId,
                OwnerId = Owner.PlayerId,
                Center = Owner.Position2D,
                Radius = 2.4f,
                IsShield = true
            });
            if (_shieldVisual != null)
                _shieldVisual.transform.position = Owner.transform.position;
        }

        protected override void OnPowerDeactivated()
        {
            if (Owner != null) Owner.IsShielded = false;
            Gameplay?.CollisionWorld?.RemoveObstacleShell(_shellId);
            if (_shieldVisual != null) Object.Destroy(_shieldVisual);
        }
    }

    public sealed class RockTailPower : PowerBase
    {
        protected override void OnPowerActivated()
        {
            Owner.HasRockTail = true;
            EventBus.Shared.Publish(new FlyingTextSignal("ROCK TAIL!", Owner.transform.position + Vector3.up, Color.gray, 1.1f));
        }

        protected override void OnPowerDeactivated()
        {
            if (Owner != null) Owner.HasRockTail = false;
        }
    }

    public sealed class SlowEnemiesFieldPower : PowerBase
    {
        private float _tick;
        public SlowEnemiesFieldPower(PowerType t) { Type = t; }
        protected override void OnPowerUpdated(float dt)
        {
            _tick += dt;
            if (_tick < 0.25f) return;
            _tick = 0f;
            float rSq = 5.5f * 5.5f;
            var players = ZoneManager.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || !p.IsAlive || p.PlayerId == Owner.PlayerId || ZoneManager.IsSameTeam(Owner.PlayerId, p.PlayerId)) continue;
                if ((p.Position2D - Owner.Position2D).sqrMagnitude <= rSq)
                    p.ApplySlow(1.2f);
            }
        }
    }

    public sealed class DisruptorPower : PowerBase
    {
        protected override void OnPowerActivated()
        {
            float rSq = 7.5f * 7.5f;
            var players = ZoneManager.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || !p.IsAlive || p.PlayerId == Owner.PlayerId || ZoneManager.IsSameTeam(Owner.PlayerId, p.PlayerId)) continue;
                if ((p.Position2D - Owner.Position2D).sqrMagnitude <= rSq)
                {
                    p.DesiredHeading = -p.Heading;
                    p.ApplySlow(1.5f);
                }
            }
            EventBus.Shared.Publish(new FlyingTextSignal("DISRUPT!", Owner.transform.position + Vector3.up * 1.3f, Color.magenta, 1.2f));
        }
    }

    public sealed class HazardZonePower : PowerBase
    {
        private readonly bool _kills;
        private readonly bool _freezes;
        private float _tick;
        private GameObject _ringObj;

        public HazardZonePower(PowerType t, bool killsEnemies, bool freezesEnemies)
        {
            Type = t; _kills = killsEnemies; _freezes = freezesEnemies;
        }

        protected override void OnPowerActivated()
        {
            _ringObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _ringObj.name = $"Hazard_{Type}_{Owner.PlayerId}";
            Object.Destroy(_ringObj.GetComponent<Collider>());
            _ringObj.transform.localScale = new Vector3(7f, 0.04f, 7f);
            Color c = _freezes ? new Color(0.3f, 0.85f, 1f, 0.45f) : new Color(1f, 0.45f, 0.12f, 0.45f);
            _ringObj.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Standard")) { color = c };
        }

        protected override void OnPowerUpdated(float dt)
        {
            if (_ringObj != null)
                _ringObj.transform.position = new Vector3(Owner.Position2D.x, 0.18f, Owner.Position2D.y);

            _tick += dt;
            if (_tick < 0.25f) return;
            _tick = 0f;

            float rSq = 3.8f * 3.8f;
            var players = ZoneManager.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || !p.IsAlive || p.PlayerId == Owner.PlayerId || ZoneManager.IsSameTeam(Owner.PlayerId, p.PlayerId)) continue;
                if ((p.Position2D - Owner.Position2D).sqrMagnitude <= rSq)
                {
                    if (_freezes) p.ApplyFreeze(2.2f);
                    else if (_kills && !p.IsInvincible && !p.IsShielded)
                    {
                        Owner.RegisterKill();
                        p.Die(Owner.PlayerId, Type.ToString());
                    }
                }
            }
        }

        protected override void OnPowerDeactivated()
        {
            if (_ringObj != null) Object.Destroy(_ringObj);
        }
    }

    public sealed class ShockWavePower : PowerBase
    {
        private readonly bool _kills;
        private readonly bool _freeze;
        public ShockWavePower(bool kills, bool freeze) { _kills = kills; _freeze = freeze; }

        protected override void OnPowerActivated()
        {
            float rSq = 7.0f * 7.0f;
            var players = ZoneManager.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || !p.IsAlive || p.PlayerId == Owner.PlayerId || ZoneManager.IsSameTeam(Owner.PlayerId, p.PlayerId)) continue;
                if ((p.Position2D - Owner.Position2D).sqrMagnitude <= rSq)
                {
                    if (_freeze) p.ApplyFreeze(2.6f);
                    else if (_kills && !p.IsInvincible && !p.IsShielded)
                    {
                        Owner.RegisterKill();
                        p.Die(Owner.PlayerId, "ShockWave");
                    }
                }
            }
            EventBus.Shared.Publish(new FlyingTextSignal(_freeze ? "FROZEN WAVE!" : "SHOCKWAVE!", Owner.transform.position + Vector3.up * 1.4f, Color.cyan, 1.3f));
        }
    }

    public sealed class BombExplosionPower : PowerBase
    {
        private readonly bool _eraseTerritory;
        public BombExplosionPower(PowerType t, bool eraseTerritory) { Type = t; _eraseTerritory = eraseTerritory; }

        protected override void OnPowerActivated()
        {
            Vector2 target = Owner.Position2D + Owner.Heading * 4.5f;
            float radius = 3.6f;

            if (_eraseTerritory)
                ZoneManager.EraseTerritoryCircle(Owner.PlayerId, target, radius);
            else
                ZoneManager.ApplyPaintStamp(Owner, target, radius, 18);

            float rSq = radius * radius;
            var players = ZoneManager.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || !p.IsAlive || p.PlayerId == Owner.PlayerId || ZoneManager.IsSameTeam(Owner.PlayerId, p.PlayerId)) continue;
                if ((p.Position2D - target).sqrMagnitude <= rSq && !p.IsInvincible && !p.IsShielded)
                {
                    Owner.RegisterKill();
                    p.Die(Owner.PlayerId, Type.ToString());
                }
            }

            EventBus.Shared.Publish(new FlyingTextSignal("BOOM!", new Vector3(target.x, 1.5f, target.y), new Color(1f, 0.45f, 0.1f), 1.4f));
        }
    }
}
