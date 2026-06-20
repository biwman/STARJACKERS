using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
public partial class LobbyManager
{
    void EnsureEnemySettingsUiExists()
    {
        EnsureEnemyTableUiExists();

        for (int i = 0; i < EnemyBotCatalog.AllDefinitions.Count; i++)
        {
            EnemyBotDefinition definition = EnemyBotCatalog.AllDefinitions[i];
            if (definition == null || !definition.ShowInEnemySettings)
                continue;

            float rowY = -112f - (GetVisibleEnemyDefinitionIndex(definition) * EnemyRowHeight);
            EnsureEnemyRowLabel(definition, new Vector2(26f, rowY));

            EnsureEnemySettingButton(definition, "enabled", GetEnemyCellPosition(0, rowY), () => CycleEnemyEnabled(definition.Kind));
            EnsureEnemySettingButton(definition, "count", GetEnemyCellPosition(1, rowY), () => CycleEnemyCount(definition.Kind));
            EnsureEnemySettingButton(definition, "respawn", GetEnemyCellPosition(2, rowY), () => CycleEnemyRespawnEnabled(definition.Kind));
            EnsureEnemySettingButton(definition, "hp", GetEnemyCellPosition(3, rowY), () => CycleEnemyHp(definition.Kind));
            EnsureEnemySettingButton(definition, "shield", GetEnemyCellPosition(4, rowY), () => CycleEnemyShield(definition.Kind));
            EnsureEnemySettingButton(definition, "damage", GetEnemyCellPosition(5, rowY), () => CycleEnemyDamage(definition.Kind));
            EnsureEnemySettingButton(definition, "speed", GetEnemyCellPosition(6, rowY), () => CycleEnemySpeed(definition.Kind));
            EnsureEnemySettingButton(definition, "time", GetEnemyCellPosition(7, rowY), () => CycleEnemySpawnSecond(definition.Kind));
            EnsureEnemySettingButton(definition, "respawnTime", GetEnemyCellPosition(8, rowY), () => CycleEnemyRespawnInterval(definition.Kind));
        }

        UpdateEnemyTableContentHeight();
    }

    void EnsureEnemySettingButton(EnemyBotDefinition definition, string suffix, Vector2 anchoredPosition, UnityEngine.Events.UnityAction callback)
    {
        string key = GetEnemySettingUiKey(definition.Kind, suffix);
        string buttonName = "EnemySettingButton_" + key;
        string textName = "EnemySettingText_" + key;

        enemySettingButtons.TryGetValue(key, out Button existingButton);
        enemySettingTexts.TryGetValue(key, out TMP_Text existingText);

        Button button = EnsureSettingButton(ref existingText, existingButton, buttonName, textName, anchoredPosition, callback);
        if (button != null && enemyTableRootRect != null)
        {
            button.transform.SetParent(enemyTableRootRect, false);
            RectTransform rect = button.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = anchoredPosition;
                rect.sizeDelta = new Vector2(EnemyColumnWidth - 8f, 42f);
            }
        }

