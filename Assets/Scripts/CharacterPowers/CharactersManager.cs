using System;
using System.Collections.Generic;
using BattleMaps.CharacterPowers.Core;
using BattleMaps.CharacterPowers.Powers;
using BattleMaps.Gameplay;
using BattleMaps.Signals;
using UnityEngine;

namespace BattleMaps.CharacterPowers.Characters
{
    /// <summary>
    /// Reconstructed from BattleMaps.CharacterPowers.Characters.CharactersManager (IL2CPP index 5043, 101 methods).
    /// Manages the character catalog, charge accumulation from territory captures, active power ticking, and upgrades.
    /// </summary>
    public class CharactersManager : MonoBehaviour
    {
        public static readonly List<CharacterConfig> Catalog = new()
        {
            new CharacterConfig { CharacterId = "char_sprinter", DisplayName = "Dash Cub", Description = "Burst forward with Sprinter speed!", PowerTypes = new[] { PowerType.Sprinter }, Class = CharacterClassType.Speedster, Rarity = CharacterRarityType.Common, PrimaryColor = new Color(0.14f, 0.78f, 0.45f), ChargeRequirement = 35f, ActiveDuration = 4.5f, UnlockCostCoins = 0 },
            new CharacterConfig { CharacterId = "char_painter", DisplayName = "Splash King", Description = "Expands instant circular paint bursts.", PowerTypes = new[] { PowerType.ExpandingPaint }, Class = CharacterClassType.Painter, Rarity = CharacterRarityType.Common, PrimaryColor = new Color(0.20f, 0.56f, 0.96f), ChargeRequirement = 40f, ActiveDuration = 4.0f, UnlockCostCoins = 150 },
            new CharacterConfig { CharacterId = "char_rock", DisplayName = "Golem", Description = "Indestructible Rock Tail blocks enemy cuts.", PowerTypes = new[] { PowerType.RockTail }, Class = CharacterClassType.Tank, Rarity = CharacterRarityType.Rare, PrimaryColor = new Color(0.55f, 0.58f, 0.64f), ChargeRequirement = 45f, ActiveDuration = 5.5f, UnlockCostCoins = 300 },
            new CharacterConfig { CharacterId = "char_dash", DisplayName = "Phantom", Description = "Invincible high-speed Instant Dash.", PowerTypes = new[] { PowerType.InstantDash }, Class = CharacterClassType.Speedster, Rarity = CharacterRarityType.Rare, PrimaryColor = new Color(0.08f, 0.80f, 0.85f), ChargeRequirement = 35f, ActiveDuration = 0.6f, UnlockCostCoins = 350 },
            new CharacterConfig { CharacterId = "char_shield", DisplayName = "Aegis", Description = "Projects a protective Shielded Area dome.", PowerTypes = new[] { PowerType.ShieldedArea }, Class = CharacterClassType.Tank, Rarity = CharacterRarityType.Rare, PrimaryColor = new Color(0.22f, 0.72f, 0.98f), ChargeRequirement = 45f, ActiveDuration = 5.5f, UnlockCostCoins = 400 },
            new CharacterConfig { CharacterId = "char_frost", DisplayName = "Glacier", Description = "Emits a Freezing Field & Frozen Wave.", PowerTypes = new[] { PowerType.FreezingField, PowerType.FrozenWave }, Class = CharacterClassType.Elemental, Rarity = CharacterRarityType.Epic, PrimaryColor = new Color(0.45f, 0.90f, 1.0f), ChargeRequirement = 50f, ActiveDuration = 5.0f, UnlockCostCoins = 600 },
            new CharacterConfig { CharacterId = "char_tesla", DisplayName = "Volt", Description = "Zaps nearby opponents with Tesla Tower & ShockWave.", PowerTypes = new[] { PowerType.TeslaTower, PowerType.ShockWave }, Class = CharacterClassType.Elemental, Rarity = CharacterRarityType.Epic, PrimaryColor = new Color(0.98f, 0.84f, 0.18f), ChargeRequirement = 55f, ActiveDuration = 5.0f, UnlockCostCoins = 750 },
            new CharacterConfig { CharacterId = "char_bomber", DisplayName = "Pyro", Description = "Throws Fire Bombs & Paint Eraser Bombs.", PowerTypes = new[] { PowerType.FireBomb, PowerType.PaintEraserBomb }, Class = CharacterClassType.Explosive, Rarity = CharacterRarityType.Epic, PrimaryColor = new Color(0.95f, 0.32f, 0.16f), ChargeRequirement = 45f, ActiveDuration = 3.0f, UnlockCostCoins = 800 },
            new CharacterConfig { CharacterId = "char_hose", DisplayName = "Artisan", Description = "Sprays continuous Paint Hose forward.", PowerTypes = new[] { PowerType.PaintHose }, Class = CharacterClassType.Painter, Rarity = CharacterRarityType.Rare, PrimaryColor = new Color(0.88f, 0.32f, 0.76f), ChargeRequirement = 40f, ActiveDuration = 4.5f, UnlockCostCoins = 450 },
            new CharacterConfig { CharacterId = "char_mines", DisplayName = "Sapper", Description = "Drops explosive Paint Mines behind you.", PowerTypes = new[] { PowerType.PaintMines }, Class = CharacterClassType.Explosive, Rarity = CharacterRarityType.Epic, PrimaryColor = new Color(0.94f, 0.52f, 0.16f), ChargeRequirement = 45f, ActiveDuration = 5.0f, UnlockCostCoins = 650 },
            new CharacterConfig { CharacterId = "char_vision", DisplayName = "Hawk", Description = "Better Vision camera zoom + MiniMap Radar.", PowerTypes = new[] { PowerType.BetterVision, PowerType.MiniMapRadar }, Class = CharacterClassType.Balanced, Rarity = CharacterRarityType.Common, PrimaryColor = new Color(0.48f, 0.84f, 0.18f), ChargeRequirement = 35f, ActiveDuration = 7.0f, UnlockCostCoins = 200 },
            new CharacterConfig { CharacterId = "char_inferno", DisplayName = "Inferno Lord", Description = "Unleashes Inferno aura & Fire Meteorite Rain.", PowerTypes = new[] { PowerType.Inferno, PowerType.FireMeteoriteRain }, Class = CharacterClassType.Elemental, Rarity = CharacterRarityType.Legendary, PrimaryColor = new Color(0.95f, 0.18f, 0.22f), ChargeRequirement = 60f, ActiveDuration = 6.0f, UnlockCostCoins = 1200 },
        };

