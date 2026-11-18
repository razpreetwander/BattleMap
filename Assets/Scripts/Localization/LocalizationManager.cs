using System;
using System.Collections.Generic;

namespace BattleMaps.Localization
{
    public enum GameLanguage
    {
        English = 0,
        Hinglish = 1,
        Spanish = 2,
        French = 3
    }

    /// <summary>
    /// Clean-room multi-language string manager replacing I2.Loc (74 types observed in Assembly-CSharp).
    /// </summary>
    public static class LocalizationManager
    {
        public static GameLanguage CurrentLanguage { get; private set; } = GameLanguage.English;
        public static event Action LanguageChanged;

        private static readonly Dictionary<string, string[]> Table = new()
        {
            ["PLAY_CLASSIC"] = new[] { "PLAY CLASSIC", "CLASSIC KHELO", "JUGAR CLASICO", "JOUER CLASSIQUE" },
            ["TEAM_BATTLE"] = new[] { "TEAM BATTLE 4v4", "TEAM BATTLE 4v4", "BATALLA EQUIPOS", "COMBAT EQUIPE" },
            ["PAINT_BRAWL"] = new[] { "PAINT BRAWL", "PAINT BRAWL", "PELEA PINTURA", "BATAILLE PEINTURE" },
            ["VICTORY"] = new[] { "VICTORY!", "JEET GAYE!", "VICTORIA!", "VICTOIRE!" },
            ["GAME_OVER"] = new[] { "GAME OVER", "GAME OVER", "FIN DEL JUEGO", "PARTIE TERMINEE" },
            ["SETTINGS"] = new[] { "SETTINGS", "SETTINGS", "AJUSTES", "PARAMETRES" },
            ["WORLD_MAP"] = new[] { "WORLD MAP", "WORLD MAP", "MAPA MUNDIAL", "CARTE DU MONDE" },
            ["CHAPTERS"] = new[] { "CHAPTERS", "CHAPTERS", "CAPITULOS", "CHAPITRES" },
            ["HEROES"] = new[] { "HEROES & POWERS", "HEROES & POWERS", "HEROES Y PODERES", "HEROS & POUVOIRS" },
            ["SHOP"] = new[] { "SHOP & CHESTS", "SHOP & CHESTS", "TIENDA Y COFRES", "BOUTIQUE" },
            ["MISSIONS"] = new[] { "DAILY MISSIONS", "DAILY MISSIONS", "MISIONES DIARIAS", "MISSIONS" },
            ["EVENTS"] = new[] { "LIVE EVENTS", "LIVE EVENTS", "EVENTOS EN VIVO", "EVENEMENTS" },
        };

        public static void SetLanguage(GameLanguage lang)
        {
            if (CurrentLanguage == lang) return;
            CurrentLanguage = lang;
            LanguageChanged?.Invoke();
        }

        public static void CycleLanguage()
        {
            int next = ((int)CurrentLanguage + 1) % 4;
            SetLanguage((GameLanguage)next);
        }

        public static string Get(string key)
        {
            if (Table.TryGetValue(key, out var arr))
                return arr[(int)CurrentLanguage % arr.Length];
            return key;
        }
    }
}
