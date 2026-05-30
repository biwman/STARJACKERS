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
    const float ClickSoundCooldownSeconds = 0.055f;

    static AudioManager instance;

    AudioClip laserClip;
    AudioClip corsairLaserClip;
    AudioClip drillingClip;
    AudioClip clickClip;
    AudioClip cashClip;
    AudioClip engineClip;
    AudioClip fusionEngineClip;
    AudioClip alarmClip;
    AudioClip explosionClip;
    AudioClip reloadClip;
    AudioClip shieldHitClip;
    AudioClip shieldChargeClip;
    AudioClip hpHitClip;
    AudioClip evacBuzzerClip;
    AudioClip extractionSequenceClip;
    AudioClip spaceMineBoomClip;
    AudioClip spaceTruckAlertClip;
    AudioClip mothershipEngineClip;
    AudioClip radarShipEngineClip;
    AudioClip rescueShipEngineClip;
    AudioClip shieldFullPowerClip;
    AudioClip magneticBeamClip;
    AudioClip shootSmallClip;
    AudioClip artilleryGunClip;
    AudioClip gatlingGunClip;
    AudioClip lazer1Clip;
    AudioClip lazer2Clip;
    AudioClip repairBayLandingClip;
    AudioClip repairBayStartingClip;
    AudioClip radarShipShootClip;
    AudioClip radarShipIncomingClip;
    AudioClip pirateFighterShotClip;
    AudioClip beaconSignalClip;
    AudioClip astroCutterClip;
    AudioClip guidanceSystemClip;
    AudioClip spaceDrillDeliveryClip;
    AudioClip spaceMantaWarningClip;
    AudioClip gravitySquidWarningClip;
    AudioClip gravitySquidTetherClip;
    AudioClip hunterLanceLockClip;
    AudioClip hunterLanceFireClip;
    AudioClip rocketLaunchClip;
    AudioClip rocketLockClip;
    AudioClip rocketFlyLoopClip;
    AudioClip rocketExplosionClip;
    AudioClip cosmicWormShotClip;

    AudioSource oneShotSource;
    AudioSource drillingLoopSource;
    AudioSource alarmLoopSource;
    Coroutine evacBuzzerRoutine;
    float lastClickSoundTime = -100f;
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
    public AudioClip MothershipEngineClip => mothershipEngineClip != null ? mothershipEngineClip : engineClip;
    public AudioClip RadarShipEngineClip => radarShipEngineClip != null ? radarShipEngineClip : mothershipEngineClip != null ? mothershipEngineClip : engineClip;
    public AudioClip RescueShipEngineClip => rescueShipEngineClip != null ? rescueShipEngineClip : radarShipEngineClip != null ? radarShipEngineClip : mothershipEngineClip != null ? mothershipEngineClip : engineClip;
    public AudioClip DrillingClip => drillingClip;
    public AudioClip AlarmClip => alarmClip;
    public AudioClip CorsairLaserClip => corsairLaserClip;
    public AudioClip BeaconSignalClip => beaconSignalClip != null ? beaconSignalClip : alarmClip;
    public AudioClip AstroCutterClip => astroCutterClip != null ? astroCutterClip : lazer2Clip != null ? lazer2Clip : laserClip;
    public AudioClip GuidanceSystemClip => guidanceSystemClip != null ? guidanceSystemClip : beaconSignalClip != null ? beaconSignalClip : alarmClip;
    public AudioClip SpaceDrillDeliveryClip => spaceDrillDeliveryClip != null ? spaceDrillDeliveryClip : shieldChargeClip != null ? shieldChargeClip : clickClip;
    public AudioClip GravitySquidTetherClip => gravitySquidTetherClip != null ? gravitySquidTetherClip : magneticBeamClip != null ? magneticBeamClip : shieldChargeClip;
    public AudioClip RocketFlyLoopClip => rocketFlyLoopClip != null ? rocketFlyLoopClip : engineClip;
    public float EvacBuzzerPulseInterval => GetEvacBuzzerPulseInterval();

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
        cashClip = Resources.Load<AudioClip>("Audio/cash_sound");
        engineClip = Resources.Load<AudioClip>("Audio/silnik");
        fusionEngineClip = Resources.Load<AudioClip>("Audio/fusion_engine_sound");
        alarmClip = Resources.Load<AudioClip>("Audio/alarm");
        explosionClip = Resources.Load<AudioClip>("Audio/explosion");
        reloadClip = Resources.Load<AudioClip>("Audio/gun_reload");
        shieldHitClip = Resources.Load<AudioClip>("Audio/trafienie_w_tarcze");
        shieldChargeClip = Resources.Load<AudioClip>("Audio/shield_charge1");
        hpHitClip = Resources.Load<AudioClip>("Audio/trafienie_w_HP");
        evacBuzzerClip = Resources.Load<AudioClip>("Audio/evac_buzzer_sound");
        extractionSequenceClip = Resources.Load<AudioClip>("Audio/extraction_4sekundy");
        spaceMineBoomClip = Resources.Load<AudioClip>("Audio/space_mine_boom_sound");
        spaceTruckAlertClip = Resources.Load<AudioClip>("Audio/alert_3times");
        mothershipEngineClip = Resources.Load<AudioClip>("Audio/mother_ship_sound");
        radarShipEngineClip = Resources.Load<AudioClip>("Audio/radar_ship_sound");
        rescueShipEngineClip = Resources.Load<AudioClip>("Audio/rescue_ship_sound");
        shieldFullPowerClip = Resources.Load<AudioClip>("Audio/Shield At Full Power");
        magneticBeamClip = Resources.Load<AudioClip>("Audio/magnetic_beam_sound");
        shootSmallClip = Resources.Load<AudioClip>("Audio/shoot_small");
        artilleryGunClip = Resources.Load<AudioClip>("Audio/artillery_gun_sound");
        gatlingGunClip = Resources.Load<AudioClip>("Audio/gatling_gun_sound");
        lazer1Clip = Resources.Load<AudioClip>("Audio/lazer1");
        lazer2Clip = Resources.Load<AudioClip>("Audio/lazer2");
        repairBayLandingClip = Resources.Load<AudioClip>("Audio/stacja_naprawcza_landing_sound");
        repairBayStartingClip = Resources.Load<AudioClip>("Audio/stacja_naprawcza_starting_sound");
        radarShipShootClip = Resources.Load<AudioClip>("Audio/radar_ship_shoot_sound");
        radarShipIncomingClip = Resources.Load<AudioClip>("Audio/radar_shop_incoming");
        pirateFighterShotClip = Resources.Load<AudioClip>("Audio/pirate_fighter_shot_sound");
        beaconSignalClip = Resources.Load<AudioClip>("Audio/beacon_signal");
        astroCutterClip = Resources.Load<AudioClip>("Audio/astro_cutter_sound");
        guidanceSystemClip = Resources.Load<AudioClip>("Audio/guidance_system_sound");
        spaceDrillDeliveryClip = Resources.Load<AudioClip>("Audio/space_drill_delivery_sound");
        spaceMantaWarningClip = Resources.Load<AudioClip>("Audio/space_manta_warning");
        gravitySquidWarningClip = Resources.Load<AudioClip>("Audio/gravity_squid_warning");
        gravitySquidTetherClip = Resources.Load<AudioClip>("Audio/gravity_squid_tether");
        hunterLanceLockClip = Resources.Load<AudioClip>("Audio/hunter_lance_lock");
        hunterLanceFireClip = Resources.Load<AudioClip>("Audio/hunter_lance_fire");
        rocketLaunchClip = Resources.Load<AudioClip>("Audio/rocket_sound");
        rocketLockClip = Resources.Load<AudioClip>("Audio/rocket_lock");
        if (rocketLockClip == null)
            rocketLockClip = CreateRocketLockConfirmationClip();
        rocketFlyLoopClip = Resources.Load<AudioClip>("Audio/rocket_fly_loop");
        rocketExplosionClip = Resources.Load<AudioClip>("Audio/rocket_boom_sound");
        cosmicWormShotClip = Resources.Load<AudioClip>("Audio/cosmic_worm_shot");
        if (cosmicWormShotClip == null)
            cosmicWormShotClip = CreateCosmicWormShotClip();
    }

    AudioClip CreateRocketLockConfirmationClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.18f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] data = new float[sampleCount];
        float phase = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float normalized = time / duration;
            float frequency = time < 0.075f ? 940f : time < 0.13f ? 1320f : 1660f;
            phase += (Mathf.PI * 2f * frequency) / sampleRate;
            float attack = Mathf.Clamp01(time / 0.012f);
            float release = Mathf.Clamp01((duration - time) / 0.04f);
            float dip = time > 0.075f && time < 0.092f ? 0.28f : 1f;
            float envelope = attack * release * dip * Mathf.Lerp(0.95f, 0.55f, normalized);
            float tone = Mathf.Sin(phase) * 0.34f + Mathf.Sin(phase * 2.01f) * 0.055f;
            data[i] = tone * envelope;
        }

        AudioClip clip = AudioClip.Create("GeneratedRocketLockConfirmed", sampleCount, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    AudioClip CreateCosmicWormShotClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.34f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] data = new float[sampleCount];
        float phaseA = 0f;
        float phaseB = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float normalized = Mathf.Clamp01(time / duration);
            float attack = Mathf.Clamp01(time / 0.018f);
            float release = Mathf.Clamp01((duration - time) / 0.11f);
            float envelope = attack * release * (1f - normalized * 0.18f);
            float glide = 1f - Mathf.SmoothStep(0f, 1f, normalized);
            float frequencyA = Mathf.Lerp(62f, 245f, glide) + Mathf.Sin(time * 38f) * 18f;
            float frequencyB = Mathf.Lerp(420f, 110f, normalized) + Mathf.Sin(time * 91f) * 35f;
            phaseA += Mathf.PI * 2f * frequencyA / sampleRate;
            phaseB += Mathf.PI * 2f * frequencyB / sampleRate;
            float ringMod = Mathf.Sin(phaseA) * Mathf.Sin(phaseB * 0.73f);
            float throat = Mathf.Sin(phaseA * 0.51f + Mathf.Sin(phaseB) * 0.75f);
            float grit = HashNoise(i) * Mathf.Lerp(0.11f, 0.035f, normalized);
            data[i] = (ringMod * 0.34f + throat * 0.23f + grit) * envelope;
        }

        AudioClip clip = AudioClip.Create("GeneratedCosmicWormShot", sampleCount, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float HashNoise(int sampleIndex)
    {
        uint value = (uint)sampleIndex;
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        return ((value & 0xffff) / 32767.5f) - 1f;
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
        if (Time.unscaledTime - lastClickSoundTime < ClickSoundCooldownSeconds)
            return;

        lastClickSoundTime = Time.unscaledTime;
        PlayOneShot(clickClip, 0.8f);
    }

    public void PlayCash()
    {
        PlayOneShot(cashClip != null ? cashClip : clickClip, 0.86f);
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

    public void PlayShootSmallAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(shootSmallClip != null ? shootSmallClip : laserClip, worldPosition, 0.58f);
    }

    public void PlayArtilleryGunAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(artilleryGunClip != null ? artilleryGunClip : explosionClip, worldPosition, 0.72f);
    }

    public void PlayGatlingGunAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(gatlingGunClip != null ? gatlingGunClip : shootSmallClip != null ? shootSmallClip : laserClip, worldPosition, 0.74f);
    }

    public void PlayLazer1At(Vector3 worldPosition)
    {
        PlaySpatialOneShot(lazer1Clip != null ? lazer1Clip : laserClip, worldPosition, 0.68f);
    }

    public void PlayLazer2At(Vector3 worldPosition)
    {
        PlaySpatialOneShot(lazer2Clip != null ? lazer2Clip : laserClip, worldPosition, 0.64f);
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

    public void PlayShieldChargeAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(shieldChargeClip != null ? shieldChargeClip : shieldHitClip, worldPosition, 0.68f);
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

    public void PlaySpaceTruckAlertAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(spaceTruckAlertClip != null ? spaceTruckAlertClip : alarmClip, worldPosition, 0.92f);
    }

    public void PlayRadarShipShootAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(radarShipShootClip != null ? radarShipShootClip : artilleryGunClip != null ? artilleryGunClip : explosionClip, worldPosition, 0.86f);
    }

    public void PlayPirateFighterShotAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(pirateFighterShotClip != null ? pirateFighterShotClip : shootSmallClip != null ? shootSmallClip : laserClip, worldPosition, 0.72f);
    }

    public void PlayRadarShipIncomingAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(radarShipIncomingClip != null ? radarShipIncomingClip : alarmClip, worldPosition, 0.94f);
    }

    public void PlayRescueShipIncomingAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(
            rescueShipEngineClip != null ? rescueShipEngineClip :
            radarShipEngineClip != null ? radarShipEngineClip :
            engineClip,
            worldPosition,
            0.84f);
    }

    public void PlayShieldFullPowerAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(shieldFullPowerClip != null ? shieldFullPowerClip : shieldChargeClip, worldPosition, 0.82f);
    }

    public void PlayMagneticBeamAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(magneticBeamClip != null ? magneticBeamClip : shieldChargeClip, worldPosition, 0.88f);
    }

    public void PlayRepairBayLandingAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(repairBayLandingClip != null ? repairBayLandingClip : shieldChargeClip, worldPosition, 0.86f);
    }

    public void PlayRepairBayStartingAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(repairBayStartingClip != null ? repairBayStartingClip : engineClip, worldPosition, 0.86f);
    }

    public void PlayBeaconSignalAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(beaconSignalClip != null ? beaconSignalClip : alarmClip, worldPosition, 0.82f);
    }

    public void PlayAstroCutterAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(AstroCutterClip, worldPosition, 0.72f);
    }

    public void PlayGuidanceSystemAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(GuidanceSystemClip, worldPosition, 0.82f);
    }

    public void PlaySpaceDrillDeliveryAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(SpaceDrillDeliveryClip, worldPosition, 0.68f);
    }

    public void PlaySpaceMantaWarningAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(spaceMantaWarningClip != null ? spaceMantaWarningClip : beaconSignalClip != null ? beaconSignalClip : alarmClip, worldPosition, 0.9f);
    }

    public void PlayGravitySquidWarningAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(gravitySquidWarningClip != null ? gravitySquidWarningClip : beaconSignalClip != null ? beaconSignalClip : alarmClip, worldPosition, 0.92f);
    }

    public void PlayHunterLanceLockAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(hunterLanceLockClip != null ? hunterLanceLockClip : guidanceSystemClip != null ? guidanceSystemClip : alarmClip, worldPosition, 0.9f);
    }

    public void PlayHunterLanceFireAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(hunterLanceFireClip != null ? hunterLanceFireClip : lazer1Clip != null ? lazer1Clip : laserClip, worldPosition, 0.88f);
    }

    public void PlayRocketLaunchAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(rocketLaunchClip != null ? rocketLaunchClip : artilleryGunClip != null ? artilleryGunClip : shootSmallClip != null ? shootSmallClip : laserClip, worldPosition, 0.82f);
    }

    public void PlayRocketLockAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(rocketLockClip != null ? rocketLockClip : hunterLanceLockClip != null ? hunterLanceLockClip : beaconSignalClip != null ? beaconSignalClip : clickClip, worldPosition, 0.72f);
    }

    public void PlayRocketExplosionAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(rocketExplosionClip != null ? rocketExplosionClip : explosionClip, worldPosition, 0.86f);
    }

    public void PlayCosmicWormShotAt(Vector3 worldPosition)
    {
        PlaySpatialOneShot(cosmicWormShotClip != null ? cosmicWormShotClip : gravitySquidTetherClip != null ? gravitySquidTetherClip : lazer2Clip != null ? lazer2Clip : laserClip, worldPosition, 0.84f);
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
            float waitTime = GetEvacBuzzerPulseInterval();
            yield return new WaitForSeconds(waitTime);
        }
    }

    IEnumerator PlayEvacBuzzerLoopRoutine(float duration)
    {
        float elapsed = 0f;
        float waitTime = GetEvacBuzzerPulseInterval();

        while (elapsed < duration)
        {
            PlayOneShot(evacBuzzerClip, 0.95f);
            yield return new WaitForSeconds(waitTime);
            elapsed += waitTime;
        }

        evacBuzzerRoutine = null;
    }

    float GetEvacBuzzerPulseInterval()
    {
        return evacBuzzerClip != null ? Mathf.Max(0.45f, evacBuzzerClip.length + 0.06f) : 0.5f;
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
