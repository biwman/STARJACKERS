using UnityEngine;

public static class EnemyBotCatalog
{
    static readonly EnemyBotDefinition[] Definitions =
    {
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.Drone,
            Id = "drone",
            DisplayName = "Drone",
            InstantiationMarker = "enemy_bot",
            VisualResourcePath = "droid1_resource",
            EditorAssetPath = "Assets/droid1.png",
            TargetSize = 1.04f,
            PhysicsMass = 2.8f,
            LinearDamping = 0.08f,
            AngularDamping = 0.22f,
            DefaultHp = 50,
            DefaultShield = 20,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = true,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.GuardAndChase,
                SpawnPattern = EnemySpawnPattern.SafePerimeter,
                MoveSpeed = 1.1f,
                TurnResponsiveness = 300f,
                DetectionRadius = 10f,
                DisengageRadius = 20f,
                OrbitDistance = 5.5f,
                PreferredDistance = 7.5f,
                ShootDistance = 12f,
                RepathInterval = 0.35f,
                TargetRefreshInterval = 0.45f,
                IdleDriftTurnSpeed = 18f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 10,
                ReloadDuration = 6f,
                FireRate = 0.15f,
                Damage = 10,
                DamageType = WeaponDamageType.Laser,
                DeliveryMethod = WeaponDeliveryMethod.BurstProjectile,
                DeliveryFlags = WeaponDeliveryFlags.None,
                BulletScaleMultiplier = 1f,
                BulletColor = new Color(1f, 0.26f, 0.08f, 1f),
                BulletSpeed = 10f,
                MuzzleOffsetDistance = 0.5f,
                InfiniteAmmo = false,
                RotateTowardAim = true,
                Range = 12f,
                ShotSoundId = string.Empty,
                HitEffectId = Bullet.DroidBoltEffectId
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 4.6f,
                LinearDamping = 0.56f,
                AngularDamping = 0.72f,
                DriftSpeed = 0.12f,
                AngularVelocityRange = 4f,
                RewardItemId = InventoryItemCatalog.DroidScrapId,
                DestroyWhenEmpty = true,
                BaseColor = new Color(0.2f, 0.23f, 0.26f, 0.94f)
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, 0.44f),
                RootRotationZ = 0f,
                TrailOffsetFactors = new[] { new Vector2(0f, 0.02f) },
                MinTrailTime = 0.22f,
                MaxTrailTime = 0.82f,
                MinTrailWidth = 0.03f,
                MaxTrailWidth = 0.16f,
                EmissionThreshold = 0.04f,
                VisualStyle = EnemyTrailVisualStyle.OrangeSmall
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.Corsair,
            Id = "corsair",
            DisplayName = "Corsair",
            InstantiationMarker = "enemy_bot_corsair",
            VisualResourcePath = "statek_duzy_resource",
            EditorAssetPath = "Assets/statek_duzy.png",
            TargetSize = 5.2f,
            PhysicsMass = 24f,
            LinearDamping = 0.16f,
            AngularDamping = 0.38f,
            DefaultHp = 200,
            DefaultShield = 20,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = true,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.OrbitMap,
                SpawnPattern = EnemySpawnPattern.WideCorners,
                MoveSpeed = 1.9f,
                TurnResponsiveness = 150f,
                DetectionRadius = 7f,
                OrbitRadiusFactor = 0.43f,
                OrbitAngularSpeed = 0.32f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 9999,
                ReloadDuration = 0f,
                FireRate = 1f,
                Damage = 20,
                DamageType = WeaponDamageType.Plasma,
                DeliveryMethod = WeaponDeliveryMethod.DirectProjectile,
                DeliveryFlags = WeaponDeliveryFlags.None,
                BulletScaleMultiplier = 2f,
                BulletColor = new Color(0.15f, 1f, 0.28f, 1f),
                BulletSpeed = 9f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 7f,
                ShotSoundId = "corsair"
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 22f,
                LinearDamping = 0.84f,
                AngularDamping = 1.05f,
                DriftSpeed = 0.07f,
                AngularVelocityRange = 1.5f,
                RewardItemId = InventoryItemCatalog.CorsairSalvageId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.17f, 0.15f, 0.16f, 0.96f),
                VisualResourcePath = "wrak_corsair_resource",
                EditorAssetPath = "Assets/wrak_corsair.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.62f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.76f, 0.08f),
                    new Vector2(0f, 0.2f),
                    new Vector2(0.76f, 0.08f)
                },
                MinTrailTime = 0.65f,
                MaxTrailTime = 1.55f,
                MinTrailWidth = 0.12f,
                MaxTrailWidth = 0.34f,
                EmissionThreshold = 0.02f,
                VisualStyle = EnemyTrailVisualStyle.RedLarge
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.SpaceMine,
            Id = "space_mine",
            DisplayName = "Space Mine",
            InstantiationMarker = "enemy_bot_space_mine",
            VisualResourcePath = "space_mine_resource",
            EditorAssetPath = "Assets/space mine.png",
            TargetSize = 1.08f,
            PhysicsMass = 3.8f,
            LinearDamping = 0.18f,
            AngularDamping = 0.42f,
            DefaultHp = 20,
            DefaultShield = 20,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = true,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.Drift,
                SpawnPattern = EnemySpawnPattern.SafePerimeter,
                MoveSpeed = 0.18f,
                TurnResponsiveness = 20f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 0,
                ReloadDuration = 0f,
                FireRate = 0f,
                Damage = 0,
                DamageType = WeaponDamageType.None,
                DeliveryMethod = WeaponDeliveryMethod.None,
                DeliveryFlags = WeaponDeliveryFlags.None,
                BulletScaleMultiplier = 1f,
                BulletColor = Color.white,
                BulletSpeed = 0f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 0f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 3.6f,
                LinearDamping = 0.78f,
                AngularDamping = 0.96f,
                DriftSpeed = 0.09f,
                AngularVelocityRange = 2.4f,
                RewardItemId = InventoryItemCatalog.SpaceMineWreckId,
                DestroyWhenEmpty = true,
                BaseColor = new Color(0.19f, 0.21f, 0.24f, 0.96f),
                VisualResourcePath = "wrak_miny_resource",
                EditorAssetPath = "Assets/wrak_miny.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = Vector2.zero,
                RootRotationZ = 0f,
                TrailOffsetFactors = System.Array.Empty<Vector2>(),
                MinTrailTime = 0f,
                MaxTrailTime = 0f,
                MinTrailWidth = 0f,
                MaxTrailWidth = 0f,
                EmissionThreshold = 1f,
                VisualStyle = EnemyTrailVisualStyle.None
            },
            Explosion = new EnemyExplosionProfile
            {
                Damage = 50,
                DamageType = WeaponDamageType.Explosive,
                DeliveryMethod = WeaponDeliveryMethod.Mine,
                DeliveryFlags = WeaponDeliveryFlags.AreaDamage,
                TriggerRadius = 2.08f,
                VisualTargetSize = 4.1f,
                VisualDuration = 1.25f,
                VisualStartFrame = 2,
                VisualColumns = 4,
                VisualRows = 6,
                VisualResourcePath = "",
                EditorAssetPath = "",
                SoundId = "space_mine_boom"
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.SpaceTruck,
            Id = "space_truck",
            DisplayName = "Space Truck",
            InstantiationMarker = "enemy_bot_space_truck",
            VisualResourcePath = "space_truck_resource",
            EditorAssetPath = "Assets/space_truck.png",
            TargetSize = 4.2f,
            PhysicsMass = 18f,
            LinearDamping = 0.1f,
            AngularDamping = 0.32f,
            DefaultHp = 100,
            DefaultShield = 50,
            DefaultSpeedMultiplier = 1.5f,
            DefaultEnabled = false,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.RouteExtractionZones,
                SpawnPattern = EnemySpawnPattern.WideCorners,
                MoveSpeed = 1.9f,
                TurnResponsiveness = 170f,
                TargetRefreshInterval = 0.45f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 0,
                ReloadDuration = 0f,
                FireRate = 0f,
                Damage = 0,
                DamageType = WeaponDamageType.None,
                DeliveryMethod = WeaponDeliveryMethod.None,
                DeliveryFlags = WeaponDeliveryFlags.None,
                BulletScaleMultiplier = 1f,
                BulletColor = Color.white,
                BulletSpeed = 0f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 0f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 18f,
                LinearDamping = 0.78f,
                AngularDamping = 0.96f,
                DriftSpeed = 0.08f,
                AngularVelocityRange = 1.8f,
                RewardItemId = InventoryItemCatalog.SpaceTruckWreckId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.18f, 0.22f, 0.2f, 0.98f),
                VisualResourcePath = "space_truck_wrak_resource",
                EditorAssetPath = "Assets/space_truck_wrak.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.48f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.36f, 0.02f),
                    new Vector2(0.36f, 0.02f)
                },
                MinTrailTime = 0.5f,
                MaxTrailTime = 1.25f,
                MinTrailWidth = 0.08f,
                MaxTrailWidth = 0.24f,
                EmissionThreshold = 0.02f,
                VisualStyle = EnemyTrailVisualStyle.GreenTwin
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.MilitaryVan,
            Id = "military_van",
            DisplayName = "Military Van",
            InstantiationMarker = "enemy_bot_military_van",
            VisualResourcePath = "Enemies/MilitaryVan/military_van",
            EditorAssetPath = "Assets/Resources/Enemies/MilitaryVan/military_van.png",
            TargetSize = 2.275f,
            PhysicsMass = 32f,
            LinearDamping = 0.18f,
            AngularDamping = 0.44f,
            DefaultHp = 285,
            DefaultShield = 130,
            MaxHp = 750,
            MaxShield = 400,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = false,
            DefaultCount = 4,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.MilitaryVan,
                SpawnPattern = EnemySpawnPattern.WideCorners,
                MoveSpeed = 1.38f,
                TurnResponsiveness = 118f,
                DetectionRadius = 17f,
                DisengageRadius = 24f,
                OrbitDistance = 4.8f,
                PreferredDistance = 6.2f,
                ShootDistance = 13.2f,
                RepathInterval = 0.32f,
                TargetRefreshInterval = 0.34f,
                IdleDriftTurnSpeed = 8f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 9999,
                ReloadDuration = 0f,
                FireRate = 0.5f,
                Damage = 9,
                DamageType = WeaponDamageType.Laser,
                DeliveryMethod = WeaponDeliveryMethod.BurstProjectile,
                DeliveryFlags = WeaponDeliveryFlags.MultiStream,
                BulletScaleMultiplier = 1f,
                BulletColor = new Color(1f, 0.18f, 0.06f, 1f),
                BulletSpeed = 10.5f,
                MuzzleOffsetDistance = 0.56f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 13.2f,
                ShotSoundId = string.Empty,
                HitEffectId = Bullet.MilitaryVanTracerEffectId,
                MuzzleStreamCount = 2
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 34f,
                LinearDamping = 0.92f,
                AngularDamping = 1.05f,
                DriftSpeed = 0.055f,
                AngularVelocityRange = 0.95f,
                RewardItemId = InventoryItemCatalog.MilitaryVanWreckId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.46f, 0.43f, 0.36f, 0.98f),
                VisualResourcePath = "Enemies/MilitaryVan/military_van_wreck",
                EditorAssetPath = "Assets/Resources/Enemies/MilitaryVan/military_van_wreck.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(-0.51f, 0f),
                RootRotationZ = 0f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.03f, -0.37f),
                    new Vector2(0.03f, -0.15f),
                    new Vector2(0.03f, 0.15f),
                    new Vector2(-0.03f, 0.37f)
                },
                MinTrailTime = 0.46f,
                MaxTrailTime = 1.22f,
                MinTrailWidth = 0.075f,
                MaxTrailWidth = 0.24f,
                EmissionThreshold = 0f,
                VisualStyle = EnemyTrailVisualStyle.BlueTwin
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.ContainerShip,
            Id = "container_ship",
            DisplayName = "Container Hauler",
            InstantiationMarker = "enemy_bot_container_ship",
            VisualResourcePath = "Enemies/ContainerShip/container_ship",
            EditorAssetPath = "Assets/Resources/Enemies/ContainerShip/container_ship.png",
            TargetSize = 3.35f,
            PhysicsMass = 15f,
            LinearDamping = 0.11f,
            AngularDamping = 0.3f,
            DefaultHp = 120,
            DefaultShield = 70,
            MaxHp = 280,
            MaxShield = 220,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = false,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.ContainerShip,
                SpawnPattern = EnemySpawnPattern.WideCorners,
                MoveSpeed = 1.32f,
                TurnResponsiveness = 180f,
                OrbitRadiusFactor = 0.38f,
                OrbitAngularSpeed = 0.24f,
                TargetRefreshInterval = 0.35f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 0,
                ReloadDuration = 0f,
                FireRate = 0f,
                Damage = 0,
                DamageType = WeaponDamageType.None,
                DeliveryMethod = WeaponDeliveryMethod.None,
                DeliveryFlags = WeaponDeliveryFlags.None,
                BulletScaleMultiplier = 1f,
                BulletColor = Color.white,
                BulletSpeed = 0f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 0f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 15f,
                LinearDamping = 0.82f,
                AngularDamping = 1f,
                DriftSpeed = 0.08f,
                AngularVelocityRange = 1.6f,
                RewardItemId = InventoryItemCatalog.ContainerShipWreckId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.34f, 0.35f, 0.36f, 0.98f),
                VisualResourcePath = "Enemies/ContainerShip/container_ship_wreck",
                EditorAssetPath = "Assets/Resources/Enemies/ContainerShip/container_ship_wreck.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0.46f, 0f),
                RootRotationZ = 90f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(0.1f, -0.34f),
                    new Vector2(0.1f, 0.34f)
                },
                MinTrailTime = 0.38f,
                MaxTrailTime = 1.1f,
                MinTrailWidth = 0.05f,
                MaxTrailWidth = 0.19f,
                EmissionThreshold = 0.02f,
                VisualStyle = EnemyTrailVisualStyle.BlueTwin
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.NeutralFighter,
            Id = "neutral_fighter",
            DisplayName = "Neutral Fighter",
            InstantiationMarker = "enemy_bot_neutral_fighter",
            VisualResourcePath = "neutral_fighter_resource",
            EditorAssetPath = "Assets/neutral_fighter.png",
            TargetSize = 0.94f,
            PhysicsMass = 5.4f,
            LinearDamping = 0.08f,
            AngularDamping = 0.2f,
            DefaultHp = 20,
            DefaultShield = 20,
            DefaultSpeedMultiplier = 1.5f,
            DefaultEnabled = false,
            DefaultCount = 2,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.NeutralFighter,
                SpawnPattern = EnemySpawnPattern.SafePerimeter,
                MoveSpeed = 1.1f,
                TurnResponsiveness = 320f,
                DetectionRadius = 6f,
                DisengageRadius = 8.5f,
                OrbitDistance = 3.1f,
                PreferredDistance = 4.4f,
                ShootDistance = 7.8f,
                RepathInterval = 0.24f,
                TargetRefreshInterval = 0.28f,
                IdleDriftTurnSpeed = 28f,
                OrbitAngularSpeed = 1.25f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 6,
                ReloadDuration = 4f,
                FireRate = 0.5f,
                Damage = 10,
                DamageType = WeaponDamageType.Laser,
                DeliveryMethod = WeaponDeliveryMethod.BurstProjectile,
                DeliveryFlags = WeaponDeliveryFlags.None,
                BulletScaleMultiplier = 0.58f,
                BulletColor = new Color(1f, 0.08f, 0.04f, 1f),
                BulletSpeed = 11.5f,
                MuzzleOffsetDistance = 0.62f,
                InfiniteAmmo = false,
                RotateTowardAim = true,
                Range = 8f,
                ShotSoundId = "shoot_small"
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 3.8f,
                LinearDamping = 0.68f,
                AngularDamping = 0.85f,
                DriftSpeed = 0.1f,
                AngularVelocityRange = 3.2f,
                RewardItemId = InventoryItemCatalog.NeutralFighterSalvageId,
                DestroyWhenEmpty = true,
                BaseColor = new Color(0.42f, 0.44f, 0.46f, 0.98f),
                VisualResourcePath = "neutral_fighter_wreck_resource",
                EditorAssetPath = "Assets/neutral_fighter_wreck.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.44f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[] { new Vector2(0f, 0.04f) },
                MinTrailTime = 0.18f,
                MaxTrailTime = 0.7f,
                MinTrailWidth = 0.025f,
                MaxTrailWidth = 0.14f,
                EmissionThreshold = 0.03f,
                VisualStyle = EnemyTrailVisualStyle.OrangeSmall
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.RadarShip,
            Id = "radar_ship",
            DisplayName = "Radar Ship",
            InstantiationMarker = "enemy_bot_radar_ship",
            VisualResourcePath = "radar_ship_resource",
            EditorAssetPath = "Assets/radar_ship.png",
            TargetSize = 3.2f,
            PhysicsMass = 16f,
            LinearDamping = 0.11f,
            AngularDamping = 0.28f,
            DefaultHp = 90,
            DefaultShield = 110,
            DefaultSpeedMultiplier = 1.1f,
            DefaultEnabled = false,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.RadarShip,
                SpawnPattern = EnemySpawnPattern.WideCorners,
                MoveSpeed = 0.88f,
                TurnResponsiveness = 150f,
                DetectionRadius = 8.5f,
                DisengageRadius = 12f,
                OrbitDistance = 5.6f,
                PreferredDistance = 7.8f,
                ShootDistance = 8.5f,
                RepathInterval = 0.26f,
                TargetRefreshInterval = 0.24f,
                IdleDriftTurnSpeed = 15f,
                OrbitAngularSpeed = 0.42f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 9999,
                ReloadDuration = 0f,
                FireRate = 3f,
                Damage = 38,
                DamageType = WeaponDamageType.Explosive,
                DeliveryMethod = WeaponDeliveryMethod.RemoteStrike,
                DeliveryFlags = WeaponDeliveryFlags.AreaDamage | WeaponDeliveryFlags.Delayed,
                BulletScaleMultiplier = 1.8f,
                BulletColor = new Color(1f, 0.55f, 0.18f, 1f),
                BulletSpeed = 18f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 8.5f,
                ShotSoundId = "radar_ship"
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 18f,
                LinearDamping = 0.82f,
                AngularDamping = 1.02f,
                DriftSpeed = 0.08f,
                AngularVelocityRange = 1.3f,
                RewardItemId = InventoryItemCatalog.RadarShipSalvageId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.46f, 0.48f, 0.54f, 0.98f),
                VisualResourcePath = "radar_ship_wreck_resource",
                EditorAssetPath = "Assets/radar_ship_wreck.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.46f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[] { new Vector2(0f, 0.02f) },
                MinTrailTime = 0.34f,
                MaxTrailTime = 1.12f,
                MinTrailWidth = 0.05f,
                MaxTrailWidth = 0.2f,
                EmissionThreshold = 0.02f,
                VisualStyle = EnemyTrailVisualStyle.OrangeSmall
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.HunterLance,
            Id = "hunter_lance",
            DisplayName = "Hunter Lance",
            InstantiationMarker = "enemy_bot_hunter_lance",
            VisualResourcePath = "Enemies/HunterLance/hunter_lance_resource",
            EditorAssetPath = "Assets/Resources/Enemies/HunterLance/hunter_lance_resource.png",
            TargetSize = 2.2f,
            PhysicsMass = 12.5f,
            LinearDamping = 0.1f,
            AngularDamping = 0.26f,
            DefaultHp = 85,
            DefaultShield = 115,
            MaxHp = 250,
            MaxShield = 250,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = false,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.HunterLance,
                SpawnPattern = EnemySpawnPattern.SafePerimeter,
                MoveSpeed = 1.28f,
                TurnResponsiveness = 295f,
                DetectionRadius = 14.5f,
                DisengageRadius = 20f,
                OrbitDistance = 6.8f,
                PreferredDistance = 9.2f,
                ShootDistance = 13.8f,
                RepathInterval = 0.18f,
                TargetRefreshInterval = 0.22f,
                IdleDriftTurnSpeed = 19f,
                OrbitAngularSpeed = 0.58f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 0,
                ReloadDuration = 0f,
                FireRate = 4.6f,
                Damage = 36,
                DamageType = WeaponDamageType.Laser,
                DeliveryMethod = WeaponDeliveryMethod.Beam,
                DeliveryFlags = WeaponDeliveryFlags.ShieldFocused,
                BulletScaleMultiplier = 0f,
                BulletColor = new Color(0.36f, 0.9f, 1f, 1f),
                BulletSpeed = 0f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 13.8f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 12f,
                LinearDamping = 0.78f,
                AngularDamping = 0.98f,
                DriftSpeed = 0.08f,
                AngularVelocityRange = 1.8f,
                RewardItemId = InventoryItemCatalog.HunterLanceCoreId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.18f, 0.2f, 0.24f, 0.98f),
                VisualResourcePath = "Enemies/HunterLance/hunter_lance_wreck_resource",
                EditorAssetPath = "Assets/Resources/Enemies/HunterLance/hunter_lance_wreck_resource.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.56f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.24f, 0.04f),
                    new Vector2(0.24f, 0.04f)
                },
                MinTrailTime = 0.34f,
                MaxTrailTime = 1.08f,
                MinTrailWidth = 0.045f,
                MaxTrailWidth = 0.18f,
                EmissionThreshold = 0.02f,
                VisualStyle = EnemyTrailVisualStyle.BlueTwin
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.PirateFighter,
            Id = "pirate_fighter",
            DisplayName = "Pirate Fighter",
            InstantiationMarker = "enemy_bot_pirate_fighter",
            VisualResourcePath = "pirate_fighter_1_resource",
            EditorAssetPath = "Assets/pirate_fighter_1.png",
            TargetSize = 1.32f,
            PhysicsMass = 5.2f,
            LinearDamping = 0.07f,
            AngularDamping = 0.18f,
            DefaultHp = 50,
            DefaultShield = 50,
            DefaultSpeedMultiplier = 1.5f,
            DefaultEnabled = false,
            DefaultCount = 2,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.PirateFighter,
                SpawnPattern = EnemySpawnPattern.SafePerimeter,
                MoveSpeed = 1.65f,
                TurnResponsiveness = 430f,
                DetectionRadius = 6f,
                DisengageRadius = 13f,
                OrbitDistance = 2.7f,
                PreferredDistance = 4.6f,
                ShootDistance = 8.6f,
                RepathInterval = 0.16f,
                TargetRefreshInterval = 0.18f,
                IdleDriftTurnSpeed = 34f,
                OrbitAngularSpeed = 1.55f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 8,
                ReloadDuration = 4f,
                FireRate = 0.12f,
                Damage = 10,
                DamageType = WeaponDamageType.Laser,
                DeliveryMethod = WeaponDeliveryMethod.BurstProjectile,
                DeliveryFlags = WeaponDeliveryFlags.MultiStream,
                BulletScaleMultiplier = 0.42f,
                BulletColor = new Color(0.08f, 0.62f, 1f, 1f),
                BulletSpeed = 18f,
                MuzzleOffsetDistance = 0.44f,
                InfiniteAmmo = false,
                RotateTowardAim = true,
                Range = 8.6f,
                ShotSoundId = "pirate_fighter",
                MuzzleStreamCount = 2
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 4.8f,
                LinearDamping = 0.66f,
                AngularDamping = 0.84f,
                DriftSpeed = 0.11f,
                AngularVelocityRange = 3.6f,
                RewardItemId = InventoryItemCatalog.PirateFighterSalvageId,
                DestroyWhenEmpty = true,
                BaseColor = new Color(0.42f, 0.34f, 0.26f, 0.98f),
                VisualResourcePath = "pirate_fighter_wreck_resource",
                EditorAssetPath = "Assets/pirate_fighter_wreck.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.42f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.38f, 0.04f),
                    new Vector2(0.38f, 0.04f)
                },
                MinTrailTime = 0.22f,
                MaxTrailTime = 0.86f,
                MinTrailWidth = 0.032f,
                MaxTrailWidth = 0.15f,
                EmissionThreshold = 0.025f,
                VisualStyle = EnemyTrailVisualStyle.OrangeRedTwin
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.PirateFighterElite,
            Id = "pirate_fighter_elite",
            DisplayName = "Pirate Fighter Elite",
            InstantiationMarker = "enemy_bot_pirate_fighter_elite",
            VisualResourcePath = "pirate_fighter_elite_resource",
            EditorAssetPath = "Assets/pirate_fighter_elite.png",
            TargetSize = 1.32f,
            PhysicsMass = 5.2f,
            LinearDamping = 0.07f,
            AngularDamping = 0.18f,
            DefaultHp = 66,
            DefaultShield = 66,
            DefaultSpeedMultiplier = 1.5f,
            DefaultEnabled = false,
            DefaultCount = 2,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.PirateFighter,
                SpawnPattern = EnemySpawnPattern.SafePerimeter,
                MoveSpeed = 1.65f,
                TurnResponsiveness = 430f,
                DetectionRadius = 6f,
                DisengageRadius = 13f,
                OrbitDistance = 2.7f,
                PreferredDistance = 4.6f,
                ShootDistance = 8.6f,
                RepathInterval = 0.16f,
                TargetRefreshInterval = 0.18f,
                IdleDriftTurnSpeed = 34f,
                OrbitAngularSpeed = 1.55f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 10,
                ReloadDuration = 4f,
                FireRate = 0.12f,
                Damage = 10,
                DamageType = WeaponDamageType.Laser,
                DeliveryMethod = WeaponDeliveryMethod.BurstProjectile,
                DeliveryFlags = WeaponDeliveryFlags.MultiStream,
                BulletScaleMultiplier = 0.42f,
                BulletColor = new Color(1f, 0.08f, 0.03f, 1f),
                BulletSpeed = 18f,
                MuzzleOffsetDistance = 0.44f,
                InfiniteAmmo = false,
                RotateTowardAim = true,
                Range = 8.6f,
                ShotSoundId = "pirate_fighter",
                MuzzleStreamCount = 2
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 4.8f,
                LinearDamping = 0.66f,
                AngularDamping = 0.84f,
                DriftSpeed = 0.11f,
                AngularVelocityRange = 3.6f,
                RewardItemId = InventoryItemCatalog.PirateFighterSalvageId,
                DestroyWhenEmpty = true,
                BaseColor = new Color(0.42f, 0.34f, 0.26f, 0.98f),
                VisualResourcePath = "pirate_fighter_wreck_resource",
                EditorAssetPath = "Assets/pirate_fighter_wreck.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.42f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.38f, 0.04f),
                    new Vector2(0.38f, 0.04f)
                },
                MinTrailTime = 0.22f,
                MaxTrailTime = 0.86f,
                MinTrailWidth = 0.032f,
                MaxTrailWidth = 0.15f,
                EmissionThreshold = 0.025f,
                VisualStyle = EnemyTrailVisualStyle.OrangeRedTwin
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.PirateFighterAce,
            Id = "pirate_fighter_ace",
            DisplayName = "Pirate Fighter Ace",
            InstantiationMarker = "enemy_bot_pirate_fighter_ace",
            VisualResourcePath = "pirate_fighter_ace_resource",
            EditorAssetPath = "Assets/pirate_fighter_ace.png",
            TargetSize = 1.32f,
            PhysicsMass = 5.2f,
            LinearDamping = 0.07f,
            AngularDamping = 0.18f,
            DefaultHp = 66,
            DefaultShield = 66,
            DefaultSpeedMultiplier = 1.5f,
            DefaultEnabled = false,
            DefaultCount = 2,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.PirateFighter,
                SpawnPattern = EnemySpawnPattern.SafePerimeter,
                MoveSpeed = 1.65f,
                TurnResponsiveness = 430f,
                DetectionRadius = 6f,
                DisengageRadius = 13f,
                OrbitDistance = 2.7f,
                PreferredDistance = 4.6f,
                ShootDistance = 8.6f,
                RepathInterval = 0.16f,
                TargetRefreshInterval = 0.18f,
                IdleDriftTurnSpeed = 34f,
                OrbitAngularSpeed = 1.55f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 10,
                ReloadDuration = 4f,
                FireRate = 0.12f,
                Damage = 10,
                DamageType = WeaponDamageType.Laser,
                DeliveryMethod = WeaponDeliveryMethod.BurstProjectile,
                DeliveryFlags = WeaponDeliveryFlags.MultiStream,
                BulletScaleMultiplier = 0.42f,
                BulletColor = new Color(1f, 0.08f, 0.03f, 1f),
                BulletSpeed = 18f,
                MuzzleOffsetDistance = 0.44f,
                InfiniteAmmo = false,
                RotateTowardAim = true,
                Range = 8.6f,
                ShotSoundId = "pirate_fighter",
                MuzzleStreamCount = 3
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 4.8f,
                LinearDamping = 0.66f,
                AngularDamping = 0.84f,
                DriftSpeed = 0.11f,
                AngularVelocityRange = 3.6f,
                RewardItemId = InventoryItemCatalog.PirateFighterSalvageId,
                DestroyWhenEmpty = true,
                BaseColor = new Color(0.42f, 0.34f, 0.26f, 0.98f),
                VisualResourcePath = "pirate_fighter_wreck_resource",
                EditorAssetPath = "Assets/pirate_fighter_wreck.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.42f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.38f, 0.04f),
                    new Vector2(0.38f, 0.04f)
                },
                MinTrailTime = 0.22f,
                MaxTrailTime = 0.86f,
                MinTrailWidth = 0.032f,
                MaxTrailWidth = 0.15f,
                EmissionThreshold = 0.025f,
                VisualStyle = EnemyTrailVisualStyle.OrangeRedTwin
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.SpaceManta,
            Id = "space_manta",
            DisplayName = "Space Manta",
            InstantiationMarker = "enemy_bot_space_manta",
            VisualResourcePath = "Enemies/SpaceManta/space_manta_flap_00",
            AnimationResourcePath = "Enemies/SpaceManta",
            EditorAssetPath = "Assets/Resources/Enemies/SpaceManta/space_manta_flap_00.png",
            AnimationFramesPerSecond = 7f,
            TargetSize = 2.42f,
            PhysicsMass = 9.2f,
            LinearDamping = 0.09f,
            AngularDamping = 0.2f,
            DefaultHp = 100,
            DefaultShield = 0,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = false,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.SpaceManta,
                SpawnPattern = EnemySpawnPattern.SafePerimeter,
                MoveSpeed = 1.32f,
                TurnResponsiveness = 330f,
                DetectionRadius = 13.5f,
                DisengageRadius = 20f,
                OrbitDistance = 3.3f,
                PreferredDistance = 5.8f,
                ShootDistance = 8.8f,
                RepathInterval = 0.22f,
                TargetRefreshInterval = 0.24f,
                IdleDriftTurnSpeed = 22f,
                OrbitAngularSpeed = 0.86f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 0,
                ReloadDuration = 0f,
                FireRate = 3.4f,
                Damage = 40,
                DamageType = WeaponDamageType.Kinetic,
                DeliveryMethod = WeaponDeliveryMethod.ContactDash,
                DeliveryFlags = WeaponDeliveryFlags.None,
                BulletScaleMultiplier = 0f,
                BulletColor = new Color(0.3f, 0.88f, 1f, 1f),
                BulletSpeed = 0f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 8.8f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 7.5f,
                LinearDamping = 0.74f,
                AngularDamping = 0.94f,
                DriftSpeed = 0.08f,
                AngularVelocityRange = 2.4f,
                RewardItemId = InventoryItemCatalog.SpaceAnimalRemainsId,
                DestroyWhenEmpty = true,
                BaseColor = new Color(0.26f, 0.28f, 0.36f, 0.96f),
                VisualResourcePath = "Enemies/SpaceManta/space_manta_wreck_resource",
                EditorAssetPath = "Assets/Resources/Enemies/SpaceManta/space_manta_wreck_resource.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.34f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.34f, 0.04f),
                    new Vector2(0.34f, 0.04f)
                },
                MinTrailTime = 0.3f,
                MaxTrailTime = 0.96f,
                MinTrailWidth = 0.04f,
                MaxTrailWidth = 0.18f,
                EmissionThreshold = 0.035f,
                VisualStyle = EnemyTrailVisualStyle.BlueTwin
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.GravitySquid,
            Id = "gravity_squid",
            DisplayName = "Gravity Squid",
            InstantiationMarker = "enemy_bot_gravity_squid",
            VisualResourcePath = "Enemies/GravitySquid/gravity_squid_flap_00",
            AnimationResourcePath = "Enemies/GravitySquid",
            EditorAssetPath = "Assets/Resources/Enemies/GravitySquid/gravity_squid_flap_00.png",
            AnimationFramesPerSecond = 6.3f,
            TargetSize = 2.68f,
            PhysicsMass = 10.5f,
            LinearDamping = 0.12f,
            AngularDamping = 0.24f,
            DefaultHp = 80,
            DefaultShield = 70,
            MaxHp = 200,
            MaxShield = 200,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = false,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.GravitySquid,
                SpawnPattern = EnemySpawnPattern.SafePerimeter,
                MoveSpeed = 1.15f,
                TurnResponsiveness = 260f,
                DetectionRadius = 13.2f,
                DisengageRadius = 21f,
                OrbitDistance = 5.1f,
                PreferredDistance = 8.2f,
                ShootDistance = 10.6f,
                RepathInterval = 0.22f,
                TargetRefreshInterval = 0.24f,
                IdleDriftTurnSpeed = 18f,
                OrbitAngularSpeed = 0.74f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 0,
                ReloadDuration = 0f,
                FireRate = 7f,
                Damage = 8,
                DamageType = WeaponDamageType.Gravitic,
                DeliveryMethod = WeaponDeliveryMethod.Tether,
                DeliveryFlags = WeaponDeliveryFlags.Continuous | WeaponDeliveryFlags.ShieldFocused,
                BulletScaleMultiplier = 0f,
                BulletColor = new Color(0.12f, 0.96f, 1f, 1f),
                BulletSpeed = 0f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 10.6f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 8.6f,
                LinearDamping = 0.82f,
                AngularDamping = 0.96f,
                DriftSpeed = 0.06f,
                AngularVelocityRange = 2.1f,
                RewardItemId = InventoryItemCatalog.SpaceAnimalRemainsId,
                DestroyWhenEmpty = true,
                BaseColor = new Color(0.16f, 0.12f, 0.28f, 0.96f),
                VisualResourcePath = "Enemies/GravitySquid/gravity_squid_wreck_resource",
                EditorAssetPath = "Assets/Resources/Enemies/GravitySquid/gravity_squid_wreck_resource.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.28f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.22f, -0.02f),
                    new Vector2(0.22f, -0.02f)
                },
                MinTrailTime = 0.26f,
                MaxTrailTime = 0.78f,
                MinTrailWidth = 0.035f,
                MaxTrailWidth = 0.14f,
                EmissionThreshold = 0.03f,
                VisualStyle = EnemyTrailVisualStyle.PurpleLarge
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.PirateBase,
            Id = "pirate_base",
            DisplayName = "Pirate Base",
            InstantiationMarker = "enemy_bot_pirate_base",
            VisualResourcePath = "pirate_base_resource",
            EditorAssetPath = "Assets/pirate_base.png",
            TargetSize = 7.4f,
            PhysicsMass = 110f,
            LinearDamping = 0.12f,
            AngularDamping = 1.2f,
            DefaultHp = 500,
            DefaultShield = 1000,
            MaxHp = 500,
            MaxShield = 1000,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = false,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.PirateBase,
                SpawnPattern = EnemySpawnPattern.WideCorners,
                MoveSpeed = 0.42f,
                TurnResponsiveness = 0f,
                DetectionRadius = 0f,
                DisengageRadius = 0f,
                PreferredDistance = 0f,
                RepathInterval = 0.42f,
                TargetRefreshInterval = 0.55f,
                IdleDriftTurnSpeed = 0f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 0,
                ReloadDuration = 0f,
                FireRate = 0f,
                Damage = 0,
                DamageType = WeaponDamageType.None,
                DeliveryMethod = WeaponDeliveryMethod.Spawner,
                DeliveryFlags = WeaponDeliveryFlags.Autonomous,
                BulletScaleMultiplier = 0f,
                BulletColor = Color.white,
                BulletSpeed = 0f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 0f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 120f,
                LinearDamping = 0.95f,
                AngularDamping = 1.3f,
                DriftSpeed = 0.04f,
                AngularVelocityRange = 0.55f,
                RewardItemId = InventoryItemCatalog.PirateBaseCoreId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.36f, 0.33f, 0.38f, 0.98f),
                VisualResourcePath = "pirate_base_wreck_resource",
                EditorAssetPath = "Assets/pirate_base_wreck.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.48f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.38f, 0.04f),
                    new Vector2(0f, 0.02f),
                    new Vector2(0.38f, 0.04f)
                },
                MinTrailTime = 0.72f,
                MaxTrailTime = 1.85f,
                MinTrailWidth = 0.08f,
                MaxTrailWidth = 0.34f,
                EmissionThreshold = 0.01f,
                VisualStyle = EnemyTrailVisualStyle.PurpleLarge
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.RescueShip,
            Id = "rescue_ship",
            DisplayName = "Rescue Ship",
            InstantiationMarker = "enemy_bot_rescue_ship",
            VisualResourcePath = "rescue_ship_resource",
            EditorAssetPath = "Assets/rescue_ship.png",
            TargetSize = 2.18f,
            PhysicsMass = 20f,
            LinearDamping = 0.1f,
            AngularDamping = 0.3f,
            DefaultHp = 85,
            DefaultShield = 95,
            DefaultSpeedMultiplier = 1.9f,
            DefaultEnabled = false,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.RescueShip,
                SpawnPattern = EnemySpawnPattern.WideCorners,
                MoveSpeed = 0.96f,
                TurnResponsiveness = 155f,
                DetectionRadius = 18f,
                DisengageRadius = 22f,
                OrbitDistance = 2.6f,
                PreferredDistance = 3.1f,
                ShootDistance = 0f,
                RepathInterval = 0.18f,
                TargetRefreshInterval = 0.2f,
                IdleDriftTurnSpeed = 14f,
                OrbitAngularSpeed = 0.25f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 0,
                ReloadDuration = 0f,
                FireRate = 0f,
                Damage = 0,
                DamageType = WeaponDamageType.None,
                DeliveryMethod = WeaponDeliveryMethod.None,
                DeliveryFlags = WeaponDeliveryFlags.None,
                BulletScaleMultiplier = 0f,
                BulletColor = new Color(0.54f, 0.9f, 1f, 1f),
                BulletSpeed = 0f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 0f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 22f,
                LinearDamping = 0.84f,
                AngularDamping = 1.08f,
                DriftSpeed = 0.07f,
                AngularVelocityRange = 1.15f,
                RewardItemId = InventoryItemCatalog.RescueShipSalvageId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.48f, 0.54f, 0.6f, 0.98f),
                VisualResourcePath = "rescue_ship_wreck_resource",
                EditorAssetPath = "Assets/rescue_ship_wreck.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.44f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.34f, 0.06f),
                    new Vector2(0.34f, 0.06f)
                },
                MinTrailTime = 0.42f,
                MaxTrailTime = 1.18f,
                MinTrailWidth = 0.05f,
                MaxTrailWidth = 0.18f,
                EmissionThreshold = 0.015f,
                VisualStyle = EnemyTrailVisualStyle.BlueTwin
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.Mothership,
            Id = "mothership",
            DisplayName = "Mothership",
            InstantiationMarker = "enemy_bot_mothership",
            VisualResourcePath = "mother_ship_resource",
            EditorAssetPath = "Assets/mother_ship.png",
            TargetSize = 7.28f,
            PhysicsMass = 95f,
            LinearDamping = 0.08f,
            AngularDamping = 0.42f,
            DefaultHp = 200,
            DefaultShield = 200,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = false,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.Mothership,
                SpawnPattern = EnemySpawnPattern.WideCorners,
                MoveSpeed = 0.82f,
                TurnResponsiveness = 28f,
                DetectionRadius = 13.5f,
                DisengageRadius = 22f,
                PreferredDistance = 6.8f,
                RepathInterval = 0.45f,
                TargetRefreshInterval = 0.35f,
                OrbitRadiusFactor = 0.38f,
                OrbitAngularSpeed = 0.18f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 10,
                ReloadDuration = 3f,
                FireRate = 0.28f,
                Damage = 10,
                DamageType = WeaponDamageType.Laser,
                DeliveryMethod = WeaponDeliveryMethod.BurstProjectile,
                DeliveryFlags = WeaponDeliveryFlags.MultiStream,
                BulletScaleMultiplier = 1f,
                BulletColor = Color.white,
                BulletSpeed = 10f,
                MuzzleOffsetDistance = 0.38f,
                InfiniteAmmo = false,
                RotateTowardAim = false,
                Range = 18f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 120f,
                LinearDamping = 0.94f,
                AngularDamping = 1.4f,
                DriftSpeed = 0.045f,
                AngularVelocityRange = 0.7f,
                RewardItemId = InventoryItemCatalog.MothershipCoreId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.52f, 0.55f, 0.58f, 0.98f),
                VisualResourcePath = "mother_ship_wrak_resource",
                EditorAssetPath = "Assets/mother_ship_wrak.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(-0.6f, 0f),
                RootRotationZ = 0f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(0f, -0.54f),
                    new Vector2(0f, -0.27f),
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0.27f),
                    new Vector2(0f, 0.54f)
                },
                MinTrailTime = 3.1f,
                MaxTrailTime = 6.2f,
                MinTrailWidth = 0.56f,
                MaxTrailWidth = 1.35f,
                EmissionThreshold = 0f,
                VisualStyle = EnemyTrailVisualStyle.RedLarge
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.CosmicWorm,
            Id = "cosmic_worm",
            DisplayName = "Cosmic Worm",
            InstantiationMarker = "enemy_bot_cosmic_worm",
            VisualResourcePath = "Enemies/CosmicWorm/cosmic_worm_resource",
            EditorAssetPath = "Assets/Resources/Enemies/CosmicWorm/cosmic_worm_resource.png",
            TargetSize = 11.8f,
            PhysicsMass = 140f,
            LinearDamping = 0.12f,
            AngularDamping = 0.52f,
            DefaultHp = 1600,
            DefaultShield = 260,
            MaxHp = 3200,
            MaxShield = 900,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = false,
            ShowInEnemySettings = false,
            DefaultCount = 1,
            DefaultSpawnSecond = 25,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.CosmicWorm,
                SpawnPattern = EnemySpawnPattern.WideCorners,
                MoveSpeed = 0.72f,
                TurnResponsiveness = 44f,
                DetectionRadius = 24f,
                DisengageRadius = 36f,
                OrbitDistance = 8.5f,
                PreferredDistance = 11.5f,
                ShootDistance = 18f,
                RepathInterval = 0.38f,
                TargetRefreshInterval = 0.32f,
                OrbitRadiusFactor = 0.44f,
                OrbitAngularSpeed = 0.2f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 9999,
                ReloadDuration = 0f,
                FireRate = 0.18f,
                Damage = 18,
                DamageType = WeaponDamageType.Plasma,
                DeliveryMethod = WeaponDeliveryMethod.SpreadProjectile,
                DeliveryFlags = WeaponDeliveryFlags.MultiStream,
                BulletScaleMultiplier = 1.75f,
                BulletColor = new Color(0.55f, 0.18f, 1f, 1f),
                BulletSpeed = 8.6f,
                MuzzleOffsetDistance = 0.54f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 18f,
                ShotSoundId = "cosmic_worm",
                MuzzleStreamCount = 5
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 155f,
                LinearDamping = 1.08f,
                AngularDamping = 1.42f,
                DriftSpeed = 0.035f,
                AngularVelocityRange = 0.45f,
                RewardItemId = InventoryItemCatalog.VoidMawCoreId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.32f, 0.28f, 0.38f, 0.98f),
                VisualResourcePath = "Enemies/CosmicWorm/cosmic_worm_resource",
                EditorAssetPath = "Assets/Resources/Enemies/CosmicWorm/cosmic_worm_resource.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(-0.48f, 0f),
                RootRotationZ = 0f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.58f, -0.16f),
                    new Vector2(-0.52f, 0.16f),
                    new Vector2(-0.28f, 0f)
                },
                MinTrailTime = 1.8f,
                MaxTrailTime = 3.8f,
                MinTrailWidth = 0.26f,
                MaxTrailWidth = 0.72f,
                EmissionThreshold = 0f,
                VisualStyle = EnemyTrailVisualStyle.PurpleLarge
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.RiftWarden,
            Id = "rift_warden",
            DisplayName = "Rift Warden",
            InstantiationMarker = "enemy_bot_rift_warden",
            VisualResourcePath = "Enemies/RiftWarden/rift_warden_resource",
            EditorAssetPath = "Assets/Resources/Enemies/RiftWarden/rift_warden_resource.png",
            TargetSize = 3.35f,
            PhysicsMass = 34f,
            LinearDamping = 0.48f,
            AngularDamping = 1.25f,
            DefaultHp = 260,
            DefaultShield = 220,
            MaxHp = 900,
            MaxShield = 700,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = false,
            ShowInEnemySettings = true,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.RiftWarden,
                SpawnPattern = EnemySpawnPattern.WideCorners,
                MoveSpeed = 1.28f,
                TurnResponsiveness = 150f,
                DetectionRadius = 18f,
                DisengageRadius = 28f,
                OrbitDistance = 5.4f,
                PreferredDistance = 7.1f,
                ShootDistance = 14f,
                RepathInterval = 0.38f,
                TargetRefreshInterval = 0.32f,
                OrbitRadiusFactor = 0.34f,
                OrbitAngularSpeed = 0.42f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 9999,
                ReloadDuration = 0f,
                FireRate = 1.1f,
                Damage = 18,
                DamageType = WeaponDamageType.Ion,
                DeliveryMethod = WeaponDeliveryMethod.Beam,
                DeliveryFlags = WeaponDeliveryFlags.Delayed | WeaponDeliveryFlags.ShieldFocused,
                BulletScaleMultiplier = 1f,
                BulletColor = new Color(0.26f, 1f, 0.92f, 1f),
                BulletSpeed = 0f,
                MuzzleOffsetDistance = 0.62f,
                InfiniteAmmo = true,
                RotateTowardAim = true,
                Range = 14f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 28f,
                LinearDamping = 0.92f,
                AngularDamping = 1.2f,
                DriftSpeed = 0.08f,
                AngularVelocityRange = 2.2f,
                RewardItemId = InventoryItemCatalog.RiftWardenWreckId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.22f, 0.38f, 0.36f, 0.96f)
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.18f),
                RootRotationZ = 0f,
                TrailOffsetFactors = new[] { new Vector2(0f, -0.08f) },
                MinTrailTime = 1.05f,
                MaxTrailTime = 2.2f,
                MinTrailWidth = 0.24f,
                MaxTrailWidth = 0.58f,
                EmissionThreshold = 0.02f,
                VisualStyle = EnemyTrailVisualStyle.PurpleLarge
            }
        }
    };

    static readonly System.Collections.Generic.Dictionary<EnemyBotKind, EnemyBotDefinition> DefinitionsByKind = BuildDefinitionsByKind();
    static readonly System.Collections.Generic.Dictionary<string, EnemyBotDefinition> DefinitionsByMarker = BuildDefinitionsByMarker();

    public static System.Collections.Generic.IReadOnlyList<EnemyBotDefinition> AllDefinitions => Definitions;

    public static void PrewarmRoundAssets()
    {
        for (int i = 0; i < Definitions.Length; i++)
            PrewarmDefinition(Definitions[i]);

        EnemyContainerShipBehavior.PrewarmCargoSprites();
        EnemyMothershipBehavior.PrewarmTurretAssets();

        RescueShipBeamVfx.Prewarm();
        PirateBaseCollectionBeamVfx.Prewarm();
        GravitySquidTetherVfx.Prewarm();
        HunterLanceBeamVfx.Prewarm();
        SpaceAnimalDeathVfx.Prewarm();
        PirateBaseLaunchVfx.Prewarm();
    }

    public static EnemyBotDefinition GetDefinition(EnemyBotKind kind)
    {
        DefinitionsByKind.TryGetValue(kind, out EnemyBotDefinition definition);
        return definition;
    }

    public static EnemyBotDefinition GetDefinition(string marker)
    {
        if (string.IsNullOrWhiteSpace(marker))
            return null;

        DefinitionsByMarker.TryGetValue(marker, out EnemyBotDefinition definition);
        return definition;
    }

    static System.Collections.Generic.Dictionary<EnemyBotKind, EnemyBotDefinition> BuildDefinitionsByKind()
    {
        System.Collections.Generic.Dictionary<EnemyBotKind, EnemyBotDefinition> result = new System.Collections.Generic.Dictionary<EnemyBotKind, EnemyBotDefinition>();
        for (int i = 0; i < Definitions.Length; i++)
            result[Definitions[i].Kind] = Definitions[i];

        return result;
    }

    static System.Collections.Generic.Dictionary<string, EnemyBotDefinition> BuildDefinitionsByMarker()
    {
        System.Collections.Generic.Dictionary<string, EnemyBotDefinition> result = new System.Collections.Generic.Dictionary<string, EnemyBotDefinition>(System.StringComparer.Ordinal);
        for (int i = 0; i < Definitions.Length; i++)
            result[Definitions[i].InstantiationMarker] = Definitions[i];

        return result;
    }

    static void PrewarmDefinition(EnemyBotDefinition definition)
    {
        if (definition == null)
            return;

        PrewarmSpriteTexture(definition.GetVisualSprite());
        if (!string.IsNullOrWhiteSpace(definition.AnimationResourcePath))
            PrewarmSprites(EnemySpriteFrameAnimator.PrewarmFrames(definition.AnimationResourcePath));

        if (definition.Wreck != null)
            PrewarmSpriteTexture(definition.Wreck.GetVisualSprite());

        if (definition.Explosion != null)
            PrewarmSprites(definition.Explosion.GetVisualFrames());
    }

    static void PrewarmSprites(Sprite[] sprites)
    {
        if (sprites == null)
            return;

        for (int i = 0; i < sprites.Length; i++)
            PrewarmSpriteTexture(sprites[i]);
    }

    static void PrewarmSpriteTexture(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        sprite.texture.GetNativeTexturePtr();
    }
}
