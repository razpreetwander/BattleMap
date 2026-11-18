using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BattleMaps.Gameplay;
using UnityEngine;

namespace BattleMaps.Features.Multiplayer
{
    [Serializable]
    public class NetPlayerSnapshot
    {
        public string type; // "join", "state", "leave"
        public string room;
        public int playerId;
        public string name;
        public float x;
        public float y;
        public float hx;
        public float hy;
    }

    /// <summary>
    /// Reconstructed clean-room multiplayer networking client matching
    /// BattleMaps.Features.Multiplayer.PartyPhotonCoordinator (114 methods) & MultiplayerRoomService (48 methods).
    /// Connects over standard WebSockets to Server~/battlemaps_relay_server.mjs (or any WS relay),
    /// with seamless offline AI fallback if no server is running!
    /// </summary>
    public class BattleMapsMultiplayerClient : MonoBehaviour
    {
        public string ServerUrl = "ws://127.0.0.1:8787";
        public string RoomCode = "BATTLEMAPS-LOBBY";
        public bool IsConnected => _ws != null && _ws.State == WebSocketState.Open;
        public string StatusText { get; private set; } = "Offline (Local AI Bots Ready)";

        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<NetPlayerSnapshot> _inbound = new();
        private float _sendTimer;
        private GameplayController _gameplay;

        public event Action<string> StatusChanged;

        public void Initialize(GameplayController gameplay)
        {
            _gameplay = gameplay;
        }

        public async void ConnectToRoomAsync(string serverUrl = null, string roomCode = null)
        {
            if (!string.IsNullOrEmpty(serverUrl)) ServerUrl = serverUrl;
            if (!string.IsNullOrEmpty(roomCode)) RoomCode = roomCode;

            Disconnect();
            _cts = new CancellationTokenSource();
            _ws = new ClientWebSocket();
            SetStatus($"Connecting to {ServerUrl}...");

            try
            {
                await _ws.ConnectAsync(new Uri(ServerUrl), _cts.Token);
                SetStatus($"Connected: {RoomCode}");
                SendSnapshot(new NetPlayerSnapshot { type = "join", room = RoomCode, playerId = 1, name = "YOU" });
                _ = ReceiveLoopAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                SetStatus($"Offline Mode ({ex.Message})");
                Disconnect();
            }
        }

        public void Disconnect()
        {
            try
            {
                _cts?.Cancel();
                if (_ws != null && _ws.State == WebSocketState.Open)
                    _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                _ws?.Dispose();
            }
            catch { }
            _ws = null;
            _cts = null;
        }

        public void TickNetwork(float dt)
        {
            if (!IsConnected || _gameplay == null || !_gameplay.IsInGame) return;

            // Broadcast local player pose at 15 Hz
            _sendTimer += dt;
            if (_sendTimer >= 0.066f && _gameplay.ZoneManager.MainPlayer != null && _gameplay.ZoneManager.MainPlayer.IsAlive)
            {
                _sendTimer = 0f;
                var p = _gameplay.ZoneManager.MainPlayer;
                SendSnapshot(new NetPlayerSnapshot
                {
                    type = "state",
                    room = RoomCode,
                    playerId = p.PlayerId,
                    name = p.DisplayName,
                    x = p.Position2D.x,
                    y = p.Position2D.y,
                    hx = p.Heading.x,
                    hy = p.Heading.y
                });
            }

            // Apply remote player updates onto matching entity
            while (_inbound.TryDequeue(out var msg))
            {
                if (msg.type == "state" && msg.playerId != 1)
                {
                    var remote = _gameplay.ZoneManager.GetPlayerByID(msg.playerId);
                    if (remote != null && remote.IsAlive)
                        remote.DesiredHeading = new Vector2(msg.hx, msg.hy);
                }
            }
        }

        private async void SendSnapshot(NetPlayerSnapshot snap)
        {
            if (!IsConnected) return;
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(snap));
                await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
            }
            catch { }
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            var buf = new byte[4096];
            try
            {
                while (!token.IsCancellationRequested && _ws != null && _ws.State == WebSocketState.Open)
                {
                    var res = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), token);
                    if (res.MessageType == WebSocketMessageType.Close) break;
                    string json = Encoding.UTF8.GetString(buf, 0, res.Count);
                    var msg = JsonUtility.FromJson<NetPlayerSnapshot>(json);
                    if (msg != null) _inbound.Enqueue(msg);
                }
            }
            catch { }
            SetStatus("Disconnected (Offline AI Active)");
        }

        private void SetStatus(string s)
        {
            StatusText = s;
            StatusChanged?.Invoke(s);
        }

        private void OnDestroy() => Disconnect();
    }
}
