using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public sealed class CatalogIntegrityTests
{
    [Test]
    public void InventoryDefinitionsHaveStableValidIdsAndKnownReferences()
    {
        IReadOnlyList<InventoryItemDefinition> items = InventoryItemCatalog.GetAllDefinitions();

        Assert.That(items, Is.Not.Empty);
        AssertUnique(items.Select(item => item.Id), "inventory item id");

        foreach (InventoryItemDefinition item in items)
        {
            Assert.That(item, Is.Not.Null);
            Assert.That(item.Id, Is.EqualTo(NormalizeId(item.Id)), "Item id should be normalized: " + item.Id);
            Assert.That(item.DisplayName, Is.Not.Null.And.Not.Empty, "Missing display name for " + item.Id);
            Assert.That(item.SellValueAstrons, Is.GreaterThanOrEqualTo(0), "Negative sell value for " + item.Id);
            Assert.That(item.ShopBuyValueAstronsOverride, Is.GreaterThanOrEqualTo(-1), "Invalid shop override for " + item.Id);

            if (item.RequiresSafePocket)
                Assert.That(item.CanEnterSafePocket, Is.True, item.Id + " requires a safe pocket but cannot enter one.");

            AssertKnownItemIds(item.SalvageOutputs, "salvage output for " + item.Id);
        }
    }

    [Test]
    public void GeneratedInventoryFamiliesRoundTripTheirVariantIndexes()
    {
        for (int i = 0; i < InventoryItemCatalog.ContainerVariantCount; i++)
        {
            string id = InventoryItemCatalog.GetContainerItemId(i);
            Assert.That(InventoryItemCatalog.IsContainerItem(id), Is.True);
            Assert.That(InventoryItemCatalog.GetContainerVariantIndex(id), Is.EqualTo(i));
            AssertKnownItemId(id, "container variant " + i);
        }

        for (int i = 0; i < InventoryItemCatalog.BlueprintScrapContainerVariantCount; i++)
        {
            string id = InventoryItemCatalog.GetBlueprintScrapContainerItemId(i);
            Assert.That(InventoryItemCatalog.IsBlueprintScrapContainerItem(id), Is.True);
            Assert.That(InventoryItemCatalog.GetBlueprintScrapContainerVariantIndex(id), Is.EqualTo(i));
            AssertKnownItemId(id, "blueprint scrap container variant " + i);
        }

        for (int i = 0; i < InventoryItemCatalog.RandomLootWreckVariantCount; i++)
        {
            string id = InventoryItemCatalog.GetRandomLootWreckItemId(i);
            Assert.That(InventoryItemCatalog.IsRandomLootWreckItem(id), Is.True);
            Assert.That(InventoryItemCatalog.GetRandomLootWreckVariantIndex(id), Is.EqualTo(i));
        }

        for (int i = 0; i < InventoryItemCatalog.AlienSecretVariantCount; i++)
        {
            string id = InventoryItemCatalog.GetAlienSecretItemId(i);
            Assert.That(InventoryItemCatalog.IsAlienSecretItem(id), Is.True);
            Assert.That(InventoryItemCatalog.GetAlienSecretVariantIndex(id), Is.EqualTo(i));
            AssertKnownItemId(id, "alien secret variant " + i);
        }
    }

    [Test]
    public void BlueprintItemsPointToKnownEquipmentItems()
    {
        string[] blueprintIds = InventoryItemCatalog.GetAllBlueprintItemIds();

        Assert.That(blueprintIds, Is.Not.Empty);
        AssertUnique(blueprintIds, "blueprint id");

        foreach (string blueprintId in blueprintIds)
        {
            AssertKnownItemId(blueprintId, "blueprint");
            string targetId = InventoryItemCatalog.GetBlueprintTargetItemId(blueprintId);
            AssertKnownItemId(targetId, "blueprint target for " + blueprintId);
            Assert.That(InventoryItemCatalog.GetItemType(targetId), Is.EqualTo(InventoryItemType.Equipment), blueprintId + " should unlock equipment.");
        }
    }

    [Test]
    public void EquipmentItemsFitAtLeastOneEquipmentSlot()
    {
        foreach (InventoryItemDefinition item in InventoryItemCatalog.GetAllDefinitions())
        {
            if (item.ItemType != InventoryItemType.Equipment)
                continue;

            bool fitsAnySlot = Enumerable.Range(0, PlayerInventoryData.EquipmentSlotCount)
                .Any(slot => InventoryItemCatalog.IsCompatibleWithEquipmentSlot(item.Id, slot));

            Assert.That(fitsAnySlot, Is.True, item.Id + " is equipment but does not fit any equipment slot.");
        }
    }

    [Test]
    public void PilotDefinitionsAreUniqueAndRoundTripThroughCatalog()
    {
        IReadOnlyList<PilotDefinition> pilots = PilotCatalog.AllDefinitions;

        Assert.That(pilots, Is.Not.Empty);
        AssertUnique(pilots.Select(pilot => pilot.Id), "pilot id");

        foreach (PilotDefinition pilot in pilots)
        {
            Assert.That(pilot.Id, Is.EqualTo(NormalizeId(pilot.Id)), "Pilot id should be normalized: " + pilot.Id);
            Assert.That(pilot.DisplayName, Is.Not.Null.And.Not.Empty, "Missing display name for " + pilot.Id);
            Assert.That(PilotCatalog.GetDefinition(pilot.Id), Is.SameAs(pilot), "Pilot should round-trip by id: " + pilot.Id);
            Assert.That(pilot.AbilityDescriptions, Is.Not.Null, "Ability descriptions should not be null for " + pilot.Id);
        }

        CollectionAssert.Contains(PilotCatalog.GetDefaultUnlockedPilotIds(), PilotCatalog.JakeId);
        Assert.That(PilotCatalog.NormalizePilotId(" missing "), Is.EqualTo(PilotCatalog.JakeId));
    }

    [Test]
    public void ShipDefinitionsHaveValidSkinsStatsAndSlots()
    {
        string[] shipTypeIds = ShipCatalog.GetAllShipTypeIds();

        Assert.That(shipTypeIds, Is.Not.Empty);
        AssertUnique(shipTypeIds, "ship type id");
        CollectionAssert.Contains(ShipCatalog.GetDefaultUnlockedShipTypeIds(), ShipCatalog.GetShipTypeId(ShipType.Explorer));

        foreach (string shipTypeId in shipTypeIds)
        {
            Assert.That(ShipCatalog.TryGetShipTypeFromId(shipTypeId, out ShipType shipType), Is.True);
            PlayerShipDefinition ship = ShipCatalog.GetShipDefinition(shipType);

            Assert.That(ship.DisplayName, Is.Not.Null.And.Not.Empty, "Missing ship display name for " + shipTypeId);
            Assert.That(ship.SkinIndices, Is.Not.Empty, "Missing skins for " + shipTypeId);
            Assert.That(ship.CargoCapacity, Is.InRange(1, PlayerInventoryData.ShipSlotCount), "Invalid cargo capacity for " + shipTypeId);
            Assert.That(ship.SafePocketSlots, Is.InRange(0, ship.CargoCapacity), "Invalid safe pocket count for " + shipTypeId);
            Assert.That(ship.BaseHp, Is.GreaterThan(0), "Invalid HP for " + shipTypeId);
            Assert.That(ship.BaseShield, Is.GreaterThanOrEqualTo(0), "Invalid shield for " + shipTypeId);
            Assert.That(ship.BaseSpeed, Is.GreaterThan(0f), "Invalid speed for " + shipTypeId);
            Assert.That(ship.BoosterDuration, Is.GreaterThan(0f), "Invalid booster duration for " + shipTypeId);
            Assert.That(ship.ThrusterOffsetFactors, Is.Not.Null.And.Not.Empty, "Missing thruster offsets for " + shipTypeId);

            int enabledSlots = CountEnabledEquipmentSlots(ship.SkinIndices[0]);
            Assert.That(enabledSlots, Is.GreaterThan(0), "Ship should expose at least one equipment slot: " + shipTypeId);
            Assert.That(enabledSlots, Is.LessThanOrEqualTo(PlayerInventoryData.EquipmentSlotCount), "Too many equipment slots for " + shipTypeId);

            foreach (int skinIndex in ship.SkinIndices)
            {
                Assert.That(skinIndex, Is.InRange(0, ShipCatalog.MaxShipSkinIndex), "Skin index out of range for " + shipTypeId);
                Assert.That(ShipCatalog.GetShipDefinition(skinIndex), Is.SameAs(ship), "Skin should resolve back to its ship: " + skinIndex);
                Assert.That(ShipCatalog.GetSkinDisplayName(skinIndex), Is.Not.Null.And.Not.Empty, "Missing skin display name for " + skinIndex);
                Assert.That(ShipCatalog.GetShipSkinResourcePath(skinIndex), Is.Not.Null.And.Not.Empty, "Missing skin resource path for " + skinIndex);
            }
        }
    }

    [Test]
    public void LobbyMapsHaveValidDefaultsAndEnemyPresets()
    {
        IReadOnlyList<LobbyMapDefinition> maps = LobbyMapCatalog.AllMaps;

        Assert.That(maps, Is.Not.Empty);
        Assert.That(LobbyMapCatalog.GetDefault(), Is.Not.Null);
        AssertUnique(maps.Select(map => map.Id), "map id");

        foreach (LobbyMapDefinition map in maps)
        {
            Assert.That(map.Id, Is.EqualTo(NormalizeId(map.Id)), "Map id should be normalized: " + map.Id);
            Assert.That(map.DisplayName, Is.Not.Null.And.Not.Empty, "Missing display name for map " + map.Id);
            Assert.That(map.Description, Is.Not.Null.And.Not.Empty, "Missing description for map " + map.Id);
            Assert.That(map.RoundDurationSeconds, Is.GreaterThan(0f), "Invalid duration for map " + map.Id);
            Assert.That(map.MapSize, Is.Not.Null.And.Not.Empty, "Missing size for map " + map.Id);
            Assert.That(map.LoneShipTimerMultiplier, Is.GreaterThan(0f), "Invalid lone ship multiplier for map " + map.Id);
            Assert.That(map.ObstacleHp, Is.GreaterThanOrEqualTo(0), "Invalid obstacle HP for map " + map.Id);
            Assert.That(map.ObstacleSizePercent, Is.GreaterThan(0), "Invalid obstacle size for map " + map.Id);
            Assert.That(map.ExtractionZoneCount, Is.GreaterThanOrEqualTo(0), "Invalid extraction count for map " + map.Id);
            Assert.That(map.RepairBayCount, Is.GreaterThanOrEqualTo(0), "Invalid repair bay count for map " + map.Id);
            Assert.That(map.SpaceFactoryCount, Is.GreaterThanOrEqualTo(0), "Invalid space factory count for map " + map.Id);
            Assert.That(map.EnemyPresets, Is.Not.Null, "Enemy presets should not be null for map " + map.Id);

            foreach (LobbyEnemyMapPreset preset in map.EnemyPresets)
            {
                Assert.That(preset.Count, Is.GreaterThanOrEqualTo(0), "Invalid enemy count for " + preset.Kind + " on " + map.Id);
                Assert.That(preset.Hp, Is.GreaterThanOrEqualTo(0), "Invalid enemy HP for " + preset.Kind + " on " + map.Id);
                Assert.That(preset.Shield, Is.GreaterThanOrEqualTo(0), "Invalid enemy shield for " + preset.Kind + " on " + map.Id);
                Assert.That(preset.SpeedMultiplier, Is.GreaterThan(0f), "Invalid enemy speed for " + preset.Kind + " on " + map.Id);
            }
        }
    }

    static int CountEnabledEquipmentSlots(int skinIndex)
    {
        int count = 0;
        for (int i = 0; i < PlayerInventoryData.EquipmentSlotCount; i++)
        {
            if (ShipCatalog.IsEquipmentSlotEnabled(i, skinIndex))
                count++;
        }

        return count;
    }

    static void AssertKnownItemIds(IEnumerable<string> itemIds, string context)
    {
        if (itemIds == null)
            return;

        foreach (string itemId in itemIds)
            AssertKnownItemId(itemId, context);
    }

    static void AssertKnownItemId(string itemId, string context)
    {
        Assert.That(itemId, Is.Not.Null.And.Not.Empty, "Missing item id for " + context);
        Assert.That(InventoryItemCatalog.GetDefinition(itemId), Is.Not.Null, "Unknown item id '" + itemId + "' in " + context);
    }

    static void AssertUnique(IEnumerable<string> ids, string label)
    {
        string[] values = ids.ToArray();
        string duplicate = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .FirstOrDefault();

        Assert.That(duplicate, Is.Null, "Duplicate " + label + ": " + duplicate);
    }

    static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant().Replace(" ", "_");
    }
}
