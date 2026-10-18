using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(RunLifecycleSystem))]
    public partial class ShopSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<EconomyConfig>();
            RequireForUpdate<WaveProgress>();
        }

        protected override void OnUpdate()
        {
            if (SystemAPI.GetSingleton<WaveProgress>().IsPaused)
            {
                using EntityQuery requests = EntityManager.CreateEntityQuery(typeof(ShopRequest), typeof(ReceiveRpcCommandRequest));
                EntityManager.DestroyEntity(requests);
                return;
            }

            EntityManager entityManager = EntityManager;
            EconomyConfig economyConfiguration = SystemAPI.GetSingleton<EconomyConfig>();
            DynamicBuffer<CatalogEntry> catalog = entityManager.GetBuffer<CatalogEntry>(SystemAPI.GetSingletonEntity<EconomyConfig>());
            WaveProgress waveProgress = SystemAPI.GetSingleton<WaveProgress>();
            foreach (var (progressReference, ghostOwner, playerEntity) in
                SystemAPI.Query<RefRW<PlayerProgress>, RefRO<GhostOwner>>().WithAll<ArenaPlayer>().WithEntityAccess())
            {
                PlayerProgress playerProgress = progressReference.ValueRO;
                if (!playerProgress.Initialized)
                {
                    playerProgress.Initialized = true;
                    playerProgress.Level = 1;
                    playerProgress.NextLevelXp = economyConfiguration.FirstLevelXp;
                    playerProgress.ShopWave = -1;
                    playerProgress.RandomState = (uint)(ghostOwner.ValueRO.NetworkId * 7919 + 17);
                    CombatWallet wallet = entityManager.GetComponentData<CombatWallet>(playerEntity);
                    wallet.Resources = economyConfiguration.StartingResources;
                    entityManager.SetComponentData(playerEntity, wallet);
                    if (economyConfiguration.StartingWeapon >= 0 && economyConfiguration.StartingWeapon < catalog.Length
                        && catalog[economyConfiguration.StartingWeapon].Kind == GoodsKind.Weapon)
                    {
                        entityManager.GetBuffer<EquippedWeapon>(playerEntity).Add(new EquippedWeapon
                        {
                            CatalogId = economyConfiguration.StartingWeapon
                        });
                    }
                }

                if ((waveProgress.Phase == WavePhase.Rewards || waveProgress.Phase == WavePhase.Shop)
                    && playerProgress.ShopWave != waveProgress.Index)
                {
                    if (playerProgress.ShopWave < 0)
                    {
                        playerProgress.RandomState = (waveProgress.RandomState ^ (uint)(ghostOwner.ValueRO.NetworkId * 7919)) | 1u;
                    }

                    playerProgress.ShopWave = waveProgress.Index;
                    playerProgress.Ready = false;
                    playerProgress.Rerolls = 0;
                    Entity waveEntity = SystemAPI.GetSingletonEntity<WaveProgress>();
                    playerProgress.PriceMultiplier = entityManager.HasBuffer<WavePlan>(waveEntity)
                        && entityManager.HasBuffer<WaveSpawnRateCurvePoint>(waveEntity)
                        ? EconomyRules.CalculateWavePriceMultiplier(entityManager.GetBuffer<WavePlan>(waveEntity),
                            entityManager.GetBuffer<WaveSpawnRateCurvePoint>(waveEntity), waveProgress.Index, economyConfiguration.EnemyPriceGrowth)
                        : 1;
                    playerProgress.Revision++;
                    playerProgress.Result = ShopResult.None;
                    EconomyRules.RefreshShopOffers(entityManager, playerEntity, catalog, ref playerProgress);
                    EconomyRules.GenerateUpgradeChoices(entityManager,
                        playerEntity,
                        ref playerProgress);
                }

                progressReference.ValueRW = playerProgress;
            }

            using var entityCommands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (shopRequest, receivedRequest, requestEntity) in
                SystemAPI.Query<RefRO<ShopRequest>, RefRO<ReceiveRpcCommandRequest>>().WithEntityAccess())
            {
                entityCommands.DestroyEntity(requestEntity);
                if (!entityManager.HasComponent<NetworkId>(receivedRequest.ValueRO.SourceConnection)
                    || !entityManager.HasComponent<LobbyMember>(receivedRequest.ValueRO.SourceConnection)
                    || !entityManager.HasComponent<NetworkStreamInGame>(receivedRequest.ValueRO.SourceConnection)
                    || entityManager.HasComponent<NetworkStreamRequestDisconnect>(receivedRequest.ValueRO.SourceConnection))
                {
                    continue;
                }

                int networkId = entityManager.GetComponentData<NetworkId>(receivedRequest.ValueRO.SourceConnection).Value;
                foreach (var (ghostOwner, playerEntity) in
                    SystemAPI.Query<RefRO<GhostOwner>>().WithAll<ArenaPlayer, PlayerProgress>().WithEntityAccess())
                if (ghostOwner.ValueRO.NetworkId == networkId)
                {
                    if (shopRequest.ValueRO.Action == ShopAction.Restart)
                    {
                        if (waveProgress.Phase != WavePhase.Complete && waveProgress.Phase != WavePhase.Defeat)
                        {
                            break;
                        }

                        PlayerProgress progress = entityManager.GetComponentData<PlayerProgress>(playerEntity);
                        if (shopRequest.ValueRO.Revision != progress.Revision)
                        {
                            break;
                        }

                        progress.RestartReady = !progress.RestartReady;
                        progress.Revision++;
                        entityManager.SetComponentData(playerEntity, progress);
                    }
                    else if (waveProgress.Phase == WavePhase.Shop || (waveProgress.Phase == WavePhase.Rewards
                        && (shopRequest.ValueRO.Action == ShopAction.Upgrade || shopRequest.ValueRO.Action == ShopAction.RerollUpgrades
                            || shopRequest.ValueRO.Action == ShopAction.TakeItemReward
                            || shopRequest.ValueRO.Action == ShopAction.RecycleItemReward)))
                    {
                        EconomyRules.ApplyShopRequest(entityManager, playerEntity, shopRequest.ValueRO, economyConfiguration, catalog);
                    }

                    break;
                }
            }

            entityCommands.Playback(entityManager);
        }
    }
}
