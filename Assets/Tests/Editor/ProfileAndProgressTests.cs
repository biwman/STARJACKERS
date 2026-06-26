using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class ProfileAndProgressTests
{
    [Test]
    public void DefaultProfileContainsPlayableNormalizedState()
    {
        PlayerProfileData profile = PlayerProfileData.Default();

        Assert.That(profile.Nickname, Is.Not.Null.And.Not.Empty);
        Assert.That(profile.ShipSkinIndex, Is.InRange(0, ShipCatalog.MaxShipSkinIndex));
        Assert.That(profile.Astrons, Is.EqualTo(PlayerProfileService.DefaultStartingAstrons));
        Assert.That(profile.Inventory, Is.Not.Null);
        Assert.That(profile.SelectedPilotId, Is.EqualTo(PilotCatalog.JakeId));
        CollectionAssert.Contains(profile.UnlockedPilotIds, PilotCatalog.JakeId);
        CollectionAssert.Contains(profile.UnlockedShipIds, ShipCatalog.GetShipTypeId(ShipType.Explorer));
        Assert.That(profile.MapUnlockProgress, Is.Not.Null);
        Assert.That(profile.ProjectProgress, Is.Not.Null);
        Assert.That(profile.CareerStats, Is.Not.Null);

        AssertInventoryShape(profile.Inventory);
    }

    [Test]
    public void InventoryNormalizePreservesExistingSlotsAndRepairsShape()
    {
        PlayerInventoryData inventory = new PlayerInventoryData
        {
            PlayerSlots = new[] { InventoryItemCatalog.AsteroidResourceId },
            ShipSlots = new[] { InventoryItemCatalog.CashSuitcaseId },
            EquipmentSlots = new[] { InventoryItemCatalog.PlasmaGunId },
            CraftingSlots = null
        };

        inventory.Normalize();

        AssertInventoryShape(inventory);
        Assert.That(inventory.PlayerSlots[0], Is.EqualTo(InventoryItemCatalog.AsteroidResourceId));
        Assert.That(inventory.ShipSlots[0], Is.EqualTo(InventoryItemCatalog.CashSuitcaseId));
        Assert.That(inventory.EquipmentSlots[0], Is.EqualTo(InventoryItemCatalog.PlasmaGunId));
    }

    [Test]
    public void InventoryCloneCreatesIndependentSlotArrays()
    {
        PlayerInventoryData inventory = PlayerInventoryData.Default();
        inventory.PlayerSlots[0] = InventoryItemCatalog.AsteroidResourceId;
        inventory.ShipSlots[0] = InventoryItemCatalog.CashSuitcaseId;

        PlayerInventoryData clone = inventory.Clone();
        clone.PlayerSlots[0] = InventoryItemCatalog.PlatinumChunkId;
        clone.ShipSlots[0] = null;

        Assert.That(inventory.PlayerSlots[0], Is.EqualTo(InventoryItemCatalog.AsteroidResourceId));
        Assert.That(inventory.ShipSlots[0], Is.EqualTo(InventoryItemCatalog.CashSuitcaseId));
        Assert.That(clone.PlayerSlots[0], Is.EqualTo(InventoryItemCatalog.PlatinumChunkId));
        Assert.That(clone.ShipSlots[0], Is.Null);
    }

    [Test]
    public void InventoryAddRemoveAndRestoreOperationsKeepSlotsConsistent()
    {
        PlayerInventoryData inventory = PlayerInventoryData.Default();

        Assert.That(inventory.TryAddToPlayer(InventoryItemCatalog.AsteroidResourceId), Is.True);
        Assert.That(inventory.PlayerSlots[0], Is.EqualTo(InventoryItemCatalog.AsteroidResourceId));
        Assert.That(inventory.RemoveFromPlayer(0), Is.EqualTo(InventoryItemCatalog.AsteroidResourceId));
        Assert.That(inventory.PlayerSlots[0], Is.Null);

        inventory.RestorePlayer(0, InventoryItemCatalog.CashSuitcaseId);
        Assert.That(inventory.PlayerSlots[0], Is.EqualTo(InventoryItemCatalog.CashSuitcaseId));

        Assert.That(inventory.TryAddToShip(InventoryItemCatalog.PlatinumChunkId, 1), Is.True);
        Assert.That(inventory.GetFirstEmptyShipSlot(1), Is.EqualTo(-1));
        Assert.That(inventory.GetFirstEmptyShipSlot(2), Is.EqualTo(1));
    }

    [Test]
    public void ProjectProgressNormalizationCreatesEntryForEveryProject()
    {
        PlayerProjectProgressData normalized = ProjectCatalog.NormalizeProgress(null);

        Assert.That(normalized, Is.Not.Null);
        Assert.That(normalized.Entries, Is.Not.Null);
        Assert.That(normalized.Entries.Length, Is.EqualTo(ProjectCatalog.AllProjects.Count));

        foreach (ProjectDefinition project in ProjectCatalog.AllProjects)
        {
            PlayerProjectProgressEntry entry = ProjectCatalog.GetProjectProgress(normalized, project.Id);
            Assert.That(entry, Is.Not.Null, "Missing progress entry for " + project.Id);
            Assert.That(entry.ProjectId, Is.EqualTo(project.Id));
            Assert.That(entry.Stages, Is.Not.Null);
            Assert.That(entry.Stages.Length, Is.EqualTo(project.Stages.Length), "Wrong stage progress count for " + project.Id);

            for (int stageIndex = 0; stageIndex < project.Stages.Length; stageIndex++)
            {
                ProjectStageDefinition stage = project.Stages[stageIndex];
                PlayerProjectStageProgress stageProgress = entry.GetStage(stageIndex);
                Assert.That(stageProgress, Is.Not.Null, "Missing stage progress for " + project.Id + " stage " + stageIndex);
                Assert.That(stageProgress.DeliveredCounts, Is.Not.Null);
                Assert.That(stageProgress.DeliveredCounts.Length, Is.EqualTo(stage.Steps.Length), "Wrong step progress count for " + project.Id + " stage " + stageIndex);
                Assert.That(stageProgress.DeliveredCounts.All(count => count >= 0), Is.True, "Negative delivered count for " + project.Id + " stage " + stageIndex);
            }
        }
    }

    [Test]
    public void MapProgressNormalizesIdsAndClampsCounts()
    {
        PlayerMapUnlockProgressData progress = PlayerProfileService.NormalizeMapUnlockProgress(new PlayerMapUnlockProgressData
        {
            ReturnCounts = new[]
            {
                new PlayerMapReturnCountEntry { MapId = " " + LobbyMapCatalog.NoobHavenMapId + " ", Count = 2 },
                new PlayerMapReturnCountEntry { MapId = "unknown_map", Count = 99 },
                new PlayerMapReturnCountEntry { MapId = LobbyMapCatalog.MinefieldMapId, Count = -3 }
            },
            MothershipKilled = true,
            CheatUnlockAllMaps = false
        });

        Assert.That(progress, Is.Not.Null);
        Assert.That(progress.ReturnCounts, Is.Not.Null);
        Assert.That(LobbyMapUnlockCatalog.GetReturnCount(progress, LobbyMapCatalog.NoobHavenMapId), Is.EqualTo(2));
        Assert.That(LobbyMapUnlockCatalog.GetReturnCount(progress, "unknown_map"), Is.EqualTo(0));
        Assert.That(LobbyMapUnlockCatalog.GetReturnCount(progress, LobbyMapCatalog.MinefieldMapId), Is.EqualTo(0));
        Assert.That(progress.MothershipKilled, Is.True);
    }

    [Test]
    public void ProfileDataSurvivesJsonRoundTripForCoreState()
    {
        PlayerProfileData source = PlayerProfileData.Default();
        source.Nickname = "Round Trip Pilot";
        source.TotalXp = RoundXpBalance.GetRequiredXpForNextLevel(1) + 12;
        source.Inventory.PlayerSlots[0] = InventoryItemCatalog.AsteroidResourceId;
        source.Inventory.ShipSlots[0] = InventoryItemCatalog.CashSuitcaseId;
        source.ProjectProgress = ProjectCatalog.NormalizeProgress(source.ProjectProgress);
        source.MapUnlockProgress.SetReturnCount(LobbyMapCatalog.NoobHavenMapId, 3);

        string json = JsonUtility.ToJson(source);
        PlayerProfileData clone = JsonUtility.FromJson<PlayerProfileData>(json);

        Assert.That(clone, Is.Not.Null);
        Assert.That(clone.Nickname, Is.EqualTo(source.Nickname));
        Assert.That(clone.TotalXp, Is.EqualTo(source.TotalXp));
        Assert.That(clone.Inventory.PlayerSlots[0], Is.EqualTo(InventoryItemCatalog.AsteroidResourceId));
        Assert.That(clone.Inventory.ShipSlots[0], Is.EqualTo(InventoryItemCatalog.CashSuitcaseId));
        Assert.That(LobbyMapUnlockCatalog.GetReturnCount(clone.MapUnlockProgress, LobbyMapCatalog.NoobHavenMapId), Is.EqualTo(3));
        Assert.That(ProjectCatalog.NormalizeProgress(clone.ProjectProgress).Entries.Length, Is.EqualTo(ProjectCatalog.AllProjects.Count));
    }

    [Test]
    public void RoundXpLevelProgressionIsMonotonic()
    {
        int previousRequirement = 0;
        int totalRequiredForLevel = 0;
        for (int level = 1; level <= 50; level++)
        {
            int requirement = RoundXpBalance.GetRequiredXpForNextLevel(level);
            Assert.That(requirement, Is.GreaterThan(previousRequirement), "XP requirement should increase at level " + level);

            int nextLevelThreshold = totalRequiredForLevel + requirement;
            Assert.That(RoundXpBalance.GetLevelForTotalXp(nextLevelThreshold - 1), Is.EqualTo(level), "Level before threshold mismatch at level " + level);
            Assert.That(RoundXpBalance.GetLevelForTotalXp(nextLevelThreshold), Is.EqualTo(level + 1), "Level threshold mismatch at level " + level);

            totalRequiredForLevel = nextLevelThreshold;
            previousRequirement = requirement;
        }
    }

    static void AssertInventoryShape(PlayerInventoryData inventory)
    {
        inventory.Normalize();
        Assert.That(inventory.PlayerSlots.Length, Is.GreaterThanOrEqualTo(PlayerInventoryData.DefaultPlayerSlotCount));
        Assert.That(inventory.ShipSlots.Length, Is.EqualTo(PlayerInventoryData.ShipSlotCount));
        Assert.That(inventory.EquipmentSlots.Length, Is.EqualTo(PlayerInventoryData.EquipmentSlotCount));
        Assert.That(inventory.CraftingSlots.Length, Is.EqualTo(PlayerInventoryData.CraftingSlotCount));
    }
}
