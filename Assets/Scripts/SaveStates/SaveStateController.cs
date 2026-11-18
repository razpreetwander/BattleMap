using System;
using System.Collections.Generic;
using System.IO;
using BattleMaps.CharacterPowers.Characters;
using BattleMaps.CharacterPowers.Powers;
using UnityEngine;

namespace BattleMaps.SaveStates
{
    /// <summary>
    /// Persistent player profile matching BattleMaps.SaveStates.Delegates & BattleMaps.DataSaving.Data.
    /// Covers Currencies, Characters, Outfits, World Countries, Chapters, Missions, XP Road, Events, Stats, Settings.
    /// </summary>
    [Serializable]
    public class BattleMapsSaveData
    {
        public string PlayerName = "PLAYER";
        public int Coins = 500;
        public int Gems = 25;
        public int Trophies = 0;
        public int TotalXp = 0;
        public int CurrentRankIndex = 0;
        public int SelectedCharacterIndex = 0;
        public int TotalGamesPlayed = 0;
        public int TotalWins = 0;
        public int TotalKills = 0;
        public int TotalLoopsClosed = 0;
        public float BestCapturePercent = 0f;
        public int ChestsOpened = 0;
        public int BonusGamesPlayed = 0;

        // Chapters progression (ChapterDataModel fields)
        public int CurrentChapterIndex = 0;
        public int CurrentChapterLevel = 1;
        public int ClaimedChapterRewards = 0;

        // Settings
        public bool SfxEnabled = true;
        public bool MusicEnabled = true;
        public bool HapticsEnabled = true;
        public int QualityTier = 2; // 0=Low, 1=Med, 2=High
        public float SteeringSensitivity = 1.0f;

        // Collections
        public List<CharacterProgressState> Characters = new();
        public List<CountryProgressEntry> Countries = new();
        public List<MissionProgressEntry> DailyMissions = new();
        public List<EventProgressEntry> ActiveEvents = new();
        public List<int> ClaimedXpSteps = new();
    }

    [Serializable]
    public class CountryProgressEntry
    {
        public string CountryId;
        public string Continent;
        public string DisplayName;
        public float BestPercent01;
        public bool Completed;
    }

    [Serializable]
    public class MissionProgressEntry
    {
        public string MissionId;
        public string Title;
        public string MissionKind; // CollectPaint, CreateLoops, Kills, CompleteCountries, ChestOpens, BonusGames
        public float Progress;
        public float Target;
        public int CoinReward;
        public int XpReward;
        public bool Claimed;
    }

    [Serializable]
    public class EventProgressEntry
    {
        public string EventId; // CollectToWin, KillToWin, TeamBattle
        public string Title;
        public float Score;
        public float MilestoneTarget;
        public int CoinReward;
        public bool Claimed;
    }

    /// <summary>
    /// Reconstructed from BattleMaps.SaveStates.SaveStateController (index 2295) & BattleMaps.DataSaving.Data.DataManager (index 3028).
    /// </summary>
    public class SaveStateController : MonoBehaviour
    {
        public static SaveStateController Instance { get; private set; }
        public BattleMapsSaveData Data { get; private set; } = new();

        public event Action DataChanged;

        private string SavePath => Path.Combine(Application.persistentDataPath, "battlemaps_save.json");

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadOrCreate();
        }

