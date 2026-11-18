using System.Collections.Generic;
using GameOps.UIFramework;
using BattleMaps.CharacterPowers.Characters;
using BattleMaps.Gameplay;
using BattleMaps.Modes;
using BattleMaps.SaveStates;
using BattleMaps.Signals;
using UnityEngine;
using UnityEngine.UI;

namespace BattleMaps.UI.Screens
{
    /// <summary>
    /// In-Game HUD matching GameOps.UIGame.GameScreenView (29 methods) +
    /// GameplayLeaderboardView + GameplayMinimapView + FlyingTextController.
    /// </summary>
    public class GameplayHudScreen : UIScreenBase
    {
        public override UIScreenId ScreenId => UIScreenId.GameplayHud;

        private GameplayController _gameplay;
        private CharactersManager _characters;
        private MatchModeRulesController _modeRules;

        private Text _capturePctText;
        private Image _captureBarFill;
        private Text _killsText;
        private Text _timerText;
        private Text _teamScoreText;
        private readonly Text[] _leaderboardRows = new Text[5];
        private Button _powerButton;
        private Image _powerChargeFill;
        private Text _powerLabel;
        private RawImage _minimapImage;
        private Texture2D _minimapTex;
        private Color32[] _minimapPixels;
        private float _minimapRefreshTimer;

        private RectTransform _flyingRoot;
        private readonly List<FlyingPopupItem> _flyingItems = new();

        private sealed class FlyingPopupItem
        {
            public Text Label;
            public Vector3 WorldPos;
            public float Elapsed;
            public float Duration;
        }

        public void BindSystems(GameplayController gp, CharactersManager chars, MatchModeRulesController rules)
        {
            _gameplay = gp;
            _characters = chars;
            _modeRules = rules;
        }

        protected override void BuildUI(RectTransform root)
        {
            // Top-center Capture % bar
            var bar = UIBuilder.CreateProgressBar("CaptureBar", root, new Color(0.1f, 0.12f, 0.18f, 0.78f), new Color(0.16f, 0.82f, 0.48f), "0.0%");
            bar.root.anchorMin = new Vector2(0.28f, 0.92f);
            bar.root.anchorMax = new Vector2(0.72f, 0.965f);
            bar.root.offsetMin = bar.root.offsetMax = Vector2.zero;
            _captureBarFill = bar.fill;
            _capturePctText = bar.label;

            // Top-left Kills + Timer + Pause
            var pauseBtn = UIBuilder.CreateButton("PauseBtn", root, "||", new Color(0.16f, 0.20f, 0.28f, 0.85f), () =>
            {
                _gameplay?.PauseGame();
                Router.OpenPopup(UIScreenId.PausePopup);
            }, 34);
            pauseBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.03f, 0.915f);
            pauseBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.13f, 0.97f);
            pauseBtn.GetComponent<RectTransform>().offsetMin = pauseBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            var killsBg = UIBuilder.CreatePanel("KillsPanel", root, new Color(0.1f, 0.12f, 0.18f, 0.75f));
            killsBg.rectTransform.anchorMin = new Vector2(0.03f, 0.855f);
            killsBg.rectTransform.anchorMax = new Vector2(0.24f, 0.905f);
            killsBg.rectTransform.offsetMin = killsBg.rectTransform.offsetMax = Vector2.zero;
            _killsText = UIBuilder.CreateText("KillsTxt", killsBg.transform, "KILLS: 0", 26);
            UIBuilder.StretchFull(_killsText.rectTransform);

            _timerText = UIBuilder.CreateText("TimerTxt", root, "", 32, TextAnchor.MiddleCenter, Color.yellow);
            _timerText.rectTransform.anchorMin = new Vector2(0.32f, 0.865f);
            _timerText.rectTransform.anchorMax = new Vector2(0.68f, 0.915f);
            _timerText.rectTransform.offsetMin = _timerText.rectTransform.offsetMax = Vector2.zero;

