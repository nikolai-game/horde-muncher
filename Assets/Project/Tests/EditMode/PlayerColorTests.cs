using HordeMuncher.Tests.Menu;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace HordeMuncher.Tests
{
    public sealed class PlayerColorTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void FourConnectionsReceiveDistinctColors(bool joinInSameTick)
        {
            using var arena = new LobbyTestArena();
            for (int playerIndex = 0; playerIndex < 4; playerIndex++)
            {
                arena.Join(playerIndex + 1, 10, playerIndex);
                if (!joinInSameTick)
                {
                    arena.JoinSystem.Update(arena.World.Unmanaged);
                }
            }

            arena.JoinSystem.Update(arena.World.Unmanaged);
            using EntityQuery query = arena.Manager.CreateEntityQuery(typeof(ArenaPlayer));
            using NativeArray<ArenaPlayer> players = query.ToComponentDataArray<ArenaPlayer>(Allocator.Temp);
            int usedColors = 0;
            Assert.That(players.Length, Is.EqualTo(4));
            foreach (ArenaPlayer player in players)
            {
                Assert.That(player.ColorIndex, Is.InRange(0, 3));
                Assert.That(usedColors & (1 << player.ColorIndex), Is.Zero);
                usedColors |= 1 << player.ColorIndex;
            }
        }

        [Test]
        public void ReplacementConnectionReceivesReleasedColor()
        {
            using var arena = new LobbyTestArena();
            for (int playerIndex = 0; playerIndex < 4; playerIndex++)
            {
                arena.Join(playerIndex + 1, 10, playerIndex);
            }

            arena.JoinSystem.Update(arena.World.Unmanaged);
            using EntityQuery query = arena.Manager.CreateEntityQuery(typeof(ArenaPlayer));
            using NativeArray<Entity> players = query.ToEntityArray(Allocator.Temp);
            Entity departedPlayer = players[0];
            int releasedColor = arena.Manager.GetComponentData<ArenaPlayer>(departedPlayer).ColorIndex;
            arena.Manager.DestroyEntity(departedPlayer);
            Entity replacementConnection = arena.Join(5, 20, 0);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            Entity replacement = arena.Manager.GetBuffer<LinkedEntityGroup>(replacementConnection)[1].Value;
            Assert.That(arena.Manager.GetComponentData<ArenaPlayer>(replacement).ColorIndex, Is.EqualTo(releasedColor));
        }
    }
}
