using BattleMaps.Gameplay;
using BattleMaps.Signals;
using UnityEngine;

namespace BattleMaps.VFX
{
    /// <summary>
    /// Reconstructed from BattleMaps.MapEdgeSparksVfx.MapEdgeSparksVfxController (112 methods)
    /// & BattleMaps.CharacterPowers.Characters.SharedVFXConfig.
    /// Spawns lightweight procedural ParticleSystem bursts for territory captures, kills, power activations, and border sparks!
    /// </summary>
    public class GameplayVfxController : MonoBehaviour
    {
        private GameplayController _gameplay;
        private ParticleSystem _burstPs;
        private ParticleSystem _edgeSparksPs;
        private Material _particleMat;

        public void Initialize(GameplayController gameplay)
        {
            _gameplay = gameplay;
            CreateParticleSystems();

            EventBus.Shared.Subscribe<ZoneCapturedSignal>(OnZoneCaptured);
            EventBus.Shared.Subscribe<PlayerKilledSignal>(OnPlayerKilled);
            EventBus.Shared.Subscribe<PowerActivatedSignal>(OnPowerActivated);
        }

        private void CreateParticleSystems()
        {
            _particleMat = new Material(Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard"));

            var burstGo = new GameObject("BurstParticlePool");
            burstGo.transform.SetParent(transform, false);
            _burstPs = burstGo.AddComponent<ParticleSystem>();
            var main = _burstPs.main;
            main.playOnAwake = false;
            main.startLifetime = 0.55f;
            main.startSpeed = 7.5f;
            main.startSize = 0.32f;
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = _burstPs.emission;
            emission.enabled = false;
            var shape = _burstPs.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.4f;
            var rend = _burstPs.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = _particleMat;

            var sparksGo = new GameObject("MapEdgeSparks");
            sparksGo.transform.SetParent(transform, false);
            _edgeSparksPs = sparksGo.AddComponent<ParticleSystem>();
            var sm = _edgeSparksPs.main;
            sm.playOnAwake = false;
            sm.startLifetime = 0.28f;
            sm.startSpeed = 5.0f;
            sm.startSize = 0.20f;
            sm.startColor = new Color(1f, 0.78f, 0.20f);
            sm.simulationSpace = ParticleSystemSimulationSpace.World;
            var se = _edgeSparksPs.emission;
            se.enabled = false;
            _edgeSparksPs.GetComponent<ParticleSystemRenderer>().sharedMaterial = _particleMat;
        }

        public void EmitBurst(Vector3 worldPos, Color color, int count = 18)
        {
            if (_burstPs == null) return;
            _burstPs.transform.position = worldPos;
            var main = _burstPs.main;
            main.startColor = color;
            _burstPs.Emit(count);
        }

        public void TickEdgeSparks()
        {
            if (_gameplay == null || !_gameplay.IsInGame || _gameplay.ZoneManager.MainPlayer == null) return;
            var local = _gameplay.ZoneManager.MainPlayer;
            if (!local.IsAlive) return;

            var border = _gameplay.CollisionWorld.Border;
            bool nearEdge = border.IsCircular
                ? local.Position2D.magnitude > border.Radius - 1.6f
                : border.IsOutside(local.Position2D, 1.6f);

            if (nearEdge && _edgeSparksPs != null)
            {
                _edgeSparksPs.transform.position = local.transform.position;
                _edgeSparksPs.Emit(2);
            }
        }

        private void OnZoneCaptured(ZoneCapturedSignal sig)
        {
            var owner = _gameplay?.ZoneManager?.GetPlayerByID(sig.OwnerId);
            Color c = owner != null ? owner.ZoneColor : Color.white;
            EmitBurst(new Vector3(sig.Centroid.x, 0.4f, sig.Centroid.y), c, Mathf.Clamp(Mathf.RoundToInt(sig.AddedArea * 1.5f), 8, 32));
        }

        private void OnPlayerKilled(PlayerKilledSignal sig)
        {
            EmitBurst(new Vector3(sig.Position.x, 0.6f, sig.Position.y), new Color(1f, 0.30f, 0.25f), 28);
        }

        private void OnPowerActivated(PowerActivatedSignal sig)
        {
            var owner = _gameplay?.ZoneManager?.GetPlayerByID(sig.PlayerId);
            if (owner == null) return;
            EmitBurst(owner.transform.position + Vector3.up * 0.4f, new Color(0.25f, 0.90f, 1f), 24);
        }

        private void OnDestroy()
        {
            EventBus.Shared.Unsubscribe<ZoneCapturedSignal>(OnZoneCaptured);
            EventBus.Shared.Unsubscribe<PlayerKilledSignal>(OnPlayerKilled);
            EventBus.Shared.Unsubscribe<PowerActivatedSignal>(OnPowerActivated);
            if (_particleMat != null) Destroy(_particleMat);
        }
    }
}
