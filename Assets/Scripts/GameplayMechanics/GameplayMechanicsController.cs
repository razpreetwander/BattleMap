using System.Collections.Generic;
using BattleMaps.CollisionSystem;
using BattleMaps.Gameplay;
using BattleMaps.Signals;
using UnityEngine;

namespace BattleMaps.GameplayMechanics
{
    /// <summary>
    /// Reconstructed from BattleMaps.GameplayMechanics.GameplayMechanicsController (index 2723, 83 methods),
    /// JumpingRampRuntime (index 2713, 74 methods), TunnelRuntime (index 2784, 107 methods),
    /// and GroundSpeedBoostRuntime (index 2773, 37 methods).
    /// </summary>
    public class GameplayMechanicsController : MonoBehaviour
    {
        public JumpingRampDataModel RampConfig = new();
        public GroundSpeedBoostDataModel BoostConfig = new();
        public TunnelDataModel TunnelConfig = new();

        private GameplayController _gameplay;
        private readonly List<PadInstance> _pads = new(16);
        private readonly Dictionary<int, ActiveJumpState> _jumps = new();
        private readonly Dictionary<int, ActiveTunnelState> _tunnels = new();
        private readonly Dictionary<int, ActiveBoostState> _boosts = new();
        private Transform _padsRoot;

        private sealed class PadInstance
        {
            public int TriggerId;
            public GameplayMechanicsPadKind Kind;
            public Vector2 Position;
            public float Radius;
            public float Cooldown;
            public GameObject Visual;
        }

        private sealed class ActiveJumpState
        {
            public PlayerBase Player;
            public float Elapsed;
            public float Duration;
        }

        private sealed class ActiveTunnelState
        {
            public PlayerBase Player;
            public float Elapsed;
            public float Duration;
        }

        private sealed class ActiveBoostState
        {
            public PlayerBase Player;
            public float Remaining;
            public float ComboResetTimer;
            public int Tier;
        }

        public void Initialize(GameplayController gameplay)
        {
            _gameplay = gameplay;
            if (_padsRoot == null)
            {
                _padsRoot = new GameObject("MapMechanicsPads").transform;
                _padsRoot.SetParent(transform, false);
            }
            if (_gameplay.CollisionWorld != null)
                _gameplay.CollisionWorld.TriggerEnter += HandleTriggerEnter;
        }

        public void SpawnArenaMechanics(float arenaRadius, int boostCount = 4, int rampCount = 3, int tunnelCount = 2)
        {
            ClearAll();
            int nextTrigId = 5000;
            float ringR = arenaRadius * 0.58f;
            int total = boostCount + rampCount + tunnelCount;

            for (int i = 0; i < total; i++)
            {
                GameplayMechanicsPadKind kind = i < boostCount
                    ? GameplayMechanicsPadKind.SpeedBoost
                    : (i < boostCount + rampCount ? GameplayMechanicsPadKind.JumpingRamp : GameplayMechanicsPadKind.Tunnel);

                float angle = (i / (float)Mathf.Max(1, total)) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                float dist = ringR * Random.Range(0.55f, 1.15f);
                Vector2 pos = new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);
                SpawnPad(nextTrigId++, kind, pos);
            }
        }

        private void SpawnPad(int triggerId, GameplayMechanicsPadKind kind, Vector2 pos)
        {
            float radius = kind == GameplayMechanicsPadKind.SpeedBoost ? 1.55f : 1.7f;
            var padObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            padObj.name = $"Pad_{kind}_{triggerId}";
            padObj.transform.SetParent(_padsRoot, false);
            padObj.transform.position = new Vector3(pos.x, 0.08f, pos.y);
            padObj.transform.localScale = new Vector3(radius * 2f, 0.09f, radius * 2f);
            Destroy(padObj.GetComponent<Collider>());

            Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");
            Color padColor = kind switch
            {
                GameplayMechanicsPadKind.SpeedBoost => new Color(0.15f, 0.92f, 0.48f),
                GameplayMechanicsPadKind.JumpingRamp => new Color(1.0f, 0.62f, 0.10f),
                GameplayMechanicsPadKind.Tunnel => new Color(0.52f, 0.25f, 0.88f),
                _ => Color.white
            };
            padObj.GetComponent<MeshRenderer>().sharedMaterial = new Material(shader) { color = padColor };

            var inst = new PadInstance
            {
                TriggerId = triggerId,
                Kind = kind,
                Position = pos,
                Radius = radius,
                Visual = padObj
            };
            _pads.Add(inst);

            _gameplay.CollisionWorld.SetTrigger(new CustomTriggerVolume
            {
                TriggerId = triggerId,
                OwnerId = -1,
                Kind = EntityKind.Pad,
                Center = pos,
                Radius = radius,
                Active = true
            });
        }

