using System.Diagnostics;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HordeMuncher.Tests
{
    public sealed class EnemyCombatSpatialIndexTests
    {
        [TestCase(128)]
        [TestCase(2048)]
        [TestCase(8192)]
        public void SweptQueriesMatchFullScanAndReportCost(int enemyCount)
        {
            using var world = new World("Combat spatial differential tests");
            EntityManager entityManager = world.EntityManager;
            var random = new Random(7391);
            for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
            {
                Entity enemy = entityManager.CreateEntity(typeof(ArenaEnemy), typeof(CombatHealth), typeof(LocalTransform));
                entityManager.SetComponentData(enemy, LocalTransform.FromPosition(random.NextFloat(-19, 19), 0, random.NextFloat(-19, 19)));
                entityManager.SetComponentData(enemy, new ArenaEnemy { Radius = random.NextFloat(0.1f, 1.5f) });
            }

            using EntityQuery enemies = entityManager.CreateEntityQuery(typeof(ArenaEnemy), typeof(CombatHealth), typeof(LocalTransform));
            using NativeArray<LocalTransform> transforms = enemies.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using NativeArray<ArenaEnemy> footprints = enemies.ToComponentDataArray<ArenaEnemy>(Allocator.Temp);
            var starts = new NativeArray<float2>(512, Allocator.Temp);
            using var startsLifetime = starts;
            var ends = new NativeArray<float2>(starts.Length, Allocator.Temp);
            using var endsLifetime = ends;
            var expectedHits = new NativeArray<int>(starts.Length, Allocator.Temp);
            using var expectedHitsLifetime = expectedHits;
            for (int queryIndex = 0; queryIndex < starts.Length; queryIndex++)
            {
                starts[queryIndex] = random.NextFloat2(-20, 20);
                ends[queryIndex] = starts[queryIndex] + random.NextFloat2(-2, 2);
            }

            ProjectileSystem.TryIntersectEnemyFootprint(float2.zero, new float2(1), float2.zero, 1, out _);
            var stopwatch = Stopwatch.StartNew();
            for (int queryIndex = 0; queryIndex < starts.Length; queryIndex++)
            {
                int hit = -1;
                float earliest = float.MaxValue;
                for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
                {
                    if (ProjectileSystem.TryIntersectEnemyFootprint(starts[queryIndex], ends[queryIndex],
                        transforms[enemyIndex].Position.xz, footprints[enemyIndex].Radius + 0.12f, out float fraction) && fraction < earliest)
                    {
                        earliest = fraction;
                        hit = enemyIndex;
                    }
                }

                expectedHits[queryIndex] = hit;
            }

            double fullScanMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            stopwatch.Restart();
            using var spatialIndex = new EnemyCombatSpatialIndex(enemies);
            var candidates = new NativeList<int>(Allocator.Temp);
            using var candidateLifetime = candidates;
            var actualHits = new NativeArray<int>(starts.Length, Allocator.Temp);
            using var actualHitsLifetime = actualHits;
            long candidateChecks = 0;
            for (int queryIndex = 0; queryIndex < starts.Length; queryIndex++)
            {
                float padding = spatialIndex.MaximumRadius + 0.12f;
                spatialIndex.CollectCandidates(math.min(starts[queryIndex], ends[queryIndex]) - padding,
                    math.max(starts[queryIndex], ends[queryIndex]) + padding, ref candidates);
                candidateChecks += candidates.Length;
                int hit = -1;
                float earliest = float.MaxValue;
                foreach (int enemyIndex in candidates)
                {
                    if (ProjectileSystem.TryIntersectEnemyFootprint(starts[queryIndex], ends[queryIndex],
                        transforms[enemyIndex].Position.xz, footprints[enemyIndex].Radius + 0.12f, out float fraction) && fraction < earliest)
                    {
                        earliest = fraction;
                        hit = enemyIndex;
                    }
                }

                actualHits[queryIndex] = hit;
            }

            stopwatch.Stop();
            for (int queryIndex = 0; queryIndex < starts.Length; queryIndex++)
            {
                Assert.That(actualHits[queryIndex], Is.EqualTo(expectedHits[queryIndex]), $"Sweep {queryIndex}");
            }

            TestContext.WriteLine($"Enemies={enemyCount}; sweeps={starts.Length}; fullScanMs={fullScanMilliseconds:F3}; "
                + $"gridBuildAndQueriesMs={stopwatch.Elapsed.TotalMilliseconds:F3}; "
                + $"candidateChecks={candidateChecks}; fullScanChecks={(long)enemyCount * starts.Length}");
        }

        [Test]
        public void KnockbackMovesEnemyBetweenCellsAndLargeSweepsFallBackSafely()
        {
            using var arena = new CombatTestArena();
            Entity enemy = arena.CreateEnemy(-8);
            for (int otherEnemy = 0; otherEnemy < 64; otherEnemy++)
            {
                arena.CreateEnemy(30 + otherEnemy);
            }
            using EntityQuery enemies = arena.EntityManager.CreateEntityQuery(typeof(ArenaEnemy), typeof(CombatHealth), typeof(LocalTransform));
            using var spatialIndex = new EnemyCombatSpatialIndex(enemies);
            var candidates = new NativeList<int>(Allocator.Temp);
            using var candidateLifetime = candidates;
            spatialIndex.UpdateTransform(0, LocalTransform.FromPosition(8, 0, 0));
            spatialIndex.CollectCandidates(new float2(-9, -1), new float2(-7, 1), ref candidates);
            Assert.That(candidates.Length, Is.Zero);
            spatialIndex.CollectCandidates(new float2(7, -1), new float2(9, 1), ref candidates);
            Assert.That(candidates.Length, Is.EqualTo(1));
            Assert.That(spatialIndex.Transforms[0].Position.x, Is.EqualTo(8));
            spatialIndex.CollectCandidates(new float2(-100000), new float2(100000), ref candidates);
            Assert.That(candidates.Length, Is.EqualTo(65));
            Assert.That(spatialIndex.Entities[candidates[0]], Is.EqualTo(enemy));
        }
    }
}