        private GameplayController _gameplay;
        private readonly Dictionary<int, List<PowerBase>> _activePowersByPlayer = new();

        public int SelectedCharacterIndex { get; private set; }
        public float LocalPowerCharge { get; private set; }
        public float LocalChargeRequirement => GetSelectedCharacter().ChargeRequirement;
        public float LocalChargeNormalized => Mathf.Clamp01(LocalPowerCharge / Mathf.Max(1f, LocalChargeRequirement));
        public bool IsLocalPowerReady => LocalPowerCharge >= LocalChargeRequirement;

        public CharacterConfig GetSelectedCharacter() => Catalog[Mathf.Clamp(SelectedCharacterIndex, 0, Catalog.Count - 1)];

        public void SelectCharacter(int index)
        {
            SelectedCharacterIndex = Mathf.Clamp(index, 0, Catalog.Count - 1);
        }

        public void Initialize(GameplayController gameplay)
        {
            _gameplay = gameplay;
            EventBus.Shared.Subscribe<ZoneCapturedSignal>(OnZoneCaptured);
        }

        public void ResetMatchPowers()
        {
            foreach (var list in _activePowersByPlayer.Values)
            {
                for (int i = 0; i < list.Count; i++) list[i].Dispose();
            }
            _activePowersByPlayer.Clear();
            LocalPowerCharge = LocalChargeRequirement * 0.5f; // Start half-charged for fun responsiveness
        }

        public void BindPlayerPowers(PlayerBase player, int characterIndex)
        {
            if (player == null) return;
            var cfg = Catalog[Mathf.Clamp(characterIndex, 0, Catalog.Count - 1)];
            var list = new List<PowerBase>(cfg.PowerTypes.Length);
            for (int i = 0; i < cfg.PowerTypes.Length; i++)
            {
                var p = PowersFactory.Create(cfg.PowerTypes[i]);
                p.Initialize(player, _gameplay, new PowerSettingsBase
                {
                    Type = cfg.PowerTypes[i],
                    Duration = cfg.ActiveDuration,
                    EffectColor = cfg.PrimaryColor
                });
                list.Add(p);
            }
            _activePowersByPlayer[player.PlayerId] = list;
        }

        public bool TryActivateLocalPlayerPower()
        {
            if (_gameplay == null || _gameplay.ZoneManager.MainPlayer == null) return false;
            var local = _gameplay.ZoneManager.MainPlayer;
            if (!local.IsAlive || !IsLocalPowerReady) return false;

            if (_activePowersByPlayer.TryGetValue(local.PlayerId, out var list))
            {
                LocalPowerCharge = 0f;
                for (int i = 0; i < list.Count; i++) list[i].Activate();
                return true;
            }
            return false;
        }

        public void TickPowers(float dt)
        {
            if (_gameplay != null && _gameplay.IsInGame)
            {
                // Passive charge trickle + Spacebar shortcut
                LocalPowerCharge = Mathf.Min(LocalChargeRequirement, LocalPowerCharge + dt * 3.5f);
                if (Input.GetKeyDown(KeyCode.Space) && IsLocalPowerReady)
                    TryActivateLocalPlayerPower();
            }

            foreach (var kv in _activePowersByPlayer)
            {
                var list = kv.Value;
                for (int i = 0; i < list.Count; i++)
                    list[i].Update(dt);
            }
        }

        private void OnZoneCaptured(ZoneCapturedSignal sig)
        {
            if (_gameplay == null || _gameplay.ZoneManager.MainPlayer == null) return;
            if (sig.OwnerId == _gameplay.ZoneManager.MainPlayer.PlayerId)
                LocalPowerCharge = Mathf.Min(LocalChargeRequirement, LocalPowerCharge + sig.AddedArea * 1.8f);
        }

        private void OnDestroy()
        {
            EventBus.Shared.Unsubscribe<ZoneCapturedSignal>(OnZoneCaptured);
            ResetMatchPowers();
        }
    }
}
