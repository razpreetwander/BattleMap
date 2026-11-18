using System;
using GameOps.UIFramework;
using BattleMaps.CharacterPowers.Characters;
using BattleMaps.Gameplay;
using BattleMaps.Modes;
using BattleMaps.SaveStates;
using UnityEngine;
using UnityEngine.UI;

namespace BattleMaps.UI.Screens
{
    /// <summary>
    /// Shared top header + 5-tab bottom NavBar helper matching BattleMaps.Home.View & NavBarController.
    /// </summary>
    public static class BattleMapsChrome
    {
        public static Text BuildTopBar(RectTransform root, UIScreenRouter router, string screenTitle, bool showBack = false)
        {
            var bar = UIBuilder.CreatePanel("TopBar", root, new Color(0.10f, 0.13f, 0.21f, 0.96f));
            bar.rectTransform.anchorMin = new Vector2(0f, 0.91f);
            bar.rectTransform.anchorMax = new Vector2(1f, 1f);
            bar.rectTransform.offsetMin = bar.rectTransform.offsetMax = Vector2.zero;

            if (showBack)
            {
                var back = UIBuilder.CreateButton("BackBtn", bar.transform, "< HOME", new Color(0.24f, 0.30f, 0.44f), () => router.ShowScreen(UIScreenId.Home), 24);
                back.GetComponent<RectTransform>().anchorMin = new Vector2(0.02f, 0.18f);
                back.GetComponent<RectTransform>().anchorMax = new Vector2(0.22f, 0.82f);
                back.GetComponent<RectTransform>().offsetMin = back.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            }

            var title = UIBuilder.CreateText("ScreenTitle", bar.transform, screenTitle, 34);
            title.rectTransform.anchorMin = new Vector2(0.22f, 0.1f);
            title.rectTransform.anchorMax = new Vector2(0.62f, 0.9f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;

            var currency = UIBuilder.CreateText("CurrencyTxt", bar.transform, "500c | 0T", 26, TextAnchor.MiddleRight, Color.yellow);
            currency.rectTransform.anchorMin = new Vector2(0.60f, 0.15f);
            currency.rectTransform.anchorMax = new Vector2(0.86f, 0.85f);
            currency.rectTransform.offsetMin = currency.rectTransform.offsetMax = Vector2.zero;

            var gear = UIBuilder.CreateButton("SettingsBtn", bar.transform, "CFG", new Color(0.24f, 0.30f, 0.44f), () => router.OpenPopup(UIScreenId.SettingsPopup), 22);
            gear.GetComponent<RectTransform>().anchorMin = new Vector2(0.88f, 0.18f);
            gear.GetComponent<RectTransform>().anchorMax = new Vector2(0.98f, 0.82f);
            gear.GetComponent<RectTransform>().offsetMin = gear.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            return currency;
        }

        public static void BuildBottomNavBar(RectTransform root, UIScreenRouter router, UIScreenId active)
        {
            var nav = UIBuilder.CreatePanel("BottomNavBar", root, new Color(0.09f, 0.12f, 0.19f, 0.98f));
            nav.rectTransform.anchorMin = new Vector2(0f, 0f);
            nav.rectTransform.anchorMax = new Vector2(1f, 0.095f);
            nav.rectTransform.offsetMin = nav.rectTransform.offsetMax = Vector2.zero;

            var hlg = nav.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.padding = new RectOffset(10, 10, 10, 10);
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            (string label, UIScreenId id)[] tabs =
            {
                ("WORLD", UIScreenId.WorldMap),
                ("CHAPTERS", UIScreenId.Chapters),
                ("HOME", UIScreenId.Home),
                ("HEROES", UIScreenId.CharacterSelection),
                ("SHOP", UIScreenId.ShopAndChests)
            };

            foreach (var t in tabs)
            {
                Color c = t.id == active ? new Color(0.16f, 0.78f, 0.46f) : new Color(0.18f, 0.22f, 0.32f);
                var tabId = t.id;
                UIBuilder.CreateButton($"Tab_{t.label}", nav.transform, t.label, c, () => router.ShowScreen(tabId), 24);
            }
        }

        public static void RefreshCurrency(Text txt)
        {
            var d = SaveStateController.Instance?.Data;
            if (txt != null && d != null)
                txt.text = $"{d.Coins} Coins | {d.Trophies} Trophies";
        }
    }

    /// <summary>
    /// 1. HomeScreenView (BattleMaps.Home.View.HomeScreenView, 88 methods)
    /// </summary>
    public class HomeScreen : UIScreenBase
    {
        public override UIScreenId ScreenId => UIScreenId.Home;

        private Action<GameMode, ArenaSubMode> _onLaunchMatch;
        private CharactersManager _characters;
        private Text _currencyTxt;
        private Text _heroPreviewTxt;
        private Text _rankTxt;

        public void Bind(CharactersManager chars, Action<GameMode, ArenaSubMode> onLaunchMatch)
        {
            _characters = chars;
            _onLaunchMatch = onLaunchMatch;
        }

