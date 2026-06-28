using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class TreasureCollector
{
    void TryBindHudReferences()
    {
        if (!photonView.IsMine)
            return;

        if (ShouldDisableUseControllerForActor())
        {
            DisableForEnemyAstronaut();
            return;
        }

        if (scoreText == null)
        {
            GameObject scoreObject = GameObject.Find("ScoreText");
            if (scoreObject != null)
                scoreText = scoreObject.GetComponent<TMP_Text>();
        }

        if (collectButton == null)
        {
            GameObject buttonObject = GameObject.Find("CollectButton");
            if (buttonObject != null)
                collectButton = buttonObject.GetComponent<Button>();
        }

        EnsureCollectButtonPositioned();

        if (collectButton == null)
            return;

        if (collectButtonHooked && collectButtonBindingOwner == this)
            return;

        useButtonVisual = collectButton.GetComponent<UseButtonVisualController>();

        EventTrigger trigger = collectButton.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = collectButton.gameObject.AddComponent<EventTrigger>();

        trigger.triggers = new List<EventTrigger.Entry>();

        EventTrigger.Entry down = new EventTrigger.Entry();
        down.eventID = EventTriggerType.PointerDown;
        down.callback.AddListener(_ => StartHolding());
        trigger.triggers.Add(down);

        collectButtonHooked = true;
        collectButtonBindingOwner = this;
        UpdateUseButtonAvailability();
    }


    void HandleKeyboardUseShortcut()
    {
        if (!photonView.IsMine)
            return;

        if (!IsKeyboardUseShortcutPressedThisFrame())
            return;

        if (IsTypingInInputField())
            return;

        StartHolding();
    }

    static bool IsKeyboardUseShortcutPressedThisFrame()
    {
        return StarjackersInputModeManager.WasUsePressedThisFrame();
    }

    static bool IsTypingInInputField()
    {
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        if (selected == null)
            return false;

        return selected.GetComponent<TMP_InputField>() != null ||
               selected.GetComponentInParent<TMP_InputField>() != null ||
               selected.GetComponent<UnityEngine.UI.InputField>() != null ||
               selected.GetComponentInParent<UnityEngine.UI.InputField>() != null;
    }


    public bool HasUseActionAvailable()
    {
        return ResolveUseActionType() != UseActionType.None;
    }

    UseActionType ResolveUseActionType()
    {
        if (!photonView.IsMine)
            return UseActionType.None;

        if (ShouldDisableUseControllerForActor())
            return UseActionType.None;

        bool astronautMode = IsAstronautMode();
        UseActionType activeProgressAction = ResolveActiveProgressUseActionType(astronautMode);
        if (activeProgressAction != UseActionType.None)
            return activeProgressAction;

        if (!astronautMode)
        {
            AvengerWarBase avengerBase = AvengerWarBase.FindClosestUsable(transform.position);
            if (avengerBase != null)
                return UseActionType.AvengerBoard;

            PlayerRepairDocking repairDocking = GetCachedPlayerRepairDocking();
            RepairBay repairBay = RepairBay.FindClosestUsable(transform.position);
            SpaceFactory spaceFactory = SpaceFactory.FindClosestUsable(transform.position);
            ScienceStation scienceStation = ScienceStation.FindClosestUsable(transform.position);
            if (repairDocking != null && (repairDocking.IsBusy || repairBay != null || spaceFactory != null || scienceStation != null))
                return UseActionType.Land;
        }

        ArtifactAsteroid artifact = ResolveUsableArtifactAsteroid();
        if (artifact != null)
            return UseActionType.Examine;

        if (!astronautMode)
        {
            PlayerHealth playerHealth = GetCachedPlayerHealth();
            if (BisonIndustrialPlotController.CanDropHaul(playerHealth))
                return UseActionType.Drop;
            if (BisonIndustrialPlotController.CanStartHaul(playerHealth))
                return UseActionType.Haul;
            if (InvaderInvasionPlotController.TryGetUseAction(playerHealth, out InvaderPlotUseAction invaderAction))
                return ConvertInvaderUseAction(invaderAction);
        }

        ExtractionZone extractionZone = ResolveUsableExtractionZone();
        if (extractionZone != null)
        {
            if (CanUseExtractionNow(extractionZone))
                return extractionZone.IsActive ? UseActionType.Escape : UseActionType.Activate;

            if (!CanCollectWhileExtractionUnavailable(extractionZone))
                return UseActionType.None;
        }

        if (astronautMode)
            return UseActionType.None;

        return ResolveCollectibleUseActionType();
    }

    UseActionType ResolveCollectibleUseActionType()
    {
        if (!HasUsableCollectibleTarget())
            return UseActionType.None;

        return CanStoreCurrentCollectible() ? UseActionType.Collect : UseActionType.None;
    }

    UseActionType ResolveActiveProgressUseActionType(bool astronautMode)
    {
        if (isActivatingExtraction)
            return UseActionType.Activate;

        if (isExaminingArtifact)
            return UseActionType.Examine;

        if (isCollecting)
            return UseActionType.Collect;

        PlayerHealth playerHealth = !astronautMode ? GetCachedPlayerHealth() : null;
        if (!astronautMode && BisonIndustrialPlotController.IsHaulChargeInProgress(playerHealth))
            return UseActionType.Haul;

        if (!astronautMode &&
            InvaderInvasionPlotController.TryGetUseChargeProgress(playerHealth, out _, out InvaderPlotUseAction invaderAction))
        {
            return ConvertInvaderUseAction(invaderAction);
        }

        return UseActionType.None;
    }

    UseActionType ConvertInvaderUseAction(InvaderPlotUseAction action)
    {
        switch (action)
        {
            case InvaderPlotUseAction.Contact:
                return UseActionType.InvaderContact;
            case InvaderPlotUseAction.Stabilize:
                return UseActionType.InvaderStabilize;
            case InvaderPlotUseAction.Sync:
                return UseActionType.InvaderSync;
            default:
                return UseActionType.None;
        }
    }

    ExtractionZone ResolveUsableExtractionZone()
    {
        if (currentExtraction != null && IsExtractionStillUsable(currentExtraction))
            return currentExtraction;

        return ResolveNearbyExtractionZone();
    }

    ArtifactAsteroid ResolveUsableArtifactAsteroid()
    {
        if (isExaminingArtifact && IsArtifactStillUsable(currentArtifactAsteroid))
            return currentArtifactAsteroid;

        if (!PlayerHasAlienTransmitter(photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer))
            return null;

        ArtifactAsteroid artifact = ArtifactAsteroid.FindClosestInactiveUsable(GetShipTipPosition());
        if (artifact == null || artifact.IsActive)
            return null;

        return artifact;
    }

    bool IsArtifactStillUsable(ArtifactAsteroid artifact)
    {
        if (artifact == null || artifact.IsActive)
            return false;

        if (!PlayerHasAlienTransmitter(photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer))
            return false;

        return artifact.GetInteractionDistanceToPoint(GetShipTipPosition()) <= ArtifactAsteroid.ExamineRange + ArtifactExamineKeepAliveDistance;
    }

    bool PlayerHasAlienTransmitter(Photon.Realtime.Player player)
    {
        RefreshLoadoutCacheIfNeeded(player);
        return cachedHasAlienTransmitter;
    }


    void UpdateUseButtonAvailability()
    {
        UseActionType actionType = ResolveUseActionType();
        SetUseButtonState(actionType != UseActionType.None, actionType);
        nextUseActionRefreshTime = Time.unscaledTime + GetUseActionRefreshInterval();
    }

    void SetUseButtonState(bool available, UseActionType actionType)
    {
        if (collectButton == null)
            return;

        if (useButtonVisual == null)
            useButtonVisual = collectButton.GetComponent<UseButtonVisualController>();

        string label = GetUseButtonLabel(actionType);
        if (useButtonVisual != null)
        {
            useButtonVisual.SetLabel(label);
            useButtonVisual.SetAvailable(available);
            if (!IsUseProgressActive())
                useButtonVisual.SetProgress(0f, false);
        }
        else
        {
            SetUseButtonText(label);
        }
    }

    string GetUseButtonLabel(UseActionType actionType)
    {
        switch (actionType)
        {
            case UseActionType.Collect:
                return "COLLECT";
            case UseActionType.Land:
            case UseActionType.AvengerBoard:
                return "LAND";
            case UseActionType.Examine:
                return "EXAMINE";
            case UseActionType.Activate:
                return "ACTIVATE";
            case UseActionType.Escape:
                return "ESCAPE";
            case UseActionType.Haul:
                return "HAUL";
            case UseActionType.Drop:
                return "DROP";
            case UseActionType.InvaderContact:
                return "CONTACT";
            case UseActionType.InvaderStabilize:
                return "STABILIZE";
            case UseActionType.InvaderSync:
                return "SYNC";
            default:
                return string.Empty;
        }
    }

    void SetUseButtonText(string label)
    {
        TMP_Text text = collectButton != null ? collectButton.GetComponentInChildren<TMP_Text>(true) : null;
        if (text != null && text.text != label)
            text.text = label;
    }

    bool IsUseProgressActive()
    {
        return ResolveActiveProgressUseActionType(IsAstronautMode()) != UseActionType.None;
    }

    void SetUseProgress(float progress, bool visible)
    {
        if (useButtonVisual == null && collectButton != null)
            useButtonVisual = collectButton.GetComponent<UseButtonVisualController>();

        if (useButtonVisual != null)
            useButtonVisual.SetProgress(progress, visible);
    }

    void UpdateHaulChargeUseProgress()
    {
        if (!photonView.IsMine)
            return;

        PlayerHealth playerHealth = GetCachedPlayerHealth();
        if (BisonIndustrialPlotController.TryGetHaulChargeProgress(playerHealth, out float progress))
        {
            SetUseProgress(progress, true);
            return;
        }

        if (InvaderInvasionPlotController.TryGetUseChargeProgress(playerHealth, out float invaderProgress, out _))
            SetUseProgress(invaderProgress, true);
    }


    void EnsureCollectButtonPositioned()
    {
        if (collectButton == null)
            return;

        RectTransform rect = collectButton.GetComponent<RectTransform>();
        if (rect == null)
            return;

        if (rect != positionedCollectButtonRect)
        {
            positionedCollectButtonRect = rect;
            collectButtonDefaultLayoutCaptured = false;
            collectButtonLayoutApplied = false;
        }

        CaptureCollectButtonDefaultLayout(rect);

        bool desktopLayout = StarjackersInputModeManager.DesktopHudLayoutActive;
        if (collectButtonLayoutApplied && collectButtonUsingDesktopLayout == desktopLayout)
            return;

        if (desktopLayout)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = CollectButtonDesktopPosition;
        }
        else
        {
            rect.anchorMin = collectButtonDefaultAnchorMin;
            rect.anchorMax = collectButtonDefaultAnchorMax;
            rect.pivot = collectButtonDefaultPivot;
            rect.anchoredPosition = CollectButtonRoundPosition;
        }

        collectButtonUsingDesktopLayout = desktopLayout;
        collectButtonLayoutApplied = true;
    }

    void CaptureCollectButtonDefaultLayout(RectTransform rect)
    {
        if (collectButtonDefaultLayoutCaptured || rect == null)
            return;

        collectButtonDefaultAnchorMin = rect.anchorMin;
        collectButtonDefaultAnchorMax = rect.anchorMax;
        collectButtonDefaultPivot = rect.pivot;
        collectButtonDefaultLayoutCaptured = true;
    }

}
