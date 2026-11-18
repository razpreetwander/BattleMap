using System.Collections.Generic;
using BattleMaps.Gameplay;
using BattleMaps.Signals;
using UnityEngine;

namespace BattleMaps.GameplayMechanics
{
    /// <summary>
    /// Reconstructed from BattleMaps.Gameplay.Train.EnemyTrain (index 5197, 59 methods) & EnemyTrainCar (63 methods)
    /// + BattleMaps.PumpTower.GenericColorPumpItem (index 3570, 76 methods).
    /// </summary>
    public class EnemyTrainAndPumpTowerController : MonoBehaviour
    {
        private GameplayController _gameplay;
        private readonly List<Transform> _trainCars = new();
        private readonly List<PumpTowerNode> _pumpTowers = new();
        private float _trainAngle;
        private float _trainOrbitRadius = 15f;
        private bool _trainActive;

        private sealed class PumpTowerNode
        {
            public Vector2 Position;
            public float Radius = 2.6f;
            public float ChargeTimer;
            public GameObject Visual;
            public Material RingMat;
        }

        public void Initialize(GameplayController gameplay)
        {
            _gameplay = gameplay;
        }

        public void SpawnHazardsAndTowers(float arenaRadius, bool enableTrain = true, int pumpTowerCount = 2)
        {
            ClearAll();
            _trainOrbitRadius = arenaRadius * 0.45f;
            _trainActive = enableTrain;
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");

            if (enableTrain)
            {
                for (int i = 0; i < 4; i++)
                {
                    var car = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    car.name = $"EnemyTrainCar_{i}";
                    car.transform.SetParent(transform, false);
                    car.transform.localScale = i == 0 ? new Vector3(1.25f, 1.0f, 1.8f) : new Vector3(1.05f, 0.85f, 1.35f);
                    Destroy(car.GetComponent<Collider>());
                    car.GetComponent<MeshRenderer>().sharedMaterial = new Material(shader)
                    {
                        color = i == 0 ? new Color(0.18f, 0.18f, 0.22f) : new Color(0.85f, 0.22f, 0.22f)
                    };
                    _trainCars.Add(car.transform);
                }
            }

            for (int i = 0; i < pumpTowerCount; i++)
            {
                float a = (i / (float)Mathf.Max(1, pumpTowerCount)) * Mathf.PI * 2f + 0.7f;
                Vector2 pos = new Vector2(Mathf.Cos(a) * arenaRadius * 0.35f, Mathf.Sin(a) * arenaRadius * 0.35f);
                var tower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tower.name = $"PumpTower_{i}";
                tower.transform.SetParent(transform, false);
                tower.transform.position = new Vector3(pos.x, 0.9f, pos.y);
                tower.transform.localScale = new Vector3(1.4f, 0.9f, 1.4f);
                Destroy(tower.GetComponent<Collider>());
                var mat = new Material(shader) { color = new Color(0.95f, 0.82f, 0.16f) };
                tower.GetComponent<MeshRenderer>().sharedMaterial = mat;

                _pumpTowers.Add(new PumpTowerNode { Position = pos, Visual = tower, RingMat = mat });
            }
        }

        public void Tick(float dt)
        {
            if (_gameplay == null || !_gameplay.IsInGame) return;

            if (_trainActive && _trainCars.Count > 0)
                TickTrain(dt);

            TickPumpTowers(dt);
        }

        private void TickTrain(float dt)
        {
            _trainAngle += dt * 0.42f;
            var players = _gameplay.ZoneManager.Players;

            for (int c = 0; c < _trainCars.Count; c++)
            {
                float a = _trainAngle - c * 0.13f;
                Vector2 pos = new Vector2(Mathf.Cos(a) * _trainOrbitRadius, Mathf.Sin(a) * _trainOrbitRadius);
                Vector2 tangent = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
                var car = _trainCars[c];
                car.position = new Vector3(pos.x, 0.55f, pos.y);
                car.rotation = Quaternion.Euler(0f, Mathf.Atan2(tangent.x, tangent.y) * Mathf.Rad2Deg, 0f);

                // Check collision against player heads & trails
                for (int i = 0; i < players.Count; i++)
                {
                    var p = players[i];
                    if (p == null || !p.IsAlive || p.IsAirborne || p.IsUnderground || p.IsInvincible) continue;
                    if ((p.Position2D - pos).sqrMagnitude < 1.35f * 1.35f)
                        p.Die(-1, "EnemyTrain");
                }
            }
        }

        private void TickPumpTowers(float dt)
        {
            var players = _gameplay.ZoneManager.Players;
            for (int t = 0; t < _pumpTowers.Count; t++)
            {
                var tower = _pumpTowers[t];
                PlayerBase occupant = null;
                for (int i = 0; i < players.Count; i++)
                {
                    var p = players[i];
                    if (p == null || !p.IsAlive || p.IsAirborne || p.IsUnderground) continue;
                    if ((p.Position2D - tower.Position).sqrMagnitude <= tower.Radius * tower.Radius)
                    {
                        occupant = p;
                        break;
                    }
                }

                if (occupant != null)
                {
                    tower.ChargeTimer += dt;
                    if (tower.RingMat != null)
                        tower.RingMat.color = Color.Lerp(Color.yellow, occupant.ZoneColor, Mathf.PingPong(Time.time * 3f, 1f));

                    if (tower.ChargeTimer >= 1.5f)
                    {
                        tower.ChargeTimer = 0f;
                        _gameplay.ZoneManager.ApplyPaintStamp(occupant, tower.Position, 4.6f, 20);
                        if (occupant.IsLocalPlayer)
                            EventBus.Shared.Publish(new FlyingTextSignal("PUMP WAVE!", tower.Visual.transform.position + Vector3.up * 1.5f, occupant.ZoneColor, 1.3f));
                    }
                }
                else
                {
                    tower.ChargeTimer = Mathf.Max(0f, tower.ChargeTimer - dt * 0.5f);
                }
            }
        }

        public void ClearAll()
        {
            for (int i = 0; i < _trainCars.Count; i++)
                if (_trainCars[i] != null) Destroy(_trainCars[i].gameObject);
            _trainCars.Clear();

            for (int i = 0; i < _pumpTowers.Count; i++)
                if (_pumpTowers[i].Visual != null) Destroy(_pumpTowers[i].Visual);
            _pumpTowers.Clear();
        }
    }
}
