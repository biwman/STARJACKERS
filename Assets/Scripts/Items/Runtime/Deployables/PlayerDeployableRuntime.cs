using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public static class PlayerDeployableRuntime
{
    public const string AutoTurretMarker = "player_auto_turret";
    public const string RocketAutoTurretMarker = "player_rocket_auto_turret";
    public const string WarBaseRocketAutoTurretMarker = "war_base_rocket_auto_turret";
    public const string ViperContainerHaulerMarker = "viper_container_hauler";
    public const string BisonIndustrialPartsMarker = "bison_industrial_parts";
    public const string ContainerShipAutoCannonMarker = "container_ship_auto_cannon";
    public const string SpaceBombMarker = "player_space_bomb";
    public const string StasisBuoyMarker = "player_stasis_buoy";
    public const string SpaceTorpedoMarker = "player_space_torpedo";
    public const string AsteroidBreacherBombMarker = "player_asteroid_breacher_bomb";
    public const string MetalDriftWallMarker = "player_metal_drift_wall";
    public const string SpaceDrillMarker = "player_space_drill";
    public const string DropbotMarker = "player_dropbot";

    public static void PrewarmRoundAssets()
    {
        RuntimeSpriteUtility.LoadSprite("auto_turret_top_down_resource", "Assets/auto_turret_top_down.png");
        RuntimeSpriteUtility.LoadSprite("Items/rocket_autoturret", "Assets/Resources/Items/rocket_autoturret.png");
        RuntimeSpriteUtility.LoadSprite("Items/space_bomb_gadget_bullet", "Assets/space_bomb_gadget_bullet.png");
        RuntimeSpriteUtility.LoadSprite("Items/stasis_buoy", "Assets/Resources/Items/stasis_buoy.png");
        RuntimeSpriteUtility.LoadSprite("Items/space_torpedo", "Assets/Resources/Items/space_torpedo.png");
        RuntimeSpriteUtility.LoadSprite("Items/space_torpedo_projectile", "Assets/Resources/Items/space_torpedo_projectile.png");
        RuntimeSpriteUtility.LoadSprite("Items/asteroid_breacher_bomb", "Assets/Resources/Items/asteroid_breacher_bomb.png");
        RuntimeSpriteUtility.LoadSprite("Items/metal_drift_wall", "Assets/Resources/Items/metal_drift_wall.png");
        RuntimeSpriteUtility.LoadSprite("space_drill_top_down_resource", "Assets/space_drill_top_down.png");
        RuntimeSpriteUtility.LoadSprite("space_trap_top_down_resource", "Assets/space_trap_top_down.png");
        RuntimeSpriteUtility.LoadSprite("looting_friend_top_down_resource", "Assets/looting_friend_top_down.png");
        RuntimeSpriteUtility.LoadSprite("firing_friend_up_resource", "Assets/firing_friend_up.png");
        RuntimeSpriteUtility.LoadSprite("dropbot_up_resource", "Assets/Resources/dropbot_up_resource.png");
        RuntimeSpriteUtility.LoadSprite("lure_beacon_onmap_resource", string.Empty);
        RuntimeSpriteUtility.LoadSprite("Items/escape_pod", string.Empty);
        RuntimeSpriteUtility.LoadSprite("Visuals/Bases/industrial_zone", "Assets/Resources/Visuals/Bases/industrial_zone.png");
        RuntimeSpriteUtility.PrewarmSprites(
            "Visuals/IndustrialParts/industrial_parts_01",
            "Visuals/IndustrialParts/industrial_parts_02",
            "Visuals/IndustrialParts/industrial_parts_03",
            "Visuals/IndustrialParts/industrial_parts_04",
            "Visuals/IndustrialParts/industrial_parts_05",
            "Visuals/IndustrialParts/industrial_parts_06");

        RuntimeSpriteUtility.CreateArrowSprite();
    }

    public static bool IsInstantiationData(object[] data)
    {
        return data != null &&
               data.Length > 0 &&
               data[0] is string marker &&
               (marker == AutoTurretMarker || marker == RocketAutoTurretMarker || marker == WarBaseRocketAutoTurretMarker || marker == ViperContainerHaulerMarker || marker == BisonIndustrialPartsMarker || marker == ContainerShipAutoCannonMarker || marker == SpaceBombMarker || marker == StasisBuoyMarker || marker == SpaceTorpedoMarker || marker == AsteroidBreacherBombMarker || marker == MetalDriftWallMarker || marker == SpaceDrillMarker || marker == DropbotMarker);
    }

    public static bool IsContainerShipAutoCannonData(object[] data)
    {
        return data != null &&
               data.Length > 0 &&
               data[0] is string marker &&
               marker == ContainerShipAutoCannonMarker;
    }

    public static bool IsComputerOwnedDeployableData(object[] data)
    {
        return IsContainerShipAutoCannonData(data) || IsWarBaseRocketAutoTurretData(data) || IsViperContainerHaulerData(data);
    }

    public static bool IsViperContainerHaulerData(object[] data)
    {
        return data != null &&
               data.Length > 0 &&
               data[0] is string marker &&
               marker == ViperContainerHaulerMarker;
    }

    public static bool IsWarBaseRocketAutoTurretData(object[] data)
    {
        return data != null &&
               data.Length > 0 &&
               data[0] is string marker &&
               marker == WarBaseRocketAutoTurretMarker;
    }

    public static bool IsBisonIndustrialPartsData(object[] data)
    {
        return data != null &&
               data.Length > 0 &&
               data[0] is string marker &&
               marker == BisonIndustrialPartsMarker;
    }

    public static PlayerDeployableBase EnsureAttached(GameObject target)
    {
        if (target == null)
            return null;

        PhotonView view = target.GetComponent<PhotonView>();
        object[] data = view != null ? view.InstantiationData : null;
        if (data == null || data.Length == 0 || !(data[0] is string marker))
            return null;

        if (marker == AutoTurretMarker || marker == ContainerShipAutoCannonMarker)
        {
            AutoTurretDeployable turret = target.GetComponent<AutoTurretDeployable>();
            if (turret == null)
                turret = target.AddComponent<AutoTurretDeployable>();

            turret.InitializeFromPhotonData();
            return turret;
        }

        if (marker == RocketAutoTurretMarker || marker == WarBaseRocketAutoTurretMarker)
        {
            RocketAutoTurretDeployable turret = target.GetComponent<RocketAutoTurretDeployable>();
            if (turret == null)
                turret = target.AddComponent<RocketAutoTurretDeployable>();

            turret.InitializeFromPhotonData();
            return turret;
        }

        if (marker == ViperContainerHaulerMarker)
        {
            ViperContainerHaulerDeployable hauler = target.GetComponent<ViperContainerHaulerDeployable>();
            if (hauler == null)
                hauler = target.AddComponent<ViperContainerHaulerDeployable>();

            hauler.InitializeFromPhotonData();
            return hauler;
        }

        if (marker == BisonIndustrialPartsMarker)
        {
            IndustrialPartsHaulable parts = target.GetComponent<IndustrialPartsHaulable>();
            if (parts == null)
                parts = target.AddComponent<IndustrialPartsHaulable>();

            parts.InitializeFromPhotonData();
            return parts;
        }

        if (marker == SpaceBombMarker)
        {
            SpaceBombDeployable bomb = target.GetComponent<SpaceBombDeployable>();
            if (bomb == null)
                bomb = target.AddComponent<SpaceBombDeployable>();

            bomb.InitializeFromPhotonData();
            return bomb;
        }

        if (marker == StasisBuoyMarker)
        {
            StasisBuoyDeployable buoy = target.GetComponent<StasisBuoyDeployable>();
            if (buoy == null)
                buoy = target.AddComponent<StasisBuoyDeployable>();

            buoy.InitializeFromPhotonData();
            return buoy;
        }

        if (marker == SpaceTorpedoMarker)
        {
            SpaceTorpedoDeployable torpedo = target.GetComponent<SpaceTorpedoDeployable>();
            if (torpedo == null)
                torpedo = target.AddComponent<SpaceTorpedoDeployable>();

            torpedo.InitializeFromPhotonData();
            return torpedo;
        }

        if (marker == AsteroidBreacherBombMarker)
        {
            AsteroidBreacherBombDeployable bomb = target.GetComponent<AsteroidBreacherBombDeployable>();
            if (bomb == null)
                bomb = target.AddComponent<AsteroidBreacherBombDeployable>();

            bomb.InitializeFromPhotonData();
            return bomb;
        }

        if (marker == MetalDriftWallMarker)
        {
            MetalDriftWallDeployable wall = target.GetComponent<MetalDriftWallDeployable>();
            if (wall == null)
                wall = target.AddComponent<MetalDriftWallDeployable>();

            wall.InitializeFromPhotonData();
            return wall;
        }

        if (marker == SpaceDrillMarker)
        {
            SpaceDrillDeployable drill = target.GetComponent<SpaceDrillDeployable>();
            if (drill == null)
                drill = target.AddComponent<SpaceDrillDeployable>();

            drill.InitializeFromPhotonData();
            return drill;
        }

        if (marker == DropbotMarker)
        {
            DropbotDeployable dropbot = target.GetComponent<DropbotDeployable>();
            if (dropbot == null)
                dropbot = target.AddComponent<DropbotDeployable>();

            dropbot.InitializeFromPhotonData();
            return dropbot;
        }

        return null;
    }
}
