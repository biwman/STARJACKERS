using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
public partial class PlayerProfilePanelUI
{
    void UpdateSelectionCarouselAnimations()
    {
        if (currentScreen == ProfileScreen.ShipSelection)
            UpdateShipSelectionSnap();
        else if (shipSelectionSnapActive || shipSelectionDragActive || Mathf.Abs(shipSelectionVisualOffset) > 0.001f)
            ResetShipSelectionCarouselMotion();

        if (currentScreen == ProfileScreen.PilotSelection)
            UpdatePilotSelectionSnap();
        else if (pilotSelectionSnapActive || pilotSelectionDragActive || Mathf.Abs(pilotSelectionVisualOffset) > 0.001f)
            ResetPilotSelectionCarouselMotion();
    }

    void ResetShipSelectionCarouselMotion()
    {
        shipSelectionDragActive = false;
        shipSelectionDragHorizontal = false;
        shipSelectionSnapActive = false;
        shipSelectionSnapElapsed = 0f;
        shipSelectionSnapIndexDelta = 0;
        shipSelectionVisualOffset = 0f;
    }

    void ResetPilotSelectionCarouselMotion()
    {
        pilotSelectionDragActive = false;
        pilotSelectionDragHorizontal = false;
        pilotSelectionSnapActive = false;
        pilotSelectionSnapElapsed = 0f;
        pilotSelectionSnapIndexDelta = 0;
        pilotSelectionVisualOffset = 0f;
    }

    public void BeginShipSelectionDrag(PointerEventData eventData, Vector2 startScreenPosition)
    {
        if (inventoryActionInProgress || currentScreen != ProfileScreen.ShipSelection || shipSelectionCardObjects == null)
            return;

        if (!TryGetSelectionLocalPoint(shipSelectionViewObject, eventData, startScreenPosition, out shipSelectionDragStartLocal))
            return;

        shipSelectionDragActive = true;
        shipSelectionDragHorizontal = false;
        shipSelectionSnapActive = false;
        shipSelectionDragStartOffset = shipSelectionVisualOffset;
    }

    public void UpdateShipSelectionDrag(PointerEventData eventData)
    {
        if (!shipSelectionDragActive || inventoryActionInProgress || currentScreen != ProfileScreen.ShipSelection)
            return;

        if (!TryGetSelectionLocalPoint(shipSelectionViewObject, eventData, eventData.position, out Vector2 localPoint))
            return;

        Vector2 delta = localPoint - shipSelectionDragStartLocal;
        if (!shipSelectionDragHorizontal)
        {
            if (delta.magnitude < SelectionCarouselAxisThreshold)
                return;

            if (Mathf.Abs(delta.x) <= Mathf.Abs(delta.y))
                return;

            shipSelectionDragHorizontal = true;
        }

        shipSelectionVisualOffset = shipSelectionDragStartOffset + delta.x / GetShipSelectionSwipeDistance();
        if (NormalizeContinuousSelectionDrag(ref shipSelectionCenterIndex, ref shipSelectionVisualOffset, ref shipSelectionDragStartOffset, SelectableShipTypes.Length))
        {
            shipSelectionCenterType = SelectableShipTypes[shipSelectionCenterIndex];
            RefreshShipSelectionView();
            return;
        }

        ApplyShipSelectionCarouselVisuals();
    }

    public void EndShipSelectionDrag(PointerEventData eventData)
    {
        if (!shipSelectionDragActive)
            return;

        shipSelectionDragActive = false;
        Vector2 delta = Vector2.zero;
        if (TryGetSelectionLocalPoint(shipSelectionViewObject, eventData, eventData.position, out Vector2 localPoint))
            delta = localPoint - shipSelectionDragStartLocal;

        if (shipSelectionDragHorizontal && Mathf.Abs(delta.x) >= SelectionCarouselClickCancelThreshold)
            shipSelectionSuppressClickUntilFrame = Time.frameCount + SelectionCarouselClickSuppressFrames;

        int indexDelta = shipSelectionDragHorizontal
            ? ResolveSelectionCarouselSnapIndexDelta(shipSelectionVisualOffset, shipSelectionCenterIndex, SelectableShipTypes.Length)
            : 0;

        StartShipSelectionSnap(indexDelta);
    }

    public void BeginPilotSelectionDrag(PointerEventData eventData, Vector2 startScreenPosition)
    {
        if (inventoryActionInProgress || currentScreen != ProfileScreen.PilotSelection || pilotSelectionCardObjects == null)
            return;

        if (!TryGetSelectionLocalPoint(pilotSelectionViewObject, eventData, startScreenPosition, out pilotSelectionDragStartLocal))
            return;

        pilotSelectionDragActive = true;
        pilotSelectionDragHorizontal = false;
        pilotSelectionSnapActive = false;
        pilotSelectionDragStartOffset = pilotSelectionVisualOffset;
    }

    public void UpdatePilotSelectionDrag(PointerEventData eventData)
    {
        if (!pilotSelectionDragActive || inventoryActionInProgress || currentScreen != ProfileScreen.PilotSelection)
            return;

        if (!TryGetSelectionLocalPoint(pilotSelectionViewObject, eventData, eventData.position, out Vector2 localPoint))
            return;

        Vector2 delta = localPoint - pilotSelectionDragStartLocal;
        if (!pilotSelectionDragHorizontal)
        {
            if (delta.magnitude < SelectionCarouselAxisThreshold)
                return;

            if (Mathf.Abs(delta.x) <= Mathf.Abs(delta.y))
                return;

            pilotSelectionDragHorizontal = true;
        }

        pilotSelectionVisualOffset = pilotSelectionDragStartOffset + delta.x / GetPilotSelectionSwipeDistance();
        if (NormalizeContinuousSelectionDrag(ref pilotSelectionCenterIndex, ref pilotSelectionVisualOffset, ref pilotSelectionDragStartOffset, PilotCatalog.AllDefinitions.Count))
        {
            RefreshPilotSelectionView();
            return;
        }

        ApplyPilotSelectionCarouselVisuals();
    }

    public void EndPilotSelectionDrag(PointerEventData eventData)
    {
        if (!pilotSelectionDragActive)
            return;

        pilotSelectionDragActive = false;
        Vector2 delta = Vector2.zero;
        if (TryGetSelectionLocalPoint(pilotSelectionViewObject, eventData, eventData.position, out Vector2 localPoint))
            delta = localPoint - pilotSelectionDragStartLocal;

        if (pilotSelectionDragHorizontal && Mathf.Abs(delta.x) >= SelectionCarouselClickCancelThreshold)
            pilotSelectionSuppressClickUntilFrame = Time.frameCount + SelectionCarouselClickSuppressFrames;

        int indexDelta = pilotSelectionDragHorizontal
            ? ResolveSelectionCarouselSnapIndexDelta(pilotSelectionVisualOffset, pilotSelectionCenterIndex, PilotCatalog.AllDefinitions.Count)
            : 0;

        StartPilotSelectionSnap(indexDelta);
    }

