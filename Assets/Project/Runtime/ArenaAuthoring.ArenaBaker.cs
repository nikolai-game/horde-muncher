using Unity.Entities;
using UnityEngine;

namespace HordeMuncher
{
    public sealed partial class ArenaAuthoring
    {
        private sealed class ArenaBaker : Baker<ArenaAuthoring>
        {
            public override void Bake(ArenaAuthoring authoring)
            {
                AddComponent(GetEntity(TransformUsageFlags.None), new ArenaConfiguration
                {
                    PlayerPrefab = GetEntity(authoring.PlayerPrefab, TransformUsageFlags.Dynamic),
                    MaxPlayers = authoring.MaxPlayers,
                    SharedViewMaxDistance = Mathf.Max(4, authoring.SharedViewMaxDistance),
                    EnemyPrefab = GetEntity(authoring.EnemyPrefab, TransformUsageFlags.Dynamic),
                    ProjectilePrefab = GetEntity(authoring.ProjectilePrefab, TransformUsageFlags.Dynamic),
                    ResourcePrefab = GetEntity(authoring.ResourcePrefab, TransformUsageFlags.Dynamic)
                });
                if (authoring.Economy != null)
                {
                    DependsOn(authoring.Economy);
                    EconomySettings economySettings = authoring.Economy;
                    Entity economyConfigurationEntity = GetEntity(TransformUsageFlags.None);
                    AddComponent(economyConfigurationEntity, new EconomyConfig
                    {
                        ResourceAttractionDuration = Mathf.Max(0.01f, economySettings.ResourceAttractionDuration),
                        WeaponOrbitRadius = Mathf.Max(0, economySettings.WeaponOrbitRadius),
                        WeaponHeight = economySettings.WeaponHeight,
                        WeaponSlots = Mathf.Clamp(economySettings.WeaponSlots, 1, 6),
                        WeaponSellFraction = Mathf.Clamp01(economySettings.WeaponSellFraction),
                        StartingWeapon = economySettings.StartingWeapon,
                        StartingResources = Mathf.Max(0, economySettings.StartingResources),
                        FirstLevelXp = Mathf.Max(1, economySettings.FirstLevelXp),
                        LevelXpGrowth = Mathf.Max(0, economySettings.LevelXpGrowth),
                        RerollPrice = Mathf.Max(0, economySettings.RerollPrice),
                        RerollGrowth = Mathf.Max(0, economySettings.RerollGrowth),
                        UpgradeRerollPrice = Mathf.Max(1, economySettings.UpgradeRerollPrice),
                        UpgradeRerollGrowth = Mathf.Max(1, economySettings.UpgradeRerollGrowth),
                        EnemyPriceGrowth = Mathf.Max(0, economySettings.EnemyPriceGrowth)
                    });
                    DynamicBuffer<ResourceAttractionCurvePoint> attractionCurvePoints =
                        AddBuffer<ResourceAttractionCurvePoint>(economyConfigurationEntity);
                    for (int curvePointIndex = 0; curvePointIndex <= 128; curvePointIndex++)
                    {
                        float progress = curvePointIndex / 128f;
                        attractionCurvePoints.Add(new ResourceAttractionCurvePoint
                        {
                            Progress = Mathf.Clamp01(economySettings.ResourceAttractionCurve?.Evaluate(progress) ?? progress * progress)
                        });
                    }

                    DynamicBuffer<CatalogEntry> catalogEntries = AddBuffer<CatalogEntry>(economyConfigurationEntity);
                    foreach (CatalogEntry entry in CatalogDataBaking.Build(economySettings))
                    {
                        catalogEntries.Add(entry);
                    }
                }

                if (authoring.Waves == null)
                {
                    return;
                }

                DependsOn(authoring.Waves);
                WaveSettings waveSettings = authoring.Waves;
                Entity configurationEntity = GetEntity(TransformUsageFlags.None);
                AddComponent(configurationEntity, new WaveRuntimeSettings
                {
                    Center = new Unity.Mathematics.float2(waveSettings.MapCenter.x, waveSettings.MapCenter.y),
                    HalfExtents = new Unity.Mathematics.float2(Mathf.Max(0.1f, waveSettings.MapHalfExtents.x),
                        Mathf.Max(0.1f, waveSettings.MapHalfExtents.y)),
                    SafeDistance = Mathf.Max(0, waveSettings.SafeDistanceFromPlayers),
                    MaxAlive = Mathf.Max(1, waveSettings.MaxAlive),
                    MaxSpawnsPerTick = Mathf.Max(1, waveSettings.MaxSpawnsPerTick),
                    PositionAttempts = Mathf.Max(1, waveSettings.PositionAttempts),
                    CompletionPauseDuration = Mathf.Max(0, waveSettings.CompletionPauseDuration),
                    EnemyHealthGrowthPerWave = Mathf.Max(0, waveSettings.EnemyHealthGrowthPerWave),
                    EnemyDamageGrowthPerWave = Mathf.Max(0, waveSettings.EnemyDamageGrowthPerWave),
                    EnemySpeedGrowthPerWave = Mathf.Max(0, waveSettings.EnemySpeedGrowthPerWave),
                    Seed = waveSettings.Seed
                });
                AddComponent(configurationEntity, new WaveProgress());
                DynamicBuffer<WavePlan> wavePlans = AddBuffer<WavePlan>(configurationEntity);
                DynamicBuffer<WaveSpawnRateCurvePoint> spawnRatePoints = AddBuffer<WaveSpawnRateCurvePoint>(configurationEntity);
                DynamicBuffer<WaveEnemy> enemyChoices = AddBuffer<WaveEnemy>(configurationEntity);
                if (waveSettings.Waves == null)
                {
                    return;
                }

                foreach (WaveDefinition waveDefinition in waveSettings.Waves)
                {
                    if (waveDefinition == null)
                    {
                        continue;
                    }

                    var wavePlan = new WavePlan
                    {
                        Duration = Mathf.Max(0.1f, waveDefinition.Duration),
                        SpawnRatePointOffset = spawnRatePoints.Length,
                        SpawnRatePointCount = 129,
                        EnemyOffset = enemyChoices.Length,
                        BossPrefab = waveDefinition.BossPrefab == null
                            ? Entity.Null
                            : GetEntity(waveDefinition.BossPrefab, TransformUsageFlags.Dynamic)
                    };
                    for (int curvePointIndex = 0; curvePointIndex < wavePlan.SpawnRatePointCount; curvePointIndex++)
                    {
                        spawnRatePoints.Add(new WaveSpawnRateCurvePoint
                        {
                            Rate = Mathf.Max(0, waveDefinition.SpawnRate?.Evaluate(curvePointIndex / 128f) ?? 0)
                        });
                    }

                    if (waveDefinition.EnemyPrefabs != null)
                    {
                        foreach (GameObject enemyPrefab in waveDefinition.EnemyPrefabs)
                        {
                            if (enemyPrefab != null)
                            {
                                enemyChoices.Add(new WaveEnemy
                                {
                                    Prefab = GetEntity(enemyPrefab, TransformUsageFlags.Dynamic)
                                });
                            }
                        }
                    }

                    if (enemyChoices.Length == wavePlan.EnemyOffset && authoring.EnemyPrefab != null)
                    {
                        enemyChoices.Add(new WaveEnemy
                        {
                            Prefab = GetEntity(authoring.EnemyPrefab, TransformUsageFlags.Dynamic)
                        });
                    }

                    wavePlan.EnemyCount = enemyChoices.Length - wavePlan.EnemyOffset;
                    wavePlans.Add(wavePlan);
                }
            }
        }
    }
}
