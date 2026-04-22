using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{
    const float SpatialMinDistance = 1.5f;
    const float SpatialMaxDistance = 18f;

    static AudioManager instance;

    AudioClip laserClip;
    AudioClip corsairLaserClip;
    AudioClip drillingClip;
    AudioClip clickClip;
    AudioClip engineClip;
    AudioClip fusionEngineClip;
    AudioClip alarmClip;
    AudioClip explosionClip;
    AudioClip reloadClip;
    AudioClip shieldHitClip;
    AudioClip hpHitClip;
    AudioClip evacBuzzerClip;
    AudioClip extractionSequenceClip;
    AudioClip spaceMineBoomClip;

    AudioSource oneShotSource;
    AudioSource drillingLoopSource;
    AudioSource alarmLoopSource;
    Coroutine evacBuzzerRoutine;
    readonly HashSet<int> hookedButtons = new HashSet<int>();

    public static AudioManager Instance
    {
        get
        {
            EnsureInstance();
            return instance;
        }
    }

    public AudioClip EngineClip => engineClip;
    public AudioClip FusionEngineClip => fusionEngineClip != null ? fusionEngineClip : engineClip;
    public AudioClip DrillingClip => drillingClip;
    public AudioClip AlarmClip => alarmClip;
    public AudioClip CorsairLaserClip => corsairLaserClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

    static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("AudioManager");
        instance = root.AddComponent<AudioManager>();
        DontDestroyOnLoad(root);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        LoadClips();
        EnsureSources();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (instance == this)
            instance = null;
    }

    void Start()
    {
        HookSceneButtons();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HookSceneButtons();
        StopDrillingLoop();
        StopAlarmLoop();
    }

    void LoadClips()
    {
        laserClip = Resources.Load<AudioClip>("Audio/strzal_pistol_cut");
        corsairLaserClip = Resources.Load<AudioClip>("Audio/laser_classic_corsair");
        drillingClip = Resources.Load<AudioClip>("Audio/drilling");
        clickClip = Resources.Load<AudioClip>("Audio/click");
        engineClip = Resources.Load<AudioClip>("Audio/silnik");
        fusionEngineClip = Resources.Load<AudioClip>("Audio/fusion_engine_sound");
        alarmClip = Resources.Load<AudioClip>("Audio/alarm");
        explosionClip = Resources.Load<AudioClip>("Audio/explosion");
        reloadClip = Resources.Load<AudioClip>("Audio/gun_reload");
        shieldHitClip = Resources.Load<AudioClip>("Audio/trafienie_w_tarcze");
        hpHitClip = Resources.Load<AudioClip>("Audio/trafienie_w_HP");
        evacBuzzerClip = Resources.Load<AudioClip>("Audio/evac_buzzer_sound");
        extractionSequenceClip = Resources.Load<AudioClip>("Audio/extraction_4sekundy");
        spaceMineBoomClip = Resources.Load<AudioClip>("Audio/space_mine_boom_sound");
    }

    void EnsureSources()
    {
        oneShotSource = CreateChildSource("OneShotSource", false, false, 0.9f);
        drillingLoopSource = CreateChildSource("DrillingLoopSource", true, false, 0.455f);
        alarmLoopSource = CreateChildSource("AlarmLoopSource", true, false, 0.55f);

        drillingLoopSource.clip = drillingClip;
        alarmLoopSource.clip = alarmClip;
    }

    AudioSource CreateChildSource(string name, bool loop, bool playOnAwake, float volume)
    {
        Transform existing = transform.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        go.transform.SetParent(transform, false);

        AudioSource source = go.GetComponent<AudioSource>();
        if (source == null)
            source = go.AddComponent<AudioSource>();

        source.loop = loop;
        source.playOnAwake = playOnAwake;
        source.spatialBlend = 0f;
        source.volume = volume;
        return source;
    }

    void HookSceneButtons()
    {
        foreach (Button button in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (button == null || !button.gameObject.scene.IsValid())
                continue;

            int id = button.GetHashCode();
            if (hookedButtons.Contains(id))
                continue;

            if (button.GetComponent<ButtonClickSoundHook>() == null)
            {
                button.gameObject.AddComponent<ButtonClickSoundHook>();
            }

            hookedButtons.Add(id);
        }
    }

    public void PlayClick()
    {
        PlayOneShot(clickClip, 0.8f);
    }

    public void PlayLaser()
    {
        PlayOneShot(laserClip, 0.55f);
    }

    public void PlayLaserAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(laserClip, worldPosition, 0.55f);
    }

    public void PlayCorsairLaserAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(corsairLaserClip != null ? corsairLaserClip : laserClip, worldPosition, 0.66f);
    }

    public void PlayExplosion()
    {
        PlayOneShot(explosionClip, 0.75f);
    }

    public void PlayExplosionAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(explosionClip, worldPosition, 0.75f);
    }

    public void PlayReloadAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(reloadClip, worldPosition, 0.62f);
    }

    public void PlayShieldHitAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(shieldHitClip, worldPosition, 0.62f);
    }

    public void PlayHpHitAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(hpHitClip, worldPosition, 0.72f);
    }

    public void PlayExtractionSequenceAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(extractionSequenceClip, worldPosition, 0.88f);
    }

    public void PlaySpaceMineBoomAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(spaceMineBoomClip != null ? spaceMineBoomClip : explosionClip, worldPosition, 0.92f);
    }

    public void PlayEvacBuzzerBurst()
    {
        if (evacBuzzerClip == null)
            return;

        StartCoroutine(PlayEvacBuzzerBurstRoutine());
    }

    public void PlayEvacBuzzerLoopForDuration(float duration)
    {
        if (evacBuzzerClip == null || duration <= 0f)
            return;

        StopEvacBuzzerLoop();
        evacBuzzerRoutine = StartCoroutine(PlayEvacBuzzerLoopRoutine(duration));
    }

    public void StopEvacBuzzerLoop()
    {
        if (evacBuzzerRoutine != null)
        {
            StopCoroutine(evacBuzzerRoutine);
            evacBuzzerRoutine = null;
        }
    }

    public void StartDrillingLoop()
    {
        if (drillingClip == null || drillingLoopSource == null)
            return;

        if (drillingLoopSource.clip != drillingClip)
            drillingLoopSource.clip = drillingClip;

        if (!drillingLoopSource.isPlaying)
            drillingLoopSource.Play();
    }

    public void StopDrillingLoop()
    {
        if (drillingLoopSource != null && drillingLoopSource.isPlaying)
            drillingLoopSource.Stop();
    }

    public void StartAlarmLoop()
    {
        if (alarmClip == null || alarmLoopSource == null)
            return;

        if (alarmLoopSource.clip != alarmClip)
            alarmLoopSource.clip = alarmClip;

        if (!alarmLoopSource.isPlaying)
            alarmLoopSource.Play();
    }

    public void StopAlarmLoop()
    {
        if (alarmLoopSource != null && alarmLoopSource.isPlaying)
            alarmLoopSource.Stop();
    }

    IEnumerator PlayEvacBuzzerBurstRoutine()
    {
        for (int i = 0; i < 5; i++)
        {
            PlayOneShot(evacBuzzerClip, 0.95f);
            float waitTime = evacBuzzerClip != null ? Mathf.Max(0.45f, evacBuzzerClip.length + 0.06f) : 0.5f;
            yield return new WaitForSeconds(waitTime);
        }
    }

    IEnumerator PlayEvacBuzzerLoopRoutine(float duration)
    {
        float elapsed = 0f;
        float waitTime = evacBuzzerClip != null ? Mathf.Max(0.45f, evacBuzzerClip.length + 0.06f) : 0.5f;

        while (elapsed < duration)
        {
            PlayOneShot(evacBuzzerClip, 0.95f);
            yield return new WaitForSeconds(waitTime);
            elapsed += waitTime;
        }

        evacBuzzerRoutine = null;
    }

    void PlayOneShot(AudioClip clip, float volumeScale)
    {
        if (clip == null || oneShotSource == null)
            return;

        oneShotSource.PlayOneShot(clip, volumeScale);
    }

    void PlaySpatialOneShot(AudioClip clip, Vector3 worldPosition, float volumeScale)
    {
        if (clip == null)
            return;

        GameObject tempObject = new GameObject("SpatialAudio_" + clip.name);
        tempObject.transform.position = worldPosition;

        AudioSource source = tempObject.AddComponent<AudioSource>();
        ConfigureSpatialSource(source, volumeScale);
        source.clip = clip;
        source.loop = false;
        source.playOnAwake = false;
        source.Play();

        Destroy(tempObject, clip.length + 0.1f);
    }

    public void ConfigureSpatialSource(AudioSource source, float volume)
    {
        if (source == null)
            return;

        source.loop = false;
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = SpatialMinDistance;
        source.maxDistance = SpatialMaxDistance;
        source.dopplerLevel = 0f;
        source.spread = 0f;
        source.volume = volume;
    }
}
