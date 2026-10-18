using HordeMuncher.UI.Menu;
using NUnit.Framework;

namespace HordeMuncher.Tests.Menu
{
    public sealed class LobbyNavigationTests
    {
        [Test]
        public void PlayersSelectDifferentCharactersAndMouseCannotControlRemotePlayer()
        {
            var lobby = new FakeLobby();
            using var presenter = new LobbyPresenter(lobby, lobby);
            presenter.MoveSelection(0, 1, 0);
            presenter.MoveSelection(1, -1, 0);
            presenter.SubmitSelection(0);
            presenter.SubmitSelection(1);
            lobby.State.Players[2].IsLocal = false;
            lobby.State.MouseLocalIndex = 2;
            lobby.ClickCharacter(1);
            lobby.State.MouseLocalIndex = 3;
            lobby.ClickCharacter(2);
            Assert.That(lobby.ReadyPlayers, Is.EqualTo(new[] { 0, 1, 3 }));
            Assert.That(lobby.SelectedCharacters, Is.EqualTo(new[] { 1, 2, 2 }));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void EachLocalPlayerKeepsItsSelectionAndConfirmsOnlyItsOwnReadiness(int localIndex)
        {
            var lobby = new FakeLobby();
            using var presenter = new LobbyPresenter(lobby, lobby);
            int otherLocalIndex = (localIndex + 1) % 4;
            presenter.MoveSelection(otherLocalIndex, 1, 0);
            presenter.SubmitSelection(localIndex);
            Assert.That(lobby.ReadyPlayers, Is.EqualTo(new[] { localIndex }));
            Assert.That(lobby.RenderedState.Players[localIndex].CharacterId, Is.Zero);
            Assert.That(lobby.RenderedState.Players[otherLocalIndex].CharacterId, Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void EveryLocalPlayerCanReachBackButton(int localIndex)
        {
            var lobby = new FakeLobby();
            using var presenter = new LobbyPresenter(lobby, lobby);
            presenter.MoveSelection(localIndex, 0, 1);
            presenter.SubmitSelection(localIndex);
            Assert.That(lobby.ReadyPlayers, Is.Empty);
            Assert.That(lobby.State.IsVisible, Is.False);
        }

        [Test]
        public void UnjoinedRemoteAndUnavailablePlayersCannotConfirm()
        {
            var lobby = new FakeLobby();
            using var presenter = new LobbyPresenter(lobby, lobby);
            lobby.State.Players[0].SelectionJoined = false;
            lobby.State.Players[1].IsLocal = false;
            lobby.State.Players[3].IsInputAvailable = false;
            presenter.SubmitSelection(0);
            presenter.SubmitSelection(1);
            presenter.SubmitSelection(3);
            presenter.SubmitSelection(99);
            Assert.That(lobby.ReadyPlayers, Is.Empty);
            presenter.JoinSelection(0);
            Assert.That(lobby.JoinedPlayers, Is.EqualTo(new[] { 0 }));
            Assert.That(lobby.ReadyPlayers, Is.Empty);
            presenter.SubmitSelection(0);
            Assert.That(lobby.ReadyPlayers, Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void CancelFirstRemovesReadinessThenReturnsParticipantToLobbyList()
        {
            var lobby = new FakeLobby();
            using var presenter = new LobbyPresenter(lobby, lobby);
            presenter.SubmitSelection(0);
            presenter.MoveSelection(0, 1, 0);
            Assert.That(lobby.State.Players[0].CharacterId, Is.Zero);
            presenter.CancelSelection(0);
            Assert.That(lobby.State.Players[0].IsReady, Is.False);
            Assert.That(lobby.State.Players[0].SelectionJoined, Is.True);
            presenter.CancelSelection(0);
            Assert.That(lobby.State.Players[0].SelectionJoined, Is.False);
            Assert.That(lobby.State.IsVisible, Is.True);
        }
    }
}
