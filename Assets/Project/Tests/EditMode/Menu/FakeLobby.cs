using System;
using System.Collections.Generic;
using HordeMuncher.UI.Contracts;

namespace HordeMuncher.Tests.Menu
{
    public sealed class FakeLobby : ILobbySession, ILobbyView
    {
        public event Action<int> OnCharacterClicked;
        public event Action OnLeaveLobbyClicked;

        public readonly List<int> SelectedCharacters = new List<int>();
        public readonly List<int> ReadyPlayers = new List<int>();
        public readonly List<int> JoinedPlayers = new List<int>();
        public readonly List<int> CanceledReadyPlayers = new List<int>();
        public LobbyState RenderedState;
        public LobbyState State = new LobbyState
        {
            IsVisible = true,
            MouseLocalIndex = 0,
            Players = new[]
            {
                new LobbyPlayerState { IsLocal = true, IsInputAvailable = true, SelectionJoined = true, LocalIndex = 0 },
                new LobbyPlayerState { IsLocal = true, IsInputAvailable = true, SelectionJoined = true, LocalIndex = 1 },
                new LobbyPlayerState { IsLocal = true, IsInputAvailable = true, SelectionJoined = true, LocalIndex = 2 },
                new LobbyPlayerState { IsLocal = true, IsInputAvailable = true, SelectionJoined = true, LocalIndex = 3 }
            }
        };

        public LobbyState ReadLobby()
        {
            LobbyState state = State;
            state.Players = (LobbyPlayerState[])State.Players.Clone();
            return state;
        }

        public void Render(LobbyState state)
        {
            RenderedState = state;
        }

        public void JoinCharacterSelection(int localIndex)
        {
            JoinedPlayers.Add(localIndex);
            State.Players[localIndex].SelectionJoined = true;
        }

        public void PreviewCharacter(int localIndex, int characterId)
        {
            State.Players[localIndex].CharacterId = characterId;
        }

        public void LeaveCharacterSelection(int localIndex)
        {
            State.Players[localIndex].SelectionJoined = false;
        }

        public void ToggleReady(int localIndex)
        {
            CanceledReadyPlayers.Add(localIndex);
            State.Players[localIndex].IsReady = !State.Players[localIndex].IsReady;
        }

        public void SelectCharacter(int localIndex, int characterId)
        {
            ReadyPlayers.Add(localIndex);
            SelectedCharacters.Add(characterId);
            State.Players[localIndex].CharacterId = characterId;
            State.Players[localIndex].IsReady = true;
        }

        public void ClickCharacter(int characterId)
        {
            OnCharacterClicked?.Invoke(characterId);
        }

        public void LeaveLobby()
        {
            State.IsVisible = false;
        }

        public void ClickLeaveLobby()
        {
            OnLeaveLobbyClicked?.Invoke();
        }
    }
}