    bool TryGetSelectionLocalPoint(GameObject viewObject, PointerEventData eventData, Vector2 screenPosition, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;
        RectTransform rect = viewObject != null ? viewObject.GetComponent<RectTransform>() : null;
        if (rect == null || eventData == null)
            return false;

        Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPosition, eventCamera, out localPoint);
    }

    float GetShipSelectionSwipeDistance()
    {
        return Mathf.Max(1f, ShipSelectionRightPosition.x - ShipSelectionCenterPosition.x);
    }

    float GetPilotSelectionSwipeDistance()
    {
        return Mathf.Max(1f, PilotSelectionRightPosition.x - PilotSelectionCenterPosition.x);
    }

    float ApplySelectionCarouselDragBounds(float offset, int centerIndex, int itemCount)
    {
        if (offset > 0f && centerIndex <= 0)
            offset *= SelectionCarouselEdgeResistance;
        else if (offset < 0f && centerIndex >= itemCount - 1)
            offset *= SelectionCarouselEdgeResistance;

        return Mathf.Clamp(offset, -SelectionCarouselMaxOffset, SelectionCarouselMaxOffset);
    }

    bool NormalizeContinuousSelectionDrag(ref int centerIndex, ref float offset, ref float dragStartOffset, int itemCount)
    {
        bool changed = false;
        while (offset <= -1f && centerIndex < itemCount - 1)
        {
            centerIndex++;
            offset += 1f;
            dragStartOffset += 1f;
            changed = true;
        }

        while (offset >= 1f && centerIndex > 0)
        {
            centerIndex--;
            offset -= 1f;
            dragStartOffset -= 1f;
            changed = true;
        }

        offset = ApplySelectionCarouselDragBounds(offset, centerIndex, itemCount);
        return changed;
    }

    int ResolveSelectionCarouselSnapIndexDelta(float offset, int centerIndex, int itemCount)
    {
        if (Mathf.Abs(offset) < SelectionCarouselSnapThreshold)
            return 0;

        if (offset > 0f && centerIndex > 0)
            return -1;
        if (offset < 0f && centerIndex < itemCount - 1)
            return 1;

        return 0;
    }

    void StartShipSelectionSnap(int indexDelta)
    {
        shipSelectionSnapActive = true;
        shipSelectionSnapElapsed = 0f;
        shipSelectionSnapStartOffset = shipSelectionVisualOffset;
        shipSelectionSnapIndexDelta = indexDelta;
        shipSelectionSnapTargetOffset = indexDelta == 0 ? 0f : -indexDelta;
    }

    void StartPilotSelectionSnap(int indexDelta)
    {
        pilotSelectionSnapActive = true;
        pilotSelectionSnapElapsed = 0f;
        pilotSelectionSnapStartOffset = pilotSelectionVisualOffset;
        pilotSelectionSnapIndexDelta = indexDelta;
        pilotSelectionSnapTargetOffset = indexDelta == 0 ? 0f : -indexDelta;
    }

    void UpdateShipSelectionSnap()
    {
        if (!shipSelectionSnapActive || shipSelectionDragActive)
            return;

        shipSelectionSnapElapsed += Time.unscaledDeltaTime;
        float t = SelectionCarouselSnapDuration > 0f ? Mathf.Clamp01(shipSelectionSnapElapsed / SelectionCarouselSnapDuration) : 1f;
        float eased = t * t * (3f - 2f * t);
        shipSelectionVisualOffset = Mathf.Lerp(shipSelectionSnapStartOffset, shipSelectionSnapTargetOffset, eased);
        ApplyShipSelectionCarouselVisuals();

        if (t >= 1f)
            FinishShipSelectionSnap();
    }

    void UpdatePilotSelectionSnap()
    {
        if (!pilotSelectionSnapActive || pilotSelectionDragActive)
            return;

        pilotSelectionSnapElapsed += Time.unscaledDeltaTime;
        float t = SelectionCarouselSnapDuration > 0f ? Mathf.Clamp01(pilotSelectionSnapElapsed / SelectionCarouselSnapDuration) : 1f;
        float eased = t * t * (3f - 2f * t);
        pilotSelectionVisualOffset = Mathf.Lerp(pilotSelectionSnapStartOffset, pilotSelectionSnapTargetOffset, eased);
        ApplyPilotSelectionCarouselVisuals();

        if (t >= 1f)
            FinishPilotSelectionSnap();
    }

    void FinishShipSelectionSnap()
    {
        int indexDelta = shipSelectionSnapIndexDelta;
        shipSelectionSnapActive = false;
        shipSelectionVisualOffset = 0f;
        shipSelectionSnapIndexDelta = 0;

        if (indexDelta != 0)
        {
            shipSelectionCenterIndex = Mathf.Clamp(shipSelectionCenterIndex + indexDelta, 0, SelectableShipTypes.Length - 1);
            shipSelectionCenterType = SelectableShipTypes[shipSelectionCenterIndex];
            RefreshShipSelectionView();
        }
        else
        {
            ApplyShipSelectionCarouselVisuals();
        }
    }

    void FinishPilotSelectionSnap()
    {
        int indexDelta = pilotSelectionSnapIndexDelta;
        pilotSelectionSnapActive = false;
        pilotSelectionVisualOffset = 0f;
        pilotSelectionSnapIndexDelta = 0;

        if (indexDelta != 0)
        {
            pilotSelectionCenterIndex = Mathf.Clamp(pilotSelectionCenterIndex + indexDelta, 0, PilotCatalog.AllDefinitions.Count - 1);
            RefreshPilotSelectionView();
        }
        else
        {
            ApplyPilotSelectionCarouselVisuals();
        }
    }

    bool ConsumeShipSelectionClickSuppression()
    {
        if (Time.frameCount > shipSelectionSuppressClickUntilFrame)
            return false;

        shipSelectionSuppressClickUntilFrame = 0;
        return true;
    }

    bool ConsumePilotSelectionClickSuppression()
    {
        if (Time.frameCount > pilotSelectionSuppressClickUntilFrame)
            return false;

        pilotSelectionSuppressClickUntilFrame = 0;
        return true;
    }

    void ApplyShipSelectionCarouselVisuals()
    {
        if (shipSelectionCardObjects == null)
            return;

        for (int i = 0; i < shipSelectionCardObjects.Length; i++)
        {
            if (shipSelectionCardObjects[i] == null || !shipSelectionCardObjects[i].activeSelf)
                continue;

            ApplyShipSelectionCardVisualState(i, (i - 1) + shipSelectionVisualOffset);
        }

        UpdateShipSelectionCardLayering();
    }

    void ApplyPilotSelectionCarouselVisuals()
    {
        if (pilotSelectionCardObjects == null)
            return;

        for (int i = 0; i < pilotSelectionCardObjects.Length; i++)
        {
            if (pilotSelectionCardObjects[i] == null || !pilotSelectionCardObjects[i].activeSelf)
                continue;

            ApplyPilotSelectionCardVisualState(i, (i - 1) + pilotSelectionVisualOffset);
        }

        UpdatePilotSelectionCardLayering();
    }

    void ApplyShipSelectionCardVisualState(int cardIndex, float slot)
    {
        GameObject cardObject = shipSelectionCardObjects != null && cardIndex >= 0 && cardIndex < shipSelectionCardObjects.Length
            ? shipSelectionCardObjects[cardIndex]
            : null;
        if (cardObject == null)
            return;

        float centerAmount = GetSelectionCarouselCenterAmount(slot);
        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        if (cardRect != null)
        {
            cardRect.anchoredPosition = EvaluateSelectionCarouselPosition(slot, ShipSelectionLeftPosition, ShipSelectionCenterPosition, ShipSelectionRightPosition);
            cardRect.sizeDelta = EvaluateSelectionCarouselSize(slot, ShipSelectionSideSize, ShipSelectionCenterSize);
        }

        Image previewImage = shipSelectionCardImages != null && cardIndex < shipSelectionCardImages.Length
            ? shipSelectionCardImages[cardIndex]
            : null;
        if (previewImage != null)
        {
            RectTransform imageRect = previewImage.rectTransform;
            imageRect.anchoredPosition = Vector2.Lerp(ShipSelectionSideImagePosition, ShipSelectionCenterImagePosition, centerAmount);
            imageRect.sizeDelta = Vector2.Lerp(ShipSelectionSideImageSize, ShipSelectionCenterImageSize, centerAmount);
        }

        Image cardImage = cardObject.GetComponent<Image>();
        if (cardImage != null)
            cardImage.color = Color.Lerp(new Color(0.07f, 0.1f, 0.15f, 0.68f), new Color(0.08f, 0.11f, 0.16f, 0.76f), centerAmount);

        LayoutShipSelectionStats(cardIndex, centerAmount);

        GameObject[] slotObjects = shipSelectionCardSlotObjects != null && cardIndex < shipSelectionCardSlotObjects.Length
            ? shipSelectionCardSlotObjects[cardIndex]
            : null;
        if (slotObjects == null)
            return;

        Vector2[] slotLayout = BuildShipSelectionSlotLayout(centerAmount);
        for (int i = 0; i < slotObjects.Length && i < slotLayout.Length; i++)
        {
            if (slotObjects[i] == null)
                continue;

            RectTransform slotRect = slotObjects[i].GetComponent<RectTransform>();
            if (slotRect == null)
                continue;

            slotRect.anchoredPosition = slotLayout[i];
            slotRect.sizeDelta = GetShipSelectionSlotSize(centerAmount >= 0.5f);
        }
    }

    void ApplyPilotSelectionCardVisualState(int cardIndex, float slot)
    {
        GameObject cardObject = pilotSelectionCardObjects != null && cardIndex >= 0 && cardIndex < pilotSelectionCardObjects.Length
            ? pilotSelectionCardObjects[cardIndex]
            : null;
        if (cardObject == null)
            return;

        float centerAmount = GetSelectionCarouselCenterAmount(slot);
        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        if (cardRect != null)
        {
            cardRect.anchoredPosition = EvaluateSelectionCarouselPosition(slot, PilotSelectionLeftPosition, PilotSelectionCenterPosition, PilotSelectionRightPosition);
            cardRect.sizeDelta = EvaluateSelectionCarouselSize(slot, PilotSelectionSideSize, PilotSelectionCenterSize);
        }

        Image previewImage = pilotSelectionCardImages != null && cardIndex < pilotSelectionCardImages.Length
            ? pilotSelectionCardImages[cardIndex]
            : null;
        if (previewImage != null)
        {
            RectTransform imageRect = previewImage.rectTransform;
            imageRect.offsetMin = Vector2.Lerp(PilotSelectionSideImageOffsetMin, PilotSelectionCenterImageOffsetMin, centerAmount);
            imageRect.offsetMax = Vector2.Lerp(PilotSelectionSideImageOffsetMax, PilotSelectionCenterImageOffsetMax, centerAmount);
        }

        int targetIndex = pilotSelectionCenterIndex + cardIndex - 1;
        bool selected = targetIndex >= 0 &&
                        targetIndex < PilotCatalog.AllDefinitions.Count &&
                        string.Equals(selectedPilotId, PilotCatalog.AllDefinitions[targetIndex].Id, StringComparison.Ordinal);
        Image cardImage = cardObject.GetComponent<Image>();
        if (cardImage != null)
        {
            Color sideColor = selected ? new Color(0.12f, 0.25f, 0.22f, 0.9f) : new Color(0.08f, 0.11f, 0.16f, 0.86f);
            Color centerColor = selected ? new Color(0.12f, 0.25f, 0.22f, 0.98f) : new Color(0.11f, 0.16f, 0.22f, 0.96f);
            cardImage.color = Color.Lerp(sideColor, centerColor, centerAmount);
        }

        TMP_Text lockText = pilotSelectionCardLockTexts != null && cardIndex < pilotSelectionCardLockTexts.Length
            ? pilotSelectionCardLockTexts[cardIndex]
            : null;
        if (lockText != null)
            lockText.fontSize = Mathf.Lerp(16f, 22f, centerAmount);
    }

    static Vector2 EvaluateSelectionCarouselPosition(float slot, Vector2 left, Vector2 center, Vector2 right)
    {
        if (slot <= -1f)
            return new Vector2(left.x - ((center.x - left.x) * (-slot - 1f)), left.y);
        if (slot < 0f)
            return Vector2.Lerp(left, center, slot + 1f);
        if (slot <= 1f)
            return Vector2.Lerp(center, right, slot);

        return new Vector2(right.x + ((right.x - center.x) * (slot - 1f)), right.y);
    }

    static Vector2 EvaluateSelectionCarouselSize(float slot, Vector2 side, Vector2 center)
    {
        return Vector2.Lerp(side, center, GetSelectionCarouselCenterAmount(slot));
    }

    static float GetSelectionCarouselCenterAmount(float slot)
    {
        return Mathf.Clamp01(1f - Mathf.Abs(slot));
    }

    static void SortSelectionCardsByCenter(GameObject[] cardObjects, float visualOffset)
    {
        if (cardObjects == null)
            return;

        int[] order = { 0, 1, 2 };
        Array.Sort(order, (a, b) =>
        {
            float aCenterAmount = GetSelectionCarouselCenterAmount((a - 1) + visualOffset);
            float bCenterAmount = GetSelectionCarouselCenterAmount((b - 1) + visualOffset);
            return aCenterAmount.CompareTo(bCenterAmount);
        });

        for (int i = 0; i < order.Length; i++)
        {
            int cardIndex = order[i];
            if (cardIndex >= 0 &&
                cardIndex < cardObjects.Length &&
                cardObjects[cardIndex] != null &&
                cardObjects[cardIndex].activeSelf)
            {
                cardObjects[cardIndex].transform.SetSiblingIndex(i);
            }
        }
    }

    void MovePilotSelectionLeft()
    {
        if (inventoryActionInProgress || pilotSelectionDragActive || pilotSelectionSnapActive)
            return;

        ResetPilotSelectionCarouselMotion();
        pilotSelectionCenterIndex = Mathf.Max(0, pilotSelectionCenterIndex - 1);
        RefreshPilotSelectionView();
    }

    void MovePilotSelectionRight()
    {
        if (inventoryActionInProgress || pilotSelectionDragActive || pilotSelectionSnapActive)
            return;

        ResetPilotSelectionCarouselMotion();
        pilotSelectionCenterIndex = Mathf.Min(PilotCatalog.AllDefinitions.Count - 1, pilotSelectionCenterIndex + 1);
        RefreshPilotSelectionView();
    }

    public void OnPilotSelectionSwiped(float horizontalDelta)
    {
        if (inventoryActionInProgress)
            return;

        if (Mathf.Abs(horizontalDelta) < 72f)
            return;

        if (horizontalDelta > 0f)
            MovePilotSelectionLeft();
        else
            MovePilotSelectionRight();
    }

    void OnPilotSelectionCardClicked(int cardIndex)
    {
        if (inventoryActionInProgress || pilotSelectionDragActive || pilotSelectionSnapActive || pilotSelectionCardObjects == null)
            return;

        if (cardIndex < 0 || cardIndex >= pilotSelectionCardObjects.Length)
            return;

        if (cardIndex == 1)
        {
            CommitPilotSelection(PilotCatalog.AllDefinitions[pilotSelectionCenterIndex].Id);
            return;
        }

        int direction = cardIndex == 0 ? -1 : 1;
        pilotSelectionCenterIndex = Mathf.Clamp(pilotSelectionCenterIndex + direction, 0, PilotCatalog.AllDefinitions.Count - 1);
        RefreshPilotSelectionView();
    }

    async void CommitPilotSelection(string pilotId)
    {
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        if (!PilotCatalog.IsPilotUnlocked(profile, pilotId))
        {
            if (pilotSelectionStatusText != null)
                pilotSelectionStatusText.text = PilotCatalog.GetUnlockRequirementText(profile, pilotId);
            RefreshPilotSelectionView();
            return;
        }

        inventoryActionInProgress = true;
        SetInteractable(false);
        if (pilotSelectionStatusText != null)
            pilotSelectionStatusText.text = "Selecting pilot...";

        try
        {
            bool changed = await PlayerProfileService.Instance.TryChangePilotAsync(pilotId);
            if (!changed)
            {
                if (pilotSelectionStatusText != null)
                    pilotSelectionStatusText.text = PilotCatalog.GetUnlockRequirementText(PlayerProfileService.Instance.CurrentProfile, pilotId);
                return;
            }

            selectedPilotId = PilotCatalog.NormalizePilotId(pilotId);
            RefreshView();
            SwitchToScreen(ProfileScreen.Home);
        }
        catch (Exception ex)
        {
            Debug.LogError("Pilot selection failed: " + ex);
            if (pilotSelectionStatusText != null)
                pilotSelectionStatusText.text = "Pilot change failed.";
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(true);
            RefreshPilotSelectionView();
        }
    }

    void RefreshPilotSelectionView()
    {
        using (PilotSelectionRefreshMarker.Auto())
        {
            if (pilotSelectionViewObject == null || pilotSelectionCardObjects == null || PilotCatalog.AllDefinitions.Count <= 0)
                return;

            pilotSelectionCenterIndex = Mathf.Clamp(pilotSelectionCenterIndex, 0, PilotCatalog.AllDefinitions.Count - 1);
            PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
            PilotDefinition centerDefinition = PilotCatalog.AllDefinitions[pilotSelectionCenterIndex];
            bool centerUnlocked = PilotCatalog.IsPilotUnlocked(profile, centerDefinition.Id);

            if (pilotSelectionTitleText != null)
                pilotSelectionTitleText.text = centerDefinition.DisplayName;

            if (pilotSelectionBackButton != null)
                LayoutSharedProfileBackButton(pilotSelectionBackButton.GetComponent<RectTransform>());

            if (!inventoryActionInProgress && pilotSelectionStatusText != null)
            {
                pilotSelectionStatusText.text = centerUnlocked
                    ? string.Equals(selectedPilotId, centerDefinition.Id, StringComparison.Ordinal) ? "SELECTED" : "TAP CENTER PORTRAIT TO SELECT"
                    : PilotCatalog.GetUnlockRequirementText(profile, centerDefinition.Id);
            }

            if (pilotSelectionAbilitiesText != null)
                pilotSelectionAbilitiesText.text = BuildPilotAbilitiesText(centerDefinition);

            for (int i = 0; i < pilotSelectionCardObjects.Length; i++)
            {
                int offset = i - 1;
                int targetIndex = pilotSelectionCenterIndex + offset;
                bool visible = targetIndex >= 0 && targetIndex < PilotCatalog.AllDefinitions.Count;
                pilotSelectionCardObjects[i].SetActive(visible);
                if (!visible)
                    continue;

                UpdatePilotSelectionCard(i, PilotCatalog.AllDefinitions[targetIndex], i == 1);
            }

            if (pilotSelectionPrevButton != null)
                pilotSelectionPrevButton.gameObject.SetActive(pilotSelectionCenterIndex > 0);
            if (pilotSelectionNextButton != null)
                pilotSelectionNextButton.gameObject.SetActive(pilotSelectionCenterIndex < PilotCatalog.AllDefinitions.Count - 1);

            ApplyPilotSelectionCarouselVisuals();
            pilotSelectionDirty = false;
        }
    }

    void UpdatePilotSelectionCard(int cardIndex, PilotDefinition definition, bool centerCard)
    {
        if (definition == null || pilotSelectionCardObjects == null || cardIndex < 0 || cardIndex >= pilotSelectionCardObjects.Length)
            return;

        RectTransform cardRect = pilotSelectionCardObjects[cardIndex].GetComponent<RectTransform>();
        if (cardRect != null)
        {
            cardRect.anchorMin = new Vector2(0.5f, 1f);
            cardRect.anchorMax = new Vector2(0.5f, 1f);
            cardRect.pivot = new Vector2(0.5f, 1f);
            cardRect.anchoredPosition = centerCard
                ? new Vector2(0f, -162f)
                : new Vector2(cardIndex == 0 ? -560f : 560f, -190f);
            cardRect.sizeDelta = centerCard
                ? new Vector2(560f, 620f)
                : new Vector2(440f, 540f);
        }

        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        bool unlocked = PilotCatalog.IsPilotUnlocked(profile, definition.Id);
        bool selected = string.Equals(selectedPilotId, definition.Id, StringComparison.Ordinal);

        if (pilotSelectionCardImages != null && pilotSelectionCardImages[cardIndex] != null)
        {
            Sprite normalSprite = LoadPilotPortraitSprite(definition);
            pilotSelectionCardImages[cardIndex].sprite = unlocked ? normalSprite : GetGrayscalePilotPortraitSprite(definition, normalSprite);
            pilotSelectionCardImages[cardIndex].color = pilotSelectionCardImages[cardIndex].sprite != null
                ? unlocked ? Color.white : new Color(0.68f, 0.68f, 0.68f, 1f)
                : new Color(1f, 1f, 1f, 0f);
        }

        if (pilotSelectionCardNames != null && pilotSelectionCardNames[cardIndex] != null)
        {
            pilotSelectionCardNames[cardIndex].gameObject.SetActive(false);
            pilotSelectionCardNames[cardIndex].text = string.Empty;
            pilotSelectionCardNames[cardIndex].fontSize = centerCard ? 28f : 22f;
            pilotSelectionCardNames[cardIndex].color = selected
                ? new Color(0.58f, 0.9f, 0.78f, 1f)
                : new Color(0.94f, 0.97f, 1f, 1f);
        }

        if (pilotSelectionCardLockTexts != null && pilotSelectionCardLockTexts[cardIndex] != null)
        {
            pilotSelectionCardLockTexts[cardIndex].gameObject.SetActive(!unlocked);
            pilotSelectionCardLockTexts[cardIndex].text = PilotCatalog.GetUnlockRequirementText(profile, definition.Id);
            pilotSelectionCardLockTexts[cardIndex].fontSize = centerCard ? 22f : 16f;
        }

        Image cardImage = pilotSelectionCardObjects[cardIndex].GetComponent<Image>();
        if (cardImage != null)
        {
            cardImage.color = selected
                ? new Color(0.12f, 0.25f, 0.22f, centerCard ? 0.98f : 0.9f)
                : centerCard
                ? new Color(0.11f, 0.16f, 0.22f, 0.96f)
                : new Color(0.08f, 0.11f, 0.16f, 0.86f);
        }
    }

    void UpdatePilotSelectionCardLayering()
    {
        if (pilotSelectionCardObjects == null)
            return;

        SortSelectionCardsByCenter(pilotSelectionCardObjects, pilotSelectionVisualOffset);

        if (pilotSelectionBackButton != null)
            pilotSelectionBackButton.transform.SetAsLastSibling();
        if (pilotSelectionPrevButton != null && pilotSelectionPrevButton.gameObject.activeSelf)
            pilotSelectionPrevButton.transform.SetAsLastSibling();
        if (pilotSelectionNextButton != null && pilotSelectionNextButton.gameObject.activeSelf)
            pilotSelectionNextButton.transform.SetAsLastSibling();
        if (pilotSelectionAbilitiesText != null)
            pilotSelectionAbilitiesText.transform.SetAsLastSibling();
        if (pilotSelectionStatusText != null)
            pilotSelectionStatusText.transform.SetAsLastSibling();
    }

    string BuildPilotAbilitiesText(PilotDefinition definition)
    {
        if (definition == null)
            return string.Empty;

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(definition.ActiveAbilityDescription))
        {
            builder.Append("<color=#72FF9B><b>ACTIVE - ");
            builder.Append(string.IsNullOrWhiteSpace(definition.ActiveAbilityName) ? "Pilot Ability" : definition.ActiveAbilityName);
            builder.Append(":</b></color> ");
            builder.Append(definition.ActiveAbilityDescription);
        }

        if (definition.AbilityDescriptions == null || definition.AbilityDescriptions.Length == 0)
            return builder.ToString();

        for (int i = 0; i < definition.AbilityDescriptions.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(definition.AbilityDescriptions[i]))
                continue;

            if (builder.Length > 0)
                builder.AppendLine();

            builder.Append("<color=#AFC8FF><b>PASSIVE ");
            builder.Append(i + 1);
            builder.Append(":</b></color> ");
            builder.Append(definition.AbilityDescriptions[i]);
        }

        return builder.ToString();
    }

    void HideShipImageModal()
    {
        if (shipImageModalObject != null)
            shipImageModalObject.SetActive(false);
    }

    void MoveShipSelectionLeft()
    {
        if (inventoryActionInProgress || shipSelectionDragActive || shipSelectionSnapActive)
            return;

        HideShipSelectionMissionDetails();
        ResetShipSelectionCarouselMotion();
        shipSelectionCenterIndex = Mathf.Max(0, shipSelectionCenterIndex - 1);
        shipSelectionCenterType = SelectableShipTypes[shipSelectionCenterIndex];
        RefreshShipSelectionView();
    }

    void MoveShipSelectionRight()
    {
        if (inventoryActionInProgress || shipSelectionDragActive || shipSelectionSnapActive)
            return;

        HideShipSelectionMissionDetails();
        ResetShipSelectionCarouselMotion();
        shipSelectionCenterIndex = Mathf.Min(SelectableShipTypes.Length - 1, shipSelectionCenterIndex + 1);
        shipSelectionCenterType = SelectableShipTypes[shipSelectionCenterIndex];
        RefreshShipSelectionView();
    }

    public void OnShipSelectionSwiped(float horizontalDelta)
    {
        if (inventoryActionInProgress)
            return;

        if (Mathf.Abs(horizontalDelta) < 72f)
            return;

        if (horizontalDelta > 0f)
            MoveShipSelectionLeft();
        else
            MoveShipSelectionRight();
    }

    void OnShipSelectionCardClicked(int cardIndex)
    {
        if (inventoryActionInProgress || shipSelectionDragActive || shipSelectionSnapActive)
            return;

        if (cardIndex < 0 || cardIndex >= shipSelectionCardObjects.Length)
            return;

        if (cardIndex == 1)
        {
            CommitShipSelection(shipSelectionCenterType);
            return;
        }

        int direction = cardIndex == 0 ? -1 : 1;
        HideShipSelectionMissionDetails();
        shipSelectionCenterIndex = Mathf.Clamp(shipSelectionCenterIndex + direction, 0, SelectableShipTypes.Length - 1);
        shipSelectionCenterType = SelectableShipTypes[shipSelectionCenterIndex];
        RefreshShipSelectionView();
    }

    async void CommitShipSelection(ShipType shipType)
    {
        if (!IsShipTypeUnlockedForUi(shipType))
        {
            RefreshShipSelectionView();
            ShowShipSelectionMissionDetails();
            return;
        }

        int targetSkin = GetShipSelectionDisplaySkin(shipType);
        inventoryActionInProgress = true;
        SetInteractable(false);
        if (shipSelectionStatusText != null)
            shipSelectionStatusText.text = "Switching ship...";

        try
        {
            bool changed = await PlayerProfileService.Instance.TryChangeShipSkinAsync(targetSkin);
            if (!changed)
            {
                if (shipSelectionStatusText != null)
                    shipSelectionStatusText.text = "No room in player inventory for extra cargo.";
                RefreshView();
                return;
            }

            selectedSkin = targetSkin;
            shipSelectionSkinByType[shipType] = targetSkin;
            RefreshView();
            SwitchToScreen(ProfileScreen.Home);
        }
        catch (Exception ex)
        {
            Debug.LogError("Ship selection failed: " + ex);
            if (shipSelectionStatusText != null)
                shipSelectionStatusText.text = "Ship change failed.";
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(true);
            RefreshShipSelectionView();
        }
    }

    void SetShipSelectionSkinByButton(int buttonIndex)
    {
        if (!IsShipTypeUnlockedForUi(shipSelectionCenterType))
            return;

        int[] allowedSkins = ShipCatalog.GetSkinsForShipType(shipSelectionCenterType);
        if (buttonIndex < 0 || buttonIndex >= allowedSkins.Length)
            return;

        shipSelectionSkinByType[shipSelectionCenterType] = allowedSkins[buttonIndex];
        RefreshShipSelectionView();
    }

    async void OnViperRepairDonateClicked()
    {
        if (inventoryActionInProgress || !PlayerProfileService.HasInstance)
            return;

        inventoryActionInProgress = true;
        SetInteractable(false);
        if (shipSelectionStatusText != null)
            shipSelectionStatusText.text = "Repairing Viper...";

        try
        {
            bool donated = await PlayerProfileService.Instance.DonateViperRepairPartsAsync();
            if (shipSelectionStatusText != null)
                shipSelectionStatusText.text = donated ? "Viper systems ready for test flights." : "Spare parts required.";

            RefreshView();
        }
        catch (Exception ex)
        {
            Debug.LogError("Viper repair donation failed: " + ex);
            if (shipSelectionStatusText != null)
                shipSelectionStatusText.text = "Repair failed.";
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(true);
            RefreshShipSelectionView();
        }
    }

    int GetShipSelectionDisplaySkin(ShipType shipType)
    {
        if (shipSelectionSkinByType.TryGetValue(shipType, out int storedSkin) && ShipCatalog.GetShipTypeFromSkinIndex(storedSkin) == shipType)
            return storedSkin;

        if (ShipCatalog.GetShipTypeFromSkinIndex(selectedSkin) == shipType)
            return selectedSkin;

        int[] skins = ShipCatalog.GetSkinsForShipType(shipType);
        return skins != null && skins.Length > 0 ? skins[0] : selectedSkin;
    }

    void RefreshShipSelectionView()
    {
        using (ShipSelectionRefreshMarker.Auto())
        {
            if (shipSelectionViewObject == null || shipSelectionCardObjects == null)
                return;

            shipSelectionCenterIndex = Mathf.Clamp(shipSelectionCenterIndex, 0, SelectableShipTypes.Length - 1);
            shipSelectionCenterType = SelectableShipTypes[shipSelectionCenterIndex];

            if (shipSelectionTitleText != null)
            {
                shipSelectionTitleText.gameObject.SetActive(true);
                shipSelectionTitleText.text = ShipCatalog.GetShipTypeDisplayName(shipSelectionCenterType).ToUpperInvariant();
                shipSelectionTitleText.transform.SetAsLastSibling();
            }

            if (shipSelectionBackButton != null)
                LayoutSharedProfileBackButton(shipSelectionBackButton.GetComponent<RectTransform>());

            if (!inventoryActionInProgress && shipSelectionStatusText != null)
                shipSelectionStatusText.text = string.Empty;

            for (int i = 0; i < shipSelectionCardObjects.Length; i++)
            {
                int offset = i - 1;
                int targetIndex = shipSelectionCenterIndex + offset;
                bool visible = targetIndex >= 0 && targetIndex < SelectableShipTypes.Length;
                shipSelectionCardObjects[i].SetActive(visible);
                if (!visible)
                    continue;

                ShipType shipType = SelectableShipTypes[targetIndex];
                UpdateShipSelectionCard(i, shipType, i == 1);
            }

            int[] allowedSkins = ShipCatalog.GetSkinsForShipType(shipSelectionCenterType);
            bool centerShipUnlocked = IsShipTypeUnlockedForUi(shipSelectionCenterType);
            for (int i = 0; i < shipSelectionSkinButtons.Length; i++)
            {
                if (shipSelectionSkinButtons[i] == null)
                    continue;

                bool active = i < allowedSkins.Length;
                shipSelectionSkinButtons[i].gameObject.SetActive(active);
                shipSelectionSkinButtons[i].interactable = active && centerShipUnlocked && inventoryControlsInteractable && !inventoryActionInProgress;
                if (!active)
                    continue;

                bool selected = allowedSkins[i] == GetShipSelectionDisplaySkin(shipSelectionCenterType);
                TMP_Text text = shipSelectionSkinButtons[i].GetComponentInChildren<TMP_Text>(true);
                if (text != null)
                {
                    text.text = ShipCatalog.GetSkinDisplayName(allowedSkins[i]).ToUpperInvariant();
                    text.enableAutoSizing = true;
                    text.fontSizeMin = 12f;
                    text.fontSizeMax = 20f;
                }

                StyleButton(
                    shipSelectionSkinButtons[i],
                    !centerShipUnlocked ? new Color(0.11f, 0.12f, 0.15f, 0.84f) : selected ? new Color(0.2f, 0.38f, 0.58f, 0.98f) : new Color(0.16f, 0.2f, 0.27f, 0.95f),
                    !centerShipUnlocked ? new Color(0.16f, 0.17f, 0.2f, 0.9f) : selected ? new Color(0.28f, 0.5f, 0.74f, 1f) : new Color(0.22f, 0.3f, 0.42f, 1f));
            }

            RefreshShipSelectionMissionDetailsControls();
            ApplyShipSelectionCarouselVisuals();
            shipSelectionDirty = false;
        }
    }

    void UpdateShipSelectionCardLayering()
    {
        if (shipSelectionCardObjects == null)
            return;

        SortSelectionCardsByCenter(shipSelectionCardObjects, shipSelectionVisualOffset);

        if (shipSelectionBackButton != null)
            shipSelectionBackButton.transform.SetAsLastSibling();
        if (shipSelectionSkinButtons != null)
        {
            for (int i = 0; i < shipSelectionSkinButtons.Length; i++)
            {
                if (shipSelectionSkinButtons[i] != null && shipSelectionSkinButtons[i].gameObject.activeSelf)
                    shipSelectionSkinButtons[i].transform.SetAsLastSibling();
            }
        }
        if (shipSelectionCardDonateButtons != null)
        {
            for (int i = 0; i < shipSelectionCardDonateButtons.Length; i++)
            {
                if (shipSelectionCardDonateButtons[i] != null && shipSelectionCardDonateButtons[i].gameObject.activeSelf)
                    shipSelectionCardDonateButtons[i].transform.SetAsLastSibling();
            }
        }
        if (shipSelectionDetailsButton != null && shipSelectionDetailsButton.gameObject.activeSelf)
            shipSelectionDetailsButton.transform.SetAsLastSibling();
        if (shipSelectionStatusText != null)
            shipSelectionStatusText.transform.SetAsLastSibling();
        if (shipMissionDetailsPanelObject != null && shipMissionDetailsPanelObject.activeSelf)
            shipMissionDetailsPanelObject.transform.SetAsLastSibling();
    }

    void UpdateShipSelectionCard(int cardIndex, ShipType shipType, bool centerCard)
    {
        if (shipSelectionCardTitles == null || cardIndex < 0 || cardIndex >= shipSelectionCardTitles.Length)
            return;

        int skinIndex = GetShipSelectionDisplaySkin(shipType);
        PlayerShipDefinition definition = ShipCatalog.GetShipDefinition(shipType);
        bool shipUnlocked = IsShipTypeUnlockedForUi(shipType);
        ViperRecoveryStage viperStage = shipType == ShipType.Viper && PlayerProfileService.HasInstance
            ? PlayerProfileService.Instance.GetViperRecoveryStage()
            : ViperRecoveryStage.Complete;
        bool viperNeedsParts = shipType == ShipType.Viper && viperStage == ViperRecoveryStage.WreckRecovered;
        bool viperTesting = shipType == ShipType.Viper && viperStage == ViperRecoveryStage.Testing;
        RectTransform cardRect = shipSelectionCardObjects[cardIndex] != null ? shipSelectionCardObjects[cardIndex].GetComponent<RectTransform>() : null;
        if (cardRect != null)
        {
            cardRect.anchorMin = new Vector2(0.5f, 1f);
            cardRect.anchorMax = new Vector2(0.5f, 1f);
            cardRect.pivot = new Vector2(0.5f, 1f);
            cardRect.anchoredPosition = centerCard
                ? new Vector2(0f, -72f)
                : new Vector2(cardIndex == 0 ? -590f : 590f, -88f);
            cardRect.sizeDelta = centerCard
                ? new Vector2(920f, 920f)
                : new Vector2(640f, 800f);
        }

        TMP_Text title = shipSelectionCardTitles[cardIndex];
        if (title != null)
        {
            title.text = ShipCatalog.GetShipTypeDisplayName(shipType).ToUpperInvariant();
            title.gameObject.SetActive(false);
        }

        Image image = shipSelectionCardImages[cardIndex];
        if (image != null)
        {
            image.sprite = LoadShipPreviewSprite(skinIndex);
            image.color = image.sprite != null
                ? shipUnlocked ? Color.white : new Color(0.34f, 0.34f, 0.34f, centerCard ? 0.72f : 0.56f)
                : new Color(1f, 1f, 1f, 0f);
            RectTransform imageRect = image.rectTransform;
            imageRect.anchoredPosition = centerCard ? new Vector2(48f, -64f) : new Vector2(30f, -48f);
            imageRect.sizeDelta = centerCard ? new Vector2(680f, 760f) : new Vector2(470f, 540f);
        }

        TMP_Text lockText = shipSelectionCardLockTexts != null && cardIndex < shipSelectionCardLockTexts.Length
            ? shipSelectionCardLockTexts[cardIndex]
            : null;
        if (lockText != null)
        {
            bool showProgressText = !shipUnlocked || viperTesting;
            lockText.gameObject.SetActive(showProgressText);
            lockText.text = BuildShipSelectionCardStatusText(shipType, shipUnlocked, viperStage);
            lockText.enableAutoSizing = false;
            lockText.fontSize = centerCard ? 24f : 18f;
            lockText.fontSizeMin = lockText.fontSize;
            lockText.fontSizeMax = lockText.fontSize;
            lockText.lineSpacing = centerCard ? 4f : 2f;
            lockText.textWrappingMode = TextWrappingModes.Normal;
            lockText.overflowMode = TextOverflowModes.Truncate;
            lockText.margin = centerCard
                ? new Vector4(18f, 8f, 18f, 8f)
                : new Vector4(12f, 6f, 12f, 6f);
            RectTransform lockRect = lockText.rectTransform;
            lockRect.anchoredPosition = centerCard ? new Vector2(18f, -92f) : new Vector2(10f, -72f);
            lockRect.sizeDelta = centerCard ? new Vector2(660f, 154f) : new Vector2(430f, 118f);
            lockText.color = new Color(1f, 0.82f, 0.48f, 0.98f);
            lockText.transform.SetAsLastSibling();
        }

        Button donateButton = shipSelectionCardDonateButtons != null && cardIndex < shipSelectionCardDonateButtons.Length
            ? shipSelectionCardDonateButtons[cardIndex]
            : null;
        if (donateButton != null)
        {
            donateButton.gameObject.SetActive(viperNeedsParts);
            donateButton.interactable = viperNeedsParts &&
                                        centerCard &&
                                        inventoryControlsInteractable &&
                                        !inventoryActionInProgress &&
                                        PlayerProfileService.Instance.IsViperRepairPartsDonationAvailable();
            RectTransform donateRect = donateButton.GetComponent<RectTransform>();
            if (donateRect != null)
            {
                donateRect.anchoredPosition = centerCard ? new Vector2(18f, -238f) : new Vector2(10f, -184f);
                donateRect.sizeDelta = centerCard ? new Vector2(118f, 118f) : new Vector2(92f, 92f);
            }

            ConfigureViperRepairButtonLabel(donateButton, centerCard);
            StyleButton(
                donateButton,
                donateButton.interactable ? new Color(0.12f, 0.38f, 0.22f, 0.98f) : new Color(0.16f, 0.17f, 0.18f, 0.86f),
                donateButton.interactable ? new Color(0.18f, 0.58f, 0.34f, 1f) : new Color(0.22f, 0.24f, 0.25f, 0.9f));
        }

        Image cardImage = shipSelectionCardObjects[cardIndex].GetComponent<Image>();
        if (cardImage != null)
        {
            cardImage.color = !shipUnlocked
                ? new Color(0.045f, 0.05f, 0.06f, centerCard ? 0.82f : 0.72f)
                : centerCard
                ? new Color(0.08f, 0.11f, 0.16f, 0.76f)
                : new Color(0.07f, 0.1f, 0.15f, 0.68f);
        }

        TMP_Text[] statLabels = shipSelectionCardStatLabelTexts != null && cardIndex < shipSelectionCardStatLabelTexts.Length
            ? shipSelectionCardStatLabelTexts[cardIndex]
            : null;
        TMP_Text[] statValues = shipSelectionCardStatValueTexts != null && cardIndex < shipSelectionCardStatValueTexts.Length
            ? shipSelectionCardStatValueTexts[cardIndex]
            : null;
        Image[] statFills = shipSelectionCardStatFillImages != null && cardIndex < shipSelectionCardStatFillImages.Length
            ? shipSelectionCardStatFillImages[cardIndex]
            : null;

        if (statLabels != null && statValues != null && statFills != null)
        {
            LayoutShipSelectionStats(cardIndex, centerCard);
            SetShipSelectionStatCard(statLabels, statValues, statFills, 0, ShipStatLabels[0], definition.BaseHp.ToString(), NormalizeShipStat(definition.BaseHp, stat => stat.BaseHp));
            SetShipSelectionStatCard(statLabels, statValues, statFills, 1, ShipStatLabels[1], definition.BaseShield.ToString(), NormalizeShipStat(definition.BaseShield, stat => stat.BaseShield));
            SetShipSelectionStatCard(statLabels, statValues, statFills, 2, ShipStatLabels[2], definition.BaseSpeed.ToString("0.0"), NormalizeShipStat(definition.BaseSpeed, stat => stat.BaseSpeed));
            SetShipSelectionStatCard(statLabels, statValues, statFills, 3, ShipStatLabels[3], "x" + definition.TurnRateMultiplier.ToString("0.00"), NormalizeShipStat(definition.TurnRateMultiplier, stat => stat.TurnRateMultiplier));
            SetShipSelectionStatCard(statLabels, statValues, statFills, 4, ShipStatLabels[4], definition.BoosterDuration.ToString("0.0") + "s", NormalizeShipStat(definition.BoosterDuration, stat => stat.BoosterDuration));
            SetShipSelectionStatCard(statLabels, statValues, statFills, 5, ShipStatLabels[5], "+" + definition.MaxBoostPercent + "%", NormalizeShipStat(definition.MaxBoostPercent, stat => stat.MaxBoostPercent));
            SetShipSelectionStatCard(statLabels, statValues, statFills, 6, ShipStatLabels[6], definition.CargoCapacity.ToString(), NormalizeShipStat(definition.CargoCapacity, stat => stat.CargoCapacity));
            SetShipSelectionStatCard(statLabels, statValues, statFills, 7, ShipStatLabels[7], definition.SafePocketSlots.ToString(), NormalizeSafePocketStat(definition.SafePocketSlots));
            SetShipSelectionStatCard(statLabels, statValues, statFills, 8, ShipStatLabels[8], definition.BrakingDriftLevel.ToString(), NormalizeBrakingDriftStat(definition.BrakingDriftLevel));
        }

        GameObject[] slotObjects = shipSelectionCardSlotObjects != null && cardIndex < shipSelectionCardSlotObjects.Length
            ? shipSelectionCardSlotObjects[cardIndex]
            : null;
        if (slotObjects != null)
        {
            Vector2[] slotLayout = BuildShipSelectionSlotLayout(centerCard);
            for (int i = 0; i < slotObjects.Length && i < slotLayout.Length; i++)
            {
                if (slotObjects[i] == null)
                    continue;

                bool slotDefined = ShipCatalog.IsEquipmentSlotEnabled(i, skinIndex);
                slotObjects[i].SetActive(slotDefined);
                if (!slotDefined)
                    continue;
                bool slotUnlocked = !viperTesting || PlayerProfileService.Instance.IsEquipmentSlotEnabledForProfile(i, skinIndex);

                RectTransform slotRect = slotObjects[i].GetComponent<RectTransform>();
                if (slotRect != null)
                {
                    slotRect.anchoredPosition = slotLayout[i];
                    slotRect.sizeDelta = GetShipSelectionSlotSize(centerCard);
                }

                Image slotImage = slotObjects[i].GetComponent<Image>();
                if (slotImage != null)
                    slotImage.color = slotUnlocked ? GetShipSelectionSlotColor(i) : new Color(0.32f, 0.06f, 0.08f, 0.94f);

                Outline outline = slotObjects[i].GetComponent<Outline>();
                if (outline != null)
                {
                    outline.effectColor = slotUnlocked ? GetShipSelectionSlotOutlineColor(i) : new Color(0.98f, 0.16f, 0.18f, 0.96f);
                    outline.effectDistance = new Vector2(2.2f, -2.2f);
                }

                TMP_Text slotText = slotObjects[i].GetComponentInChildren<TMP_Text>(true);
                ApplyEquipmentSlotPreviewTextStyle(slotText);
                if (slotText != null && !slotUnlocked)
                {
                    slotText.text = "X";
                    slotText.fontSize = centerCard ? 30f : 24f;
                    slotText.fontSizeMax = slotText.fontSize;
                    slotText.color = new Color(1f, 0.24f, 0.24f, 1f);
                }
                else if (slotText != null)
                {
                    slotText.text = GetShipSelectionSlotLabel(i);
                }
            }
        }
    }

    string BuildShipSelectionCardStatusText(ShipType shipType, bool shipUnlocked, ViperRecoveryStage viperStage)
    {
        if (shipType == ShipType.Viper)
        {
            if (viperStage == ViperRecoveryStage.WreckRecovered)
                return "VIPER REPAIR\nParts checklist in details";

            if (viperStage == ViperRecoveryStage.Testing)
                return "VIPER TEST FLIGHTS\nStabilize locked systems";

            if (!shipUnlocked)
                return "LOCKED\nRecover Viper wreck";
        }

        if (shipType == ShipType.Avenger && !shipUnlocked)
            return "LOCKED\nRecover Avenger";

        if (shipType == ShipType.CargoTruck && !shipUnlocked && PlayerProfileService.HasInstance)
        {
            int delivered = PlayerProfileService.Instance.GetBisonIndustrialPartsDeliveredCount();
            return "BISON HAUL\nIndustrial parts " + delivered + "/" + PlayerProfileService.BisonIndustrialPartsRequired;
        }

        if (shipType == ShipType.Invader && !shipUnlocked && PlayerProfileService.HasInstance)
        {
            int imprints = PlayerProfileService.Instance.GetInvaderImprintsRecoveredCount();
            return "INVADER DATA\nAlien imprints " + imprints + "/" + PlayerProfileService.InvaderImprintsRequired;
        }

        if (shipType == ShipType.Pathfinder && !shipUnlocked && PlayerProfileService.HasInstance)
        {
            PathfinderResearchProgressData progress = PlayerProfileService.Instance.GetPathfinderResearchProgress();
            PathfinderResearchStage pathfinderStage = (PathfinderResearchStage)Mathf.Clamp(progress.Stage, (int)PathfinderResearchStage.Locked, (int)PathfinderResearchStage.Complete);
            int hackedCount = progress.HackedShipTypeIds != null
                ? Mathf.Clamp(progress.HackedShipTypeIds.Length, 0, PlayerProfileService.PathfinderHackedShipTypesRequired)
                : 0;
            int deliveredValuables = Mathf.Clamp(progress.DeliveredValuableItems, 0, PlayerProfileService.PathfinderValuableItemsRequired);

            if (pathfinderStage == PathfinderResearchStage.DocumentationReady)
                return "PATHFINDER RESEARCH\nDeliver documentation";

            if (pathfinderStage == PathfinderResearchStage.ResourcesRequired)
                return "PATHFINDER RESEARCH\nValuables: " + deliveredValuables + "/" + PlayerProfileService.PathfinderValuableItemsRequired;

            if (pathfinderStage == PathfinderResearchStage.FinalVisitRequired)
                return "PATHFINDER RESEARCH\nVisit Research Station";

            return "PATHFINDER RESEARCH\nShip data: " + hackedCount + "/" + PlayerProfileService.PathfinderHackedShipTypesRequired;
        }

        if (shipType == ShipType.Arrow && !shipUnlocked && PlayerProfileService.HasInstance)
        {
            ArrowLicenseProgressData progress = PlayerProfileService.Instance.GetArrowLicenseProgress();
            int qualificationCount = Mathf.Clamp(progress.QualifierChips, 0, PlayerProfileService.ArrowQualifierChipsRequired);
            int completedMapCount = PlayerProfileService.CountCompletedArrowRaceMaps(progress);
            return "ARROW LICENSE\nQualifiers " + qualificationCount + "/" + PlayerProfileService.ArrowQualifierChipsRequired +
                   ", maps " + completedMapCount + "/" + PlayerProfileService.ArrowMapRacesRequired;
        }

        return shipUnlocked ? string.Empty : "LOCKED";
    }

    string BuildShipSelectionProgressText(ShipType shipType, bool shipUnlocked, ViperRecoveryStage viperStage)
    {
        if (shipType == ShipType.Viper)
        {
            if (viperStage == ViperRecoveryStage.WreckRecovered)
            {
                int neutral = PlayerProfileService.Instance.CountViperRepairPartItem(InventoryItemCatalog.NeutralFighterSalvageId);
                int drones = PlayerProfileService.Instance.CountViperRepairPartItem(InventoryItemCatalog.DroidScrapId);
                int trucks = PlayerProfileService.Instance.CountViperRepairPartItem(InventoryItemCatalog.SpaceTruckWreckId);
                return "Repair the recovered Viper wreck before it can fly safely.\n\n" +
                       "Collect the required spare parts during missions. Keep them in your inventory or ship cargo, then return here and use the repair button when the checklist is complete.\n\n" +
                       "Parts checklist:\n" +
                       neutral + "/" + PlayerProfileService.ViperNeutralFighterWrecksRequired + " Neutral Fighter wrecks\n" +
                       drones + "/" + PlayerProfileService.ViperDroneWrecksRequired + " Drone wrecks\n" +
                       trucks + "/" + PlayerProfileService.ViperSpaceTruckWrecksRequired + " Space Truck wrecks";
            }

            if (viperStage == ViperRecoveryStage.Testing)
            {
                ViperRecoveryProgressData progress = PlayerProfileService.Instance.GetViperRecoveryProgress();
                int subsystemTotal = PlayerProfileService.GetAllViperTestSubsystemIds().Length;
                int unlockedSubsystems = progress.UnlockedSubsystemIds != null
                    ? Mathf.Clamp(progress.UnlockedSubsystemIds.Length, 0, subsystemTotal)
                    : 0;
                int remainingSubsystems = Mathf.Max(0, subsystemTotal - unlockedSubsystems);
                int estimatedFlights = Mathf.CeilToInt(remainingSubsystems / (float)PlayerProfileService.ViperTestFlightSubsystemUnlocksPerReturn);
                return "The Viper is repaired, but its systems still need flight testing.\n\n" +
                       "Start rounds with Viper and survive at least " + Mathf.RoundToInt(PlayerProfileService.ViperMinimumTestFlightSeconds) + " seconds before returning. Each valid return stabilizes up to " + PlayerProfileService.ViperTestFlightSubsystemUnlocksPerReturn + " locked systems.\n\n" +
                       "Systems stabilized: " + unlockedSubsystems + "/" + subsystemTotal + "\n" +
                       "Estimated successful test flights left: " + estimatedFlights;
            }

            if (!shipUnlocked)
                return "Recover the Viper wreck during a mission.\n\n" +
                       "After the wreck is recovered, this screen will show the repair checklist and the parts needed to rebuild it.";
        }

        if (shipType == ShipType.Avenger)
            return "Recover Avenger during its unlock mission.\n\n" +
                   "Bring it back safely to add it to your ship roster.";

        if (shipType == ShipType.CargoTruck && PlayerProfileService.HasInstance)
        {
            int delivered = PlayerProfileService.Instance.GetBisonIndustrialPartsDeliveredCount();
            return "Earn Bison by completing industrial haul missions.\n\n" +
                   "When the Industrial Zone event appears, pick up industrial parts and escape through the Extraction Zone while carrying them.\n\n" +
                   "Industrial parts delivered: " + delivered + "/" + PlayerProfileService.BisonIndustrialPartsRequired;
        }

        if (shipType == ShipType.Invader && PlayerProfileService.HasInstance)
        {
            int imprints = PlayerProfileService.Instance.GetInvaderImprintsRecoveredCount();
            return "Unlock Invader by recovering alien imprints from Invader events.\n\n" +
                   "Follow the active alien objective in the round, such as contact, stabilize, or sync. Escape after the objective is complete to preserve the imprint.\n\n" +
                   "Alien imprints recovered: " + imprints + "/" + PlayerProfileService.InvaderImprintsRequired;
        }

        if (shipType == ShipType.Pathfinder && PlayerProfileService.HasInstance)
        {
            PathfinderResearchProgressData progress = PlayerProfileService.Instance.GetPathfinderResearchProgress();
            PathfinderResearchStage pathfinderStage = (PathfinderResearchStage)Mathf.Clamp(progress.Stage, (int)PathfinderResearchStage.Locked, (int)PathfinderResearchStage.Complete);
            if (pathfinderStage == PathfinderResearchStage.Complete)
                return string.Empty;

            int hackedCount = progress.HackedShipTypeIds != null
                ? Mathf.Clamp(progress.HackedShipTypeIds.Length, 0, PlayerProfileService.PathfinderHackedShipTypesRequired)
                : 0;
            int deliveredValuables = Mathf.Clamp(progress.DeliveredValuableItems, 0, PlayerProfileService.PathfinderValuableItemsRequired);

            if (pathfinderStage == PathfinderResearchStage.DocumentationReady)
                return "You have enough ship data to prepare prototype documentation.\n\n" +
                       "Deliver Ship Prototype Documentation to a Research Station. Keep the package safe until the station accepts it.";

            if (pathfinderStage == PathfinderResearchStage.ResourcesRequired)
            {
                return "The Research Station needs valuable items to continue Pathfinder work.\n\n" +
                       "Deliver valuable cargo to a Research Station. Accepted examples include Legendary Asteroid, Cash Suitcase, and Pirate Case.\n\n" +
                       "Valuable items delivered: " + deliveredValuables + "/" + PlayerProfileService.PathfinderValuableItemsRequired;
            }

            if (pathfinderStage == PathfinderResearchStage.FinalVisitRequired)
                return "Pathfinder research is almost complete.\n\n" +
                       "Visit another Research Station to finalize the project and unlock the ship.";

            return "Collect research data by hacking different ship types during missions.\n\n" +
                   "Each unique ship type counts once, so look for variety instead of repeating the same target.\n\n" +
                   "Ship data collected: " + hackedCount + "/" + PlayerProfileService.PathfinderHackedShipTypesRequired;
        }

        if (shipType == ShipType.Arrow && PlayerProfileService.HasInstance)
        {
            ArrowLicenseProgressData progress = PlayerProfileService.Instance.GetArrowLicenseProgress();
            ArrowLicenseStage arrowStage = (ArrowLicenseStage)Mathf.Clamp(progress.Stage, (int)ArrowLicenseStage.Locked, (int)ArrowLicenseStage.Complete);
            if (arrowStage == ArrowLicenseStage.Complete)
                return string.Empty;

            int qualificationCount = Mathf.Clamp(progress.QualifierChips, 0, PlayerProfileService.ArrowQualifierChipsRequired);
            int completedMapCount = PlayerProfileService.CountCompletedArrowRaceMaps(progress);
            string partsProgress = "Ion Nozzle: " + FormatDelivered(progress.IonNozzleDelivered) + ", " +
                                   "Gyro Stabilizer: " + FormatDelivered(progress.GyroStabilizerDelivered) + ", " +
                                   "Race Transponder: " + FormatDelivered(progress.RaceTransponderDelivered);
            string bestRank = FormatArrowRank(progress.BestTimeTrialRank);

            return "Arrow Racing License\n" +
                   "Complete the racing license chain to unlock Arrow.\n\n" +
                   "1. Complete qualification races: " + qualificationCount + "/" + PlayerProfileService.ArrowQualifierChipsRequired + "\n" +
                   "2. Collect Arrow Race Tokens from AI players and race on different maps: " + completedMapCount + "/" + PlayerProfileService.ArrowMapRacesRequired + "\n" +
                   "3. Deliver racing parts: " + partsProgress + "\n" +
                   "4. Finish the time trial with rank " + ((ArrowTimeTrialRank)PlayerProfileService.ArrowTimeTrialMinimumRank).ToString() + " or better. Best rank: " + bestRank + "\n" +
                   "5. Win the ghost race: " + FormatDelivered(progress.GhostRaceWon) + "\n" +
                   "6. Finish the Final Race with Arrow and escape.";
        }

        return "LOCKED";
    }

    string FormatDelivered(bool delivered)
    {
        return delivered ? "OK" : "-";
    }

    string FormatArrowRank(int rank)
    {
        ArrowTimeTrialRank safeRank = (ArrowTimeTrialRank)Mathf.Clamp(rank, (int)ArrowTimeTrialRank.None, (int)ArrowTimeTrialRank.S);
        return safeRank == ArrowTimeTrialRank.None ? "-" : safeRank.ToString();
    }

    void LayoutShipSelectionStats(int cardIndex, bool centerCard)
    {
        LayoutShipSelectionStats(cardIndex, centerCard ? 1f : 0f);
    }

    void LayoutShipSelectionStats(int cardIndex, float centerAmount)
    {
        TMP_Text[] statLabels = shipSelectionCardStatLabelTexts != null && cardIndex < shipSelectionCardStatLabelTexts.Length
            ? shipSelectionCardStatLabelTexts[cardIndex]
            : null;
        if (statLabels == null)
            return;

        centerAmount = Mathf.Clamp01(centerAmount);
        float x = Mathf.Lerp(186f, 260f, centerAmount);
        float topY = Mathf.Lerp(-130f, -144f, centerAmount);
        Vector2 cardSize = Vector2.Lerp(new Vector2(182f, 46f), new Vector2(236f, 58f), centerAmount);
        float spacing = Mathf.Lerp(10f, 12f, centerAmount);
        float labelTop = Mathf.Lerp(-10f, -12f, centerAmount);
        float textHeight = Mathf.Lerp(16f, 20f, centerAmount);
        float statFontSize = Mathf.Lerp(14f, 18f, centerAmount);

        for (int i = 0; i < statLabels.Length; i++)
        {
            TMP_Text label = statLabels[i];
            TMP_Text value = shipSelectionCardStatValueTexts != null && cardIndex < shipSelectionCardStatValueTexts.Length && i < shipSelectionCardStatValueTexts[cardIndex].Length
                ? shipSelectionCardStatValueTexts[cardIndex][i]
                : null;
            if (label == null)
                continue;

            RectTransform cardRect = label.transform.parent != null ? label.transform.parent.GetComponent<RectTransform>() : null;
            if (cardRect == null)
                continue;

            cardRect.anchorMin = new Vector2(0.5f, 1f);
            cardRect.anchorMax = new Vector2(0.5f, 1f);
            cardRect.pivot = new Vector2(0.5f, 1f);
            cardRect.anchoredPosition = new Vector2(x, topY - (i * (cardSize.y + spacing)));
            cardRect.sizeDelta = cardSize;

            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 1f);
            labelRect.anchoredPosition = new Vector2(12f, labelTop);
            labelRect.sizeDelta = new Vector2(cardSize.x * 0.48f, textHeight);
            label.fontSize = statFontSize;

            if (value != null)
            {
                RectTransform valueRect = value.rectTransform;
                valueRect.anchorMin = new Vector2(1f, 1f);
                valueRect.anchorMax = new Vector2(1f, 1f);
                valueRect.pivot = new Vector2(1f, 1f);
                valueRect.anchoredPosition = new Vector2(Mathf.Lerp(-8f, -10f, centerAmount), labelTop);
                valueRect.sizeDelta = new Vector2(cardSize.x * 0.42f, textHeight);
                value.fontSize = statFontSize;
            }

            Transform barBgTransform = cardRect.Find("BarBg");
            if (barBgTransform != null)
            {
                RectTransform barBgRect = barBgTransform.GetComponent<RectTransform>();
                if (barBgRect != null)
                {
                    barBgRect.anchorMin = new Vector2(0f, 0f);
                    barBgRect.anchorMax = new Vector2(1f, 0f);
                    barBgRect.pivot = new Vector2(0.5f, 0f);
                    barBgRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(8f, 10f, centerAmount));
                    barBgRect.sizeDelta = new Vector2(-18f, Mathf.Lerp(10f, 14f, centerAmount));
                }
            }
        }
    }

    void SetShipSelectionStatCard(TMP_Text[] labels, TMP_Text[] values, Image[] fills, int index, string label, string valueText, float normalized)
    {
        if (labels == null || values == null || fills == null || index < 0 || index >= labels.Length || index >= values.Length || index >= fills.Length)
            return;

        if (labels[index] != null)
            labels[index].text = label;
        if (values[index] != null)
            values[index].text = valueText;

        Image fillImage = fills[index];
        if (fillImage == null)
            return;

        float clamped = Mathf.Clamp01(normalized);
        RectTransform fillRect = fillImage.rectTransform;
        if (fillRect != null)
        {
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(clamped, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
        }

        fillImage.color = EvaluateShipStatColor(clamped);
    }

    void RefreshEquipmentSlotPreview()
    {
        if (equipmentSlotPreviewTexts == null || equipmentSlotPreviewTexts.Length < PlayerInventoryData.EquipmentSlotCount)
            return;

        PlayerInventoryData inventory = PlayerProfileService.Instance.CurrentProfile != null
            ? PlayerProfileService.Instance.CurrentProfile.Inventory
            : null;

        for (int i = 0; i < PlayerInventoryData.EquipmentSlotCount; i++)
        {
            bool enabled = inventory != null && IsEquipmentSlotEnabledForSelectedSkin(i);
            string itemId = inventory != null && inventory.EquipmentSlots != null && i < inventory.EquipmentSlots.Length
                ? inventory.EquipmentSlots[i]
                : null;
            SetEquipmentSlotState(i, enabled, GetEquipmentSlotLabel(i), itemId);
        }
    }

    void SetEquipmentSlotState(int slotIndex, bool enabled, string label, string itemId)
    {
        TMP_Text text = equipmentSlotPreviewTexts != null && slotIndex >= 0 && slotIndex < equipmentSlotPreviewTexts.Length
            ? equipmentSlotPreviewTexts[slotIndex]
            : null;
        Image icon = equipmentSlotPreviewIcons != null && slotIndex >= 0 && slotIndex < equipmentSlotPreviewIcons.Length
            ? equipmentSlotPreviewIcons[slotIndex]
            : null;
        Button button = equipmentSlotButtons != null && slotIndex >= 0 && slotIndex < equipmentSlotButtons.Length
            ? equipmentSlotButtons[slotIndex]
            : null;

        if (button != null)
            button.gameObject.SetActive(enabled);

        if (!enabled)
        {
            if (text != null)
                text.text = string.Empty;

            if (icon != null)
            {
                icon.sprite = null;
                icon.enabled = false;
            }

            return;
        }

        if (text == null)
            return;

        ApplyEquipmentSlotPreviewTextStyle(text);

        bool occupied = enabled && !string.IsNullOrWhiteSpace(itemId);
        Sprite itemSprite = occupied ? InventoryItemCatalog.GetIcon(itemId) : null;
        bool showDefaultWeaponPlaceholder = !occupied && InventoryItemCatalog.GetEquipmentSlotCategory(slotIndex) == InventoryItemCategory.Weapon;
        Sprite placeholderSprite = showDefaultWeaponPlaceholder ? WeaponAttackCatalog.GetWeaponIcon(WeaponAttackCatalog.SimpleGunId) : null;

        text.text = occupied && itemSprite == null ? InventoryItemCatalog.GetShortLabel(itemId) : label;
        text.color = new Color(0.92f, 0.96f, 0.98f, 0.98f);
        Image bg = text.transform.parent != null ? text.transform.parent.GetComponent<Image>() : null;
        if (bg != null)
            bg.color = GetShipSelectionSlotColor(slotIndex);

        Outline outline = text.transform.parent != null ? text.transform.parent.GetComponent<Outline>() : null;
        if (outline != null)
        {
            outline.effectColor = GetShipSelectionSlotOutlineColor(slotIndex);
            outline.effectDistance = new Vector2(2.2f, -2.2f);
            outline.enabled = true;
        }

        if (icon != null)
        {
            icon.sprite = occupied ? itemSprite : placeholderSprite;
            icon.enabled = occupied
                ? itemSprite != null
                : placeholderSprite != null;
            icon.color = occupied ? Color.white : new Color(1f, 1f, 1f, 0.28f);
            icon.transform.SetAsLastSibling();
        }

        if (button != null)
            button.interactable = preserveInventoryButtonVisualsDuringSave || !inventoryActionInProgress;
    }

    string GetEquipmentSlotLabel(int slotIndex)
    {
        return ShipCatalog.GetEquipmentSlotLabel(slotIndex);
    }
}