            _teamScoreText = UIBuilder.CreateText("TeamScoreTxt", root, "", 28, TextAnchor.MiddleCenter, Color.white);
            _teamScoreText.rectTransform.anchorMin = new Vector2(0.25f, 0.825f);
            _teamScoreText.rectTransform.anchorMax = new Vector2(0.75f, 0.865f);
            _teamScoreText.rectTransform.offsetMin = _teamScoreText.rectTransform.offsetMax = Vector2.zero;

            // Top-right Top-5 Crown Leaderboard (GameplayLeaderboardView)
            var lbPanel = UIBuilder.CreatePanel("Leaderboard", root, new Color(0.08f, 0.10f, 0.16f, 0.72f));
            lbPanel.rectTransform.anchorMin = new Vector2(0.73f, 0.78f);
            lbPanel.rectTransform.anchorMax = new Vector2(0.98f, 0.97f);
            lbPanel.rectTransform.offsetMin = lbPanel.rectTransform.offsetMax = Vector2.zero;

            var lbLayout = lbPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            lbLayout.padding = new RectOffset(10, 10, 8, 8);
            lbLayout.spacing = 4f;
            lbLayout.childControlHeight = true;
            lbLayout.childControlWidth = true;

            for (int i = 0; i < 5; i++)
                _leaderboardRows[i] = UIBuilder.CreateText($"Row{i}", lbPanel.transform, $"{i + 1}. ---", 22, TextAnchor.MiddleLeft);

            // Bottom-left Procedural Minimap (GameplayMinimapView)
            var mmBorder = UIBuilder.CreatePanel("MinimapBorder", root, new Color(0.08f, 0.10f, 0.16f, 0.82f));
            mmBorder.rectTransform.anchorMin = new Vector2(0.03f, 0.03f);
            mmBorder.rectTransform.anchorMax = new Vector2(0.27f, 0.175f);
            mmBorder.rectTransform.offsetMin = mmBorder.rectTransform.offsetMax = Vector2.zero;

            var mmGo = new GameObject("MinimapRaw");
            mmGo.transform.SetParent(mmBorder.transform, false);
            _minimapImage = mmGo.AddComponent<RawImage>();
            UIBuilder.StretchFull(_minimapImage.rectTransform, 6f);
            _minimapTex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            _minimapPixels = new Color32[64 * 64];
            _minimapImage.texture = _minimapTex;

            // Bottom-right Active Character Power Button
            var pwrBar = UIBuilder.CreateProgressBar("PowerBtnRoot", root, new Color(0.14f, 0.16f, 0.24f, 0.9f), new Color(0.98f, 0.68f, 0.12f), "POWER");
            pwrBar.root.anchorMin = new Vector2(0.68f, 0.04f);
            pwrBar.root.anchorMax = new Vector2(0.96f, 0.135f);
            pwrBar.root.offsetMin = pwrBar.root.offsetMax = Vector2.zero;
            _powerChargeFill = pwrBar.fill;
            _powerLabel = pwrBar.label;
            _powerButton = pwrBar.root.gameObject.AddComponent<Button>();
            _powerButton.onClick.AddListener(() => _characters?.TryActivateLocalPlayerPower());

            // Flying Text Container (FlyingTextController)
            _flyingRoot = UIBuilder.CreateContainer("FlyingTextLayer", root);
            UIBuilder.StretchFull(_flyingRoot);

