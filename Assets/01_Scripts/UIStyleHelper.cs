using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Estilos visuales de la UI del juego. No altera la lógica de mensajes.
/// </summary>
public static class UIStyleHelper
{
    public enum MessageKind
    {
        Turn,
        Hint,
        Tracking,
        Invalid,
        GameOver
    }

    static readonly Color PanelDark = new Color(0.11f, 0.08f, 0.06f, 0.9f);
    static readonly Color PanelHint = new Color(0.08f, 0.07f, 0.06f, 0.78f);
    static readonly Color PanelTracking = new Color(0.18f, 0.11f, 0.04f, 0.92f);
    static readonly Color PanelInvalid = new Color(0.22f, 0.07f, 0.06f, 0.94f);
    static readonly Color PanelGameOver = new Color(0.1f, 0.07f, 0.06f, 0.96f);

    static readonly Color TextCream = new Color(1f, 0.96f, 0.88f, 1f);
    static readonly Color TextGold = new Color(0.95f, 0.78f, 0.35f, 1f);
    static readonly Color TextHint = new Color(0.92f, 0.9f, 0.82f, 0.95f);
    static readonly Color TextTracking = new Color(1f, 0.82f, 0.45f, 1f);
    static readonly Color TextInvalid = new Color(1f, 0.62f, 0.52f, 1f);
    static readonly Color TextGameOver = new Color(1f, 0.93f, 0.82f, 1f);

    static readonly Color OverlayDim = new Color(0.04f, 0.03f, 0.02f, 0.78f);
    static readonly Color ButtonNormal = new Color(0.82f, 0.62f, 0.22f, 1f);
    static readonly Color ButtonHighlighted = new Color(0.95f, 0.78f, 0.38f, 1f);
    static readonly Color ButtonPressed = new Color(0.62f, 0.46f, 0.14f, 1f);

