using Unity.Entities;
using UnityEngine;

namespace HordeMuncher
{
    public sealed partial class ArenaAuthoring
    {
        private sealed class ArenaBaker : Baker<ArenaAuthoring>
        {
            private const float MIN_ATTRACTION_DURATION = 0.01f;
            private const int CURVE_SAMPLE_INTERVALS = 128;
            private const float MIN_WAVE_DIMENSION = 0.1f;
            private const int CURVE_SAMPLE_COUNT = CURVE_SAMPLE_INTERVALS + 1;

            public override void Bake(ArenaAuthoring authoring)
            {
                AddComponent(GetEntity(TransformUsageFlags.None), new ArenaConfiguration
                {
                    PlayerPrefab = GetEntity(authoring.PlayerPrefab, TransformUsageFlags.Dynamic),
                    MaxPlayers = authoring.MaxPlayers,
                    SharedViewMaxDistance = Mathf.Max(ArenaLimits.MIN_SHARED_VIEW_DISTANCE, authoring.SharedViewMaxDistance),
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
                        ResourceAttractionDuration = Mathf.Max(MIN_ATTRACTION_DURATION, economySettings.ResourceAttractionDuration),
                        WeaponOrbitRadius = Mathf.Max(0, economySettings.WeaponOrbitRadius),
                        WeaponHeight = economySettings.WeaponHeight,
                        WeaponSlots = Mathf.Clamp(economySettings.WeaponSlots, 1, ArenaLimits.MAX_WEAPON_SLOTS),
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
                    for (int curvePointIndex = 0; curvePointIndex <= CURVE_SAMPLE_INTERVALS; curvePointIndex++)
                    {
                        float progress = curvePointIndex / (float)CURVE_SAMPLE_INTERVALS;
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
                    HalfExtents = new Unity.Mathematics.float2(Mathf.Max(MIN_WAVE_DIMENSION, waveSettings.MapHalfExtents.x),
                        Mathf.Max(MIN_WAVE_DIMENSION, waveSettings.MapHalfExtents.y)),
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
                        Duration = Mathf.Max(MIN_WAVE_DIMENSION, waveDefinition.Duration),
                        SpawnRatePointOffset = spawnRatePoints.Length,
                        SpawnRatePointCount = CURVE_SAMPLE_COUNT,
                        EnemyOffset = enemyChoices.Length,
                        BossPrefab = waveDefinition.BossPrefab == null
                            ? Entity.Null
                            : GetEntity(waveDefinition.BossPrefab, TransformUsageFlags.Dynamic)
                    };
                    for (int curvePointIndex = 0; curvePointIndex < wavePlan.SpawnRatePointCount; curvePointIndex++)
                    {
                        spawnRatePoints.Add(new WaveSpawnRateCurvePoint
                        {
                            Rate = Mathf.Max(0, waveDefinition.SpawnRate?.Evaluate(curvePointIndex / (float)CURVE_SAMPLE_INTERVALS) ?? 0)
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
