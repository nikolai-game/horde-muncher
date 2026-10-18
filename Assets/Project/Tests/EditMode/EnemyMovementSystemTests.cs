using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HordeMuncher.Tests
{
    public sealed class EnemyMovementSystemTests
    {
        [Test]
        public void EnemyChasesNearestLivingPlayerAndStopsAtStoppingDistance()
        {
            using var world = new World("Enemy target selection tests");
            EntityManager entityManager = world.EntityManager;
            CreatePlayer(entityManager, new float2(-0.1f, 0), 0);
            CreatePlayer(entityManager, new float2(10, 0), 100);
            CreatePlayer(entityManager, new float2(2, 0), 100);
            Entity enemyEntity = CreateEnemy(entityManager, float2.zero, 10);
            SimulationSystemGroup movementGroup = CreateMovementGroup(world);
            Step(world, movementGroup);
            float3 enemyPosition = entityManager.GetComponentData<LocalTransform>(enemyEntity).Position;
            Assert.That(enemyPosition.x, Is.EqualTo(1.2f).Within(0.0001f));
            Assert.That(enemyPosition.z, Is.Zero);
            Step(world, movementGroup);
            Assert.That(entityManager.GetComponentData<LocalTransform>(enemyEntity).Position.x, Is.EqualTo(enemyPosition.x).Within(0.0001f));
        }

        [Test]
        public void CoincidentEnemiesSeparateInsideConfiguredArenaBounds()
        {
            using var world = new World("Coincident enemy separation tests");
            EntityManager entityManager = world.EntityManager;
            CreatePlayer(entityManager, new float2(10, 10), 100);
            Entity arenaSettingsEntity = entityManager.CreateEntity(typeof(WaveRuntimeSettings));
            entityManager.SetComponentData(arenaSettingsEntity, new WaveRuntimeSettings
            {
                Center = new float2(10, 10),
                HalfExtents = new float2(2, 2)
            });
            Entity firstEnemy = CreateEnemy(entityManager, new float2(11.5f, 11.5f), 0);
            Entity secondEnemy = CreateEnemy(entityManager, new float2(11.5f, 11.5f), 0);
            SimulationSystemGroup movementGroup = CreateMovementGroup(world);
            for (int tickIndex = 0; tickIndex < 60; tickIndex++)
            {
                Step(world, movementGroup);
            }

            float3 firstPosition = entityManager.GetComponentData<LocalTransform>(firstEnemy).Position;
            float3 secondPosition = entityManager.GetComponentData<LocalTransform>(secondEnemy).Position;
            Assert.That(math.all(math.isfinite(firstPosition)), Is.True);
            Assert.That(math.all(math.isfinite(secondPosition)), Is.True);
            Assert.That(math.distance(firstPosition.xz, secondPosition.xz), Is.GreaterThan(0.79f));
            Assert.That(math.all(math.abs(firstPosition.xz - new float2(10, 10)) <= 1.7f), Is.True);
            Assert.That(math.all(math.abs(secondPosition.xz - new float2(10, 10)) <= 1.7f), Is.True);
        }

        [Test]
        public void EnemyFacesMovementAndKeepsFacingWhenStopped()
        {
            using var world = new World("Enemy facing tests");
            EntityManager entityManager = world.EntityManager;
            CreatePlayer(entityManager, new float2(-2, -2), 100);
            Entity enemyEntity = CreateEnemy(entityManager, float2.zero, 10);
            SimulationSystemGroup movementGroup = CreateMovementGroup(world);
            Step(world, movementGroup);
            LocalTransform movingTransform = entityManager.GetComponentData<LocalTransform>(enemyEntity);
            float3 movementDirection = math.normalize(new float3(movingTransform.Position.x, 0, movingTransform.Position.z));
            Assert.That(math.distance(math.forward(movingTransform.Rotation), movementDirection), Is.LessThan(0.0001f));

            ArenaEnemy enemy = entityManager.GetComponentData<ArenaEnemy>(enemyEntity);
            enemy.Speed = 0;
            entityManager.SetComponentData(enemyEntity, enemy);
            Step(world, movementGroup);
            LocalTransform stoppedTransform = entityManager.GetComponentData<LocalTransform>(enemyEntity);
            Assert.That(math.distance(math.forward(stoppedTransform.Rotation), movementDirection), Is.LessThan(0.0001f));
            Assert.That(math.all(math.isfinite(stoppedTransform.Rotation.value)), Is.True);
        }

        private static Entity CreatePlayer(EntityManager entityManager, float2 position, int health)
        {
            Entity playerEntity = entityManager.CreateEntity(typeof(ArenaPlayer), typeof(LocalTransform), typeof(CombatHealth));
            entityManager.SetComponentData(playerEntity, LocalTransform.FromPosition(position.x, 1, position.y));
            entityManager.SetComponentData(playerEntity, new CombatHealth
            {
                Current = health,
                Maximum = 100
            });
            return playerEntity;
        }

        private static Entity CreateEnemy(EntityManager entityManager, float2 position, float speed)
        {
            Entity enemyEntity = entityManager.CreateEntity(typeof(ArenaEnemy), typeof(LocalTransform));
            entityManager.SetComponentData(enemyEntity, LocalTransform.FromPosition(position.x, 0.65f, position.y));
            entityManager.SetComponentData(enemyEntity, new ArenaEnemy
            {
                Speed = speed,
                StopDistance = 0.8f,
                Radius = 0.3f,
                SeparationGap = 0.2f,
                SeparationSpeed = 12
            });
            return enemyEntity;
        }

        private static SimulationSystemGroup CreateMovementGroup(World world)
        {
            SimulationSystemGroup movementGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            movementGroup.AddSystemToUpdateList(world.GetOrCreateSystem<EnemyMovementSystem>());
            return movementGroup;
        }

        private static void Step(World world, SimulationSystemGroup movementGroup)
        {
            world.SetTime(new TimeData(world.Time.ElapsedTime + 0.2, 0.2f));
            movementGroup.Update();
            world.EntityManager.CompleteAllTrackedJobs();
        }
    }
}
