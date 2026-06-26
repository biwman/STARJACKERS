using System;
using System.Threading.Tasks;
using Unity.Services.Analytics;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
using UnityEngine.UnityConsent;

public static class BrawlRaidersAnalytics
{
    const string EnvironmentName = "production";
    const string ConsentPlayerPrefsKey = "brawl_raiders.analytics_consent";
    const int ConsentUnset = -1;
    const int ConsentDenied = 0;
    const int ConsentGranted = 1;

    static bool initializationStarted;
    static bool initialized;
    static bool sessionStartedRecorded;

    public static bool IsReady => initialized;

    public static bool HasAnalyticsConsent => GetStoredConsentStatus() == ConsentStatus.Granted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InitializeOnLoad()
    {
        _ = InitializeAsync();
    }

    public static async Task InitializeAsync()
    {
        if (initializationStarted)
            return;

        initializationStarted = true;

        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                InitializationOptions options = new InitializationOptions()
                    .SetEnvironmentName(EnvironmentName);
                await UnityServices.InitializeAsync(options);
            }

            ConsentStatus consentStatus = GetStoredConsentStatus();
            ApplyConsent(consentStatus);

            initialized = consentStatus == ConsentStatus.Granted;
            if (!initialized)
            {
                Debug.Log("BrawlRaidersAnalytics: analytics disabled until player consent is granted.");
                return;
            }

            RecordSessionStarted();
            AnalyticsService.Instance.Flush();
            Debug.Log("BrawlRaidersAnalytics: analytics initialized for '" + EnvironmentName + "' and session start event queued.");
        }
        catch (Exception ex)
        {
            initializationStarted = false;
            initialized = false;
            Debug.LogWarning("BrawlRaidersAnalytics: initialization failed. " + ex.Message);
        }
    }

    public static void SetPlayerAnalyticsConsent(bool granted)
    {
        PlayerPrefs.SetInt(ConsentPlayerPrefsKey, granted ? ConsentGranted : ConsentDenied);
        PlayerPrefs.Save();

        ApplyConsent(granted ? ConsentStatus.Granted : ConsentStatus.Denied);
        initialized = granted && UnityServices.State == ServicesInitializationState.Initialized;

        if (initialized)
        {
            RecordSessionStarted();
            AnalyticsService.Instance.Flush();
        }
    }

    public static void RecordEvent(string eventName)
    {
        if (!CanRecord(eventName))
            return;

        try
        {
            AnalyticsService.Instance.RecordEvent(eventName);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("BrawlRaidersAnalytics: event '" + eventName + "' failed. " + ex.Message);
        }
    }

    public static void RecordEvent(CustomEvent analyticsEvent)
    {
        if (analyticsEvent == null || !CanRecord("custom_event"))
            return;

        try
        {
            AnalyticsService.Instance.RecordEvent(analyticsEvent);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("BrawlRaidersAnalytics: custom event failed. " + ex.Message);
        }
    }

    static void RecordSessionStarted()
    {
        if (sessionStartedRecorded)
            return;

        RecordEvent("game_session_started");
        sessionStartedRecorded = true;
    }

    static bool CanRecord(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            return false;

        if (!initialized)
            return false;

        if (UnityServices.State != ServicesInitializationState.Initialized)
            return false;

        return GetStoredConsentStatus() == ConsentStatus.Granted;
    }

    static ConsentStatus GetStoredConsentStatus()
    {
        int stored = PlayerPrefs.GetInt(ConsentPlayerPrefsKey, ConsentUnset);
        if (stored == ConsentGranted)
            return ConsentStatus.Granted;
        if (stored == ConsentDenied)
            return ConsentStatus.Denied;

#if UNITY_EDITOR
        return ConsentStatus.Granted;
#else
        return ConsentStatus.Denied;
#endif
    }

    static void ApplyConsent(ConsentStatus analyticsConsent)
    {
        EndUserConsent.SetConsentState(new ConsentState
        {
            AnalyticsIntent = analyticsConsent,
            AdsIntent = ConsentStatus.Denied
        });
    }
}