        protected override void BuildUI(RectTransform root)
        {
            var bg = UIBuilder.CreatePanel("Bg", root, new Color(0.11f, 0.15f, 0.24f, 0.96f));
            UIBuilder.StretchFull(bg.rectTransform);

            _currencyTxt = BattleMapsChrome.BuildTopBar(root, Router, "BATTLEMAPS");
            BattleMapsChrome.BuildBottomNavBar(root, Router, UIScreenId.Home);

            // Title & Rank
            var title = UIBuilder.CreateText("Logo", root, "BATTLEMAPS\nRECONSTRUCTED", 64, TextAnchor.MiddleCenter, Color.white);
            title.rectTransform.anchorMin = new Vector2(0.08f, 0.73f);
            title.rectTransform.anchorMax = new Vector2(0.92f, 0.88f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;

            _rankTxt = UIBuilder.CreateText("Rank", root, "Rank: Bronze I | Best: 0%", 28, TextAnchor.MiddleCenter, new Color(0.75f, 0.90f, 1f));
            _rankTxt.rectTransform.anchorMin = new Vector2(0.1f, 0.67f);
            _rankTxt.rectTransform.anchorMax = new Vector2(0.9f, 0.73f);
            _rankTxt.rectTransform.offsetMin = _rankTxt.rectTransform.offsetMax = Vector2.zero;

            // Side Feature Pills (Missions, XP Road, Events, Party, Bonus)
            BuildSidePill(root, "MISSIONS", new Vector2(0.03f, 0.56f), new Vector2(0.27f, 0.63f), new Color(0.22f, 0.55f, 0.90f), () => Router.ShowScreen(UIScreenId.Missions));
            BuildSidePill(root, "XP ROAD", new Vector2(0.03f, 0.47f), new Vector2(0.27f, 0.54f), new Color(0.62f, 0.30f, 0.88f), () => Router.ShowScreen(UIScreenId.XPRoad));
            BuildSidePill(root, "EVENTS", new Vector2(0.73f, 0.56f), new Vector2(0.97f, 0.63f), new Color(0.92f, 0.45f, 0.14f), () => Router.ShowScreen(UIScreenId.Events));
            BuildSidePill(root, "PARTY", new Vector2(0.73f, 0.47f), new Vector2(0.97f, 0.54f), new Color(0.88f, 0.24f, 0.54f), () => Router.ShowScreen(UIScreenId.PartyLobby));
            BuildSidePill(root, "BONUS", new Vector2(0.73f, 0.38f), new Vector2(0.97f, 0.45f), new Color(0.95f, 0.72f, 0.12f), () => Router.ShowScreen(UIScreenId.BonusGame));

            // Selected Character Banner
            var heroCard = UIBuilder.CreatePanel("HeroCard", root, new Color(0.16f, 0.21f, 0.33f));
            heroCard.rectTransform.anchorMin = new Vector2(0.18f, 0.33f);
            heroCard.rectTransform.anchorMax = new Vector2(0.69f, 0.45f);
            heroCard.rectTransform.offsetMin = heroCard.rectTransform.offsetMax = Vector2.zero;
            _heroPreviewTxt = UIBuilder.CreateText("HeroTxt", heroCard.transform, "Hero: Dash Cub", 26);
            UIBuilder.StretchFull(_heroPreviewTxt.rectTransform, 10f);

            // Primary PLAY CLASSIC CTA
            var playBtn = UIBuilder.CreateButton("PlayBtn", root, "PLAY CLASSIC", new Color(0.14f, 0.80f, 0.44f),
                () => _onLaunchMatch?.Invoke(GameMode.Default, ArenaSubMode.Classic), 42);
            playBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.16f, 0.21f);
            playBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.84f, 0.30f);
            playBtn.GetComponent<RectTransform>().offsetMin = playBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // Quick Mode Buttons (Team Battle & Paint Brawl)
            var tbBtn = UIBuilder.CreateButton("TeamBattleBtn", root, "TEAM BATTLE 4v4", new Color(0.18f, 0.52f, 0.92f),
                () => _onLaunchMatch?.Invoke(GameMode.TeamBattle, ArenaSubMode.TeamBattle), 26);
            tbBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.10f, 0.125f);
            tbBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.49f, 0.19f);
            tbBtn.GetComponent<RectTransform>().offsetMin = tbBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            var pbBtn = UIBuilder.CreateButton("PaintBrawlBtn", root, "PAINT BRAWL", new Color(0.92f, 0.34f, 0.28f),
                () => _onLaunchMatch?.Invoke(GameMode.PartyMode, ArenaSubMode.PaintBrawl), 26);
            pbBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.51f, 0.125f);
            pbBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.90f, 0.19f);
            pbBtn.GetComponent<RectTransform>().offsetMin = pbBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        }

        private static void BuildSidePill(RectTransform root, string text, Vector2 min, Vector2 max, Color col, Action onClick)
        {
            var b = UIBuilder.CreateButton($"Pill_{text}", root, text, col, onClick, 24);
            var rt = b.GetComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public override void Refresh()
        {
            BattleMapsChrome.RefreshCurrency(_currencyTxt);
            var d = SaveStateController.Instance?.Data;
            if (d != null && _rankTxt != null)
            {
                string rankName = MetaProgressionController.XpRoadRanks[Mathf.Clamp(d.CurrentRankIndex, 0, MetaProgressionController.XpRoadRanks.Length - 1)];
                _rankTxt.text = $"{d.PlayerName}  |  {rankName} ({d.TotalXp} XP)  |  Best: {d.BestCapturePercent:0.0}%";
            }
            if (_characters != null && _heroPreviewTxt != null)
            {
                var c = _characters.GetSelectedCharacter();
                _heroPreviewTxt.text = $"EQUIPPED: {c.DisplayName} ({c.Rarity})\nPower: {string.Join(" + ", c.PowerTypes)}";
            }
        }
    }

    /// <summary>
    /// 2. CharacterSelectionScreenView + CharacterInfosScreenView (BattleMaps.CharacterPowers.SelectionScreen & UI)
    /// </summary>
    public class CharacterSelectionScreen : UIScreenBase
    {
        public override UIScreenId ScreenId => UIScreenId.CharacterSelection;

        private CharactersManager _characters;
        private MetaProgressionController _meta;
        private Text _currencyTxt;
        private Text _statusTxt;
        private RectTransform _listContent;

        public void Bind(CharactersManager chars, MetaProgressionController meta)
        {
            _characters = chars;
            _meta = meta;
        }

        protected override void BuildUI(RectTransform root)
        {
            var bg = UIBuilder.CreatePanel("Bg", root, new Color(0.10f, 0.14f, 0.22f, 0.98f));
            UIBuilder.StretchFull(bg.rectTransform);
            _currencyTxt = BattleMapsChrome.BuildTopBar(root, Router, "HEROES & POWERS");
            BattleMapsChrome.BuildBottomNavBar(root, Router, UIScreenId.CharacterSelection);

            _statusTxt = UIBuilder.CreateText("Status", root, "Select a Hero & Power to equip!", 26, TextAnchor.MiddleCenter, Color.yellow);
            _statusTxt.rectTransform.anchorMin = new Vector2(0.05f, 0.855f);
            _statusTxt.rectTransform.anchorMax = new Vector2(0.95f, 0.905f);
            _statusTxt.rectTransform.offsetMin = _statusTxt.rectTransform.offsetMax = Vector2.zero;

            _listContent = UIBuilder.CreateScrollList("HeroList", root, 12f);
            var scrollRt = _listContent.parent.parent.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0.04f, 0.11f);
            scrollRt.anchorMax = new Vector2(0.96f, 0.85f);
            scrollRt.offsetMin = scrollRt.offsetMax = Vector2.zero;
        }

        public override void Refresh()
        {
            BattleMapsChrome.RefreshCurrency(_currencyTxt);
            if (_listContent == null || _characters == null) return;
            for (int i = _listContent.childCount - 1; i >= 0; i--) Destroy(_listContent.GetChild(i).gameObject);

            var save = SaveStateController.Instance;
            for (int i = 0; i < CharactersManager.Catalog.Count; i++)
            {
                int idx = i;
                var cfg = CharactersManager.Catalog[i];
                var st = save?.GetCharacterState(cfg.CharacterId);
                bool unlocked = st != null && st.Unlocked;
                bool equipped = _characters.SelectedCharacterIndex == idx;

                var card = UIBuilder.CreatePanel($"Hero_{idx}", _listContent, equipped ? new Color(0.16f, 0.34f, 0.28f) : new Color(0.16f, 0.20f, 0.30f));
                card.rectTransform.sizeDelta = new Vector2(0f, 130f);

                var info = UIBuilder.CreateText("Info", card.transform,
                    $"{cfg.DisplayName} [{cfg.Rarity} | Lv.{(st?.Level ?? 1)}]\nPowers: {string.Join(", ", cfg.PowerTypes)}\n{cfg.Description}",
                    22, TextAnchor.MiddleLeft);
                info.rectTransform.anchorMin = new Vector2(0.03f, 0.05f);
                info.rectTransform.anchorMax = new Vector2(0.68f, 0.95f);
                info.rectTransform.offsetMin = info.rectTransform.offsetMax = Vector2.zero;

                string btnTxt = !unlocked ? $"UNLOCK\n{cfg.UnlockCostCoins}c" : (equipped ? "EQUIPPED" : "EQUIP");
                Color btnCol = !unlocked ? new Color(0.88f, 0.56f, 0.14f) : (equipped ? new Color(0.16f, 0.78f, 0.46f) : new Color(0.22f, 0.52f, 0.90f));
                var actionBtn = UIBuilder.CreateButton("Act", card.transform, btnTxt, btnCol, () =>
                {
                    if (!unlocked)
                    {
                        var res = _meta.TryUpgradeCharacter(cfg.CharacterId);
                        _statusTxt.text = res.message;
                    }
                    else
                    {
                        _characters.SelectCharacter(idx);
                        if (save != null) { save.Data.SelectedCharacterIndex = idx; save.Save(); }
                        _statusTxt.text = $"Equipped {cfg.DisplayName}!";
                    }
                    Refresh();
                }, 22);
                actionBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.70f, 0.52f);
                actionBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.97f, 0.92f);
                actionBtn.GetComponent<RectTransform>().offsetMin = actionBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;

                if (unlocked)
                {
                    int upgCost = (st?.Level ?? 1) * 150;
                    var upgBtn = UIBuilder.CreateButton("Upg", card.transform, $"UPGRADE ({upgCost}c)", new Color(0.55f, 0.30f, 0.85f), () =>
                    {
                        var res = _meta.TryUpgradeCharacter(cfg.CharacterId);
                        _statusTxt.text = res.message;
                        Refresh();
                    }, 20);
                    upgBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.70f, 0.08f);
                    upgBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.97f, 0.46f);
                    upgBtn.GetComponent<RectTransform>().offsetMin = upgBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;
                }
            }
        }
    }

    /// <summary>
    /// 3. CharactersShopScreenView + ChestsUIView (BattleMaps.CharacterPowers.UI & BattleMaps.Chests)
    /// </summary>
    public class ShopAndChestsScreen : UIScreenBase
    {
        public override UIScreenId ScreenId => UIScreenId.ShopAndChests;
        private MetaProgressionController _meta;
        private Text _currencyTxt;
        private Text _rewardBanner;

        public void Bind(MetaProgressionController meta) => _meta = meta;

        protected override void BuildUI(RectTransform root)
        {
            var bg = UIBuilder.CreatePanel("Bg", root, new Color(0.11f, 0.14f, 0.22f, 0.98f));
            UIBuilder.StretchFull(bg.rectTransform);
            _currencyTxt = BattleMapsChrome.BuildTopBar(root, Router, "SHOP & CHESTS");
            BattleMapsChrome.BuildBottomNavBar(root, Router, UIScreenId.ShopAndChests);

            _rewardBanner = UIBuilder.CreateText("Banner", root, "Open Power Chests to unlock Heroes & Cards!", 30, TextAnchor.MiddleCenter, Color.yellow);
            _rewardBanner.rectTransform.anchorMin = new Vector2(0.06f, 0.75f);
            _rewardBanner.rectTransform.anchorMax = new Vector2(0.94f, 0.88f);
            _rewardBanner.rectTransform.offsetMin = _rewardBanner.rectTransform.offsetMax = Vector2.zero;

            AddChestCard(root, "COMMON CHEST (200 Coins)", 200, new Color(0.24f, 0.56f, 0.88f), 0.57f, 0.71f);
            AddChestCard(root, "EPIC POWER CHEST (450 Coins)", 450, new Color(0.62f, 0.28f, 0.88f), 0.40f, 0.54f);
            AddChestCard(root, "LEGENDARY CHEST (900 Coins)", 900, new Color(0.95f, 0.64f, 0.12f), 0.23f, 0.37f);

            var freeCoins = UIBuilder.CreateButton("DevCoins", root, "+500 FREE TEST COINS", new Color(0.16f, 0.76f, 0.44f), () =>
            {
                SaveStateController.Instance?.AddCoins(500);
                _rewardBanner.text = "+500 Coins added!";
                Refresh();
            }, 26);
            freeCoins.GetComponent<RectTransform>().anchorMin = new Vector2(0.20f, 0.12f);
            freeCoins.GetComponent<RectTransform>().anchorMax = new Vector2(0.80f, 0.20f);
            freeCoins.GetComponent<RectTransform>().offsetMin = freeCoins.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        }

        private void AddChestCard(RectTransform root, string label, int cost, Color col, float yMin, float yMax)
        {
            var btn = UIBuilder.CreateButton($"Chest_{cost}", root, label, col, () =>
            {
                var res = _meta.TryOpenPowerChest(cost);
                _rewardBanner.text = res.rewardSummary;
                Refresh();
            }, 32);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.10f, yMin);
            rt.anchorMax = new Vector2(0.90f, yMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public override void Refresh() => BattleMapsChrome.RefreshCurrency(_currencyTxt);
    }

    /// <summary>
    /// 4. WorldScreenView (BattleMaps.World.View — Interactive Visual Continent Map + Clickable Country Pins + World League)
    /// </summary>
    public class WorldMapScreen : UIScreenBase
    {
        public override UIScreenId ScreenId => UIScreenId.WorldMap;
        private MetaProgressionController _meta;
        private Action<string> _onPlayCountry;
        private Text _currencyTxt;
        private RectTransform _pinCanvas;
        private RectTransform _listContent;
        private Text _leagueTxt;
        private int _selectedContinentIndex;
        private bool _showLeague;

        public void Bind(MetaProgressionController meta, Action<string> onPlayCountry)
        {
            _meta = meta;
            _onPlayCountry = onPlayCountry;
        }

        protected override void BuildUI(RectTransform root)
        {
            var bg = UIBuilder.CreatePanel("Bg", root, new Color(0.08f, 0.14f, 0.25f, 0.98f));
            UIBuilder.StretchFull(bg.rectTransform);
            _currencyTxt = BattleMapsChrome.BuildTopBar(root, Router, "WORLD MAP & LEAGUE");
            BattleMapsChrome.BuildBottomNavBar(root, Router, UIScreenId.WorldMap);

            // 5 Continent Selector Tabs + World League Toggle
            var tabsBar = UIBuilder.CreateContainer("ContinentTabs", root);
            tabsBar.anchorMin = new Vector2(0.02f, 0.845f);
            tabsBar.anchorMax = new Vector2(0.98f, 0.90f);
            tabsBar.offsetMin = tabsBar.offsetMax = Vector2.zero;
            var hlg = tabsBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 5f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            for (int i = 0; i < MetaProgressionController.Continents.Length; i++)
            {
                int ci = i;
                string shortName = MetaProgressionController.Continents[i].Split(' ')[0];
                UIBuilder.CreateButton($"Cont_{i}", tabsBar, shortName, new Color(0.20f, 0.42f, 0.72f), () =>
                {
                    _showLeague = false;
                    _selectedContinentIndex = ci;
                    Refresh();
                }, 18);
            }
            UIBuilder.CreateButton("LeagueTab", tabsBar, "LEAGUE", new Color(0.92f, 0.62f, 0.12f), () =>
            {
                _showLeague = !_showLeague;
                Refresh();
            }, 18);

            // Interactive Visual Continent Pin Map Canvas (top half)
            var mapPanel = UIBuilder.CreatePanel("ContinentMapCanvas", root, new Color(0.11f, 0.24f, 0.40f));
            mapPanel.rectTransform.anchorMin = new Vector2(0.04f, 0.56f);
            mapPanel.rectTransform.anchorMax = new Vector2(0.96f, 0.835f);
            mapPanel.rectTransform.offsetMin = mapPanel.rectTransform.offsetMax = Vector2.zero;
            _pinCanvas = mapPanel.rectTransform;

            _leagueTxt = UIBuilder.CreateText("LeagueInfo", mapPanel.transform, "", 24, TextAnchor.MiddleCenter, Color.white);
            UIBuilder.StretchFull(_leagueTxt.rectTransform, 14f);

            // Country List Cards (bottom half)
            _listContent = UIBuilder.CreateScrollList("CountryList", root, 10f);
            var scrollRt = _listContent.parent.parent.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0.04f, 0.11f);
            scrollRt.anchorMax = new Vector2(0.96f, 0.545f);
            scrollRt.offsetMin = scrollRt.offsetMax = Vector2.zero;
        }

        public override void Refresh()
        {
            BattleMapsChrome.RefreshCurrency(_currencyTxt);
            if (_listContent == null || _pinCanvas == null) return;
            for (int i = _listContent.childCount - 1; i >= 0; i--) Destroy(_listContent.GetChild(i).gameObject);
            for (int i = _pinCanvas.childCount - 1; i >= 0; i--)
            {
                var ch = _pinCanvas.GetChild(i);
                if (ch.name != "LeagueInfo") Destroy(ch.gameObject);
            }

            if (_showLeague)
            {
                var d = SaveStateController.Instance?.Data;
                int trophies = d?.Trophies ?? 0;
                _leagueTxt.text = $"WORLD LEAGUE STANDINGS\n\n1. Champion_X — {trophies + 420} Trophies\n2. BattleKing — {trophies + 210} Trophies\n3. YOU ({d?.PlayerName}) — {trophies} Trophies\n4. BlitzBot — {Mathf.Max(0, trophies - 80)} Trophies\n5. Nova — {Mathf.Max(0, trophies - 150)} Trophies";
                return;
            }

            string cont = MetaProgressionController.Continents[_selectedContinentIndex];
            _leagueTxt.text = $"{cont.ToUpper()} — TAP A COUNTRY PIN TO PLAY ITS EXACT POLYGON MAP!";
            _leagueTxt.alignment = TextAnchor.UpperCenter;

            var countries = SaveStateController.Instance?.Data.Countries;
            if (countries == null) return;

            for (int i = 0; i < countries.Count; i++)
            {
                var c = countries[i];
                if (c.Continent != cont) continue;
                string cid = c.CountryId;

                // Place interactive clickable Country Pin on the visual continent map!
                if (BattleMaps.World.Controller.WorldCountryGeometryResolver.TryGetDefinition(cid, out var def))
                {
                    Color pinCol = c.Completed ? new Color(0.16f, 0.84f, 0.45f) : def.FlagPrimary;
                    var pinBtn = UIBuilder.CreateButton($"Pin_{cid}", _pinCanvas, $"{c.DisplayName}\n{c.BestPercent01 * 100f:0}%", pinCol, () =>
                    {
                        _meta?.SelectWorldCountry(cid);
                        _onPlayCountry?.Invoke(cid);
                    }, 17);
                    var prt = pinBtn.GetComponent<RectTransform>();
                    prt.anchorMin = prt.anchorMax = def.MapPinNormalized;
                    prt.sizeDelta = new Vector2(165f, 68f);
                    prt.anchoredPosition = Vector2.zero;
                }

                // Detailed card row below
                var card = UIBuilder.CreatePanel($"C_{cid}", _listContent, c.Completed ? new Color(0.14f, 0.36f, 0.25f) : new Color(0.16f, 0.20f, 0.31f));
                card.rectTransform.sizeDelta = new Vector2(0f, 96f);

                var lbl = UIBuilder.CreateText("Name", card.transform,
                    $"{c.DisplayName} (Custom Polygon Map)\nBest: {c.BestPercent01 * 100f:0.0}% {(c.Completed ? "[CONQUERED]" : "")}",
                    22, TextAnchor.MiddleLeft);
                lbl.rectTransform.anchorMin = new Vector2(0.04f, 0.1f);
                lbl.rectTransform.anchorMax = new Vector2(0.68f, 0.9f);
                lbl.rectTransform.offsetMin = lbl.rectTransform.offsetMax = Vector2.zero;

                var play = UIBuilder.CreateButton("Play", card.transform, "CONQUER", new Color(0.16f, 0.78f, 0.46f), () =>
                {
                    _meta?.SelectWorldCountry(cid);
                    _onPlayCountry?.Invoke(cid);
                }, 22);
                play.GetComponent<RectTransform>().anchorMin = new Vector2(0.71f, 0.18f);
                play.GetComponent<RectTransform>().anchorMax = new Vector2(0.96f, 0.82f);
                play.GetComponent<RectTransform>().offsetMin = play.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// 5. ChaptersScreenView (BattleMaps.Chapters.ChaptersController)
    /// </summary>
    public class ChaptersScreen : UIScreenBase
    {
        public override UIScreenId ScreenId => UIScreenId.Chapters;
        private MetaProgressionController _meta;
        private Action<int> _onPlayChapter;
        private Text _currencyTxt;
        private RectTransform _listContent;

        public void Bind(MetaProgressionController meta, Action<int> onPlayChapter)
        {
            _meta = meta;
            _onPlayChapter = onPlayChapter;
        }

        protected override void BuildUI(RectTransform root)
        {
            var bg = UIBuilder.CreatePanel("Bg", root, new Color(0.11f, 0.13f, 0.22f, 0.98f));
            UIBuilder.StretchFull(bg.rectTransform);
            _currencyTxt = BattleMapsChrome.BuildTopBar(root, Router, "CHAPTERS");
            BattleMapsChrome.BuildBottomNavBar(root, Router, UIScreenId.Chapters);

            _listContent = UIBuilder.CreateScrollList("ChaptersList", root, 14f);
            var scrollRt = _listContent.parent.parent.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0.04f, 0.11f);
            scrollRt.anchorMax = new Vector2(0.96f, 0.89f);
            scrollRt.offsetMin = scrollRt.offsetMax = Vector2.zero;
        }

        public override void Refresh()
        {
            BattleMapsChrome.RefreshCurrency(_currencyTxt);
            if (_listContent == null) return;
            for (int i = _listContent.childCount - 1; i >= 0; i--) Destroy(_listContent.GetChild(i).gameObject);

            var d = SaveStateController.Instance?.Data;
            int unlockedIdx = d?.CurrentChapterIndex ?? 0;

            for (int i = 0; i < MetaProgressionController.ChapterTitles.Length; i++)
            {
                int ci = i;
                bool unlocked = ci <= unlockedIdx;
                string status = ci < unlockedIdx ? "COMPLETED" : (ci == unlockedIdx ? $"LEVEL {d.CurrentChapterLevel}/3" : "LOCKED");

                var card = UIBuilder.CreatePanel($"Chap_{i}", _listContent, unlocked ? new Color(0.18f, 0.24f, 0.36f) : new Color(0.14f, 0.15f, 0.20f));
                card.rectTransform.sizeDelta = new Vector2(0f, 125f);

                var txt = UIBuilder.CreateText("Lbl", card.transform, $"{MetaProgressionController.ChapterTitles[i]}\nStatus: {status}", 25, TextAnchor.MiddleLeft);
                txt.rectTransform.anchorMin = new Vector2(0.04f, 0.1f);
                txt.rectTransform.anchorMax = new Vector2(0.68f, 0.9f);
                txt.rectTransform.offsetMin = txt.rectTransform.offsetMax = Vector2.zero;

                var btn = UIBuilder.CreateButton("PlayChap", card.transform, unlocked ? "PLAY" : "LOCKED",
                    unlocked ? new Color(0.16f, 0.80f, 0.45f) : new Color(0.32f, 0.34f, 0.40f), () =>
                    {
                        if (!unlocked) return;
                        _meta?.SelectChapter(ci);
                        _onPlayChapter?.Invoke(ci);
                    }, 26);
                btn.GetComponent<RectTransform>().anchorMin = new Vector2(0.71f, 0.2f);
                btn.GetComponent<RectTransform>().anchorMax = new Vector2(0.96f, 0.8f);
                btn.GetComponent<RectTransform>().offsetMin = btn.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// 6. PartyLobbyScreenView (BattleMaps.Features.Party & Multiplayer — TeamBattle / PaintBrawl Squad Lobby)
    /// </summary>
    public class PartyLobbyScreen : UIScreenBase
    {
        public override UIScreenId ScreenId => UIScreenId.PartyLobby;
        private Action<GameMode, ArenaSubMode> _onLaunch;
        private BattleMaps.Features.Multiplayer.BattleMapsMultiplayerClient _net;
        private Text _currencyTxt;
        private Text _modeTxt;
        private Text _netStatusTxt;
        private bool _isTeamBattle = true;

        public void Bind(Action<GameMode, ArenaSubMode> onLaunch, BattleMaps.Features.Multiplayer.BattleMapsMultiplayerClient net = null)
        {
            _onLaunch = onLaunch;
            _net = net;
            if (_net != null) _net.StatusChanged += s => { if (_netStatusTxt != null) _netStatusTxt.text = $"NET: {s}"; };
        }

        protected override void BuildUI(RectTransform root)
        {
            var bg = UIBuilder.CreatePanel("Bg", root, new Color(0.12f, 0.12f, 0.22f, 0.98f));
            UIBuilder.StretchFull(bg.rectTransform);
            _currencyTxt = BattleMapsChrome.BuildTopBar(root, Router, "PARTY LOBBY", showBack: true);

            _modeTxt = UIBuilder.CreateText("Mode", root, "MODE: TEAM BATTLE (4v4)", 34, TextAnchor.MiddleCenter, Color.cyan);
            _modeTxt.rectTransform.anchorMin = new Vector2(0.08f, 0.81f);
            _modeTxt.rectTransform.anchorMax = new Vector2(0.92f, 0.88f);
            _modeTxt.rectTransform.offsetMin = _modeTxt.rectTransform.offsetMax = Vector2.zero;

            var toggleBtn = UIBuilder.CreateButton("ToggleMode", root, "SWITCH: TEAM BATTLE / PAINT BRAWL", new Color(0.26f, 0.48f, 0.86f), () =>
            {
                _isTeamBattle = !_isTeamBattle;
                _modeTxt.text = _isTeamBattle ? "MODE: TEAM BATTLE (4v4)" : "MODE: PAINT BRAWL (FFA)";
            }, 24);
            toggleBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.12f, 0.73f);
            toggleBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.88f, 0.80f);
            toggleBtn.GetComponent<RectTransform>().offsetMin = toggleBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // Online / LAN WebSocket Connect row
            _netStatusTxt = UIBuilder.CreateText("NetStatus", root, "NET: Offline (Local AI Bots Ready)", 22, TextAnchor.MiddleLeft, Color.yellow);
            _netStatusTxt.rectTransform.anchorMin = new Vector2(0.08f, 0.65f);
            _netStatusTxt.rectTransform.anchorMax = new Vector2(0.66f, 0.72f);
            _netStatusTxt.rectTransform.offsetMin = _netStatusTxt.rectTransform.offsetMax = Vector2.zero;

            var connBtn = UIBuilder.CreateButton("ConnectWS", root, "CONNECT LAN/WS", new Color(0.58f, 0.28f, 0.86f), () =>
            {
                _net?.ConnectToRoomAsync();
            }, 20);
            connBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.68f, 0.655f);
            connBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.92f, 0.715f);
            connBtn.GetComponent<RectTransform>().offsetMin = connBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // 4 Squad Slots (PartyPlayerCell + PartyBotFiller)
            string[] squad = { "1. YOU (HOST - READY)", "2. Nova [Teammate - READY]", "3. Blitz [Teammate - READY]", "4. Vortex [Teammate - READY]" };
            for (int i = 0; i < squad.Length; i++)
            {
                var slot = UIBuilder.CreatePanel($"Slot{i}", root, new Color(0.18f, 0.22f, 0.34f));
                slot.rectTransform.anchorMin = new Vector2(0.10f, 0.54f - i * 0.095f);
                slot.rectTransform.anchorMax = new Vector2(0.90f, 0.62f - i * 0.095f);
                slot.rectTransform.offsetMin = slot.rectTransform.offsetMax = Vector2.zero;
                var txt = UIBuilder.CreateText("Txt", slot.transform, squad[i], 25);
                UIBuilder.StretchFull(txt.rectTransform);
            }

            var startBtn = UIBuilder.CreateButton("StartParty", root, "START PARTY MATCH", new Color(0.16f, 0.82f, 0.46f), () =>
            {
                if (_isTeamBattle) _onLaunch?.Invoke(GameMode.TeamBattle, ArenaSubMode.TeamBattle);
                else _onLaunch?.Invoke(GameMode.PartyMode, ArenaSubMode.PaintBrawl);
            }, 36);
            startBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.14f, 0.09f);
            startBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.86f, 0.19f);
            startBtn.GetComponent<RectTransform>().offsetMin = startBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        }

        public override void Refresh()
        {
            BattleMapsChrome.RefreshCurrency(_currencyTxt);
            if (_net != null && _netStatusTxt != null) _netStatusTxt.text = $"NET: {_net.StatusText}";
        }
    }

    /// <summary>
    /// 7. MissionsScreenView (BattleMaps.Missions.MissionsV2Manager)
    /// </summary>
    public class MissionsScreen : UIScreenBase
    {
        public override UIScreenId ScreenId => UIScreenId.Missions;
        private Text _currencyTxt;
        private RectTransform _listContent;

        protected override void BuildUI(RectTransform root)
        {
            var bg = UIBuilder.CreatePanel("Bg", root, new Color(0.10f, 0.14f, 0.22f, 0.98f));
            UIBuilder.StretchFull(bg.rectTransform);
            _currencyTxt = BattleMapsChrome.BuildTopBar(root, Router, "DAILY MISSIONS", showBack: true);

            _listContent = UIBuilder.CreateScrollList("MissionsList", root, 14f);
            var scrollRt = _listContent.parent.parent.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0.04f, 0.05f);
            scrollRt.anchorMax = new Vector2(0.96f, 0.89f);
            scrollRt.offsetMin = scrollRt.offsetMax = Vector2.zero;
        }

        public override void Refresh()
        {
            BattleMapsChrome.RefreshCurrency(_currencyTxt);
            if (_listContent == null) return;
            for (int i = _listContent.childCount - 1; i >= 0; i--) Destroy(_listContent.GetChild(i).gameObject);

            var save = SaveStateController.Instance;
            if (save == null) return;

            foreach (var m in save.Data.DailyMissions)
            {
                var mission = m;
                bool ready = mission.Progress >= mission.Target && !mission.Claimed;

                var card = UIBuilder.CreatePanel($"M_{mission.MissionId}", _listContent, new Color(0.16f, 0.21f, 0.32f));
                card.rectTransform.sizeDelta = new Vector2(0f, 120f);

                var lbl = UIBuilder.CreateText("Lbl", card.transform,
                    $"{mission.Title}\nProgress: {mission.Progress:0}/{mission.Target:0}  |  Reward: {mission.CoinReward}c + {mission.XpReward} XP",
                    23, TextAnchor.MiddleLeft);
                lbl.rectTransform.anchorMin = new Vector2(0.04f, 0.1f);
                lbl.rectTransform.anchorMax = new Vector2(0.70f, 0.9f);
                lbl.rectTransform.offsetMin = lbl.rectTransform.offsetMax = Vector2.zero;

                string bTxt = mission.Claimed ? "CLAIMED" : (ready ? "CLAIM!" : "IN PROG");
                Color bCol = mission.Claimed ? Color.gray : (ready ? new Color(0.16f, 0.82f, 0.45f) : new Color(0.28f, 0.32f, 0.42f));
                var btn = UIBuilder.CreateButton("Claim", card.transform, bTxt, bCol, () =>
                {
                    if (!ready) return;
                    mission.Claimed = true;
                    save.AddCoins(mission.CoinReward);
                    save.AddXp(mission.XpReward);
                    Refresh();
                }, 22);
                btn.GetComponent<RectTransform>().anchorMin = new Vector2(0.72f, 0.2f);
                btn.GetComponent<RectTransform>().anchorMax = new Vector2(0.96f, 0.8f);
                btn.GetComponent<RectTransform>().offsetMin = btn.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// 8. XPRoadScreenView (BattleMaps.XPRoad.View)
    /// </summary>
    public class XPRoadScreen : UIScreenBase
    {
        public override UIScreenId ScreenId => UIScreenId.XPRoad;
        private Text _currencyTxt;
        private RectTransform _listContent;

        protected override void BuildUI(RectTransform root)
        {
            var bg = UIBuilder.CreatePanel("Bg", root, new Color(0.12f, 0.11f, 0.24f, 0.98f));
            UIBuilder.StretchFull(bg.rectTransform);
            _currencyTxt = BattleMapsChrome.BuildTopBar(root, Router, "XP ROAD", showBack: true);

            _listContent = UIBuilder.CreateScrollList("XpList", root, 12f);
            var scrollRt = _listContent.parent.parent.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0.04f, 0.05f);
            scrollRt.anchorMax = new Vector2(0.96f, 0.89f);
            scrollRt.offsetMin = scrollRt.offsetMax = Vector2.zero;
        }

        public override void Refresh()
        {
            BattleMapsChrome.RefreshCurrency(_currencyTxt);
            if (_listContent == null) return;
            for (int i = _listContent.childCount - 1; i >= 0; i--) Destroy(_listContent.GetChild(i).gameObject);

            var save = SaveStateController.Instance;
            int xp = save?.Data.TotalXp ?? 0;

            for (int i = 0; i < MetaProgressionController.XpRoadRanks.Length; i++)
            {
                int step = i;
                int reqXp = step * 250;
                int reward = 150 + step * 75;
                bool reached = xp >= reqXp;
                bool claimed = save != null && save.Data.ClaimedXpSteps.Contains(step);

                var card = UIBuilder.CreatePanel($"Step_{step}", _listContent, reached ? new Color(0.22f, 0.20f, 0.42f) : new Color(0.15f, 0.16f, 0.24f));
                card.rectTransform.sizeDelta = new Vector2(0f, 105f);

                var lbl = UIBuilder.CreateText("Lbl", card.transform,
                    $"Tier {step + 1}: {MetaProgressionController.XpRoadRanks[step]}  ({reqXp} XP)\nMilestone Reward: +{reward} Coins",
                    24, TextAnchor.MiddleLeft);
                lbl.rectTransform.anchorMin = new Vector2(0.04f, 0.1f);
                lbl.rectTransform.anchorMax = new Vector2(0.70f, 0.9f);
                lbl.rectTransform.offsetMin = lbl.rectTransform.offsetMax = Vector2.zero;

                string bTxt = claimed ? "CLAIMED" : (reached ? "CLAIM" : "LOCKED");
                Color bCol = claimed ? Color.gray : (reached ? new Color(0.16f, 0.82f, 0.46f) : new Color(0.30f, 0.30f, 0.38f));
                var btn = UIBuilder.CreateButton("Claim", card.transform, bTxt, bCol, () =>
                {
                    if (!reached || claimed || save == null) return;
                    save.Data.ClaimedXpSteps.Add(step);
                    save.AddCoins(reward);
                    Refresh();
                }, 22);
                btn.GetComponent<RectTransform>().anchorMin = new Vector2(0.72f, 0.2f);
                btn.GetComponent<RectTransform>().anchorMax = new Vector2(0.96f, 0.8f);
                btn.GetComponent<RectTransform>().offsetMin = btn.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// 9. BonusGameScreenView (BattleMaps.BonusGame.BonusGameController — 3x3 Mystery Prize Picker!)
    /// </summary>
    public class BonusGameScreen : UIScreenBase
    {
        public override UIScreenId ScreenId => UIScreenId.BonusGame;
        private Text _currencyTxt;
        private Text _infoTxt;
        private readonly Button[] _tiles = new Button[9];
        private readonly Text[] _tileLabels = new Text[9];
        private int _picksRemaining;
        private int _totalWon;

        protected override void BuildUI(RectTransform root)
        {
            var bg = UIBuilder.CreatePanel("Bg", root, new Color(0.15f, 0.11f, 0.22f, 0.98f));
            UIBuilder.StretchFull(bg.rectTransform);
            _currencyTxt = BattleMapsChrome.BuildTopBar(root, Router, "BONUS MINI-GAME", showBack: true);

            _infoTxt = UIBuilder.CreateText("Info", root, "Pick 3 Mystery Chests to win Bonus Coins!", 32, TextAnchor.MiddleCenter, Color.yellow);
            _infoTxt.rectTransform.anchorMin = new Vector2(0.06f, 0.78f);
            _infoTxt.rectTransform.anchorMax = new Vector2(0.94f, 0.88f);
            _infoTxt.rectTransform.offsetMin = _infoTxt.rectTransform.offsetMax = Vector2.zero;

            var gridObj = UIBuilder.CreateContainer("Grid", root);
            gridObj.anchorMin = new Vector2(0.10f, 0.25f);
            gridObj.anchorMax = new Vector2(0.90f, 0.75f);
            gridObj.offsetMin = gridObj.offsetMax = Vector2.zero;

            var glg = gridObj.gameObject.AddComponent<GridLayoutGroup>();
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 3;
            glg.cellSize = new Vector2(240f, 240f);
            glg.spacing = new Vector2(24f, 24f);
            glg.childAlignment = TextAnchor.MiddleCenter;

            for (int i = 0; i < 9; i++)
            {
                int idx = i;
                var b = UIBuilder.CreateButton($"Tile_{i}", gridObj, "?", new Color(0.90f, 0.58f, 0.12f), () => PickTile(idx), 52);
                _tiles[i] = b;
                _tileLabels[i] = b.GetComponentInChildren<Text>();
            }

            var reset = UIBuilder.CreateButton("DoneBtn", root, "COLLECT & RETURN HOME", new Color(0.16f, 0.80f, 0.45f), () => Router.ShowScreen(UIScreenId.Home), 32);
            reset.GetComponent<RectTransform>().anchorMin = new Vector2(0.16f, 0.10f);
            reset.GetComponent<RectTransform>().anchorMax = new Vector2(0.84f, 0.19f);
            reset.GetComponent<RectTransform>().offsetMin = reset.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        }

        public override void OnShow()
        {
            _picksRemaining = 3;
            _totalWon = 0;
            for (int i = 0; i < 9; i++)
            {
                _tiles[i].interactable = true;
                _tiles[i].GetComponent<Image>().color = new Color(0.90f, 0.58f, 0.12f);
                _tileLabels[i].text = "?";
            }
            if (_infoTxt != null) _infoTxt.text = "Pick 3 Mystery Chests to win Bonus Coins!";
            base.OnShow();
        }

        private void PickTile(int idx)
        {
            if (_picksRemaining <= 0) return;
            _picksRemaining--;
            _tiles[idx].interactable = false;

            int[] prizes = { 50, 75, 100, 150, 200, 300 };
            int prize = prizes[UnityEngine.Random.Range(0, prizes.Length)];
            _totalWon += prize;

            _tiles[idx].GetComponent<Image>().color = new Color(0.16f, 0.72f, 0.45f);
            _tileLabels[idx].text = $"+{prize}c";

            SaveStateController.Instance?.AddCoins(prize);
            if (_picksRemaining == 0)
            {
                SaveStateController.Instance.Data.BonusGamesPlayed++;
                SaveStateController.Instance.Save();
                _infoTxt.text = $"JACKPOT! Total Won: +{_totalWon} Coins!";
            }
            else
            {
                _infoTxt.text = $"Won +{prize}c! Picks left: {_picksRemaining}";
            }
            Refresh();
        }

        public override void Refresh() => BattleMapsChrome.RefreshCurrency(_currencyTxt);
    }

    /// <summary>
    /// 10. EventsScreenView (CollectToWin, KillToWin, TeamBattle live events hub)
    /// </summary>
    public class EventsScreen : UIScreenBase
    {
        public override UIScreenId ScreenId => UIScreenId.Events;
        private Text _currencyTxt;
        private RectTransform _listContent;

        protected override void BuildUI(RectTransform root)
        {
            var bg = UIBuilder.CreatePanel("Bg", root, new Color(0.14f, 0.12f, 0.20f, 0.98f));
            UIBuilder.StretchFull(bg.rectTransform);
            _currencyTxt = BattleMapsChrome.BuildTopBar(root, Router, "LIVE EVENTS", showBack: true);

            _listContent = UIBuilder.CreateScrollList("EventsList", root, 16f);
            var scrollRt = _listContent.parent.parent.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0.04f, 0.05f);
            scrollRt.anchorMax = new Vector2(0.96f, 0.89f);
            scrollRt.offsetMin = scrollRt.offsetMax = Vector2.zero;
        }

        public override void Refresh()
        {
            BattleMapsChrome.RefreshCurrency(_currencyTxt);
            if (_listContent == null) return;
            for (int i = _listContent.childCount - 1; i >= 0; i--) Destroy(_listContent.GetChild(i).gameObject);

            var save = SaveStateController.Instance;
            if (save == null) return;

            foreach (var ev in save.Data.ActiveEvents)
            {
                var item = ev;
                bool ready = item.Score >= item.MilestoneTarget && !item.Claimed;

                var card = UIBuilder.CreatePanel($"Ev_{item.EventId}", _listContent, new Color(0.24f, 0.17f, 0.34f));
                card.rectTransform.sizeDelta = new Vector2(0f, 140f);

                var lbl = UIBuilder.CreateText("Lbl", card.transform,
                    $"{item.Title}\nScore: {item.Score:0}/{item.MilestoneTarget:0}  |  Grand Prize: {item.CoinReward} Coins",
                    25, TextAnchor.MiddleLeft);
                lbl.rectTransform.anchorMin = new Vector2(0.04f, 0.1f);
                lbl.rectTransform.anchorMax = new Vector2(0.70f, 0.9f);
                lbl.rectTransform.offsetMin = lbl.rectTransform.offsetMax = Vector2.zero;

                var btn = UIBuilder.CreateButton("Claim", card.transform, item.Claimed ? "CLAIMED" : (ready ? "CLAIM" : "PLAY"),
                    ready ? new Color(0.16f, 0.82f, 0.45f) : new Color(0.88f, 0.48f, 0.14f), () =>
                    {
                        if (ready)
                        {
                            item.Claimed = true;
                            save.AddCoins(item.CoinReward);
                            Refresh();
                        }
                        else if (!item.Claimed)
                        {
                            Router.ShowScreen(UIScreenId.Home);
                        }
                    }, 24);
                btn.GetComponent<RectTransform>().anchorMin = new Vector2(0.72f, 0.2f);
                btn.GetComponent<RectTransform>().anchorMax = new Vector2(0.96f, 0.8f);
                btn.GetComponent<RectTransform>().offsetMin = btn.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// 11. SettingsPopupView (BattleMaps.Core.Controller.SettingsController)
    /// </summary>
    public class SettingsPopupScreen : UIScreenBase
    {
        public override UIScreenId ScreenId => UIScreenId.SettingsPopup;
        public override bool IsPopup => true;

        private Text _sfxTxt;
        private Text _musicTxt;
        private Text _qualityTxt;
        private Text _langTxt;

        protected override void BuildUI(RectTransform root)
        {
            var dim = UIBuilder.CreatePanel("Dim", root, new Color(0f, 0f, 0f, 0.68f));
            UIBuilder.StretchFull(dim.rectTransform);

            var card = UIBuilder.CreatePanel("Card", root, new Color(0.14f, 0.17f, 0.26f, 0.98f));
            card.rectTransform.anchorMin = new Vector2(0.12f, 0.20f);
            card.rectTransform.anchorMax = new Vector2(0.88f, 0.80f);
            card.rectTransform.offsetMin = card.rectTransform.offsetMax = Vector2.zero;

            var title = UIBuilder.CreateText("Title", card.transform, "SETTINGS", 44);
            title.rectTransform.anchorMin = new Vector2(0.1f, 0.84f);
            title.rectTransform.anchorMax = new Vector2(0.9f, 0.96f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;

            var sfxBtn = UIBuilder.CreateButton("SfxBtn", card.transform, "SFX: ON", new Color(0.22f, 0.50f, 0.82f), () =>
            {
                var d = SaveStateController.Instance.Data;
                d.SfxEnabled = !d.SfxEnabled;
                SaveStateController.Instance.Save();
                Refresh();
            }, 26);
            sfxBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.12f, 0.69f);
            sfxBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.88f, 0.80f);
            sfxBtn.GetComponent<RectTransform>().offsetMin = sfxBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            _sfxTxt = sfxBtn.GetComponentInChildren<Text>();

            var musBtn = UIBuilder.CreateButton("MusicBtn", card.transform, "MUSIC: ON", new Color(0.22f, 0.50f, 0.82f), () =>
            {
                var d = SaveStateController.Instance.Data;
                d.MusicEnabled = !d.MusicEnabled;
                SaveStateController.Instance.Save();
                Refresh();
            }, 26);
            musBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.12f, 0.55f);
            musBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.88f, 0.66f);
            musBtn.GetComponent<RectTransform>().offsetMin = musBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            _musicTxt = musBtn.GetComponentInChildren<Text>();

            var qualBtn = UIBuilder.CreateButton("QualBtn", card.transform, "QUALITY: HIGH", new Color(0.45f, 0.32f, 0.78f), () =>
            {
                var d = SaveStateController.Instance.Data;
                d.QualityTier = (d.QualityTier + 1) % 3;
                QualitySettings.SetQualityLevel(Mathf.Min(d.QualityTier, QualitySettings.names.Length - 1));
                SaveStateController.Instance.Save();
                Refresh();
            }, 26);
            qualBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.12f, 0.41f);
            qualBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.88f, 0.52f);
            qualBtn.GetComponent<RectTransform>().offsetMin = qualBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            _qualityTxt = qualBtn.GetComponentInChildren<Text>();

            var langBtn = UIBuilder.CreateButton("LangBtn", card.transform, "LANGUAGE: English", new Color(0.84f, 0.50f, 0.16f), () =>
            {
                BattleMaps.Localization.LocalizationManager.CycleLanguage();
                Refresh();
            }, 26);
            langBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.12f, 0.27f);
            langBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.88f, 0.38f);
            langBtn.GetComponent<RectTransform>().offsetMin = langBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            _langTxt = langBtn.GetComponentInChildren<Text>();

            var closeBtn = UIBuilder.CreateButton("Close", card.transform, "CLOSE", new Color(0.16f, 0.78f, 0.45f), () => Router.CloseTopPopup(), 32);
            closeBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.22f, 0.07f);
            closeBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.78f, 0.20f);
            closeBtn.GetComponent<RectTransform>().offsetMin = closeBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        }

        public override void Refresh()
        {
            var d = SaveStateController.Instance?.Data;
            if (d == null) return;
            if (_sfxTxt != null) _sfxTxt.text = $"SFX: {(d.SfxEnabled ? "ON" : "OFF")}";
            if (_musicTxt != null) _musicTxt.text = $"MUSIC: {(d.MusicEnabled ? "ON" : "OFF")}";
            string[] qNames = { "LOW", "MEDIUM", "HIGH" };
            if (_qualityTxt != null) _qualityTxt.text = $"QUALITY: {qNames[Mathf.Clamp(d.QualityTier, 0, 2)]}";
            if (_langTxt != null) _langTxt.text = $"LANGUAGE: {BattleMaps.Localization.LocalizationManager.CurrentLanguage}";
        }
    }
}
