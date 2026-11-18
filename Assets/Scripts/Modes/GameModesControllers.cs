using System;
using System.Collections.Generic;
using BattleMaps.CharacterPowers.Characters;
using BattleMaps.Gameplay;
using BattleMaps.SaveStates;
using BattleMaps.Signals;
using UnityEngine;

namespace BattleMaps.Modes
{
    /// <summary>
    /// Reconstructed from Quantum.TeamBattle.Systems (TeamScoringSystem, TeamWinConditionSystem)
    /// & BattleMaps.Features.Multiplayer.PaintBattle.PartyModeGameplayController.
    /// Manages timed TeamBattle (4v4) & PaintBrawl (FFA timed) match rules.
    /// </summary>
    public class MatchModeRulesController : MonoBehaviour
    {
        public float MatchTimeLimitSeconds = 90f;
        public float RemainingTimeSeconds { get; private set; }
        public float BlueTeamPercent01 { get; private set; }
        public float RedTeamPercent01 { get; private set; }
        public bool IsTimedMode { get; private set; }

        private GameplayController _gameplay;

        public void Initialize(GameplayController gameplay)
        {
            _gameplay = gameplay;
        }

        public void OnMatchStarted(GameMode mode, ArenaSubMode subMode)
        {
            IsTimedMode = mode == GameMode.TeamBattle || subMode == ArenaSubMode.PaintBrawl;
            MatchTimeLimitSeconds = mode == GameMode.TeamBattle ? 90f : 75f;
            RemainingTimeSeconds = MatchTimeLimitSeconds;
            BlueTeamPercent01 = 0f;
            RedTeamPercent01 = 0f;
        }

