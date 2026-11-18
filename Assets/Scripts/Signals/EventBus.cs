using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleMaps.Signals
{
    /// <summary>
    /// Typed decoupled publish/subscribe bus reconstructed from BattleMaps.Signals.EventBus (index 5295).
    /// </summary>
    public sealed class EventBus
    {
        private static EventBus _instance;
        public static EventBus Shared => _instance ??= new EventBus();

        private readonly Dictionary<Type, List<Delegate>> _listeners = new();
        private readonly List<Delegate> _dispatchBuffer = new();

        public void Subscribe<TSignal>(Action<TSignal> handler)
        {
            if (handler == null) return;
            var key = typeof(TSignal);
            if (!_listeners.TryGetValue(key, out var list))
            {
                list = new List<Delegate>(8);
                _listeners[key] = list;
            }
            if (!list.Contains(handler)) list.Add(handler);
        }

        public void Unsubscribe<TSignal>(Action<TSignal> handler)
        {
            if (handler == null) return;
            if (_listeners.TryGetValue(typeof(TSignal), out var list))
                list.Remove(handler);
        }

        public void Publish<TSignal>(TSignal signal)
        {
            if (!_listeners.TryGetValue(typeof(TSignal), out var list) || list.Count == 0)
                return;

            _dispatchBuffer.Clear();
            _dispatchBuffer.AddRange(list);
            for (int i = 0; i < _dispatchBuffer.Count; i++)
            {
                try
                {
                    ((Action<TSignal>)_dispatchBuffer[i])?.Invoke(signal);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        public void Clear() => _listeners.Clear();
    }

    // Core gameplay signals derived from Quantum FrameEvents & BattleMaps.Gameplay events
    public readonly struct PlayerSpawnedSignal
    {
        public readonly int PlayerId;
        public readonly bool IsLocalPlayer;
        public readonly Vector2 Position;
        public PlayerSpawnedSignal(int id, bool isLocal, Vector2 pos)
        {
            PlayerId = id; IsLocalPlayer = isLocal; Position = pos;
        }
    }

    public readonly struct PlayerKilledSignal
    {
        public readonly int VictimId;
        public readonly int KillerId;
        public readonly string Reason;
        public readonly Vector2 Position;
        public PlayerKilledSignal(int victimId, int killerId, string reason, Vector2 pos)
        {
            VictimId = victimId; KillerId = killerId; Reason = reason; Position = pos;
        }
    }

    public readonly struct ZoneCapturedSignal
    {
        public readonly int OwnerId;
        public readonly float AddedArea;
        public readonly float TotalPercent01;
        public readonly Vector2 Centroid;
        public ZoneCapturedSignal(int ownerId, float addedArea, float totalPct, Vector2 centroid)
        {
            OwnerId = ownerId; AddedArea = addedArea; TotalPercent01 = totalPct; Centroid = centroid;
        }
    }

    public readonly struct ZoneStolenSignal
    {
        public readonly int ThiefId;
        public readonly int VictimId;
        public readonly float StolenArea;
        public ZoneStolenSignal(int thiefId, int victimId, float stolenArea)
        {
            ThiefId = thiefId; VictimId = victimId; StolenArea = stolenArea;
        }
    }

    public readonly struct MatchEndedSignal
    {
        public readonly bool LocalPlayerWon;
        public readonly float FinalPercent01;
        public readonly int Kills;
        public readonly int WinningTeamId;
        public MatchEndedSignal(bool won, float pct, int kills, int teamId = -1)
        {
            LocalPlayerWon = won; FinalPercent01 = pct; Kills = kills; WinningTeamId = teamId;
        }
    }

    public readonly struct FlyingTextSignal
    {
        public readonly string Text;
        public readonly Vector3 WorldPosition;
        public readonly Color Color;
        public readonly float Scale;
        public FlyingTextSignal(string text, Vector3 worldPos, Color color, float scale = 1f)
        {
            Text = text; WorldPosition = worldPos; Color = color; Scale = scale;
        }
    }

    public readonly struct PowerActivatedSignal
    {
        public readonly int PlayerId;
        public readonly string PowerName;
        public readonly float Duration;
        public PowerActivatedSignal(int playerId, string powerName, float duration)
        {
            PlayerId = playerId; PowerName = powerName; Duration = duration;
        }
    }
}
