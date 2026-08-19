using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// UI de turno, pistas de Jenga, aviso de tracking y movimientos inválidos.
/// </summary>
public class UIManager : MonoBehaviour
{
    public TMP_Text turnText;
    public GameObject gameOverPanel;
    public TMP_Text gameOverText;

    TMP_Text hintText;
    TMP_Text trackingText;
    TMP_Text invalidText;
    Coroutine invalidRoutine;

    void Start()
    {
        EnsureOverlays();

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnInvalidMove.AddListener(ShowInvalidMove);
            UpdateTurnText(TurnManager.Instance.CurrentPlayer);
        }

        if (ARTrackingGate.Instance != null)
        {
            ARTrackingGate.Instance.OnStabilityChanged += OnTrackingChanged;
            OnTrackingChanged(ARTrackingGate.Instance.IsStable);
        }
        else
        {
            OnTrackingChanged(false);
        }
    }

    void OnDestroy()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnInvalidMove.RemoveListener(ShowInvalidMove);

        if (ARTrackingGate.Instance != null)
            ARTrackingGate.Instance.OnStabilityChanged -= OnTrackingChanged;
    }

    public void UpdateTurnText(string playerName)
    {
        if (turnText == null) return;

        if (TurnManager.Instance != null)
            turnText.text = TurnManager.Instance.StatusLine;
        else
            turnText.text = $"Turno: {playerName}";
    }

    public void ShowGameOver(string message)
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (gameOverText != null) gameOverText.text = message;
        if (hintText != null) hintText.gameObject.SetActive(false);
        if (invalidText != null) invalidText.gameObject.SetActive(false);
    }

    public void ShowInvalidMove(string reason)
    {
        if (invalidText == null) return;
        if (invalidRoutine != null) StopCoroutine(invalidRoutine);
        invalidRoutine = StartCoroutine(ShowInvalidRoutine(reason));
    }

    public void RestartGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    void OnTrackingChanged(bool stable)
    {
        if (trackingText != null)
        {
            trackingText.gameObject.SetActive(!stable);
            trackingText.text = "Apunta a la imagen impresa para jugar.\nLa torre solo se puede tocar con seguimiento estable.";
        }

        if (hintText != null)
        {
            bool playing = stable && (TurnManager.Instance == null || !TurnManager.Instance.IsGameOver);
            hintText.gameObject.SetActive(playing);
            if (playing)
            {
                hintText.text = TurnManager.Instance != null && TurnManager.Instance.Phase == TurnManager.TurnPhase.Place
                    ? "Arrastra el bloque a una ranura verde de la cima y suéltalo."
                    : "Toca un bloque (no el de arriba), sácalo y luego colócalo en la cima.";
            }
        }

        UpdateTurnText(TurnManager.Instance != null ? TurnManager.Instance.CurrentPlayer : "");
    }

    IEnumerator ShowInvalidRoutine(string reason)
    {
        invalidText.text = reason;
        invalidText.gameObject.SetActive(true);
        yield return new WaitForSeconds(2.4f);
        if (invalidText != null) invalidText.gameObject.SetActive(false);
        invalidRoutine = null;
    }

    void Update()
    {
        if (hintText == null || TurnManager.Instance == null) return;
        if (ARTrackingGate.Instance != null && !ARTrackingGate.Instance.IsStable) return;
        if (TurnManager.Instance.IsGameOver) return;

        hintText.gameObject.SetActive(true);
        hintText.text = TurnManager.Instance.Phase == TurnManager.TurnPhase.Place
            ? "Arrastra el bloque a una ranura verde de la cima y suéltalo."
            : "Toca un bloque (no el de arriba), sácalo y luego colócalo en la cima.";
    }

    void EnsureOverlays()
    {
        Canvas canvas = turnText != null
            ? turnText.GetComponentInParent<Canvas>()
            : FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        trackingText = CreateLabel(canvas.transform, "TrackingWarning", new Vector2(0f, 220f), 36, Color.white);
        hintText = CreateLabel(canvas.transform, "HintText", new Vector2(0f, -280f), 28, new Color(1f, 0.95f, 0.75f));
        invalidText = CreateLabel(canvas.transform, "InvalidMoveText", new Vector2(0f, 80f), 32, new Color(1f, 0.45f, 0.35f));

        trackingText.gameObject.SetActive(false);
        hintText.gameObject.SetActive(false);
        invalidText.gameObject.SetActive(false);
    }

    static TMP_Text CreateLabel(Transform parent, string name, Vector2 anchoredPos, int fontSize, Color color)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            TMP_Text existingText = existing.GetComponent<TMP_Text>();
            if (existingText != null) return existingText;
        }

        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.08f, 0.5f);
        rect.anchorMax = new Vector2(0.92f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(0f, 120f);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = Color.black;
        return tmp;
    }
}