        enemySettingButtons[key] = button;
        enemySettingTexts[key] = existingText;
    }

    string GetEnemySettingUiKey(EnemyBotKind kind, string suffix)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(kind);
        string prefix = definition != null ? definition.Id : kind.ToString().ToLowerInvariant();
        return prefix + "_" + suffix;
    }

    Vector2 GetEnemyCellPosition(int columnIndex, float rowY)
    {
        return new Vector2(EnemyNameColumnWidth + 18f + (columnIndex * EnemyColumnWidth), rowY);
    }

    void EnsureEnemyTableUiExists()
    {
        Transform desiredParent = developerSettingsRootObject != null ? developerSettingsRootObject.transform : transform;

        if (enemyTableViewportRect == null || !enemyTableViewportRect.gameObject.scene.IsValid())
        {
            GameObject viewportObject = FindOrCreateChild(developerSettingsRootObject != null ? developerSettingsRootObject : gameObject, "EnemySettingsViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            enemyTableViewportRect = viewportObject.GetComponent<RectTransform>();
        }

        if (enemyTableViewportRect.transform.parent != desiredParent)
            enemyTableViewportRect.transform.SetParent(desiredParent, false);

        enemyTableViewportRect.anchorMin = new Vector2(0.5f, 1f);
        enemyTableViewportRect.anchorMax = new Vector2(0.5f, 1f);
        enemyTableViewportRect.pivot = new Vector2(0.5f, 1f);
        enemyTableViewportRect.anchoredPosition = new Vector2(RightTableX, RightTableY);
        enemyTableViewportRect.sizeDelta = new Vector2(RightTableWidth, RightTableHeight);

        Image viewportImage = enemyTableViewportRect.GetComponent<Image>();
        if (viewportImage != null)
        {
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewportImage.raycastTarget = true;
        }

        enemyTableScrollRect = enemyTableViewportRect.GetComponent<ScrollRect>();
        enemyTableScrollRect.horizontal = false;
        enemyTableScrollRect.vertical = true;
        enemyTableScrollRect.movementType = ScrollRect.MovementType.Clamped;
        enemyTableScrollRect.scrollSensitivity = 36f;
        enemyTableScrollRect.viewport = enemyTableViewportRect;

        if (enemyTableRootRect == null || !enemyTableRootRect.gameObject.scene.IsValid())
        {
            Transform tableTransform = enemyTableViewportRect.transform.Find("EnemySettingsTable");
            if (tableTransform == null && desiredParent != null)
                tableTransform = desiredParent.Find("EnemySettingsTable");

            GameObject tableObject = tableTransform != null
                ? tableTransform.gameObject
                : new GameObject("EnemySettingsTable", typeof(RectTransform), typeof(Image));

            enemyTableRootRect = tableObject.GetComponent<RectTransform>();
            if (enemyTableRootRect == null)
                enemyTableRootRect = tableObject.AddComponent<RectTransform>();
        }

        if (enemyTableRootRect.transform.parent != enemyTableViewportRect.transform)
            enemyTableRootRect.transform.SetParent(enemyTableViewportRect, false);

        enemyTableRootRect.anchorMin = new Vector2(0f, 1f);
        enemyTableRootRect.anchorMax = new Vector2(0f, 1f);
        enemyTableRootRect.pivot = new Vector2(0f, 1f);
        enemyTableRootRect.anchoredPosition = Vector2.zero;
        enemyTableRootRect.sizeDelta = new Vector2(RightTableWidth, ResolveEnemyTableContentHeight());

        Image bg = enemyTableRootRect.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0f);
        bg.raycastTarget = false;

        enemyTableScrollRect.content = enemyTableRootRect;

        EnsureTableHeaderLabel("EnemyTableTitle", "ENEMIES", new Vector2(24f, -18f), new Vector2(220f, 30f), 24f, TextAlignmentOptions.Left);
        EnsureTableHeaderLabel("EnemyHeader_ACTIVE", "ACTIVE", GetEnemyHeaderPosition(0), new Vector2(EnemyColumnWidth - 8f, 26f), 14f, TextAlignmentOptions.Center);
        EnsureTableHeaderLabel("EnemyHeader_COUNT", "COUNT", GetEnemyHeaderPosition(1), new Vector2(EnemyColumnWidth - 8f, 26f), 14f, TextAlignmentOptions.Center);
        EnsureTableHeaderLabel("EnemyHeader_RESPAWN", "RESPAWN", GetEnemyHeaderPosition(2), new Vector2(EnemyColumnWidth - 8f, 26f), 14f, TextAlignmentOptions.Center);
        EnsureTableHeaderLabel("EnemyHeader_HP", "HP", GetEnemyHeaderPosition(3), new Vector2(EnemyColumnWidth - 8f, 26f), 14f, TextAlignmentOptions.Center);
        EnsureTableHeaderLabel("EnemyHeader_SHIELD", "SHIELD", GetEnemyHeaderPosition(4), new Vector2(EnemyColumnWidth - 8f, 26f), 14f, TextAlignmentOptions.Center);
        EnsureTableHeaderLabel("EnemyHeader_DAMAGE", "DAMAGE", GetEnemyHeaderPosition(5), new Vector2(EnemyColumnWidth - 8f, 26f), 13f, TextAlignmentOptions.Center);
        EnsureTableHeaderLabel("EnemyHeader_SPEED", "SPEED", GetEnemyHeaderPosition(6), new Vector2(EnemyColumnWidth - 8f, 26f), 14f, TextAlignmentOptions.Center);
        EnsureTableHeaderLabel("EnemyHeader_FIRSTRESPAWN", "FIRST\nRESPAWN", GetEnemyHeaderPosition(7), new Vector2(EnemyColumnWidth - 8f, 38f), 12f, TextAlignmentOptions.Center);
        EnsureTableHeaderLabel("EnemyHeader_RESPAWNLOOP", "RESPAWN\nLOOP", GetEnemyHeaderPosition(8), new Vector2(EnemyColumnWidth - 8f, 38f), 12f, TextAlignmentOptions.Center);
    }

    float ResolveEnemyTableContentHeight()
    {
        float rowsHeight = 126f + GetVisibleEnemyDefinitionCount() * EnemyRowHeight + 24f;
        return Mathf.Max(RightTableHeight, rowsHeight);
    }

    int GetVisibleEnemyDefinitionCount()
    {
        int count = 0;
        for (int i = 0; i < EnemyBotCatalog.AllDefinitions.Count; i++)
        {
            EnemyBotDefinition definition = EnemyBotCatalog.AllDefinitions[i];
            if (definition != null && definition.ShowInEnemySettings)
                count++;
        }

        return count;
    }

    int GetVisibleEnemyDefinitionIndex(EnemyBotDefinition target)
    {
        int index = 0;
        for (int i = 0; i < EnemyBotCatalog.AllDefinitions.Count; i++)
        {
            EnemyBotDefinition definition = EnemyBotCatalog.AllDefinitions[i];
            if (definition == null || !definition.ShowInEnemySettings)
                continue;

            if (definition == target)
                return index;

            index++;
        }

        return index;
    }

    void UpdateEnemyTableContentHeight()
    {
        if (enemyTableRootRect == null)
            return;

        enemyTableRootRect.sizeDelta = new Vector2(RightTableWidth, ResolveEnemyTableContentHeight());
        if (enemyTableScrollRect != null && !enemyTableScrollInitialized)
        {
            Canvas.ForceUpdateCanvases();
            enemyTableScrollRect.verticalNormalizedPosition = 1f;
            enemyTableScrollInitialized = true;
        }
    }

    Vector2 GetEnemyHeaderPosition(int columnIndex)
    {
        return new Vector2(EnemyNameColumnWidth + 18f + (columnIndex * EnemyColumnWidth), -54f);
    }

    void EnsureEnemyRowLabel(EnemyBotDefinition definition, Vector2 anchoredPosition)
    {
        if (definition == null || enemyTableRootRect == null)
            return;

        if (!enemyRowLabels.TryGetValue(definition.Id, out TMP_Text label) || label == null || !label.gameObject.scene.IsValid())
        {
            label = CreateStandaloneLabel(enemyTableRootRect.transform, "EnemyRowLabel_" + definition.Id, definition.DisplayName.ToUpperInvariant(), anchoredPosition, new Vector2(EnemyNameColumnWidth - 12f, 28f), 16f, TextAlignmentOptions.Left);
            enemyRowLabels[definition.Id] = label;
        }

        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
    }

    TMP_Text EnsureTableHeaderLabel(string key, string value, Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        if (!enemyHeaderLabels.TryGetValue(key, out TMP_Text label) || label == null || !label.gameObject.scene.IsValid())
        {
            label = CreateStandaloneLabel(enemyTableRootRect.transform, key, value, anchoredPosition, size, fontSize, alignment);
            enemyHeaderLabels[key] = label;
        }

        label.text = value;
        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return label;
    }
}
