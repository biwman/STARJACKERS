using System;
using System.Collections.Generic;
using UnityEngine;

public enum ProjectRequirementKind
{
    ExactItem,
    AnyContainer,
    AnySpaceJunk
}

[Serializable]
public sealed class ProjectRewardDefinition
{
    public int Astrons;
    public string[] ItemIds;
}

[Serializable]
public sealed class ProjectStepDefinition
{
    public string Id;
    public string DisplayName;
    public string ItemId;
    public string IconItemId;
    public int RequiredCount;
    public ProjectRequirementKind RequirementKind;

    public bool MatchesItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        switch (RequirementKind)
        {
            case ProjectRequirementKind.AnyContainer:
                return InventoryItemCatalog.IsContainerItem(itemId);
            case ProjectRequirementKind.AnySpaceJunk:
                return InventoryItemCatalog.GetCategory(itemId) == InventoryItemCategory.SpaceJunk;
            default:
                return string.Equals(itemId, ItemId, StringComparison.Ordinal);
        }
    }

    public string ResolveIconItemId()
    {
        if (!string.IsNullOrWhiteSpace(IconItemId))
            return IconItemId;

        if (!string.IsNullOrWhiteSpace(ItemId))
            return ItemId;

        if (RequirementKind == ProjectRequirementKind.AnyContainer)
            return InventoryItemCatalog.GetContainerItemId(0);

        if (RequirementKind == ProjectRequirementKind.AnySpaceJunk)
            return InventoryItemCatalog.SpaceJunkStandardId;

        return null;
    }
}

[Serializable]
public sealed class ProjectStageDefinition
{
    public string Id;
    public string DisplayName;
    public ProjectStepDefinition[] Steps;
    public ProjectRewardDefinition Reward;
}

[Serializable]
public sealed class ProjectDefinition
{
    public string Id;
    public string DisplayName;
    public string Description;
    public string TileResourcePath;
    public string BackgroundResourcePath;
    public ProjectStageDefinition[] Stages;
}

public static class ProjectCatalog
{
    public const string SupplyToSurviveId = "supply_to_survive";
    public const string SpaceMayhemId = "space_mayhem";

    static readonly ProjectDefinition[] Projects = BuildProjects();
    static readonly Dictionary<string, ProjectDefinition> ProjectsById = BuildProjectsById();

    public static IReadOnlyList<ProjectDefinition> AllProjects => Projects;

    public static ProjectDefinition Get(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            return null;

        ProjectsById.TryGetValue(projectId, out ProjectDefinition project);
        return project;
    }

    public static ProjectDefinition GetDefault()
    {
        return Projects.Length > 0 ? Projects[0] : null;
    }

    public static PlayerProjectProgressData NormalizeProgress(PlayerProjectProgressData progress)
    {
        PlayerProjectProgressData normalized = progress != null ? progress.Clone() : new PlayerProjectProgressData();
        if (normalized.Entries == null)
            normalized.Entries = Array.Empty<PlayerProjectProgressEntry>();

        List<PlayerProjectProgressEntry> entries = new List<PlayerProjectProgressEntry>();
        for (int i = 0; i < Projects.Length; i++)
        {
            ProjectDefinition project = Projects[i];
            PlayerProjectProgressEntry entry = FindEntry(normalized.Entries, project.Id);
            entries.Add(NormalizeProjectEntry(project, entry));
        }

        normalized.Entries = entries.ToArray();
        return normalized;
    }

    public static PlayerProjectProgressEntry GetProjectProgress(PlayerProjectProgressData progress, string projectId)
    {
        PlayerProjectProgressData normalized = NormalizeProgress(progress);
        return FindEntry(normalized.Entries, projectId);
    }

    public static bool IsProjectComplete(PlayerProjectProgressData progress, string projectId)
    {
        ProjectDefinition project = Get(projectId);
        PlayerProjectProgressEntry entry = GetProjectProgress(progress, projectId);
        if (project == null || entry == null || project.Stages == null)
            return false;

        for (int i = 0; i < project.Stages.Length; i++)
        {
            if (!IsStageComplete(project, entry, i))
                return false;
        }

        return project.Stages.Length > 0;
    }

