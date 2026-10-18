using Unity.Entities;
using Unity.Mathematics;

namespace HordeMuncher
{
    public static class ItemDropRules
    {
        public static float CalculateDropChance(int luck) => math.min(0.25f, math.max(0, luck) * 0.001f);

        public static bool TryRollItem(DynamicBuffer<CatalogEntry> catalog, int luck, ref Random random, out int catalogId)
        {
            catalogId = -1;
            if (random.NextFloat() >= CalculateDropChance(luck))
            {
                return false;
            }

            catalogId = EconomyRules.RollCatalogEntry(catalog, ref random, true);
            return catalogId >= 0;
        }

        public static bool TryCollectItem(EntityManager entityManager, Entity collector, int catalogId,
            DynamicBuffer<CatalogEntry> catalog)
        {
            if (catalogId < 0 || catalogId >= catalog.Length || catalog[catalogId].Kind != GoodsKind.Item
                || !entityManager.HasComponent<PlayerProgress>(collector) || !entityManager.HasComponent<PlayerBonuses>(collector)
                || !entityManager.HasBuffer<PendingItemReward>(collector))
            {
                return false;
            }

            entityManager.GetBuffer<PendingItemReward>(collector).Add(new PendingItemReward { CatalogId = catalogId });
            PlayerProgress progress = entityManager.GetComponentData<PlayerProgress>(collector);
            progress.CollectedItems++;
            entityManager.SetComponentData(collector, progress);
            return true;
        }
    }
}
