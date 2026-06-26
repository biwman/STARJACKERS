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
    async void SetSelectedShipType(ShipType shipType)
    {
        if (!IsShipTypeUnlockedForUi(shipType))
        {
            SetStatus(ShipCatalog.GetShipTypeDisplayName(shipType).ToUpperInvariant() + " locked.");
            RefreshView();
            return;
        }

        int[] allowedSkins = ShipCatalog.GetSkinsForShipType(shipType);
        int targetSkin = System.Array.IndexOf(allowedSkins, selectedSkin) >= 0 ? selectedSkin : allowedSkins[0];
        if (inventoryActionInProgress)
            return;

        inventoryActionInProgress = true;
        SetInteractable(false);
        SetStatus("Switching ship...");

        try
        {
            bool changed = await PlayerProfileService.Instance.TryChangeShipSkinAsync(targetSkin);
            if (!changed)
            {
                SetStatus("No room in player inventory for extra cargo.");
                RefreshView();
                return;
            }

            selectedSkin = targetSkin;
            RefreshView();
            SetStatus("Ship changed.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Ship switch failed: " + ex);
            SetStatus("Ship change failed.");
            RefreshView();
        }
        finally
        {
            inventoryActionInProgress = false;
            SetInteractable(true);
        }
    }

    void OnExitGameClicked()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void ApplySkinChoiceByButtonIndex(int buttonIndex)
    {
        if (!IsShipTypeUnlockedForUi(GetSelectedShipType()))
            return;

        int[] allowedSkins = ShipCatalog.GetSkinsForShipType(GetSelectedShipType());
        if (buttonIndex < 0 || buttonIndex >= allowedSkins.Length)
            return;

        selectedSkin = allowedSkins[buttonIndex];
        UpdateShipTypeButtonVisuals();
        UpdateSkinButtonsForSelectedShip();
        UpdateSkinButtonVisuals();
        RefreshShipPreview();
    }

    void UpdateShipTypeButtonVisuals()
    {
        if (shipTypeButtons == null)
            return;

        ShipType selectedType = GetSelectedShipType();
        for (int i = 0; i < shipTypeButtons.Length; i++)
        {
            if (shipTypeButtons[i] == null)
                continue;

            Image image = shipTypeButtons[i].GetComponent<Image>();
            if (image != null)
            {
                bool isSelected = i < SelectableShipTypes.Length && SelectableShipTypes[i] == selectedType;
                bool locked = i < SelectableShipTypes.Length && !IsShipTypeUnlockedForUi(SelectableShipTypes[i]);
                image.color = locked
                    ? new Color(0.13f, 0.14f, 0.16f, 0.86f)
                    : isSelected
                    ? new Color(0.19f, 0.61f, 0.5f, 0.98f)
                    : new Color(0.16f, 0.2f, 0.27f, 0.95f);
            }
        }
    }

    void UpdateSkinButtonsForSelectedShip()
    {
        if (skinButtons == null)
            return;

        ShipType shipType = GetSelectedShipType();
        int[] allowedSkins = ShipCatalog.GetSkinsForShipType(shipType);
        bool shipUnlocked = IsShipTypeUnlockedForUi(shipType);

        if (shipTypeLabelText != null)
            shipTypeLabelText.text = "SHIP: " + ShipCatalog.GetShipTypeDisplayName(shipType).ToUpperInvariant();

        if (shipSkinLabelText != null)
            shipSkinLabelText.text = "SHIP SKIN (" + ShipCatalog.GetShipTypeDisplayName(shipType).ToUpperInvariant() + ")";

        for (int i = 0; i < skinButtons.Length; i++)
        {
            if (skinButtons[i] == null)
                continue;

            bool active = i < allowedSkins.Length;
            skinButtons[i].gameObject.SetActive(active);
            skinButtons[i].interactable = active && shipUnlocked && inventoryControlsInteractable && !inventoryActionInProgress;
            if (!active)
                continue;

            TMP_Text text = skinButtons[i].GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = ShipCatalog.GetSkinDisplayName(allowedSkins[i]).ToUpperInvariant();
        }
    }

    void UpdateSkinButtonVisuals()
    {
        if (skinButtons == null)
            return;

        int[] allowedSkins = ShipCatalog.GetSkinsForShipType(GetSelectedShipType());
        bool shipUnlocked = IsShipTypeUnlockedForUi(GetSelectedShipType());

        for (int i = 0; i < skinButtons.Length; i++)
        {
            if (skinButtons[i] == null)
                continue;

            if (i >= allowedSkins.Length)
                continue;

            Image image = skinButtons[i].GetComponent<Image>();
            if (image != null)
            {
                image.color = !shipUnlocked
                    ? new Color(0.12f, 0.13f, 0.15f, 0.82f)
                    : allowedSkins[i] == selectedSkin
                    ? new Color(0.19f, 0.61f, 0.5f, 0.98f)
                    : new Color(0.16f, 0.2f, 0.27f, 0.95f);
            }
        }
    }

    void ApplySaveAndRunButtonStyle()
    {
        if (saveAndRunButton == null)
            return;

        Image image = saveAndRunButton.GetComponent<Image>();
        if (image != null)
            image.raycastTarget = true;

        TMP_Text text = saveAndRunButton.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = "PLAY";
        }
    }

    void ApplyItemPreviewLayout()
    {
        if (itemPreviewPanelObject == null)
            return;

        RectTransform rect = itemPreviewPanelObject.GetComponent<RectTransform>();
        if (rect == null)
            return;

        Transform targetParent = panelObject.transform;
        Vector2 anchoredPosition = new Vector2(-366f, -108f);
        Vector2 size = new Vector2(304f, 326f);

        if (currentScreen == ProfileScreen.Crafting && craftingViewRootObject != null)
        {
            targetParent = craftingViewRootObject.transform;
            anchoredPosition = new Vector2(-24f, -158f);
            size = new Vector2(304f, 326f);
        }
        else if (currentScreen == ProfileScreen.Inventory && inventoryViewRootObject != null)
        {
            targetParent = inventoryViewRootObject.transform;
            anchoredPosition = new Vector2(-154f, -182f);
            size = new Vector2(304f, 326f);
        }
        else if (currentScreen == ProfileScreen.Trader && traderViewRootObject != null)
        {
            targetParent = traderViewRootObject.transform;
            anchoredPosition = new Vector2(-204f, -132f);
            size = new Vector2(304f, 326f);
        }

        if (itemPreviewPanelObject.transform.parent != targetParent)
            itemPreviewPanelObject.transform.SetParent(targetParent, false);

        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    void UpdateEquipmentSlotLayout()
    {
        if (equipmentSlotRects == null || equipmentSlotRects.Length < PlayerInventoryData.EquipmentSlotCount)
            return;

        for (int i = 0; i < equipmentSlotRects.Length && i < EquipmentSlotLayoutPositions.Length; i++)
        {
            RectTransform rect = equipmentSlotRects[i];
            if (rect == null)
                continue;

            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = EquipmentSlotLayoutPositions[i];
            rect.sizeDelta = new Vector2(EquipmentSlotPreviewSize, EquipmentSlotPreviewSize);
        }

        ApplyEquipmentSlotPreviewSizing();
    }

    void LayoutEquipmentSlotsColumn(float leftX, float rightX, float topY, float rowSpacing, float slotSize)
    {
        if (equipmentSlotRects == null || equipmentSlotRects.Length < PlayerInventoryData.EquipmentSlotCount)
            return;

        int[][] rowOrder =
        {
            new[] { 0, 1 },
            new[] { 2, 3 },
            new[] { 4, 5 },
            new[] { 8, 9 },
            new[] { 10, 11 },
            new[] { 6, 7 }
        };

        for (int row = 0; row < rowOrder.Length; row++)
        {
            for (int col = 0; col < rowOrder[row].Length; col++)
            {
                int slotIndex = rowOrder[row][col];
                if (slotIndex < 0 || slotIndex >= equipmentSlotRects.Length)
                    continue;

                RectTransform rect = equipmentSlotRects[slotIndex];
                if (rect == null)
                    continue;

                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(col == 0 ? leftX : rightX, topY - (row * rowSpacing));
                rect.sizeDelta = new Vector2(slotSize, slotSize);
            }
        }

        ApplyEquipmentSlotPreviewSizing();
    }

    void ApplyEquipmentSlotPreviewSizing()
    {
        if (equipmentSlotPreviewIcons != null)
        {
            for (int i = 0; i < equipmentSlotPreviewIcons.Length; i++)
            {
                Image icon = equipmentSlotPreviewIcons[i];
                if (icon == null)
                    continue;

                RectTransform iconRect = icon.rectTransform;
                if (iconRect != null)
                    iconRect.sizeDelta = new Vector2(EquipmentSlotPreviewIconSize, EquipmentSlotPreviewIconSize);
            }
        }

        if (equipmentSlotPreviewTexts != null)
        {
            for (int i = 0; i < equipmentSlotPreviewTexts.Length; i++)
                ApplyEquipmentSlotPreviewTextStyle(equipmentSlotPreviewTexts[i]);
        }

        KeepEquipmentSlotItemsAboveLabels();
    }

    void KeepEquipmentSlotItemsAboveLabels()
    {
        if (equipmentSlotPreviewIcons == null)
            return;

        for (int i = 0; i < equipmentSlotPreviewIcons.Length; i++)
        {
            if (equipmentSlotPreviewIcons[i] != null)
                equipmentSlotPreviewIcons[i].transform.SetAsLastSibling();
        }
    }

    void ApplyEquipmentSlotPreviewTextStyle(TMP_Text text)
    {
        if (text == null)
            return;

        text.fontSize = EquipmentSlotPreviewFontSize;
        text.fontSizeMin = 9f;
        text.fontSizeMax = EquipmentSlotPreviewFontSize;
        text.enableAutoSizing = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.92f, 0.96f, 0.98f, 0.98f);
        text.margin = new Vector4(6f, 4f, 6f, 4f);
    }

    void LayoutShipStatsVertical(float x, float topY, Vector2 cardSize, float spacing)
    {
        if (shipStatLabelTexts == null || shipStatValueTexts == null)
            return;

        bool largeMode = cardSize.y >= 60f;

        for (int i = 0; i < shipStatLabelTexts.Length; i++)
        {
            TMP_Text label = shipStatLabelTexts[i];
            TMP_Text value = i < shipStatValueTexts.Length ? shipStatValueTexts[i] : null;
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
            labelRect.anchoredPosition = new Vector2(14f, largeMode ? -14f : -10f);
            labelRect.sizeDelta = new Vector2(cardSize.x * 0.48f, largeMode ? 26f : 16f);
            label.fontSize = largeMode ? 25f : 13f;

            if (value != null)
            {
                RectTransform valueRect = value.rectTransform;
                valueRect.anchorMin = new Vector2(1f, 1f);
                valueRect.anchorMax = new Vector2(1f, 1f);
                valueRect.pivot = new Vector2(1f, 1f);
                valueRect.anchoredPosition = new Vector2(-12f, largeMode ? -14f : -10f);
                valueRect.sizeDelta = new Vector2(cardSize.x * 0.44f, largeMode ? 26f : 16f);
                value.fontSize = largeMode ? 25f : 13f;
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
                    barBgRect.anchoredPosition = new Vector2(0f, largeMode ? 12f : 8f);
                    barBgRect.sizeDelta = new Vector2(-20f, largeMode ? 18f : 12f);
                }
            }
        }
    }

    void RefreshShipPreview()
    {
        PlayerShipDefinition definition = ShipCatalog.GetShipDefinition(selectedSkin);
        if (shipPreviewTitleText != null)
        {
            shipPreviewTitleText.text = definition.DisplayName.ToUpperInvariant();
        }

        RefreshShipStatCards(definition);

        UpdateEquipmentSlotLayout();

        if (shipPreviewImage != null)
        {
            shipPreviewImage.sprite = LoadShipPreviewSprite(selectedSkin);
            shipPreviewImage.color = shipPreviewImage.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }

        if (shipPreviewButton != null)
            shipPreviewButton.interactable = shipPreviewImage != null && shipPreviewImage.sprite != null && !inventoryActionInProgress;

        if (shipImageModalObject != null && shipImageModalObject.activeSelf && shipImageModalImage != null)
        {
            shipImageModalImage.sprite = shipPreviewImage != null ? shipPreviewImage.sprite : null;
            shipImageModalImage.color = shipImageModalImage.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }

        RefreshEquipmentSlotPreview();
    }

    void RefreshPilotPortrait()
    {
        PlayerProfileData profile = PlayerProfileService.Instance.CurrentProfile;
        selectedPilotId = PilotCatalog.NormalizePilotId(profile != null ? profile.SelectedPilotId : selectedPilotId);
        if (profile != null && !PilotCatalog.IsPilotUnlocked(profile, selectedPilotId))
            selectedPilotId = PilotCatalog.JakeId;

        PilotDefinition definition = PilotCatalog.GetDefinition(selectedPilotId);
        if (pilotPortraitImage != null)
        {
            pilotPortraitImage.sprite = LoadPilotPortraitSprite(definition);
            pilotPortraitImage.color = pilotPortraitImage.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }

        if (pilotPortraitNameText != null)
            pilotPortraitNameText.text = definition.DisplayName;

        if (pilotPortraitButton != null)
            pilotPortraitButton.interactable = !inventoryActionInProgress;
    }

    void RefreshShipStatCards(PlayerShipDefinition definition)
    {
        if (definition == null || shipStatLabelTexts == null || shipStatValueTexts == null || shipStatFillImages == null)
            return;

        SetShipStatCard(0, ShipStatLabels[0], definition.BaseHp.ToString(), NormalizeShipStat(definition.BaseHp, stat => stat.BaseHp));
        SetShipStatCard(1, ShipStatLabels[1], definition.BaseShield.ToString(), NormalizeShipStat(definition.BaseShield, stat => stat.BaseShield));
        SetShipStatCard(2, ShipStatLabels[2], definition.BaseSpeed.ToString("0.0"), NormalizeShipStat(definition.BaseSpeed, stat => stat.BaseSpeed));
        SetShipStatCard(3, ShipStatLabels[3], "x" + definition.TurnRateMultiplier.ToString("0.00"), NormalizeShipStat(definition.TurnRateMultiplier, stat => stat.TurnRateMultiplier));
        SetShipStatCard(4, ShipStatLabels[4], definition.BoosterDuration.ToString("0.0") + "s", NormalizeShipStat(definition.BoosterDuration, stat => stat.BoosterDuration));
        SetShipStatCard(5, ShipStatLabels[5], "+" + definition.MaxBoostPercent + "%", NormalizeShipStat(definition.MaxBoostPercent, stat => stat.MaxBoostPercent));
        SetShipStatCard(6, ShipStatLabels[6], definition.CargoCapacity.ToString(), NormalizeShipStat(definition.CargoCapacity, stat => stat.CargoCapacity));
        SetShipStatCard(7, ShipStatLabels[7], definition.SafePocketSlots.ToString(), NormalizeSafePocketStat(definition.SafePocketSlots));
        SetShipStatCard(8, ShipStatLabels[8], definition.BrakingDriftLevel.ToString(), NormalizeBrakingDriftStat(definition.BrakingDriftLevel));
    }

    void SetShipStatCard(int index, string label, string valueText, float normalized)
    {
        if (index < 0 ||
            shipStatLabelTexts == null || index >= shipStatLabelTexts.Length ||
            shipStatValueTexts == null || index >= shipStatValueTexts.Length ||
            shipStatFillImages == null || index >= shipStatFillImages.Length)
        {
            return;
        }

        TMP_Text labelText = shipStatLabelTexts[index];
        TMP_Text value = shipStatValueTexts[index];
        Image fillImage = shipStatFillImages[index];
        if (labelText != null)
            labelText.text = label;
        if (value != null)
            value.text = valueText;
        if (fillImage != null)
        {
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
    }

    float NormalizeShipStat(float value, Func<PlayerShipDefinition, float> selector)
    {
        if (selector == null)
            return 0f;

        float min = float.MaxValue;
        float max = float.MinValue;
        for (int i = 0; i < SelectableShipTypes.Length; i++)
        {
            PlayerShipDefinition definition = ShipCatalog.GetShipDefinition(SelectableShipTypes[i]);
            if (definition == null)
                continue;

            float candidate = selector(definition);
            min = Mathf.Min(min, candidate);
            max = Mathf.Max(max, candidate);
        }

        if (min >= float.MaxValue || max <= float.MinValue)
            return 0f;

        if (Mathf.Abs(max - min) <= 0.001f)
            return 1f;

        return Mathf.InverseLerp(min, max, value);
    }

    float NormalizeBrakingDriftStat(int brakingDriftLevel)
    {
        return 1f - NormalizeShipStat(brakingDriftLevel, stat => stat.BrakingDriftLevel);
    }

    float NormalizeSafePocketStat(int safePocketSlots)
    {
        return Mathf.InverseLerp(0f, 3f, safePocketSlots);
    }

    Color EvaluateShipStatColor(float t)
    {
        t = Mathf.Clamp01(t);
        Color red = new Color(0.86f, 0.24f, 0.2f, 0.98f);
        Color orange = new Color(0.93f, 0.48f, 0.15f, 0.98f);
        Color yellow = new Color(0.94f, 0.8f, 0.2f, 0.98f);
        Color green = new Color(0.28f, 0.84f, 0.38f, 0.98f);

        if (t <= 0.33f)
            return Color.Lerp(red, orange, t / 0.33f);
        if (t <= 0.66f)
            return Color.Lerp(orange, yellow, (t - 0.33f) / 0.33f);

        return Color.Lerp(yellow, green, (t - 0.66f) / 0.34f);
    }
}
