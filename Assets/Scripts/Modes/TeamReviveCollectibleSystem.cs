using System.Collections.Generic;
using BattleMaps.Gameplay;
using BattleMaps.Signals;
using UnityEngine;

namespace BattleMaps.Modes
{
    /// <summary>
    /// Reconstructed from Quantum.TeamBattle.Systems.TeamReviveCollectibleSystem (IL2CPP index 13784, 19 methods).
    /// When a teammate is eliminated in Team Battle, drops a glowing Revive Orb at their death spot.
    /// Any living teammate touching the orb immediately revives the fallen teammate with spawn invincibility!
    /// </summary>
    public class TeamReviveCollectibleSystem : MonoBehaviour
    {
        private GameplayController _gameplay;
        private readonly List<ReviveOrb> _orbs = new(8);

        private sealed class ReviveOrb
        {
            public int DeadPlayerId;
            public string DisplayName;
            public int TeamId;
            public Vector2 Position;
            public float RemainingLifetime;
            public GameObject Visual;
        }

        public void Initialize(GameplayController gameplay)
        {
            _gameplay = gameplay;
            EventBus.Shared.Subscribe<PlayerKilledSignal>(OnPlayerKilled);
        }

        public void ClearAll()
        {
            for (int i = 0; i < _orbs.Count; i++)
                if (_orbs[i].Visual != null) Destroy(_orbs[i].Visual);
            _orbs.Clear();
        }

        private void OnPlayerKilled(PlayerKilledSignal sig)
        {
            if (_gameplay == null || !_gameplay.IsInGame || _gameplay.CurrentMode != GameMode.TeamBattle)
                return;

            var victim = _gameplay.ZoneManager.GetPlayerByID(sig.VictimId);
            if (victim == null || victim.TeamId < 0) return;

            SpawnReviveOrb(victim, sig.Position);
        }

        private void SpawnReviveOrb(PlayerBase victim, Vector2 pos)
        {
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = $"ReviveOrb_{victim.PlayerId}";
            orb.transform.SetParent(transform, false);
            orb.transform.position = new Vector3(pos.x, 0.85f, pos.y);
            orb.transform.localScale = Vector3.one * 1.15f;
            Destroy(orb.GetComponent<Collider>());

            Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");
            Color teamCol = PlayerSkinPalette.GetTeamColor(victim.TeamId);
            orb.GetComponent<MeshRenderer>().sharedMaterial = new Material(shader)
            {
                color = Color.Lerp(teamCol, Color.white, 0.35f)
            };

            _orbs.Add(new ReviveOrb
            {
                DeadPlayerId = victim.PlayerId,
                DisplayName = victim.DisplayName,
                TeamId = victim.TeamId,
                Position = pos,
                RemainingLifetime = 15f,
                Visual = orb
            });
        }

        public void Tick(float dt)
        {
            if (_gameplay == null || !_gameplay.IsInGame || _orbs.Count == 0) return;
            var players = _gameplay.ZoneManager.Players;

            for (int i = _orbs.Count - 1; i >= 0; i--)
            {
                var orb = _orbs[i];
                orb.RemainingLifetime -= dt;
                if (orb.Visual != null)
                {
                    float bob = 0.85f + Mathf.Sin(Time.time * 4f) * 0.25f;
                    orb.Visual.transform.position = new Vector3(orb.Position.x, bob, orb.Position.y);
                    orb.Visual.transform.Rotate(0f, 90f * dt, 0f);
                }

                if (orb.RemainingLifetime <= 0f)
                {
                    if (orb.Visual != null) Destroy(orb.Visual);
                    _orbs.RemoveAt(i);
                    continue;
                }

                // Check if any living teammate collects the orb
                for (int pIdx = 0; pIdx < players.Count; pIdx++)
                {
                    var ally = players[pIdx];
                    if (ally == null || !ally.IsAlive || ally.TeamId != orb.TeamId) continue;
                    if ((ally.Position2D - orb.Position).sqrMagnitude <= 1.8f * 1.8f)
                    {
                        ReviveFallenTeammate(orb);
                        if (orb.Visual != null) Destroy(orb.Visual);
                        _orbs.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private void ReviveFallenTeammate(ReviveOrb orb)
        {
            Color col = PlayerSkinPalette.GetTeamColor(orb.TeamId);
            var model = new SpawnPlayerModel
            {
                PlayerId = 0, // assign fresh ID
                DisplayName = orb.DisplayName,
                IsLocalPlayer = false,
                TeamId = orb.TeamId,
                PrimaryColor = col,
                ShadowColor = PlayerSkinPalette.GetShadow(col),
                TrailColor = PlayerSkinPalette.GetTrail(col),
                SpawnPosition = orb.Position,
                SpawnZoneRadius = _gameplay.ZoneManager.DefaultSpawnZoneRadius
            };
            var revived = _gameplay.ZoneManager.SpawnPlayer(model);
            revived.SetupInvincibilityMode(2.5f);

            EventBus.Shared.Publish(new FlyingTextSignal(
                $"REVIVED {orb.DisplayName}!",
                new Vector3(orb.Position.x, 1.5f, orb.Position.y),
                col,
                1.25f));
        }

        private void OnDestroy()
        {
            EventBus.Shared.Unsubscribe<PlayerKilledSignal>(OnPlayerKilled);
            ClearAll();
        }
    }
}
