using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HordeMuncher.Tests.Menu
{
    public sealed class LobbyTests
    {
        [Test]
        public void ConnectedParticipantRemainsInRosterWithoutOccupyingSelectionAndCannotReadyBeforeJoining()
        {
            using var arena = new LobbyTestArena();
            Entity connection = arena.Join(1, 10, 0);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            arena.SetReady(connection, true, joinSelection: false);
            arena.LobbySystem.Update();
            using EntityQuery snapshots = arena.Manager.CreateEntityQuery(typeof(LobbySnapshotRpc));
            LobbyParticipant participant = LobbyRoster.Parse(snapshots.GetSingleton<LobbySnapshotRpc>().Players.ToString()).Single();
            Assert.That(participant.SelectionJoined, Is.False);
            Assert.That(participant.Ready, Is.False);
            Assert.That(participant.DisplayName, Is.EqualTo("Гость 1"));
            Assert.That(arena.Manager.GetComponentData<ArenaLobby>(arena.Lobby).Started, Is.False);
        }

        [Test]
        public void JoiningAndPreviewingDoesNotReadyPlayerAndReturningToRosterClearsReadiness()
        {
            using var arena = new LobbyTestArena();
            Entity first = arena.Join(1, 10, 0);
            arena.Join(2, 20, 0);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            arena.UpdateSelection(first, true, 2);
            arena.LobbySystem.Update();
            LobbyMember member = arena.Manager.GetComponentData<LobbyMember>(first);
            Assert.That(member.SelectionJoined, Is.True);
            Assert.That(member.CharacterId, Is.EqualTo(2));
            Assert.That(member.Ready, Is.False);
            arena.SetReady(first, true, 2);
            arena.LobbySystem.Update();
            arena.UpdateSelection(first, false);
            arena.LobbySystem.Update();
            member = arena.Manager.GetComponentData<LobbyMember>(first);
            Assert.That(member.SelectionJoined, Is.False);
            Assert.That(member.Ready, Is.False);
            Assert.That(arena.Manager.GetComponentData<ArenaLobby>(arena.Lobby).Started, Is.False);
        }

        [TestCase(0, 100, 0f, 0f)]
        [TestCase(1, 130, 0.4f, -0.15f)]
        [TestCase(2, 70, 0f, 0.3f)]
        public void CharacterSelectionSetsAuthoritativeStatsAndSnapshot(int characterId, int maximumHealth, float damage, float speed)
        {
            using var arena = new LobbyTestArena();
            Entity connection = arena.Join(1, 10, 0);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            arena.SetReady(connection, true, characterId);
            arena.LobbySystem.Update();
            using EntityQuery players = arena.Manager.CreateEntityQuery(typeof(ArenaPlayer), typeof(GhostOwner));
            Entity player = players.GetSingletonEntity();
            Assert.That(arena.Manager.GetComponentData<CombatHealth>(player).Maximum, Is.EqualTo(maximumHealth));
            PlayerBonuses bonuses = arena.Manager.GetComponentData<PlayerBonuses>(player);
            Assert.That(bonuses.Damage, Is.EqualTo(damage).Within(0.001f));
            Assert.That(bonuses.MoveSpeed, Is.EqualTo(speed).Within(0.001f));
            using EntityQuery snapshots = arena.Manager.CreateEntityQuery(typeof(LobbySnapshotRpc));
            LobbyParticipant participant = LobbyRoster.Parse(snapshots.GetSingleton<LobbySnapshotRpc>().Players.ToString()).Single();
            Assert.That(participant.CharacterId, Is.EqualTo(characterId));
            Assert.That(participant.Ready, Is.True);
        }

        [Test]
        public void InvalidCharacterCannotReadyPlayerOrStartGame()
        {
            using var arena = new LobbyTestArena();
            Entity connection = arena.Join(1, 10, 0);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            arena.SetReady(connection, true, 99);
            arena.LobbySystem.Update();
            Assert.That(arena.Manager.GetComponentData<LobbyMember>(connection).Ready, Is.False);
            Assert.That(arena.Manager.GetComponentData<ArenaLobby>(arena.Lobby).Started, Is.False);
        }

        [TestCase(3)]
        [TestCase(2)]
        [TestCase(4)]
        public void FourSeatsSupportMixedMachinesAndRejectFifthConnection(int firstMachinePlayers)
        {
            using var arena = new LobbyTestArena();
            for (int playerIndex = 0; playerIndex < 4; playerIndex++)
            {
                arena.Join(playerIndex + 1, playerIndex < firstMachinePlayers ? 10 : 20,
                    playerIndex < firstMachinePlayers ? playerIndex : playerIndex - firstMachinePlayers);
            }

            Entity rejected = arena.Join(5, 30, 0);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            Assert.That(arena.Manager.HasComponent<NetworkStreamRequestDisconnect>(rejected), Is.True);
            using EntityQuery players = arena.Manager.CreateEntityQuery(typeof(ArenaPlayer));
            using NativeArray<ArenaPlayer> memberships = players.ToComponentDataArray<ArenaPlayer>(Allocator.Temp);
            Assert.That(memberships.Length, Is.EqualTo(4));
            Assert.That(memberships.Count(player => player.SharedViewGroup == arena.ReadGroupId(10)), Is.EqualTo(firstMachinePlayers));
            Assert.That(memberships.Count(player => player.SharedViewGroup == arena.ReadGroupId(20)), Is.EqualTo(4 - firstMachinePlayers));
        }

        [Test]
        public void LobbySnapshotPreservesAssignedCharacterColorsAcrossMachines()
        {
            using var arena = new LobbyTestArena();
            arena.Join(2, 10, 0);
            arena.Join(5, 20, 0);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            arena.LobbySystem.Update();
            using EntityQuery snapshots = arena.Manager.CreateEntityQuery(typeof(LobbySnapshotRpc));
            using NativeArray<LobbySnapshotRpc> messages = snapshots.ToComponentDataArray<LobbySnapshotRpc>(Allocator.Temp);
            using EntityQuery players = arena.Manager.CreateEntityQuery(typeof(ArenaPlayer), typeof(GhostOwner));
            using NativeArray<Entity> characters = players.ToEntityArray(Allocator.Temp);
            foreach (LobbySnapshotRpc message in messages)
            {
                List<LobbyParticipant> roster = LobbyRoster.Parse(message.Players.ToString());
                Assert.That(roster.Count, Is.EqualTo(2));
                foreach (Entity character in characters)
                {
                    int playerNumber = arena.Manager.GetComponentData<GhostOwner>(character).NetworkId;
                    int colorIndex = arena.Manager.GetComponentData<ArenaPlayer>(character).ColorIndex;
                    Assert.That(roster.Single(player => player.PlayerNumber == playerNumber).ColorIndex, Is.EqualTo(colorIndex));
                }
            }
        }

        [Test]
        public void LobbyStartsOnlyAfterEveryPlayerIsReadyAndRejectsLateJoin()
        {
            using var arena = new LobbyTestArena();
            Entity first = arena.Join(1, 10, 0);
            Entity second = arena.Join(2, 20, 0);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            arena.SetReady(first, true);
            arena.LobbySystem.Update();
            Assert.That(arena.Manager.GetComponentData<ArenaLobby>(arena.Lobby).Started, Is.False);
            arena.SetReady(second, true);
            arena.LobbySystem.Update();
            Assert.That(arena.Manager.GetComponentData<ArenaLobby>(arena.Lobby).Started, Is.True);
            Entity late = arena.Join(3, 30, 0);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            Assert.That(arena.Manager.HasComponent<NetworkStreamRequestDisconnect>(late), Is.True);
        }

        [Test]
        public void DisconnectRemovesMemberFromSnapshotAndReadinessRequirement()
        {
            using var arena = new LobbyTestArena();
            Entity first = arena.Join(1, 10, 0);
            Entity second = arena.Join(2, 20, 0);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            arena.Manager.AddComponent<NetworkStreamRequestDisconnect>(second);
            arena.SetReady(first, true);
            arena.LobbySystem.Update();
            Assert.That(arena.Manager.GetComponentData<ArenaLobby>(arena.Lobby).Started, Is.True);
            using EntityQuery snapshots = arena.Manager.CreateEntityQuery(typeof(LobbySnapshotRpc));
            LobbySnapshotRpc snapshot = snapshots.GetSingleton<LobbySnapshotRpc>();
            Assert.That(LobbyRoster.Parse(snapshot.Players.ToString()).Select(player => player.PlayerNumber), Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void DedicatedServerStartsAutomaticallyWhenAllMachinesAreReady()
        {
            using var arena = new LobbyTestArena();
            Entity host = arena.Join(1, 10, 0);
            Entity guest = arena.Join(2, 20, 0);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            arena.SetReady(guest, true);
            arena.LobbySystem.Update();
            Assert.That(arena.Manager.GetComponentData<ArenaLobby>(arena.Lobby).Started, Is.False);
            arena.SetReady(host, true);
            arena.LobbySystem.Update();
            Assert.That(arena.Manager.GetComponentData<ArenaLobby>(arena.Lobby).Started, Is.True);
        }

        [Test]
        public void EmptyLobbyDoesNotStartAndSinglePlayerStartsWhenReady()
        {
            using var arena = new LobbyTestArena();
            arena.LobbySystem.Update();
            Assert.That(arena.Manager.GetComponentData<ArenaLobby>(arena.Lobby).Started, Is.False);
            Entity player = arena.Join(1, 10, 0);
            arena.JoinSystem.Update(arena.World.Unmanaged);
            arena.SetReady(player, true);
            arena.LobbySystem.Update();
            Assert.That(arena.Manager.GetComponentData<ArenaLobby>(arena.Lobby).Started, Is.True);
        }

        [Test]
        public void WaitingWaveDoesNotStartWhileLobbyIsOpen()
        {
            using var arena = new LobbyTestArena();
            Entity run = arena.Manager.CreateEntity(typeof(WaveRuntimeSettings), typeof(WaveProgress));
            arena.Manager.SetComponentData(run, new WaveProgress { Phase = WavePhase.Waiting });
            Entity player = arena.Manager.CreateEntity(typeof(ArenaPlayer), typeof(CombatHealth), typeof(LocalTransform));
            arena.Manager.SetComponentData(player, new CombatHealth { Current = 100, Maximum = 100 });
            arena.World.CreateSystemManaged<RunLifecycleSystem>().Update();
            Assert.That(arena.Manager.GetComponentData<WaveProgress>(run).Phase, Is.EqualTo(WavePhase.Waiting));
        }

        [Test]
        public void KeyboardLayoutsAndControllerDpadKeepTheirOwnUiDirection()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad first = InputSystem.AddDevice<Gamepad>();
            Gamepad second = InputSystem.AddDevice<Gamepad>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.RightArrow));
                InputSystem.QueueStateEvent(first, new GamepadState().WithButton(GamepadButton.DpadLeft));
                InputSystem.QueueStateEvent(second, new GamepadState { leftStick = Vector2.down });
                InputSystem.Update();

                Assert.That(ArenaLocalInput.ReadNavigation(new LocalPlayerInput { KeyboardLayout = 1 }, true), Is.EqualTo(Vector2.up));
                Assert.That(ArenaLocalInput.ReadNavigation(new LocalPlayerInput { KeyboardLayout = 2 }, true), Is.EqualTo(Vector2.right));
                Assert.That(ArenaLocalInput.ReadNavigation(new LocalPlayerInput { DeviceId = first.deviceId }, true), Is.EqualTo(Vector2.left));
                Assert.That(ArenaLocalInput.ReadNavigation(new LocalPlayerInput { DeviceId = second.deviceId }, true), Is.EqualTo(Vector2.down));
                Assert.That(ArenaLocalInput.ReadNavigation(new LocalPlayerInput { DeviceId = first.deviceId }, false), Is.EqualTo(Vector2.zero));
            }
            finally
            {
                InputSystem.RemoveDevice(second);
                InputSystem.RemoveDevice(first);
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [Test]
        public void ExplicitControllerBindingDoesNotReadAnotherGamepadOrKeyboard()
        {
            Gamepad first = InputSystem.AddDevice<Gamepad>();
            Gamepad second = InputSystem.AddDevice<Gamepad>();
            try
            {
                InputSystem.QueueStateEvent(first, new GamepadState { leftStick = Vector2.right });
                InputSystem.QueueStateEvent(second, new GamepadState { leftStick = Vector2.up });
                InputSystem.Update();
                var input = new LocalPlayerInput { LocalIndex = 3, DeviceId = second.deviceId };
                Unity.Mathematics.float2 movement = ArenaLocalInput.Read(input, true);
                Assert.That(movement.x, Is.Zero.Within(0.001f));
                Assert.That(movement.y, Is.GreaterThan(0.9f));
                Assert.That(ArenaLocalInput.Read(input, false), Is.EqualTo(Unity.Mathematics.float2.zero));
            }
            finally
            {
                InputSystem.RemoveDevice(first);
                InputSystem.RemoveDevice(second);
            }
        }
    }
}