        private void HandleTriggerEnter(TriggerContact contact)
        {
            PadInstance pad = null;
            for (int i = 0; i < _pads.Count; i++)
            {
                if (_pads[i].TriggerId == contact.TriggerId) { pad = _pads[i]; break; }
            }
            if (pad == null || pad.Cooldown > 0f) return;

            var player = _gameplay.ZoneManager.GetPlayerByID(contact.HeadId);
            if (player == null || !player.IsAlive || player.IsAirborne || player.IsUnderground) return;

            pad.Cooldown = 1.0f;
            switch (pad.Kind)
            {
                case GameplayMechanicsPadKind.SpeedBoost:
                    TriggerSpeedBoost(player);
                    break;
                case GameplayMechanicsPadKind.JumpingRamp:
                    TriggerJump(player);
                    break;
                case GameplayMechanicsPadKind.Tunnel:
                    TriggerTunnel(player);
                    break;
            }
        }

        private void TriggerSpeedBoost(PlayerBase player)
        {
            if (!_boosts.TryGetValue(player.PlayerId, out var st))
            {
                st = new ActiveBoostState { Player = player };
                _boosts[player.PlayerId] = st;
            }
            st.Tier = st.ComboResetTimer > 0f ? Mathf.Min(BoostConfig.SpeedByTier.Length - 1, st.Tier + 1) : 0;
            st.Remaining = BoostConfig.DurationSeconds;
            st.ComboResetTimer = BoostConfig.ComboWindowSeconds;
            player.PadSpeedMultiplier = BoostConfig.SpeedByTier[st.Tier];

            if (player.IsLocalPlayer)
                EventBus.Shared.Publish(new FlyingTextSignal($"BOOST x{st.Tier + 1}!", player.transform.position + Vector3.up * 1.4f, Color.green, 1.1f));
        }

        private void TriggerJump(PlayerBase player)
        {
            if (_jumps.ContainsKey(player.PlayerId)) return;
            player.IsAirborne = true;
            player.PadSpeedMultiplier = RampConfig.SpeedBoostMultiplier;
            if (RampConfig.LaunchZoneDraw)
                _gameplay.ZoneManager.ApplyPaintStamp(player, player.Position2D, RampConfig.LaunchZoneRadius, 12);

            if (player.IsLocalPlayer && RampConfig.JumpZoomEnabled)
            {
                _gameplay.SetMechanicCameraZoom(RampConfig.JumpZoomMultiplier);
                EventBus.Shared.Publish(new FlyingTextSignal("JUMP!", player.transform.position + Vector3.up * 1.5f, new Color(1f, 0.7f, 0.1f), 1.2f));
            }

            _jumps[player.PlayerId] = new ActiveJumpState { Player = player, Elapsed = 0f, Duration = RampConfig.HangDurationSeconds };
        }

        private void TriggerTunnel(PlayerBase player)
        {
            if (_tunnels.ContainsKey(player.PlayerId)) return;
            player.IsUnderground = true;
            player.PadSpeedMultiplier = TunnelConfig.SpeedMultiplier;
            if (TunnelConfig.EnterZoneDraw)
                _gameplay.ZoneManager.ApplyPaintStamp(player, player.Position2D, TunnelConfig.EnterZoneRadius, 12);

            if (player.IsLocalPlayer && TunnelConfig.CameraZoomEnabled)
            {
                _gameplay.SetMechanicCameraZoom(TunnelConfig.CameraZoomMultiplier);
                EventBus.Shared.Publish(new FlyingTextSignal("TUNNEL!", player.transform.position + Vector3.up * 1.3f, new Color(0.65f, 0.35f, 1f), 1.2f));
            }

            _tunnels[player.PlayerId] = new ActiveTunnelState { Player = player, Elapsed = 0f, Duration = TunnelConfig.DurationSeconds };
        }

