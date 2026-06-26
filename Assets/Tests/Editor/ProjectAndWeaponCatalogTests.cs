using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public sealed class ProjectAndWeaponCatalogTests
{
    [Test]
    public void ProjectsHaveValidStagesStepsRewardsAndUnlockRequirements()
    {
        IReadOnlyList<ProjectDefinition> projects = ProjectCatalog.AllProjects;

        Assert.That(projects, Is.Not.Empty);
        AssertUnique(projects.Select(project => project.Id), "project id");
        Assert.That(ProjectCatalog.GetDefault(), Is.Not.Null);

        foreach (ProjectDefinition project in projects)
        {
            Assert.That(project.Id, Is.EqualTo(NormalizeId(project.Id)), "Project id should be normalized: " + project.Id);
            Assert.That(project.DisplayName, Is.Not.Null.And.Not.Empty, "Missing display name for " + project.Id);
            Assert.That(project.Description, Is.Not.Null.And.Not.Empty, "Missing description for " + project.Id);
            Assert.That(project.Stages, Is.Not.Null.And.Not.Empty, "Project should have stages: " + project.Id);

            AssertValidUnlockRequirements(project);

            for (int stageIndex = 0; stageIndex < project.Stages.Length; stageIndex++)
            {
                ProjectStageDefinition stage = project.Stages[stageIndex];
                Assert.That(stage, Is.Not.Null, "Null stage in " + project.Id);
                Assert.That(stage.Id, Is.Not.Null.And.Not.Empty, "Missing stage id in " + project.Id);
                Assert.That(stage.DisplayName, Is.Not.Null.And.Not.Empty, "Missing stage display name in " + project.Id);
                Assert.That(stage.Steps, Is.Not.Null.And.Not.Empty, "Missing steps in " + project.Id + " stage " + stageIndex);
                AssertValidReward(stage.Reward, project.Id + " stage " + stageIndex);
                AssertUnique(stage.Steps.Select(step => step.Id), "step id in " + project.Id + " stage " + stageIndex);

                foreach (ProjectStepDefinition step in stage.Steps)
                    AssertValidStep(step, project.Id + " stage " + stageIndex);
            }
        }
    }

    [Test]
    public void WeaponProfilesAndEditableParametersAreValid()
    {
        IReadOnlyList<string> weaponIds = WeaponAttackCatalog.GetEditableWeaponIds();
        IReadOnlyList<WeaponAttackParameterDefinition> parameters = WeaponAttackCatalog.GetEditableParameters();

        Assert.That(weaponIds, Is.Not.Empty);
        Assert.That(parameters, Is.Not.Empty);
        AssertUnique(weaponIds, "weapon id");
        AssertUnique(parameters.Select(parameter => parameter.Key), "weapon parameter key");

        foreach (string weaponId in weaponIds)
        {
            WeaponAttackProfile profile = WeaponAttackCatalog.GetDefaultNormalAttackByWeaponId(weaponId);

            Assert.That(profile, Is.Not.Null, "Missing default profile for " + weaponId);
            Assert.That(profile.Id, Is.Not.Null.And.Not.Empty, "Missing profile id for " + weaponId);
            Assert.That(profile.DisplayName, Is.Not.Null.And.Not.Empty, "Missing profile display name for " + weaponId);
            Assert.That(profile.MaxAmmo, Is.GreaterThan(0), "Invalid ammo for " + weaponId);
            Assert.That(profile.RangeMultiplier, Is.GreaterThan(0f), "Invalid range for " + weaponId);
            Assert.That(profile.ProjectileSize, Is.GreaterThan(0f), "Invalid projectile size for " + weaponId);
            Assert.That(profile.AttackCooldown, Is.GreaterThan(0f), "Invalid attack cooldown for " + weaponId);
            Assert.That(profile.AmmoReloadTime, Is.GreaterThanOrEqualTo(0f), "Invalid reload time for " + weaponId);
            Assert.That(profile.ProjectileCount, Is.GreaterThan(0), "Invalid projectile count for " + weaponId);
            Assert.That(profile.HpDamage + profile.ShieldDamage, Is.GreaterThan(0), "Weapon should deal damage: " + weaponId);
        }

        foreach (WeaponAttackParameterDefinition parameter in parameters)
        {
            Assert.That(parameter.Key, Is.Not.Null.And.Not.Empty, "Missing key for weapon parameter.");
            Assert.That(parameter.Key.Trim(), Is.EqualTo(parameter.Key), "Parameter key should not have edge whitespace: " + parameter.Key);
            Assert.That(parameter.Label, Is.Not.Null.And.Not.Empty, "Missing label for parameter " + parameter.Key);
            Assert.That(parameter.ValueType, Is.Not.Null.And.Not.Empty, "Missing value type for parameter " + parameter.Key);
            Assert.That(parameter.Max, Is.GreaterThanOrEqualTo(parameter.Min), "Invalid range for parameter " + parameter.Key);
        }
    }

    [Test]
    public void WeaponItemsResolveToKnownWeaponProfiles()
    {
        foreach (InventoryItemDefinition item in InventoryItemCatalog.GetAllDefinitions())
        {
            if (item.Category != InventoryItemCategory.Weapon)
                continue;

            string weaponId = WeaponAttackCatalog.GetWeaponIdForItem(item.Id);
            WeaponAttackProfile profile = WeaponAttackCatalog.GetDefaultNormalAttackByWeaponId(weaponId);

            Assert.That(weaponId, Is.Not.Null.And.Not.Empty, "Missing weapon id for item " + item.Id);
            Assert.That(profile, Is.Not.Null, "Missing weapon profile for item " + item.Id);
            Assert.That(profile.Id, Is.Not.Null.And.Not.Empty, "Missing weapon profile id for item " + item.Id);
        }
    }

    static void AssertValidStep(ProjectStepDefinition step, string context)
    {
        Assert.That(step, Is.Not.Null, "Null step in " + context);
        Assert.That(step.Id, Is.Not.Null.And.Not.Empty, "Missing step id in " + context);
        Assert.That(step.DisplayName, Is.Not.Null.And.Not.Empty, "Missing step display name in " + context);
        Assert.That(step.RequiredCount, Is.GreaterThan(0), "Invalid required count for " + step.Id + " in " + context);

        string iconItemId = step.ResolveIconItemId();
        AssertKnownItemId(iconItemId, "icon for " + step.Id + " in " + context);

        if (step.RequirementKind == ProjectRequirementKind.ExactItem)
            AssertKnownItemId(step.ItemId, "required item for " + step.Id + " in " + context);
    }

    static void AssertValidReward(ProjectRewardDefinition reward, string context)
    {
        Assert.That(reward, Is.Not.Null, "Missing reward for " + context);
        Assert.That(reward.Astrons, Is.GreaterThanOrEqualTo(0), "Invalid Astrons reward for " + context);

        if (reward.ItemIds == null)
            return;

        foreach (string itemId in reward.ItemIds)
            AssertKnownItemId(itemId, "reward in " + context);
    }

    static void AssertValidUnlockRequirements(ProjectDefinition project)
    {
        if (project.UnlockRequirements == null)
            return;

        foreach (ProjectUnlockRequirementDefinition requirement in project.UnlockRequirements)
        {
            Assert.That(requirement, Is.Not.Null, "Null unlock requirement for " + project.Id);
            Assert.That(requirement.RequirementText, Is.Not.Null.And.Not.Empty, "Missing unlock text for " + project.Id);

            if (requirement.Kind == ProjectUnlockRequirementKind.AnyProjectComplete)
                continue;

            Assert.That(requirement.ProjectId, Is.Not.Null.And.Not.Empty, "Missing unlock project id for " + project.Id);
            ProjectDefinition requiredProject = ProjectCatalog.Get(requirement.ProjectId);
            Assert.That(requiredProject, Is.Not.Null, "Unknown unlock project id " + requirement.ProjectId + " for " + project.Id);

            if (requirement.Kind == ProjectUnlockRequirementKind.ProjectStageUnlocked)
                Assert.That(requirement.StageIndex, Is.InRange(0, requiredProject.Stages.Length - 1), "Invalid unlock stage index for " + project.Id);
        }
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