        public void LoadOrCreate()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    string json = File.ReadAllText(SavePath);
                    var loaded = JsonUtility.FromJson<BattleMapsSaveData>(json);
                    if (loaded != null) Data = loaded;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveStateController] Failed to load save, creating default: {ex.Message}");
                Data = new BattleMapsSaveData();
            }

            EnsureDefaultCollections();
            Save();
        }

        private void EnsureDefaultCollections()
        {
            // Ensure every character in Catalog has a progress state
            foreach (var cfg in CharactersManager.Catalog)
            {
                if (!Data.Characters.Exists(c => c.CharacterId == cfg.CharacterId))
                {
                    Data.Characters.Add(new CharacterProgressState
                    {
                        CharacterId = cfg.CharacterId,
                        Unlocked = cfg.UnlockCostCoins <= 0,
                        Level = 1,
                        Cards = 0,
                        EquippedOutfitIndex = 0
                    });
                }
            }

            // Ensure 5 Continents / 25 World Countries exist (matching feature_world_assets_* bundles)
            if (Data.Countries.Count == 0)
            {
                AddContinent("Europe", new[] { "France", "Germany", "Italy", "Spain", "United Kingdom" });
                AddContinent("Asia & Oceania", new[] { "India", "Japan", "Australia", "South Korea", "Thailand" });
                AddContinent("North America", new[] { "USA", "Canada", "Mexico", "Cuba", "Costa Rica" });
                AddContinent("South America", new[] { "Brazil", "Argentina", "Peru", "Chile", "Colombia" });
                AddContinent("Africa", new[] { "Egypt", "Kenya", "Morocco", "South Africa", "Nigeria" });
            }

            // Ensure Daily Missions V2 exist (matching V2Mission* classes)
            if (Data.DailyMissions.Count == 0)
            {
                Data.DailyMissions.Add(new MissionProgressEntry { MissionId = "m_paint", Title = "Capture 150% Total Territory", MissionKind = "CollectPaint", Target = 150f, CoinReward = 200, XpReward = 100 });
                Data.DailyMissions.Add(new MissionProgressEntry { MissionId = "m_loops", Title = "Close 15 Territory Loops", MissionKind = "CreateLoops", Target = 15f, CoinReward = 150, XpReward = 80 });
                Data.DailyMissions.Add(new MissionProgressEntry { MissionId = "m_kills", Title = "Eliminate 8 Opponents", MissionKind = "Kills", Target = 8f, CoinReward = 250, XpReward = 120 });
                Data.DailyMissions.Add(new MissionProgressEntry { MissionId = "m_chest", Title = "Open 2 Power Chests", MissionKind = "ChestOpens", Target = 2f, CoinReward = 300, XpReward = 150 });
                Data.DailyMissions.Add(new MissionProgressEntry { MissionId = "m_country", Title = "Complete 1 World Country", MissionKind = "CompleteCountries", Target = 1f, CoinReward = 400, XpReward = 200 });
            }

            // Ensure Live Events exist (matching event_collecttowin, event_killtowin, event_teambattle)
            if (Data.ActiveEvents.Count == 0)
            {
                Data.ActiveEvents.Add(new EventProgressEntry { EventId = "CollectToWin", Title = "Collect To Win!", MilestoneTarget = 200f, CoinReward = 500 });
                Data.ActiveEvents.Add(new EventProgressEntry { EventId = "KillToWin", Title = "Kill To Win Arena", MilestoneTarget = 15f, CoinReward = 600 });
                Data.ActiveEvents.Add(new EventProgressEntry { EventId = "TeamBattle", Title = "Team Battle Cup", MilestoneTarget = 3f, CoinReward = 750 });
            }
        }

        private void AddContinent(string continent, string[] countries)
        {
            for (int i = 0; i < countries.Length; i++)
            {
                Data.Countries.Add(new CountryProgressEntry
                {
                    CountryId = $"{continent}_{countries[i]}".Replace(" ", "_").ToLowerInvariant(),
                    Continent = continent,
                    DisplayName = countries[i],
                    BestPercent01 = 0f,
                    Completed = false
                });
            }
        }

        public void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(Data, true);
                File.WriteAllText(SavePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveStateController] Save failed: {ex.Message}");
            }
            DataChanged?.Invoke();
        }

        public void AddCoins(int amount)
        {
            Data.Coins = Mathf.Max(0, Data.Coins + amount);
            Save();
        }

        public bool TrySpendCoins(int cost)
        {
            if (Data.Coins < cost) return false;
            Data.Coins -= cost;
            Save();
            return true;
        }

        public void AddXp(int amount)
        {
            Data.TotalXp += Mathf.Max(0, amount);
            Data.CurrentRankIndex = Data.TotalXp / 250;
            Save();
        }

        public void IncrementMission(string kind, float amount)
        {
            for (int i = 0; i < Data.DailyMissions.Count; i++)
            {
                var m = Data.DailyMissions[i];
                if (m.MissionKind == kind && !m.Claimed)
                    m.Progress = Mathf.Min(m.Target, m.Progress + amount);
            }
            Save();
        }

        public void IncrementEvent(string eventId, float amount)
        {
            for (int i = 0; i < Data.ActiveEvents.Count; i++)
            {
                var e = Data.ActiveEvents[i];
                if (e.EventId == eventId && !e.Claimed)
                    e.Score = Mathf.Min(e.MilestoneTarget, e.Score + amount);
            }
            Save();
        }

        public CharacterProgressState GetCharacterState(string characterId)
        {
            return Data.Characters.Find(c => c.CharacterId == characterId);
        }
    }
}