    public static bool IsStageUnlocked(PlayerProjectProgressData progress, string projectId, int stageIndex)
    {
        ProjectDefinition project = Get(projectId);
        PlayerProjectProgressEntry entry = GetProjectProgress(progress, projectId);
        if (project == null || entry == null || stageIndex < 0 || project.Stages == null || stageIndex >= project.Stages.Length)
            return false;

        for (int i = 0; i < stageIndex; i++)
        {
            if (!IsStageComplete(project, entry, i))
                return false;
        }

        return true;
    }

    public static bool IsStageComplete(ProjectDefinition project, PlayerProjectProgressEntry progress, int stageIndex)
    {
        if (project == null || progress == null || project.Stages == null || stageIndex < 0 || stageIndex >= project.Stages.Length)
            return false;

        ProjectStageDefinition stage = project.Stages[stageIndex];
        PlayerProjectStageProgress stageProgress = progress.GetStage(stageIndex);
        if (stage == null || stage.Steps == null || stageProgress == null)
            return false;

        for (int i = 0; i < stage.Steps.Length; i++)
        {
            ProjectStepDefinition step = stage.Steps[i];
            if (step == null || stageProgress.GetDelivered(i) < Mathf.Max(0, step.RequiredCount))
                return false;
        }

        return stage.Steps.Length > 0;
    }

    static PlayerProjectProgressEntry NormalizeProjectEntry(ProjectDefinition project, PlayerProjectProgressEntry source)
    {
        PlayerProjectProgressEntry entry = source != null ? source.Clone() : new PlayerProjectProgressEntry();
        entry.ProjectId = project.Id;

        int stageCount = project.Stages != null ? project.Stages.Length : 0;
        PlayerProjectStageProgress[] oldStages = entry.Stages;
        entry.Stages = new PlayerProjectStageProgress[stageCount];
        for (int stageIndex = 0; stageIndex < stageCount; stageIndex++)
        {
            ProjectStageDefinition stage = project.Stages[stageIndex];
            PlayerProjectStageProgress oldStage = oldStages != null && stageIndex < oldStages.Length ? oldStages[stageIndex] : null;
            PlayerProjectStageProgress newStage = oldStage != null ? oldStage.Clone() : new PlayerProjectStageProgress();
            int stepCount = stage.Steps != null ? stage.Steps.Length : 0;
            int[] oldDelivered = newStage.DeliveredCounts;
            newStage.DeliveredCounts = new int[stepCount];
            for (int stepIndex = 0; stepIndex < stepCount; stepIndex++)
            {
                int required = Mathf.Max(0, stage.Steps[stepIndex].RequiredCount);
                int delivered = oldDelivered != null && stepIndex < oldDelivered.Length ? oldDelivered[stepIndex] : 0;
                newStage.DeliveredCounts[stepIndex] = Mathf.Clamp(delivered, 0, required);
            }

            entry.Stages[stageIndex] = newStage;
        }

        return entry;
    }

