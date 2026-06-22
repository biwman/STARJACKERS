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
    void CreatePlayerView(Transform parent)
    {
        playerStatsPanelObject = CreatePlayerSectionPanel(parent, "PlayerStatsPanel", "BASIC STATS", out playerStatsTitleText);
        playerStatLabelTexts = new TMP_Text[PlayerCareerStatLabels.Length];
        playerStatValueTexts = new TMP_Text[PlayerCareerStatLabels.Length];

        for (int i = 0; i < PlayerCareerStatLabels.Length; i++)
        {
            playerStatLabelTexts[i] = CreateText(
                playerStatsPanelObject.transform,
                "PlayerStatLabel_" + i,
                PlayerCareerStatLabels[i],
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(300f, 32f),
                20f,
                TextAlignmentOptions.Left);
            ConfigurePlayerStatLabelText(playerStatLabelTexts[i]);

            playerStatValueTexts[i] = CreateText(
                playerStatsPanelObject.transform,
                "PlayerStatValue_" + i,
                "0",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(120f, 32f),
                22f,
                TextAlignmentOptions.Right);
            ConfigurePlayerStatValueText(playerStatValueTexts[i]);
        }

        playerQuestItemsPanelObject = CreatePlayerSectionPanel(parent, "PlayerQuestItemsPanel", "QUEST ITEMS GAINED", out _);
        playerQuestItemsText = CreateText(
            playerQuestItemsPanelObject.transform,
            "PlayerQuestItemsList",
            string.Empty,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -86f),
            new Vector2(500f, 610f),
            22f,
            TextAlignmentOptions.TopLeft);
        ConfigurePlayerQuestItemsText(playerQuestItemsText);

        if (playerViewRootObject != null)
            playerViewRootObject.SetActive(false);
    }

    GameObject CreatePlayerSectionPanel(Transform parent, string objectName, string title, out TMP_Text titleText)
    {
        GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);

        Image image = panel.GetComponent<Image>();
        image.color = new Color(0.025f, 0.05f, 0.08f, 0.84f);
        image.raycastTarget = false;

        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.32f, 0.52f, 0.68f, 0.58f);
        outline.effectDistance = new Vector2(2.5f, -2.5f);
        outline.useGraphicAlpha = true;

        Shadow shadow = panel.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(6f, -6f);
        shadow.useGraphicAlpha = true;

        titleText = CreateText(panel.transform, objectName + "Title", title, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(420f, 38f), 29f, TextAlignmentOptions.Center);
        titleText.fontStyle = FontStyles.Bold;
        titleText.characterSpacing = 2f;
        titleText.color = new Color(0.84f, 0.94f, 1f, 1f);
        titleText.raycastTarget = false;

        return panel;
    }

    void ConfigurePlayerStatLabelText(TMP_Text text)
    {
        if (text == null)
            return;

        text.fontStyle = FontStyles.Bold;
        text.fontSize = 19f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 13f;
        text.fontSizeMax = 19f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        text.color = new Color(0.82f, 0.9f, 0.96f, 0.98f);
        text.characterSpacing = 0.4f;
        text.margin = new Vector4(2f, 2f, 8f, 2f);
        text.raycastTarget = false;
    }

    void ConfigurePlayerStatValueText(TMP_Text text)
    {
        if (text == null)
            return;

        text.fontStyle = FontStyles.Bold;
        text.fontSize = 22f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 15f;
        text.fontSizeMax = 22f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        text.color = new Color(0.96f, 0.82f, 0.34f, 1f);
        text.characterSpacing = 0.6f;
        text.margin = new Vector4(6f, 2f, 2f, 2f);
        text.raycastTarget = false;
    }

    void ConfigurePlayerQuestItemsText(TMP_Text text)
    {
        if (text == null)
            return;

        text.fontStyle = FontStyles.Bold;
        text.fontSize = 22f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 15f;
        text.fontSizeMax = 22f;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.color = new Color(0.82f, 0.9f, 0.96f, 0.98f);
        text.characterSpacing = 0.6f;
        text.lineSpacing = 12f;
        text.margin = new Vector4(10f, 4f, 10f, 4f);
        text.raycastTarget = false;
    }
}
