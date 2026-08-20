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
        sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void PlayBlockExtract()
    {
        if (blockExtractClip != null)
            sfxSource.PlayOneShot(blockExtractClip);
    }

    public void PlayTowerCollapse()
    {
        if (towerCollapseClip != null)
            sfxSource.PlayOneShot(towerCollapseClip);
    }
}