        public void TickModeRules(float dt)
        {
            if (_gameplay == null || !_gameplay.IsInGame) return;

            if (_gameplay.CurrentMode == GameMode.TeamBattle)
            {
                BlueTeamPercent01 = _gameplay.ZoneManager.GetTeamCapturedPercent01(0);
                RedTeamPercent01 = _gameplay.ZoneManager.GetTeamCapturedPercent01(1);

                // Early victory if one team reaches 80% combined territory
                if (BlueTeamPercent01 >= 0.80f)
                {
                    _gameplay.FinishGame(localPlayerWon: true, winningTeamId: 0);
                    return;
                }
                if (RedTeamPercent01 >= 0.80f)
                {
                    _gameplay.FinishGame(localPlayerWon: false, winningTeamId: 1);
                    return;
                }
            }

            if (IsTimedMode)
            {
                RemainingTimeSeconds -= dt;
                if (RemainingTimeSeconds <= 0f)
                {
                    RemainingTimeSeconds = 0f;
                    if (_gameplay.CurrentMode == GameMode.TeamBattle)
                    {
                        bool blueWon = BlueTeamPercent01 >= RedTeamPercent01;
                        _gameplay.FinishGame(blueWon, blueWon ? 0 : 1);
                    }
                    else
                    {
                        var local = _gameplay.ZoneManager.MainPlayer;
                        bool isFirst = local != null && _gameplay.ZoneManager.GetPlayerRankById(local.PlayerId) == 1;
                        _gameplay.FinishGame(isFirst);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Reconstructed from BattleMaps.World.Controller.WorldController (52 methods),
    /// BattleMaps.Chapters.ChaptersController (128 methods),
    /// BattleMaps.Chests.ChestsController & BattleMaps.BonusGame.BonusGameController (66 methods).
    /// </summary>
    public class MetaProgressionController : MonoBehaviour
    {
        public string ActiveWorldCountryId { get; private set; }
        public int ActiveChapterIndex { get; private set; }

        public static readonly string[] Continents =
        {
            "Europe", "Asia & Oceania", "North America", "South America", "Africa"
        };

        public static readonly string[] ChapterTitles =
        {
            "Chapter 1: Battle Plains",
            "Chapter 2: Turbo Ramps",
            "Chapter 3: Underground Tunnels",
            "Chapter 4: Express Train Valley",
            "Chapter 5: Pump Tower Stadium",
            "Chapter 6: Grand Masters Arena"
        };

        public static readonly string[] XpRoadRanks =
        {
            "Bronze I", "Bronze II", "Silver I", "Silver II",
            "Gold I", "Gold II", "Platinum", "Diamond", "Master", "Grand Legend"
        };

        public void SelectWorldCountry(string countryId) => ActiveWorldCountryId = countryId;
        public void SelectChapter(int chapterIndex) => ActiveChapterIndex = Mathf.Clamp(chapterIndex, 0, ChapterTitles.Length - 1);

        public void RecordMatchCompleted(ArenaSubMode subMode, bool won, float capturedPercent01, int kills, int loopsClosed, int coinsEarned)
        {
            var save = SaveStateController.Instance;
            if (save == null) return;

            var d = save.Data;
            d.TotalGamesPlayed++;
            if (won) d.TotalWins++;
            d.TotalKills += kills;
            d.TotalLoopsClosed += loopsClosed;
            d.BestCapturePercent = Mathf.Max(d.BestCapturePercent, capturedPercent01 * 100f);
            d.Trophies += won ? 30 : Mathf.RoundToInt(capturedPercent01 * 20f);

            save.AddCoins(coinsEarned);
            save.AddXp(25 + kills * 15 + Mathf.RoundToInt(capturedPercent01 * 120f));

            // Missions & Live Events progression
            save.IncrementMission("CollectPaint", capturedPercent01 * 100f);
            save.IncrementMission("CreateLoops", loopsClosed);
            save.IncrementMission("Kills", kills);

            save.IncrementEvent("CollectToWin", capturedPercent01 * 100f);
            save.IncrementEvent("KillToWin", kills);
            if (subMode == ArenaSubMode.TeamBattle && won)
                save.IncrementEvent("TeamBattle", 1f);

            // World Country progression
            if (subMode == ArenaSubMode.WorldMap && !string.IsNullOrEmpty(ActiveWorldCountryId))
            {
                var country = d.Countries.Find(c => c.CountryId == ActiveWorldCountryId);
                if (country != null)
                {
                    bool wasDone = country.Completed;
                    country.BestPercent01 = Mathf.Max(country.BestPercent01, capturedPercent01);
                    if (capturedPercent01 >= 0.65f || won) country.Completed = true;
                    if (!wasDone && country.Completed)
                        save.IncrementMission("CompleteCountries", 1f);
                }
            }

            // Chapters progression
            if (subMode == ArenaSubMode.Chapters && (won || capturedPercent01 >= 0.50f))
            {
                d.CurrentChapterLevel++;
                if (d.CurrentChapterLevel > 3)
                {
                    d.CurrentChapterLevel = 1;
                    d.CurrentChapterIndex = Mathf.Min(ChapterTitles.Length - 1, d.CurrentChapterIndex + 1);
                }
            }

            save.Save();
        }

        /// <summary>
        /// Opens a Power Chest (BattleMaps.Chests & BattleMaps.CharacterPowers.Chests),
        /// awarding coins + random character cards or unlocking a new character!
        /// </summary>
        public (bool ok, string rewardSummary) TryOpenPowerChest(int costCoins = 200)
        {
            var save = SaveStateController.Instance;
            if (save == null || !save.TrySpendCoins(costCoins))
                return (false, "Not enough coins! Need " + costCoins + " coins.");

            save.Data.ChestsOpened++;
            save.IncrementMission("ChestOpens", 1f);

            // Pick a random character to grant cards / unlock
            var catalog = CharactersManager.Catalog;
            var picked = catalog[UnityEngine.Random.Range(0, catalog.Count)];
            var st = save.GetCharacterState(picked.CharacterId);
            int cards = UnityEngine.Random.Range(3, 10);
            int bonusCoins = UnityEngine.Random.Range(40, 120);

            if (st != null)
            {
                st.Cards += cards;
                bool newlyUnlocked = !st.Unlocked && st.Cards >= 5;
                if (newlyUnlocked) st.Unlocked = true;
                save.AddCoins(bonusCoins);
                save.Save();

                return newlyUnlocked
                    ? (true, $"UNLOCKED {picked.DisplayName} ({picked.Rarity}) + {bonusCoins} Coins!")
                    : (true, $"+{cards} {picked.DisplayName} Cards + {bonusCoins} Coins!");
            }

            save.AddCoins(bonusCoins);
            return (true, $"+{bonusCoins} Bonus Coins!");
        }

        public (bool ok, string message) TryUpgradeCharacter(string characterId)
        {
            var save = SaveStateController.Instance;
            var st = save?.GetCharacterState(characterId);
            if (st == null) return (false, "Character not found.");

            var cfg = CharactersManager.Catalog.Find(c => c.CharacterId == characterId);
            if (!st.Unlocked)
            {
                if (!save.TrySpendCoins(cfg.UnlockCostCoins))
                    return (false, $"Need {cfg.UnlockCostCoins} coins to unlock.");
                st.Unlocked = true;
                save.Save();
                return (true, $"Unlocked {cfg.DisplayName}!");
            }

            int upgradeCost = st.Level * 150;
            if (!save.TrySpendCoins(upgradeCost))
                return (false, $"Need {upgradeCost} coins to upgrade.");

            st.Level++;
            save.Save();
            return (true, $"{cfg.DisplayName} upgraded to Lv.{st.Level}!");
        }
    }
}