    static PlayerProjectProgressEntry FindEntry(PlayerProjectProgressEntry[] entries, string projectId)
    {
        if (entries == null || string.IsNullOrWhiteSpace(projectId))
            return null;

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null && string.Equals(entries[i].ProjectId, projectId, StringComparison.Ordinal))
                return entries[i];
        }

        return null;
    }

    static Dictionary<string, ProjectDefinition> BuildProjectsById()
    {
        Dictionary<string, ProjectDefinition> result = new Dictionary<string, ProjectDefinition>(StringComparer.Ordinal);
        for (int i = 0; i < Projects.Length; i++)
        {
            if (Projects[i] != null && !string.IsNullOrWhiteSpace(Projects[i].Id))
                result[Projects[i].Id] = Projects[i];
        }

        return result;
    }

    static ProjectDefinition[] BuildProjects()
    {
        return new[]
        {
            new ProjectDefinition
            {
                Id = SupplyToSurviveId,
                DisplayName = "SUPPLY TO SURVIVE",
                Description = "The raider guided his old cargo ship through the asteroid field, scanning the drifting rocks in silence. The hull rattled with every maneuver, held together by cheap repairs and luck. But the ship still flew, and that was enough.\n\nBack on the station near Callisto, people depended on him. His family needed money to survive. His friends needed work, food, and a reason to believe things could still get better. Every trip into deep space was a risk, but staying home meant watching them slowly lose everything.\n\nHe wasn't searching for glory. Just enough credits to keep everyone alive a little longer.\n\nAs the ship's scanners swept across the field, a warning light suddenly flashed on the console.\n\nUnknown ships approaching fast.\n\nThe raider tightened his grip on the controls and stared into the darkness ahead. Around here, desperate people were often more dangerous than warships.",
                TileResourcePath = "SUPPLY_TO_SURVIVE_PROJECT",
                BackgroundResourcePath = "SUPPLY_TO_SURVIVE_PROJECT",
                Stages = new[]
                {
                    Stage("stage_1", "STAGE 1",
                        Reward(10000, InventoryItemCatalog.PlasmaGunId),
                        Exact("common_asteroid", "Common Asteroid", InventoryItemCatalog.AsteroidCommonId, 20),
                        Exact("uncommon_asteroid", "Uncommon Asteroid", InventoryItemCatalog.AsteroidUncommonId, 10),
                        Exact("rare_asteroid", "Rare Asteroid", InventoryItemCatalog.AsteroidRareId, 5)),
                    Stage("stage_2", "STAGE 2",
                        Reward(20000, InventoryItemCatalog.FusionEngineId),
                        AnyContainer("containers", "Any Container", 30),
                        AnySpaceJunk("space_junk", "Any Space Junk", 10),
                        Exact("very_rare_asteroid", "Very Rare Asteroid", InventoryItemCatalog.AsteroidVeryRareId, 3)),
                    Stage("stage_3", "STAGE 3",
                        Reward(30000, InventoryItemCatalog.RailGunId),
                        Exact("cash_suitcase", "Cash Suitcase", InventoryItemCatalog.CashSuitcaseId, 1),
                        Exact("legendary_asteroid", "Legendary Asteroid", InventoryItemCatalog.AsteroidLegendaryId, 1),
                        Exact("corsair_wreck", "Corsair Wreck", InventoryItemCatalog.CorsairSalvageId, 1))
                }
            },
            new ProjectDefinition
            {
                Id = SpaceMayhemId,
                DisplayName = "SPACE MAYHEM",
                Description = "The raider powered down his ship's main lights and drifted silently in the shadow of a large asteroid. Ahead of him, deep inside the red-orange glow of the nebula, the battle raged on. Engine trails cut across the darkness between drifting rocks, while flashes of cannon fire reflected across his cockpit like distant storms over an ocean.\n\nHe wasn't a soldier. He hauled spare parts between colonies near Mars, sometimes food and medical supplies to mining stations beyond Jupiter. He stayed away from wars. Wars usually meant inspections, debts, or bodies floating in the vacuum.\n\nBut this felt different.\n\nA few minutes earlier, he had intercepted part of a damaged transmission. No one was asking for reinforcements or ammunition. They were talking about a discovery - something unique and valuable buried inside one of the asteroids.\n\nThe raider glanced at the radar display. One side of the battle was desperately trying to escort a ship carrying something out of the combat zone. The opposing fleet had thrown everything they had after it.\n\nNo matter who wins, there will be plenty of wrecks left to scavenge...",
                TileResourcePath = "SPACE_MAYHEM",
                BackgroundResourcePath = "SPACE_MAYHEM",
                Stages = new[]
                {
                    Stage("stage_1", "STAGE 1",
                        Reward(5000),
                        Exact("drone_wrecks", "Drone Wreck", InventoryItemCatalog.DroidScrapId, 3),
                        Exact("neutral_fighter_wrecks", "Neutral Fighter Wreck", InventoryItemCatalog.NeutralFighterSalvageId, 5)),
                    Stage("stage_2", "STAGE 2",
                        Reward(6000),
                        Exact("rescue_ship_wreck", "Rescue Ship Wreck", InventoryItemCatalog.RescueShipSalvageId, 1),
                        Exact("corsair_wrecks", "Corsair Wreck", InventoryItemCatalog.CorsairSalvageId, 2)),
                    Stage("stage_3", "STAGE 3",
                        Reward(7000),
                        Exact("space_mine_wrecks", "Space Mine Wreck", InventoryItemCatalog.SpaceMineWreckId, 10),
                        Exact("pirate_fighter_wreck", "Pirate Fighter Wreck", InventoryItemCatalog.PirateFighterSalvageId, 1)),
                    Stage("stage_4", "STAGE 4",
                        Reward(10000, InventoryItemCatalog.AlienTransmitterId),
                        Exact("radar_ship_wreck", "Radar Ship Wreck", InventoryItemCatalog.RadarShipSalvageId, 1),
                        Exact("pirate_fighter_wrecks", "Pirate Fighter Wreck", InventoryItemCatalog.PirateFighterSalvageId, 3))
                }
            }
        };
    }

    static ProjectStageDefinition Stage(string id, string displayName, ProjectRewardDefinition reward, params ProjectStepDefinition[] steps)
    {
        return new ProjectStageDefinition
        {
            Id = id,
            DisplayName = displayName,
            Reward = reward,
            Steps = steps ?? Array.Empty<ProjectStepDefinition>()
        };
    }

    static ProjectRewardDefinition Reward(int astrons, params string[] itemIds)
    {
        return new ProjectRewardDefinition
        {
            Astrons = Mathf.Max(0, astrons),
            ItemIds = itemIds ?? Array.Empty<string>()
        };
    }

    static ProjectStepDefinition Exact(string id, string displayName, string itemId, int requiredCount)
    {
        return new ProjectStepDefinition
        {
            Id = id,
            DisplayName = displayName,
            ItemId = itemId,
            IconItemId = itemId,
            RequiredCount = Mathf.Max(1, requiredCount),
            RequirementKind = ProjectRequirementKind.ExactItem
        };
    }

    static ProjectStepDefinition AnyContainer(string id, string displayName, int requiredCount)
    {
        return new ProjectStepDefinition
        {
            Id = id,
            DisplayName = displayName,
            IconItemId = InventoryItemCatalog.GetContainerItemId(0),
            RequiredCount = Mathf.Max(1, requiredCount),
            RequirementKind = ProjectRequirementKind.AnyContainer
        };
    }

    static ProjectStepDefinition AnySpaceJunk(string id, string displayName, int requiredCount)
    {
        return new ProjectStepDefinition
        {
            Id = id,
            DisplayName = displayName,
            IconItemId = InventoryItemCatalog.SpaceJunkStandardId,
            RequiredCount = Mathf.Max(1, requiredCount),
            RequirementKind = ProjectRequirementKind.AnySpaceJunk
        };
    }
}

