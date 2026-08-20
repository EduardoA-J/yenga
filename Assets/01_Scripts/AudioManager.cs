using UnityEngine;

/// <summary>
/// Efectos de sonido del juego (extracción de bloque y derrumbe de la torre).
/// La música de fondo se reproduce por separado en la escena.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Efectos")]
    public AudioClip blockExtractClip;
    public AudioClip towerCollapseClip;

    AudioSource sfxSource;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureSource();
    }

    void Start()
    {
        EnsureSource();
        if (blockExtractClip == null)
            blockExtractClip = Resources.Load<AudioClip>("block");
        if (towerCollapseClip == null)
            towerCollapseClip = Resources.Load<AudioClip>("collapse");
    }

    void EnsureSource()
    {
        if (sfxSource == null)
            sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.mute = false;
        sfxSource.volume = 1f;
        sfxSource.spatialBlend = 0f;
        sfxSource.ignoreListenerPause = true;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void PlayBlockExtract()
    {
        PlaySfx(blockExtractClip);
    }

    public void PlayTowerCollapse()
    {
        PlaySfx(towerCollapseClip);
    }

    void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;
        EnsureSource();
        sfxSource.PlayOneShot(clip, 1f);
    }
}