    public static void ConfigureCanvas(CanvasScaler scaler)
    {
        if (scaler == null) return;

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    public static TMP_Text GetOrCreateMessage(Transform canvas, string name, MessageKind kind, Vector2 anchoredPos)
    {
        Transform existing = canvas.Find(name);
        if (existing != null)
        {
            TMP_Text existingText = existing.GetComponentInChildren<TMP_Text>(true);
            if (existingText != null)
            {
                ApplyMessageStyle(existing.gameObject, existingText, kind);
                return existingText;
            }
        }

        return CreateMessage(canvas, name, kind, anchoredPos);
    }

    public static TMP_Text CreateMessage(Transform canvas, string name, MessageKind kind, Vector2 anchoredPos)
    {
        GameObject card = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        card.transform.SetParent(canvas, false);

        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.06f, 0.5f);
        cardRect.anchorMax = new Vector2(0.94f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = anchoredPos;
        cardRect.sizeDelta = GetCardSize(kind);

        Image cardImage = card.GetComponent<Image>();
        cardImage.sprite = GetRoundedSprite();
        cardImage.type = Image.Type.Sliced;
        cardImage.raycastTarget = false;

        GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(card.transform, false);

        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = GetPadding(kind);
        labelRect.offsetMax = -GetPadding(kind);

        TextMeshProUGUI tmp = labelGo.GetComponent<TextMeshProUGUI>();
        ApplyMessageStyle(card, tmp, kind);
        return tmp;
    }

    public static void StyleTurnBanner(TMP_Text turnText)
    {
        if (turnText == null) return;

        RectTransform rect = turnText.rectTransform;
        rect.anchorMin = new Vector2(0.08f, 0f);
        rect.anchorMax = new Vector2(0.92f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 150f);
        rect.sizeDelta = new Vector2(0f, 110f);

        EnsureCardBehind(turnText.transform, "TurnBannerCard", PanelDark, new Vector2(0f, 110f));
        StyleText(turnText, TextCream, 34f, FontStyles.Bold, 0.4f, 6f);
        turnText.enableAutoSizing = true;
        turnText.fontSizeMin = 24f;
        turnText.fontSizeMax = 38f;
        turnText.margin = new Vector4(24f, 14f, 24f, 14f);
    }

    public static void StyleGameOverPanel(GameObject panel, TMP_Text messageText, Button restartButton)
    {
        if (panel != null)
        {
            Image overlay = panel.GetComponent<Image>();
            if (overlay != null)
            {
                overlay.color = OverlayDim;
                overlay.sprite = GetRoundedSprite();
                overlay.type = Image.Type.Simple;
            }

            Transform card = panel.transform.Find("GameOverCard");
            if (card == null)
            {
                GameObject cardGo = new GameObject("GameOverCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                cardGo.transform.SetParent(panel.transform, false);
                cardGo.transform.SetAsFirstSibling();

                RectTransform cardRect = cardGo.GetComponent<RectTransform>();
                cardRect.anchorMin = new Vector2(0.08f, 0.28f);
                cardRect.anchorMax = new Vector2(0.92f, 0.78f);
                cardRect.offsetMin = Vector2.zero;
                cardRect.offsetMax = Vector2.zero;

                Image cardImage = cardGo.GetComponent<Image>();
                cardImage.sprite = GetRoundedSprite();
                cardImage.type = Image.Type.Sliced;
                cardImage.color = PanelGameOver;
                cardImage.raycastTarget = false;
            }
        }

        if (messageText != null)
        {
            StyleText(messageText, TextGameOver, 36f, FontStyles.Bold, 0.35f, 8f);
            messageText.enableAutoSizing = true;
            messageText.fontSizeMin = 26f;
            messageText.fontSizeMax = 40f;
            messageText.margin = new Vector4(28f, 20f, 28f, 20f);

            RectTransform rect = messageText.rectTransform;
            rect.anchorMin = new Vector2(0.1f, 0.42f);
            rect.anchorMax = new Vector2(0.9f, 0.72f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        StyleRestartButton(restartButton);
    }

    static void StyleRestartButton(Button button)
    {
        if (button == null) return;

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = GetRoundedSprite();
            image.type = Image.Type.Sliced;
            image.color = ButtonNormal;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = ButtonNormal;
        colors.highlightedColor = ButtonHighlighted;
        colors.pressedColor = ButtonPressed;
        colors.selectedColor = ButtonHighlighted;
        colors.fadeDuration = 0.12f;
        button.colors = colors;

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.22f, 0.18f);
        rect.anchorMax = new Vector2(0.78f, 0.28f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            StyleText(label, TextCream, 30f, FontStyles.Bold, 0.5f, 0f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 22f;
            label.fontSizeMax = 34f;
        }
    }

    static void ApplyMessageStyle(GameObject card, TMP_Text tmp, MessageKind kind)
    {
        Image image = card.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = GetRoundedSprite();
            image.type = Image.Type.Sliced;
            image.color = GetPanelColor(kind);
        }

        RectTransform cardRect = card.GetComponent<RectTransform>();
        if (cardRect != null)
            cardRect.sizeDelta = GetCardSize(kind);

        switch (kind)
        {
            case MessageKind.Turn:
                StyleText(tmp, TextGold, 32f, FontStyles.Bold, 0.45f, 4f);
                break;
            case MessageKind.Hint:
                StyleText(tmp, TextHint, 26f, FontStyles.Italic, 0.25f, 5f);
                break;
            case MessageKind.Tracking:
                StyleText(tmp, TextTracking, 30f, FontStyles.Bold, 0.35f, 6f);
                break;
            case MessageKind.Invalid:
                StyleText(tmp, TextInvalid, 30f, FontStyles.Bold, 0.4f, 5f);
                break;
            case MessageKind.GameOver:
                StyleText(tmp, TextGameOver, 34f, FontStyles.Bold, 0.35f, 8f);
                break;
        }

        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = kind == MessageKind.Hint ? 20f : 22f;
        tmp.fontSizeMax = kind == MessageKind.Tracking ? 34f : 32f;
        tmp.raycastTarget = false;
    }

    static void StyleText(TMP_Text tmp, Color color, float fontSize, FontStyles style, float outline, float lineSpacing)
    {
        tmp.color = color;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.lineSpacing = lineSpacing;
        tmp.characterSpacing = 0.6f;
        tmp.outlineWidth = outline;
        tmp.outlineColor = new Color(0f, 0f, 0f, 0.85f);

        EnsureShadow(tmp);
    }

    static void EnsureShadow(Graphic graphic)
    {
        Shadow shadow = graphic.GetComponent<Shadow>();
        if (shadow == null)
            shadow = graphic.gameObject.AddComponent<Shadow>();

        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(2f, -2f);
        shadow.useGraphicAlpha = true;
    }

    static void EnsureCardBehind(Transform textTransform, string cardName, Color color, Vector2 size)
    {
        Transform parent = textTransform.parent;
        Transform existing = parent != null ? parent.Find(cardName) : null;
        if (existing != null) return;

        GameObject card = new GameObject(cardName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (parent != null)
        {
            card.transform.SetParent(parent, false);
            card.transform.SetSiblingIndex(textTransform.GetSiblingIndex());
        }

        RectTransform cardRect = card.GetComponent<RectTransform>();
        RectTransform textRect = textTransform as RectTransform;
        if (textRect != null)
        {
            cardRect.anchorMin = textRect.anchorMin;
            cardRect.anchorMax = textRect.anchorMax;
            cardRect.pivot = textRect.pivot;
            cardRect.anchoredPosition = textRect.anchoredPosition;
            cardRect.sizeDelta = size;
        }

        Image image = card.GetComponent<Image>();
        image.sprite = GetRoundedSprite();
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
    }

    static Vector2 GetCardSize(MessageKind kind)
    {
        switch (kind)
        {
            case MessageKind.Tracking: return new Vector2(0f, 150f);
            case MessageKind.Hint: return new Vector2(0f, 120f);
            case MessageKind.Invalid: return new Vector2(0f, 110f);
            default: return new Vector2(0f, 120f);
        }
    }

    static Vector2 GetPadding(MessageKind kind)
    {
        float horizontal = 22f;
        float vertical = kind == MessageKind.Tracking ? 18f : 14f;
        return new Vector2(horizontal, vertical);
    }

    static Color GetPanelColor(MessageKind kind)
    {
        switch (kind)
        {
            case MessageKind.Turn: return PanelDark;
            case MessageKind.Hint: return PanelHint;
            case MessageKind.Tracking: return PanelTracking;
            case MessageKind.Invalid: return PanelInvalid;
            default: return PanelGameOver;
        }
    }

    static Sprite GetRoundedSprite()
    {
        return Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
    }
}
