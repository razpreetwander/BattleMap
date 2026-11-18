using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameOps.UIFramework
{
    public enum UIScreenId
    {
        Home,
        GameplayHud,
        RevivePopup,
        LevelEnd,
        CharacterSelection,
        ShopAndChests,
        WorldMap,
        Chapters,
        PartyLobby,
        Missions,
        XPRoad,
        BonusGame,
        Events,
        SettingsPopup,
        PausePopup
    }

    /// <summary>
    /// Base class for all procedural A-to-Z screens & popups (GameOps.UIFramework.UIPopupView / ScreenView).
    /// </summary>
    public abstract class UIScreenBase : MonoBehaviour
    {
        public abstract UIScreenId ScreenId { get; }
        public virtual bool IsPopup => false;

        protected RectTransform RootRect { get; private set; }
        protected UIScreenRouter Router { get; private set; }
        private bool _built;

        public void Initialize(UIScreenRouter router)
        {
            Router = router;
            RootRect = gameObject.GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            UIBuilder.StretchFull(RootRect);
            if (!_built)
            {
                BuildUI(RootRect);
                _built = true;
            }
            gameObject.SetActive(false);
        }

        protected abstract void BuildUI(RectTransform root);

        public virtual void OnShow()
        {
            gameObject.SetActive(true);
            Refresh();
        }

        public virtual void OnHide()
        {
            gameObject.SetActive(false);
        }

        public virtual void Refresh() { }
    }

    /// <summary>
    /// Central screen & popup router managing all 15 A-to-Z UI screens on one responsive Canvas.
    /// </summary>
    public class UIScreenRouter : MonoBehaviour
    {
        public Canvas MainCanvas { get; private set; }
        public RectTransform ScreenLayer { get; private set; }
        public RectTransform PopupLayer { get; private set; }

        private readonly Dictionary<UIScreenId, UIScreenBase> _screens = new();
        private UIScreenBase _activeScreen;
        private readonly Stack<UIScreenBase> _activePopups = new();

        public UIScreenId CurrentScreenId => _activeScreen != null ? _activeScreen.ScreenId : UIScreenId.Home;

        public void EnsureCanvas()
        {
            if (MainCanvas != null) return;

            var canvasGo = new GameObject("BattleMaps_UICanvas");
            canvasGo.transform.SetParent(transform, false);
            MainCanvas = canvasGo.AddComponent<Canvas>();
            MainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            MainCanvas.sortingOrder = 10;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            ScreenLayer = UIBuilder.CreateContainer("ScreenLayer", canvasGo.transform);
            UIBuilder.StretchFull(ScreenLayer);

            PopupLayer = UIBuilder.CreateContainer("PopupLayer", canvasGo.transform);
            UIBuilder.StretchFull(PopupLayer);
        }

        public T RegisterScreen<T>() where T : UIScreenBase
        {
            EnsureCanvas();
            var go = new GameObject(typeof(T).Name);
            var screen = go.AddComponent<T>();
            go.transform.SetParent(screen.IsPopup ? PopupLayer : ScreenLayer, false);
            screen.Initialize(this);
            _screens[screen.ScreenId] = screen;
            return screen;
        }

        public T GetScreen<T>(UIScreenId id) where T : UIScreenBase
            => _screens.TryGetValue(id, out var s) ? s as T : null;

        public void ShowScreen(UIScreenId id)
        {
            CloseAllPopups();
            if (_activeScreen != null) _activeScreen.OnHide();
            if (_screens.TryGetValue(id, out var next))
            {
                _activeScreen = next;
                _activeScreen.OnShow();
            }
        }

        public void OpenPopup(UIScreenId id)
        {
            if (!_screens.TryGetValue(id, out var popup)) return;
            popup.transform.SetAsLastSibling();
            popup.OnShow();
            _activePopups.Push(popup);
        }

        public void CloseTopPopup()
        {
            if (_activePopups.Count == 0) return;
            var top = _activePopups.Pop();
            top.OnHide();
            _activeScreen?.Refresh();
        }

        public void CloseAllPopups()
        {
            while (_activePopups.Count > 0)
                _activePopups.Pop().OnHide();
        }
    }

    /// <summary>
    /// Zero-asset procedural Unity UGUI widget factory (works out-of-the-box in Unity 6 without external prefabs or TMP).
    /// </summary>
    public static class UIBuilder
    {
        private static Font _cachedFont;

        public static Font DefaultFont
        {
            get
            {
                if (_cachedFont == null)
                {
                    _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (_cachedFont == null)
                        _cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                return _cachedFont;
            }
        }

        public static RectTransform CreateContainer(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        public static void StretchFull(RectTransform rt, float pad = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
        }

        public static Image CreatePanel(string name, Transform parent, Color color)
        {
            var rt = CreateContainer(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text CreateText(
            string name,
            Transform parent,
            string text,
            int fontSize = 32,
            TextAnchor align = TextAnchor.MiddleCenter,
            Color? color = null,
            FontStyle style = FontStyle.Bold)
        {
            var rt = CreateContainer(name, parent);
            var txt = rt.gameObject.AddComponent<Text>();
            txt.font = DefaultFont;
            txt.text = text;
            txt.fontSize = fontSize;
            txt.alignment = align;
            txt.fontStyle = style;
            txt.color = color ?? Color.white;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        public static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Color bgColor,
            Action onClick,
            int fontSize = 30,
            Color? textColor = null)
        {
            var img = CreatePanel(name, parent, bgColor);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = Color.Lerp(bgColor, Color.white, 0.15f);
            colors.pressedColor = Color.Lerp(bgColor, Color.black, 0.18f);
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var txt = CreateText("Label", img.transform, label, fontSize, TextAnchor.MiddleCenter, textColor ?? Color.white);
            StretchFull(txt.rectTransform, 8f);
            return btn;
        }

        public static (RectTransform root, Image fill, Text label) CreateProgressBar(
            string name,
            Transform parent,
            Color bgColor,
            Color fillColor,
            string initialLabel = "")
        {
            var bg = CreatePanel(name, parent, bgColor);
            var fillImg = CreatePanel("Fill", bg.transform, fillColor);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = 0;
            fillImg.fillAmount = 0f;
            StretchFull(fillImg.rectTransform, 4f);

            var txt = CreateText("Text", bg.transform, initialLabel, 24, TextAnchor.MiddleCenter, Color.white);
            StretchFull(txt.rectTransform, 4f);
            return (bg.rectTransform, fillImg, txt);
        }

        public static RectTransform CreateScrollList(string name, Transform parent, float spacing = 14f)
        {
            var bg = CreatePanel(name, parent, new Color(0f, 0f, 0f, 0.14f));
            var scroll = bg.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;

            var viewport = CreatePanel("Viewport", bg.transform, new Color(1f, 1f, 1f, 0.01f));
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            StretchFull(viewport.rectTransform, 8f);
            scroll.viewport = viewport.rectTransform;

            var content = CreateContainer("Content", viewport.transform);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);

            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.padding = new RectOffset(14, 14, 14, 14);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = content.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = content;
            return content;
        }
    }
}
