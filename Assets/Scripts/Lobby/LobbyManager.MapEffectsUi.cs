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
    void EnsureMapEffectChanceSettingsUiExists()
    {
        EnsureMapEffectChanceTableUiExists();

        IReadOnlyList<LobbyMapDefinition> maps = LobbyMapCatalog.AllMaps;
        for (int mapIndex = 0; mapIndex < maps.Count; mapIndex++)
        {
            LobbyMapDefinition map = maps[mapIndex];
            if (map == null)
                continue;

            float rowY = -104f - (mapIndex * MapEffectChanceRowHeight);
            EnsureMapEffectChanceRowLabel(map, new Vector2(26f, rowY));

            for (int ruleIndex = 0; ruleIndex < MapEffectChanceRuleIds.Length; ruleIndex++)
            {
                string mapId = map.Id;
                string ruleId = MapEffectChanceRuleIds[ruleIndex];
                EnsureMapEffectChanceButton(mapId, ruleId, GetMapEffectChanceCellPosition(ruleIndex, rowY), () => CycleMapEffectChancePercent(mapId, ruleId));
            }
        }

        UpdateMapEffectChanceTableContentHeight();
    }

    void EnsureMapEffectChanceTableUiExists()
    {
        Transform desiredParent = developerSettingsRootObject != null ? developerSettingsRootObject.transform : transform;

        if (mapEffectChanceTableViewportRect == null || !mapEffectChanceTableViewportRect.gameObject.scene.IsValid())
        {
            GameObject viewportObject = FindOrCreateChild(developerSettingsRootObject != null ? developerSettingsRootObject : gameObject, "MapEffectChanceViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            mapEffectChanceTableViewportRect = viewportObject.GetComponent<RectTransform>();
        }

        if (mapEffectChanceTableViewportRect.transform.parent != desiredParent)
            mapEffectChanceTableViewportRect.transform.SetParent(desiredParent, false);

        mapEffectChanceTableViewportRect.anchorMin = new Vector2(0.5f, 1f);
        mapEffectChanceTableViewportRect.anchorMax = new Vector2(0.5f, 1f);
        mapEffectChanceTableViewportRect.pivot = new Vector2(0.5f, 1f);
        mapEffectChanceTableViewportRect.anchoredPosition = new Vector2(RightTableX, RightTableY - RightTableHeight - 24f);
        mapEffectChanceTableViewportRect.sizeDelta = new Vector2(RightTableWidth, MapEffectChanceTableHeight);

        Image viewportImage = mapEffectChanceTableViewportRect.GetComponent<Image>();
        if (viewportImage != null)
        {
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewportImage.raycastTarget = true;
        }

        mapEffectChanceTableScrollRect = mapEffectChanceTableViewportRect.GetComponent<ScrollRect>();
        mapEffectChanceTableScrollRect.horizontal = false;
        mapEffectChanceTableScrollRect.vertical = true;
        mapEffectChanceTableScrollRect.movementType = ScrollRect.MovementType.Clamped;
        mapEffectChanceTableScrollRect.scrollSensitivity = 32f;
        mapEffectChanceTableScrollRect.viewport = mapEffectChanceTableViewportRect;

        if (mapEffectChanceTableRootRect == null || !mapEffectChanceTableRootRect.gameObject.scene.IsValid())
        {
            Transform tableTransform = mapEffectChanceTableViewportRect.transform.Find("MapEffectChanceTable");
            if (tableTransform == null && desiredParent != null)
                tableTransform = desiredParent.Find("MapEffectChanceTable");

            GameObject tableObject = tableTransform != null
                ? tableTransform.gameObject
                : new GameObject("MapEffectChanceTable", typeof(RectTransform), typeof(Image));

            mapEffectChanceTableRootRect = tableObject.GetComponent<RectTransform>();
            if (mapEffectChanceTableRootRect == null)
                mapEffectChanceTableRootRect = tableObject.AddComponent<RectTransform>();
        }

        if (mapEffectChanceTableRootRect.transform.parent != mapEffectChanceTableViewportRect.transform)
            mapEffectChanceTableRootRect.transform.SetParent(mapEffectChanceTableViewportRect.transform, false);

        mapEffectChanceTableRootRect.anchorMin = new Vector2(0f, 1f);
        mapEffectChanceTableRootRect.anchorMax = new Vector2(0f, 1f);
        mapEffectChanceTableRootRect.pivot = new Vector2(0f, 1f);
        mapEffectChanceTableRootRect.anchoredPosition = Vector2.zero;
        mapEffectChanceTableRootRect.sizeDelta = new Vector2(RightTableWidth, ResolveMapEffectChanceTableContentHeight());

        Image bg = mapEffectChanceTableRootRect.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0f);
        bg.raycastTarget = false;

        mapEffectChanceTableScrollRect.content = mapEffectChanceTableRootRect;

        EnsureMapEffectChanceHeaderLabel("MapEffectChanceTableTitle", "ROUND RULE CHANCES", new Vector2(24f, -18f), new Vector2(340f, 30f), 24f, TextAlignmentOptions.Left);
        EnsureMapEffectChanceHeaderLabel("MapEffectChanceHeader_MAP", "MAP", new Vector2(26f, -58f), new Vector2(MapEffectChanceNameColumnWidth - 12f, 26f), 14f, TextAlignmentOptions.Left);
        for (int i = 0; i < MapEffectChanceColumnLabels.Length; i++)
        {
            EnsureMapEffectChanceHeaderLabel(
                "MapEffectChanceHeader_" + MapEffectChanceRuleIds[i],
                MapEffectChanceColumnLabels[i],
                GetMapEffectChanceHeaderPosition(i),
                new Vector2(MapEffectChanceColumnWidth - 10f, 26f),
                14f,
                TextAlignmentOptions.Center);
        }
    }

    void EnsureMapEffectChanceButton(string mapId, string ruleId, Vector2 anchoredPosition, UnityEngine.Events.UnityAction callback)
    {
        string key = GetMapEffectChanceUiKey(mapId, ruleId);
        string buttonName = "MapEffectChanceButton_" + key;
        string textName = "MapEffectChanceText_" + key;

        mapEffectChanceButtons.TryGetValue(key, out Button existingButton);
        mapEffectChanceTexts.TryGetValue(key, out TMP_Text existingText);

        Button button = EnsureSettingButton(ref existingText, existingButton, buttonName, textName, anchoredPosition, callback);
        if (button != null && mapEffectChanceTableRootRect != null)
        {
            button.transform.SetParent(mapEffectChanceTableRootRect, false);
            RectTransform rect = button.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = anchoredPosition;
                rect.sizeDelta = new Vector2(MapEffectChanceColumnWidth - 10f, 38f);
            }
        }

        mapEffectChanceButtons[key] = button;
        mapEffectChanceTexts[key] = existingText;
    }

    string GetMapEffectChanceUiKey(string mapId, string ruleId)
    {
        return (string.IsNullOrWhiteSpace(mapId) ? "unknown" : mapId.Trim()) + "_" +
               (string.IsNullOrWhiteSpace(ruleId) ? "unknown" : ruleId.Trim());
    }

    Vector2 GetMapEffectChanceCellPosition(int columnIndex, float rowY)
    {
        return new Vector2(MapEffectChanceNameColumnWidth + 18f + (columnIndex * MapEffectChanceColumnWidth), rowY);
    }

    Vector2 GetMapEffectChanceHeaderPosition(int columnIndex)
    {
        return new Vector2(MapEffectChanceNameColumnWidth + 18f + (columnIndex * MapEffectChanceColumnWidth), -58f);
    }

    float ResolveMapEffectChanceTableContentHeight()
    {
        float rowsHeight = 118f + LobbyMapCatalog.AllMaps.Count * MapEffectChanceRowHeight + 20f;
        return Mathf.Max(MapEffectChanceTableHeight, rowsHeight);
    }

    void UpdateMapEffectChanceTableContentHeight()
    {
        if (mapEffectChanceTableRootRect == null)
            return;

        mapEffectChanceTableRootRect.sizeDelta = new Vector2(RightTableWidth, ResolveMapEffectChanceTableContentHeight());
        if (mapEffectChanceTableScrollRect != null && !mapEffectChanceTableScrollInitialized)
        {
            Canvas.ForceUpdateCanvases();
            mapEffectChanceTableScrollRect.verticalNormalizedPosition = 1f;
            mapEffectChanceTableScrollInitialized = true;
        }
    }

    void EnsureMapEffectChanceRowLabel(LobbyMapDefinition map, Vector2 anchoredPosition)
    {
        if (map == null || mapEffectChanceTableRootRect == null)
            return;

        if (!mapEffectChanceRowLabels.TryGetValue(map.Id, out TMP_Text label) || label == null || !label.gameObject.scene.IsValid())
        {
            label = CreateStandaloneLabel(mapEffectChanceTableRootRect.transform, "MapEffectChanceRowLabel_" + map.Id, map.DisplayName.ToUpperInvariant(), anchoredPosition, new Vector2(MapEffectChanceNameColumnWidth - 12f, 28f), 16f, TextAlignmentOptions.Left);
            mapEffectChanceRowLabels[map.Id] = label;
        }

        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
    }

    TMP_Text EnsureMapEffectChanceHeaderLabel(string key, string value, Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        if (!mapEffectChanceHeaderLabels.TryGetValue(key, out TMP_Text label) || label == null || !label.gameObject.scene.IsValid())
        {
            label = CreateStandaloneLabel(mapEffectChanceTableRootRect.transform, key, value, anchoredPosition, size, fontSize, alignment);
            mapEffectChanceHeaderLabels[key] = label;
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
