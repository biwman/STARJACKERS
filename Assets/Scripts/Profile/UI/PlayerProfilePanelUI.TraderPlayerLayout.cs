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
    void LayoutTraderScreen()
    {
        if (currentScreen != ProfileScreen.Trader)
            return;

        if (storageViewRootObject != null && storageViewRootObject.activeSelf)
            LayoutCraftingStoragePanel();

        LayoutAmbientShipBackdrop();
        if (shipWorkspaceRootObject != null)
            shipWorkspaceRootObject.transform.SetAsFirstSibling();
        if (traderViewRootObject != null)
            traderViewRootObject.transform.SetAsLastSibling();
        if (storageViewRootObject != null)
            storageViewRootObject.transform.SetAsLastSibling();
        if (shopBrowserObject != null)
            shopBrowserObject.transform.SetAsLastSibling();
        if (traderFuturePanelObject != null)
            traderFuturePanelObject.transform.SetAsLastSibling();

        if (traderFuturePanelObject != null)
        {
            RectTransform rect = traderFuturePanelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(786f, -156f);
            rect.sizeDelta = new Vector2(420f, 736f);
        }
    }

    void LayoutPlayerScreen()
    {
        if (currentScreen != ProfileScreen.Player)
            return;

        LayoutAmbientShipBackdrop();
        if (shipWorkspaceRootObject != null)
            shipWorkspaceRootObject.transform.SetAsFirstSibling();
        if (playerViewRootObject != null)
            playerViewRootObject.transform.SetAsLastSibling();

        if (playerStatsPanelObject != null)
            SetAnchoredRect(playerStatsPanelObject.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-246f, -156f), new Vector2(814f, 736f));
        if (playerQuestItemsPanelObject != null)
            SetAnchoredRect(playerQuestItemsPanelObject.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(536f, -156f), new Vector2(588f, 736f));
        if (playerQuestItemsText != null)
            SetAnchoredRect(playerQuestItemsText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(500f, 610f));

        LayoutPlayerStatRows();
        RefreshPlayerStats();
    }

    void LayoutPlayerStatRows()
    {
        if (playerStatLabelTexts == null || playerStatValueTexts == null)
            return;

        const float topY = -88f;
        const float rowHeight = 42f;
        for (int i = 0; i < PlayerCareerStatLabels.Length; i++)
        {
            float y = topY - i * rowHeight;
            if (i < playerStatLabelTexts.Length && playerStatLabelTexts[i] != null)
                SetAnchoredRect(playerStatLabelTexts[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-112f, y), new Vector2(520f, 34f));
            if (i < playerStatValueTexts.Length && playerStatValueTexts[i] != null)
                SetAnchoredRect(playerStatValueTexts[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(306f, y), new Vector2(160f, 34f));
        }
    }

    void RefreshPlayerStats()
    {
        if (playerStatValueTexts == null)
            return;

        PlayerProfileData profile = PlayerProfileService.HasInstance ? PlayerProfileService.Instance.CurrentProfile : null;
        PlayerCareerStatsData careerStats = PlayerProfileService.NormalizeCareerStats(profile != null ? profile.CareerStats : null);
        int totalReturns = LobbyMapUnlockCatalog.GetTotalReturnCount(profile != null ? profile.MapUnlockProgress : null);
        int astronautEscapes = Mathf.Max(0, careerStats.AstronautEscapes);
        int shipEscapes = Mathf.Max(careerStats.ShipEscapes, Mathf.Max(0, totalReturns - astronautEscapes));
        int enemyKills = Mathf.Max(careerStats.EnemyKills, profile != null ? Mathf.Max(0, profile.PilotDroneKills) : 0);
        int astronsEarned = Mathf.Max(careerStats.AstronsEarned, profile != null ? Mathf.Max(0, profile.PilotSoldItemsAstrons) : 0);

        SetPlayerStatValue(0, shipEscapes.ToString());
        SetPlayerStatValue(1, astronautEscapes.ToString());
        SetPlayerStatValue(2, (profile != null ? Mathf.Max(0, profile.GamesPlayed) : 0).ToString());
        SetPlayerStatValue(3, careerStats.ReturnStreakWithoutDeath.ToString());
        SetPlayerStatValue(4, enemyKills.ToString());
        SetPlayerStatValue(5, careerStats.NeutralRaiderKills.ToString());
        SetPlayerStatValue(6, careerStats.HumanPlayerKills.ToString());
        SetPlayerStatValue(7, FormatAstronStat(astronsEarned));
        SetPlayerStatValue(8, FormatAstronStat(careerStats.HighestLootReturnedAstrons));
        SetPlayerStatValue(9, ProjectCatalog.CountCompletedProjects(profile != null ? profile.ProjectProgress : null).ToString());
        SetPlayerStatValue(10, CountUnlockedMaps(profile).ToString());
        SetPlayerStatValue(11, CountUnlockedPilots(profile).ToString());
        SetPlayerStatValue(12, CountUnlockedShips(profile).ToString());
        List<string> uniqueQuestItemIds = GetUniqueQuestItemIdsInProfile(profile);
        SetPlayerStatValue(13, uniqueQuestItemIds.Count.ToString());
        RefreshPlayerQuestItems(uniqueQuestItemIds);
    }

    void SetPlayerStatValue(int index, string value)
    {
        if (playerStatValueTexts == null || index < 0 || index >= playerStatValueTexts.Length || playerStatValueTexts[index] == null)
            return;

        playerStatValueTexts[index].text = value ?? "0";
    }

    string FormatAstronStat(int value)
    {
        return Mathf.Max(0, value) + " Astrons";
    }

    int CountUnlockedMaps(PlayerProfileData profile)
    {
        if (LobbyMapCatalog.AllMaps == null)
            return 0;

        int unlocked = 0;
        int totalXp = profile != null ? Mathf.Max(0, profile.TotalXp) : 0;
        for (int i = 0; i < LobbyMapCatalog.AllMaps.Count; i++)
        {
            LobbyMapDefinition map = LobbyMapCatalog.AllMaps[i];
            if (map == null)
                continue;

            LobbyMapUnlockStatus status = PlayerProfileService.HasInstance
                ? PlayerProfileService.Instance.GetMapUnlockStatus(map.Id)
                : LobbyMapUnlockCatalog.GetStatus(map.Id, profile != null ? profile.MapUnlockProgress : null, totalXp);
            if (status != null && status.IsUnlocked)
                unlocked++;
        }

        return unlocked;
    }

    int CountUnlockedPilots(PlayerProfileData profile)
    {
        return PilotCatalog.NormalizeUnlockedPilotIds(profile != null ? profile.UnlockedPilotIds : null).Length;
    }

    int CountUnlockedShips(PlayerProfileData profile)
    {
        return PlayerProfileService.NormalizeUnlockedShipIds(profile != null ? profile.UnlockedShipIds : null).Length;
    }

    List<string> GetUniqueQuestItemIdsInProfile(PlayerProfileData profile)
    {
        List<string> uniqueItemIds = new List<string>();
        PlayerInventoryData inventory = profile != null ? profile.Inventory : null;
        if (inventory == null)
            return uniqueItemIds;

        inventory.Normalize();
        HashSet<string> uniqueItemIdSet = new HashSet<string>(StringComparer.Ordinal);
        AddQuestItemsInSlots(inventory.PlayerSlots, uniqueItemIdSet);
        AddQuestItemsInSlots(inventory.ShipSlots, uniqueItemIdSet);
        AddQuestItemsInSlots(inventory.EquipmentSlots, uniqueItemIdSet);
        AddQuestItemsInSlots(inventory.CraftingSlots, uniqueItemIdSet);

        uniqueItemIds.AddRange(uniqueItemIdSet);
        uniqueItemIds.Sort(CompareQuestItemNames);
        return uniqueItemIds;
    }

    void AddQuestItemsInSlots(string[] slots, HashSet<string> uniqueItemIds)
    {
        if (slots == null || uniqueItemIds == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            string itemId = slots[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (InventoryItemCatalog.IsAlienSecretItem(itemId))
                continue;

            InventoryItemDefinition definition = InventoryItemCatalog.GetDefinition(itemId);
            if (definition == null)
                continue;

            if (definition.ItemType != InventoryItemType.Quest)
                continue;

            uniqueItemIds.Add(itemId);
        }
    }

    int CompareQuestItemNames(string leftItemId, string rightItemId)
    {
        string leftName = InventoryItemCatalog.GetDisplayName(leftItemId);
        string rightName = InventoryItemCatalog.GetDisplayName(rightItemId);
        int result = string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
        return result != 0 ? result : string.Compare(leftItemId, rightItemId, StringComparison.Ordinal);
    }

    void RefreshPlayerQuestItems(List<string> itemIds)
    {
        if (playerQuestItemsText == null)
            return;

        if (itemIds == null || itemIds.Count == 0)
        {
            playerQuestItemsText.text = "NONE YET";
            return;
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < itemIds.Count; i++)
        {
            if (i > 0)
                builder.AppendLine();
            builder.Append("- ");
            builder.Append(InventoryItemCatalog.GetDisplayName(itemIds[i]));
        }

        playerQuestItemsText.text = builder.ToString();
    }

    void ConfigureEmbeddedTraderBrowser()
    {
        if (shopBrowserObject == null)
            return;

        Image overlay = shopBrowserObject.GetComponent<Image>();
        if (overlay != null)
        {
            overlay.color = new Color(0f, 0f, 0f, 0f);
            overlay.raycastTarget = false;
        }

        Transform panel = shopBrowserObject.transform.Find("ShopBrowserPanel");
        if (panel != null)
        {
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(192f, -156f);
            panelRect.sizeDelta = new Vector2(628f, 736f);
        }

        if (shopCloseButton != null)
            shopCloseButton.gameObject.SetActive(false);

        if (shopAstronsText != null)
            shopAstronsText.gameObject.SetActive(false);

        TMP_Text title = shopBrowserObject.transform.Find("ShopBrowserPanel/ShopBrowserTitle")?.GetComponent<TMP_Text>();
        if (title != null)
        {
            title.text = TraderOpensShop(selectedTraderShop) ? GetTraderDisplayName(selectedTraderShop) : "TRADER";
            RectTransform titleRect = title.rectTransform;
            titleRect.anchoredPosition = new Vector2(-92f, -32f);
            titleRect.sizeDelta = new Vector2(300f, 34f);
        }

        if (shopSortButton != null)
        {
            RectTransform sortRect = shopSortButton.GetComponent<RectTransform>();
            sortRect.anchorMin = new Vector2(0.5f, 1f);
            sortRect.anchorMax = new Vector2(0.5f, 1f);
            sortRect.pivot = new Vector2(0.5f, 1f);
            sortRect.anchoredPosition = new Vector2(186f, -28f);
            sortRect.sizeDelta = new Vector2(236f, 48f);
            shopSortButton.transform.SetAsLastSibling();
        }
        RefreshShopSortButton();

        RectTransform viewportRect = shopBrowserObject.transform.Find("ShopBrowserPanel/ShopViewport")?.GetComponent<RectTransform>();
        if (viewportRect != null)
        {
            viewportRect.anchoredPosition = new Vector2(-20f, -22f);
            viewportRect.sizeDelta = new Vector2(604f, 610f);
        }

        RectTransform scrollbarRect = shopBrowserObject.transform.Find("ShopBrowserPanel/ShopScrollbar")?.GetComponent<RectTransform>();
        if (scrollbarRect != null)
        {
            scrollbarRect.anchoredPosition = new Vector2(318f, -22f);
            scrollbarRect.sizeDelta = new Vector2(RuntimeScrollbarStyler.GetPreferredWidth(RuntimeScrollbarStyler.Size.Small), 610f);
        }
    }
}