        public void TickMechanics(float dt)
        {
            for (int i = 0; i < _pads.Count; i++)
            {
                if (_pads[i].Cooldown > 0f) _pads[i].Cooldown -= dt;
                if (_pads[i].Visual != null)
                    _pads[i].Visual.transform.Rotate(0f, 35f * dt, 0f);
            }

            // Tick SpeedBoosts
            var doneBoosts = new List<int>();
            foreach (var kv in _boosts)
            {
                var st = kv.Value;
                if (st.Player == null || !st.Player.IsAlive) { doneBoosts.Add(kv.Key); continue; }
                if (st.Remaining > 0f)
                {
                    st.Remaining -= dt;
                    if (st.Remaining <= 0f) st.Player.PadSpeedMultiplier = 1f;
                }
                st.ComboResetTimer -= dt;
                if (st.ComboResetTimer <= 0f && st.Remaining <= 0f) doneBoosts.Add(kv.Key);
            }
            foreach (var id in doneBoosts) _boosts.Remove(id);

            // Tick JumpingRamps
            var doneJumps = new List<int>();
            foreach (var kv in _jumps)
            {
                var st = kv.Value;
                if (st.Player == null || !st.Player.IsAlive) { doneJumps.Add(kv.Key); continue; }
                st.Elapsed += dt;
                float t = Mathf.Clamp01(st.Elapsed / Mathf.Max(0.1f, st.Duration));
                st.Player.VerticalOffset = Mathf.Sin(t * Mathf.PI) * RampConfig.JumpHeight;
                if (t >= 1f)
                {
                    st.Player.VerticalOffset = 0f;
                    st.Player.IsAirborne = false;
                    st.Player.PadSpeedMultiplier = 1f;
                    if (st.Player.IsLocalPlayer) _gameplay.SetMechanicCameraZoom(1f);

                    if (RampConfig.LandZoneDraw)
                        _gameplay.ZoneManager.ApplyPaintStamp(st.Player, st.Player.Position2D, RampConfig.LandZoneRadius, 16);
                    KillNearbyOnImpact(st.Player, RampConfig.LandKillRadius, "RampStomp");
                    doneJumps.Add(kv.Key);
                }
            }
            foreach (var id in doneJumps) _jumps.Remove(id);

            // Tick Tunnels
            var doneTunnels = new List<int>();
            foreach (var kv in _tunnels)
            {
                var st = kv.Value;
                if (st.Player == null || !st.Player.IsAlive) { doneTunnels.Add(kv.Key); continue; }
                st.Elapsed += dt;
                float t = Mathf.Clamp01(st.Elapsed / Mathf.Max(0.1f, st.Duration));
                st.Player.VerticalOffset = Mathf.Sin(t * Mathf.PI) * TunnelConfig.Depth;
                if (t >= 1f)
                {
                    st.Player.VerticalOffset = 0f;
                    st.Player.IsUnderground = false;
                    st.Player.PadSpeedMultiplier = 1f;
                    if (st.Player.IsLocalPlayer) _gameplay.SetMechanicCameraZoom(1f);

                    if (TunnelConfig.ExitZoneDraw)
                        _gameplay.ZoneManager.ApplyPaintStamp(st.Player, st.Player.Position2D, TunnelConfig.ExitZoneRadius, 16);
                    KillNearbyOnImpact(st.Player, TunnelConfig.EmergeKillRadius, "TunnelEmerge");
                    doneTunnels.Add(kv.Key);
                }
            }
            foreach (var id in doneTunnels) _tunnels.Remove(id);
        }

        private void KillNearbyOnImpact(PlayerBase attacker, float radius, string reason)
        {
            float rSq = radius * radius;
            var players = _gameplay.ZoneManager.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || !p.IsAlive || p.PlayerId == attacker.PlayerId || _gameplay.ZoneManager.IsSameTeam(attacker.PlayerId, p.PlayerId)) continue;
                if ((p.Position2D - attacker.Position2D).sqrMagnitude <= rSq && !p.IsInvincible && !p.IsShielded)
                {
                    attacker.RegisterKill();
                    p.Die(attacker.PlayerId, reason);
                }
            }
        }

        public void ClearAll()
        {
            for (int i = 0; i < _pads.Count; i++)
            {
                _gameplay?.CollisionWorld?.RemoveTrigger(_pads[i].TriggerId);
                if (_pads[i].Visual != null) Destroy(_pads[i].Visual);
            }
            _pads.Clear();
            _jumps.Clear();
            _tunnels.Clear();
            _boosts.Clear();
        }
    }
}