[Serializable]
public sealed class PlayerProjectProgressData
{
    public PlayerProjectProgressEntry[] Entries;

    public PlayerProjectProgressData Clone()
    {
        PlayerProjectProgressEntry[] clonedEntries = Entries != null
            ? new PlayerProjectProgressEntry[Entries.Length]
            : Array.Empty<PlayerProjectProgressEntry>();

        for (int i = 0; i < clonedEntries.Length; i++)
            clonedEntries[i] = Entries[i]?.Clone();

        return new PlayerProjectProgressData { Entries = clonedEntries };
    }
}

[Serializable]
public sealed class PlayerProjectProgressEntry
{
    public string ProjectId;
    public PlayerProjectStageProgress[] Stages;

    public PlayerProjectProgressEntry Clone()
    {
        PlayerProjectStageProgress[] clonedStages = Stages != null
            ? new PlayerProjectStageProgress[Stages.Length]
            : Array.Empty<PlayerProjectStageProgress>();

        for (int i = 0; i < clonedStages.Length; i++)
            clonedStages[i] = Stages[i]?.Clone();

        return new PlayerProjectProgressEntry
        {
            ProjectId = ProjectId,
            Stages = clonedStages
        };
    }

    public PlayerProjectStageProgress GetStage(int index)
    {
        if (Stages == null || index < 0 || index >= Stages.Length)
            return null;

        return Stages[index];
    }
}

[Serializable]
public sealed class PlayerProjectStageProgress
{
    public int[] DeliveredCounts;
    public bool RewardClaimed;

    public PlayerProjectStageProgress Clone()
    {
        return new PlayerProjectStageProgress
        {
            DeliveredCounts = DeliveredCounts != null ? (int[])DeliveredCounts.Clone() : Array.Empty<int>(),
            RewardClaimed = RewardClaimed
        };
    }

    public int GetDelivered(int stepIndex)
    {
        if (DeliveredCounts == null || stepIndex < 0 || stepIndex >= DeliveredCounts.Length)
            return 0;

        return Mathf.Max(0, DeliveredCounts[stepIndex]);
    }

    public void SetDelivered(int stepIndex, int value)
    {
        if (DeliveredCounts == null || stepIndex < 0 || stepIndex >= DeliveredCounts.Length)
            return;

        DeliveredCounts[stepIndex] = Mathf.Max(0, value);
    }
}

public sealed class ProjectCommitResult
{
    public bool Success;
    public int CommittedCount;
    public bool StageCompleted;
    public bool RewardClaimed;
    public string Message;
}