            EventBus.Shared.Subscribe<FlyingTextSignal>(OnFlyingText);
        }

        private void OnFlyingText(FlyingTextSignal sig)
        {
            if (!gameObject.activeInHierarchy || _flyingRoot == null) return;
            var txt = UIBuilder.CreateText("Fly", _flyingRoot, sig.Text, Mathf.RoundToInt(36 * sig.Scale), TextAnchor.MiddleCenter, sig.Color);
            txt.rectTransform.sizeDelta = new Vector2(400f, 80f);
            _flyingItems.Add(new FlyingPopupItem { Label = txt, WorldPos = sig.WorldPosition, Elapsed = 0f, Duration = 1.15f });
        }

        private void Update()
        {
            if (_gameplay == null || _gameplay.ZoneManager == null) return;

            var local = _gameplay.ZoneManager.MainPlayer;
            if (local != null)
            {
                float pct = local.GetCapturedPercent01();
                _captureBarFill.fillAmount = pct;
                _capturePctText.text = $"{pct * 100f:0.0}%";
                _killsText.text = $"KILLS: {local.Kills}";
            }

            if (_modeRules != null && _modeRules.IsTimedMode)
            {
                int secs = Mathf.CeilToInt(_modeRules.RemainingTimeSeconds);
                _timerText.text = $"TIME {secs / 60}:{secs % 60:00}";
            }
            else
            {
                _timerText.text = "";
            }

            if (_gameplay.CurrentMode == GameMode.TeamBattle && _modeRules != null)
                _teamScoreText.text = $"BLUE {_modeRules.BlueTeamPercent01 * 100f:0.0}%   vs   RED {_modeRules.RedTeamPercent01 * 100f:0.0}%";
            else
                _teamScoreText.text = "";

            // Leaderboard refresh
            var ranked = _gameplay.ZoneManager.RankedPlayers;
            for (int i = 0; i < 5; i++)
            {
                if (i < ranked.Count)
                {
                    var p = ranked[i];
                    string crown = i == 0 ? "[KING] " : "";
                    _leaderboardRows[i].text = $"{i + 1}. {crown}{p.DisplayName}  {p.GetCapturedPercent01() * 100f:0.0}%";
                    _leaderboardRows[i].color = p.IsLocalPlayer ? Color.yellow : Color.white;
                }
                else
                {
                    _leaderboardRows[i].text = "";
                }
            }

            // Power charge bar
            if (_characters != null)
            {
                _powerChargeFill.fillAmount = _characters.LocalChargeNormalized;
                var cfg = _characters.GetSelectedCharacter();
                _powerLabel.text = _characters.IsLocalPowerReady
                    ? $"[SPACE] {cfg.PowerTypes[0]}!"
                    : $"{cfg.PowerTypes[0]} ({Mathf.RoundToInt(_characters.LocalChargeNormalized * 100f)}%)";
            }

            // Minimap periodic redraw (5 Hz)
            _minimapRefreshTimer += Time.deltaTime;
            if (_minimapRefreshTimer >= 0.2f)
            {
                _minimapRefreshTimer = 0f;
                RedrawMinimap();
            }

            // Animate flying popups
            UpdateFlyingTexts(Time.deltaTime);
        }

        private void RedrawMinimap()
        {
            if (_minimapTex == null || _gameplay == null) return;
            var border = _gameplay.CollisionWorld.Border;
            float r = Mathf.Max(1f, border.Radius);
            var bg = new Color32(22, 28, 42, 255);
            var inside = new Color32(232, 236, 245, 255);
            var players = _gameplay.ZoneManager.Players;

            // Render exact Circular or Custom Country/Chapter Polygon silhouette + real painted territories!
            for (int y = 0; y < 64; y++)
            {
                float wy = ((y / 63f) * 2f - 1f) * r;
                for (int x = 0; x < 64; x++)
                {
                    float wx = ((x / 63f) * 2f - 1f) * r;
                    Vector2 pt = new Vector2(wx, wy);
                    if (border.IsOutside(pt))
                    {
                        _minimapPixels[y * 64 + x] = bg;
                        continue;
                    }

                    Color32 cellColor = inside;
                    for (int pIdx = 0; pIdx < players.Count; pIdx++)
                    {
                        var p = players[pIdx];
                        if (p == null || !p.IsAlive || p.OwnedZone == null) continue;
                        if (p.OwnedZone.IsPointInside(pt))
                        {
                            cellColor = (Color32)p.ZoneColor;
                            break;
                        }
                    }
                    _minimapPixels[y * 64 + x] = cellColor;
                }
            }

            // Overlay bright player head blips on top of territories
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || !p.IsAlive) continue;
                int px = Mathf.Clamp(Mathf.RoundToInt(((p.Position2D.x / r) * 0.5f + 0.5f) * 63f), 2, 61);
                int py = Mathf.Clamp(Mathf.RoundToInt(((p.Position2D.y / r) * 0.5f + 0.5f) * 63f), 2, 61);
                Color32 c = p.IsLocalPlayer ? new Color32(255, 240, 40, 255) : new Color32(255, 255, 255, 255);
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                        _minimapPixels[(py + dy) * 64 + (px + dx)] = c;
            }

            _minimapTex.SetPixels32(_minimapPixels);
            _minimapTex.Apply(false);
        }

        private void UpdateFlyingTexts(float dt)
        {
            Camera cam = _gameplay != null ? _gameplay.GameplayCamera : Camera.main;
            for (int i = _flyingItems.Count - 1; i >= 0; i--)
            {
                var item = _flyingItems[i];
                item.Elapsed += dt;
                if (item.Elapsed >= item.Duration)
                {
                    Destroy(item.Label.gameObject);
                    _flyingItems.RemoveAt(i);
                    continue;
                }
                float t = item.Elapsed / item.Duration;
                if (cam != null)
                {
                    Vector3 sp = cam.WorldToScreenPoint(item.WorldPos + Vector3.up * (t * 2.2f));
                    item.Label.rectTransform.position = sp;
                }
                var c = item.Label.color;
                c.a = 1f - t;
                item.Label.color = c;
            }
        }

        private void OnDestroy()
        {
            EventBus.Shared.Unsubscribe<FlyingTextSignal>(OnFlyingText);
            if (_minimapTex != null) Destroy(_minimapTex);
        }
    }

    /// <summary>
    /// Reconstructed from BattleMaps.Core.Revive.RevivePopupView (34 methods).
    /// </summary>
    public class RevivePopupScreen : UIScreenBase
    {
        public override UIScreenId ScreenId => UIScreenId.RevivePopup;
        public override bool IsPopup => true;

        private GameplayController _gameplay;
        private Text _countdownTxt;
        private float _timer;

        public void Bind(GameplayController gp) => _gameplay = gp;

        protected override void BuildUI(RectTransform root)
        {
            var dim = UIBuilder.CreatePanel("Dim", root, new Color(0f, 0f, 0f, 0.65f));
            UIBuilder.StretchFull(dim.rectTransform);

            var card = UIBuilder.CreatePanel("Card", root, new Color(0.14f, 0.17f, 0.25f, 0.98f));
            card.rectTransform.anchorMin = new Vector2(0.15f, 0.34f);
            card.rectTransform.anchorMax = new Vector2(0.85f, 0.66f);
            card.rectTransform.offsetMin = card.rectTransform.offsetMax = Vector2.zero;

            var title = UIBuilder.CreateText("Title", card.transform, "SECOND CHANCE!", 44, TextAnchor.MiddleCenter, Color.yellow);
            title.rectTransform.anchorMin = new Vector2(0.05f, 0.72f);
            title.rectTransform.anchorMax = new Vector2(0.95f, 0.94f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;

            _countdownTxt = UIBuilder.CreateText("Count", card.transform, "5", 72, TextAnchor.MiddleCenter, Color.white);
            _countdownTxt.rectTransform.anchorMin = new Vector2(0.2f, 0.42f);
            _countdownTxt.rectTransform.anchorMax = new Vector2(0.8f, 0.72f);
            _countdownTxt.rectTransform.offsetMin = _countdownTxt.rectTransform.offsetMax = Vector2.zero;

            var revBtn = UIBuilder.CreateButton("ReviveBtn", card.transform, "REVIVE NOW", new Color(0.16f, 0.80f, 0.45f), () =>
            {
                Router.CloseTopPopup();
                _gameplay?.RevivePlayer();
            }, 34);
            revBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.12f, 0.20f);
            revBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.88f, 0.38f);
            revBtn.GetComponent<RectTransform>().offsetMin = revBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            var skipBtn = UIBuilder.CreateButton("SkipBtn", card.transform, "No Thanks", new Color(0.32f, 0.36f, 0.44f), () =>
            {
                Router.CloseTopPopup();
                Router.ShowScreen(UIScreenId.LevelEnd);
            }, 26);
            skipBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.28f, 0.05f);
            skipBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.72f, 0.16f);
            skipBtn.GetComponent<RectTransform>().offsetMin = skipBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        }

        public override void OnShow()
        {
            _timer = 5.0f;
            base.OnShow();
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_countdownTxt != null) _countdownTxt.text = Mathf.CeilToInt(Mathf.Max(0f, _timer)).ToString();
            if (_timer <= 0f)
            {
                Router.CloseTopPopup();
                Router.ShowScreen(UIScreenId.LevelEnd);
            }
        }
    }

    /// <summary>
    /// Reconstructed from BattleMaps.LevelEnd.View.LevelEndScreenView (98 methods) & TeamBattleResultsScreen (15 methods).
    /// </summary>
    public class LevelEndScreen : UIScreenBase
    {
        public override UIScreenId ScreenId => UIScreenId.LevelEnd;

        private Text _headerTxt;
        private Text _statsTxt;
        private Text _coinsTxt;
        private Image _progressFill;
        private Text _progressLbl;
        private System.Action _onPlayAgain;

        public void Configure(bool won, float pct01, int kills, int coins, System.Action onPlayAgain)
        {
            _onPlayAgain = onPlayAgain;
            if (_headerTxt != null)
            {
                _headerTxt.text = won ? "VICTORY!" : "GAME OVER";
                _headerTxt.color = won ? new Color(0.25f, 0.95f, 0.55f) : new Color(1f, 0.35f, 0.35f);
            }
            if (_statsTxt != null) _statsTxt.text = $"Captured: {pct01 * 100f:0.0}%   |   Kills: {kills}";
            if (_coinsTxt != null) _coinsTxt.text = $"+{coins} COINS EARNED";
            if (_progressFill != null) _progressFill.fillAmount = pct01;
            if (_progressLbl != null) _progressLbl.text = $"Territory {pct01 * 100f:0.0}%";
        }

        protected override void BuildUI(RectTransform root)
        {
            var bg = UIBuilder.CreatePanel("Bg", root, new Color(0.08f, 0.11f, 0.18f, 0.94f));
            UIBuilder.StretchFull(bg.rectTransform);

            _headerTxt = UIBuilder.CreateText("Header", root, "GAME OVER", 68);
            _headerTxt.rectTransform.anchorMin = new Vector2(0.08f, 0.78f);
            _headerTxt.rectTransform.anchorMax = new Vector2(0.92f, 0.90f);
            _headerTxt.rectTransform.offsetMin = _headerTxt.rectTransform.offsetMax = Vector2.zero;

            var bar = UIBuilder.CreateProgressBar("EndProgress", root, new Color(0.18f, 0.22f, 0.32f), new Color(0.18f, 0.78f, 0.50f), "0%");
            bar.root.anchorMin = new Vector2(0.12f, 0.64f);
            bar.root.anchorMax = new Vector2(0.88f, 0.71f);
            bar.root.offsetMin = bar.root.offsetMax = Vector2.zero;
            _progressFill = bar.fill;
            _progressLbl = bar.label;

            _statsTxt = UIBuilder.CreateText("Stats", root, "Captured: 0% | Kills: 0", 36);
            _statsTxt.rectTransform.anchorMin = new Vector2(0.1f, 0.54f);
            _statsTxt.rectTransform.anchorMax = new Vector2(0.9f, 0.62f);
            _statsTxt.rectTransform.offsetMin = _statsTxt.rectTransform.offsetMax = Vector2.zero;

            _coinsTxt = UIBuilder.CreateText("Coins", root, "+0 COINS", 40, TextAnchor.MiddleCenter, Color.yellow);
            _coinsTxt.rectTransform.anchorMin = new Vector2(0.1f, 0.45f);
            _coinsTxt.rectTransform.anchorMax = new Vector2(0.9f, 0.53f);
            _coinsTxt.rectTransform.offsetMin = _coinsTxt.rectTransform.offsetMax = Vector2.zero;

            var bonusBtn = UIBuilder.CreateButton("BonusGameBtn", root, "PLAY BONUS MINI-GAME", new Color(0.92f, 0.60f, 0.12f), () => Router.ShowScreen(UIScreenId.BonusGame), 32);
            bonusBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.16f, 0.32f);
            bonusBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.84f, 0.41f);
            bonusBtn.GetComponent<RectTransform>().offsetMin = bonusBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            var againBtn = UIBuilder.CreateButton("PlayAgainBtn", root, "PLAY AGAIN", new Color(0.16f, 0.80f, 0.45f), () => _onPlayAgain?.Invoke(), 38);
            againBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.16f, 0.20f);
            againBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.84f, 0.29f);
            againBtn.GetComponent<RectTransform>().offsetMin = againBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            var homeBtn = UIBuilder.CreateButton("HomeBtn", root, "HOME MENU", new Color(0.24f, 0.30f, 0.42f), () => Router.ShowScreen(UIScreenId.Home), 30);
            homeBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.24f, 0.09f);
            homeBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.76f, 0.17f);
            homeBtn.GetComponent<RectTransform>().offsetMin = homeBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        }
    }

    /// <summary>
    /// Reconstructed from BattleMaps.Gameplay.PauseController popup.
    /// </summary>
    public class PausePopupScreen : UIScreenBase
    {
        public override UIScreenId ScreenId => UIScreenId.PausePopup;
        public override bool IsPopup => true;

        private GameplayController _gameplay;
        public void Bind(GameplayController gp) => _gameplay = gp;

        protected override void BuildUI(RectTransform root)
        {
            var dim = UIBuilder.CreatePanel("Dim", root, new Color(0f, 0f, 0f, 0.65f));
            UIBuilder.StretchFull(dim.rectTransform);

            var card = UIBuilder.CreatePanel("Card", root, new Color(0.14f, 0.17f, 0.25f, 0.98f));
            card.rectTransform.anchorMin = new Vector2(0.18f, 0.33f);
            card.rectTransform.anchorMax = new Vector2(0.82f, 0.67f);
            card.rectTransform.offsetMin = card.rectTransform.offsetMax = Vector2.zero;

            var title = UIBuilder.CreateText("Title", card.transform, "PAUSED", 48);
            title.rectTransform.anchorMin = new Vector2(0.1f, 0.74f);
            title.rectTransform.anchorMax = new Vector2(0.9f, 0.94f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;

            var resume = UIBuilder.CreateButton("Resume", card.transform, "RESUME", new Color(0.16f, 0.80f, 0.45f), () =>
            {
                Router.CloseTopPopup();
                _gameplay?.ResumeGame();
            }, 34);
            resume.GetComponent<RectTransform>().anchorMin = new Vector2(0.14f, 0.48f);
            resume.GetComponent<RectTransform>().anchorMax = new Vector2(0.86f, 0.66f);
            resume.GetComponent<RectTransform>().offsetMin = resume.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            var settings = UIBuilder.CreateButton("Settings", card.transform, "SETTINGS", new Color(0.25f, 0.46f, 0.82f), () => Router.OpenPopup(UIScreenId.SettingsPopup), 30);
            settings.GetComponent<RectTransform>().anchorMin = new Vector2(0.14f, 0.28f);
            settings.GetComponent<RectTransform>().anchorMax = new Vector2(0.86f, 0.43f);
            settings.GetComponent<RectTransform>().offsetMin = settings.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            var quit = UIBuilder.CreateButton("Quit", card.transform, "QUIT TO HOME", new Color(0.82f, 0.25f, 0.28f), () =>
            {
                _gameplay?.FinishGame(false);
                Router.CloseAllPopups();
                Router.ShowScreen(UIScreenId.Home);
            }, 30);
            quit.GetComponent<RectTransform>().anchorMin = new Vector2(0.14f, 0.08f);
            quit.GetComponent<RectTransform>().anchorMax = new Vector2(0.86f, 0.23f);
            quit.GetComponent<RectTransform>().offsetMin = quit.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        }
    }
}
