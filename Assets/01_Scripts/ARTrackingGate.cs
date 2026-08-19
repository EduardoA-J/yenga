using UnityEngine;
using Vuforia;

/// <summary>
/// La manipulación solo se habilita con seguimiento AR estable
/// (TRACKED o EXTENDED_TRACKED sobre el Image Target).
/// </summary>
public class ARTrackingGate : MonoBehaviour
{
    public static ARTrackingGate Instance { get; private set; }

    public bool IsStable { get; private set; }
    public event System.Action<bool> OnStabilityChanged;

    ObserverBehaviour observer;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        IsStable = false;
    }

    void Start()
    {
        BindObserver();
    }

    void BindObserver()
    {
        if (observer != null) return;

        GameObject target = GameObject.Find("ImageTarget");
        if (target == null) return;

        observer = target.GetComponent<ObserverBehaviour>();
        if (observer == null) return;

        observer.OnTargetStatusChanged += HandleStatusChanged;
        HandleStatusChanged(observer, observer.TargetStatus);
    }

    void OnDestroy()
    {
        if (observer != null)
            observer.OnTargetStatusChanged -= HandleStatusChanged;

        if (Instance == this)
            Instance = null;
    }

    void HandleStatusChanged(ObserverBehaviour behaviour, TargetStatus status)
    {
        bool stable = status.Status == Status.TRACKED
                      || status.Status == Status.EXTENDED_TRACKED;

        if (stable == IsStable) return;

        IsStable = stable;
        OnStabilityChanged?.Invoke(IsStable);
    }
}
