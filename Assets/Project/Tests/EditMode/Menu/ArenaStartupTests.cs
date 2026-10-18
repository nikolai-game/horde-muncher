using NUnit.Framework;
using Unity.Entities;

namespace HordeMuncher.Tests.Menu
{
    public sealed class ArenaStartupTests
    {
        [TestCase("menu", null)]
        [TestCase("host", "Host 192.168.1.5:8000")]
        [TestCase("client", "Join room ABC123")]
        [TestCase("local", "Local 192.168.1.5:8000")]
        [TestCase("server", "Server 192.168.1.5:8000")]
        public void StartupRoutesEachModeThroughSessionCommands(string mode, string expectedCommand)
        {
            var commands = new FakeArenaMenuSession();
            var options = new ArenaStartupOptions(new[]
            {
                "-arenaMode", mode, "-arenaAddress", "192.168.1.5", "-arenaPort", "8000", "-arenaRoomCode", "ABC123"
            }, "menu", "127.0.0.1", "7979");

            options.StartSession(commands);

            Assert.That(commands.Commands, Is.EqualTo(expectedCommand == null ? new string[0] : new[] { expectedCommand }));
        }

        [TestCase("menu")]
        [TestCase("server")]
        public void StartupPreservesPlatformDefaultAndNormalizesSceneConnectionSettings(string defaultMode)
        {
            var options = new ArenaStartupOptions(new[] { "-arenaPort" }, defaultMode, " 192.168.1.10 ", " ");
            Assert.That(options.Mode, Is.EqualTo(defaultMode));
            Assert.That(options.ServerAddress, Is.EqualTo("192.168.1.10"));
            Assert.That(options.Port, Is.EqualTo("7979"));
        }

        [Test]
        public void ServerStartsFirstWaveOnlyAfterEveryParticipantIsReadyAndDoesNotRestartItOnNextUpdate()
        {
            using var arena = new LobbyTestArena();
            Entity run = arena.Manager.CreateEntity(typeof(WaveRuntimeSettings), typeof(WaveProgress));
            arena.Manager.SetComponentData(run, new WaveRuntimeSettings { Seed = 123 });
            arena.Manager.AddBuffer<WavePlan>(run).Add(default);
            Entity first = arena.Join(1, 10, 0);
            Entity second = arena.Join(2, 20, 0);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            RunLifecycleSystem lifecycle = arena.World.CreateSystemManaged<RunLifecycleSystem>();

            arena.SetReady(first, true);
            arena.LobbySystem.Update();
            lifecycle.Update();
            Assert.That(arena.Manager.GetComponentData<WaveProgress>(run).Phase, Is.EqualTo(WavePhase.Waiting));

            arena.SetReady(second, true);
            arena.LobbySystem.Update();
            lifecycle.Update();
            WaveProgress progress = arena.Manager.GetComponentData<WaveProgress>(run);
            Assert.That(arena.Manager.GetComponentData<ArenaLobby>(arena.Lobby).Started, Is.True);
            Assert.That(progress.Phase, Is.EqualTo(WavePhase.Spawning));
            Assert.That(progress.RandomState, Is.EqualTo(123u));

            progress.Elapsed = 7;
            progress.RandomState = 456;
            arena.Manager.SetComponentData(run, progress);
            arena.LobbySystem.Update();
            lifecycle.Update();
            progress = arena.Manager.GetComponentData<WaveProgress>(run);
            Assert.That(progress.Elapsed, Is.EqualTo(7));
            Assert.That(progress.RandomState, Is.EqualTo(456u));
        }
    }
}
