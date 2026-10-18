using System;
using System.Collections.Generic;
using HordeMuncher.UI.Contracts;

namespace HordeMuncher.UI.Menu
{
    public sealed class LobbyPresenter : IDisposable
    {
        private readonly ILobbySession _session;
        private readonly ILobbyView _view;
        private readonly Dictionary<int, int> _selectedCharacters = new Dictionary<int, int>();
        private readonly HashSet<int> _backSelections = new HashSet<int>();
        private LobbyState _state;
        private bool _disposed;

        public LobbyPresenter(ILobbySession session, ILobbyView view)
        {
            _session = session;
            _view = view;
            _view.OnCharacterClicked += CharacterClickedHandler;
            _view.OnLeaveLobbyClicked += LeaveLobbyClickedHandler;
            Refresh();
        }

        public void Refresh()
        {
            if (_disposed)
            {
                return;
            }

            _state = _session.ReadLobby();
            _state.Players ??= Array.Empty<LobbyPlayerState>();
            if (!_state.IsVisible)
            {
                _selectedCharacters.Clear();
                _backSelections.Clear();
            }

            foreach (int localIndex in new List<int>(_selectedCharacters.Keys))
            {
                if (!Array.Exists(_state.Players, player => player.IsLocal && player.SelectionJoined && player.LocalIndex == localIndex))
                {
                    _selectedCharacters.Remove(localIndex);
                    _backSelections.Remove(localIndex);
                }
            }

            for (int playerIndex = 0; playerIndex < _state.Players.Length; playerIndex++)
            {
                LobbyPlayerState player = _state.Players[playerIndex];
                if (!_state.IsVisible || !player.IsLocal || !player.SelectionJoined)
                {
                    continue;
                }

                if (player.IsReady || !_selectedCharacters.ContainsKey(player.LocalIndex))
                {
                    _selectedCharacters[player.LocalIndex] = player.CharacterId;
                }

                player.CharacterId = _selectedCharacters[player.LocalIndex];
                player.IsBackSelected = _backSelections.Contains(player.LocalIndex);
                _state.Players[playerIndex] = player;
            }

            _view.Render(_state);
        }

        public void JoinSelection(int localIndex)
        {
            Refresh();
            if (TryGetLocalPlayer(localIndex, out LobbyPlayerState player) && !player.SelectionJoined)
            {
                _session.JoinCharacterSelection(localIndex);
            }
        }

        public void MoveSelection(int localIndex, float horizontal, float vertical)
        {
            Refresh();
            if (!CanSelect(localIndex, out LobbyPlayerState player) || horizontal == 0 && vertical == 0)
            {
                return;
            }

            if (Math.Abs(vertical) > Math.Abs(horizontal))
            {
                if (vertical > 0)
                {
                    _backSelections.Add(localIndex);
                }
                else
                {
                    _backSelections.Remove(localIndex);
                }
            }
            else if (!player.IsReady && !_backSelections.Contains(localIndex))
            {
                int characterId = (player.CharacterId + Math.Sign(horizontal) + 3) % 3;
                _selectedCharacters[localIndex] = characterId;
                _session.PreviewCharacter(localIndex, characterId);
            }

            Refresh();
        }

        public void SubmitSelection(int localIndex)
        {
            Refresh();
            if (!CanSelect(localIndex, out LobbyPlayerState player))
            {
                return;
            }

            if (_backSelections.Contains(localIndex))
            {
                _session.LeaveLobby();
            }
            else if (player.IsReady)
            {
                _session.ToggleReady(localIndex);
            }
            else
            {
                _session.SelectCharacter(localIndex, player.CharacterId);
            }

            Refresh();
        }

        public void CancelSelection(int localIndex)
        {
            Refresh();
            if (!CanSelect(localIndex, out LobbyPlayerState player))
            {
                return;
            }

            if (player.IsReady)
            {
                _session.ToggleReady(localIndex);
            }
            else
            {
                _session.LeaveCharacterSelection(localIndex);
                _selectedCharacters.Remove(localIndex);
            }

            _backSelections.Remove(localIndex);
            Refresh();
        }

        public void Dispose()
        {
            _disposed = true;
            _view.OnCharacterClicked -= CharacterClickedHandler;
            _view.OnLeaveLobbyClicked -= LeaveLobbyClickedHandler;
        }

        private bool CanSelect(int localIndex, out LobbyPlayerState player)
        {
            return TryGetLocalPlayer(localIndex, out player) && player.SelectionJoined;
        }

        private bool TryGetLocalPlayer(int localIndex, out LobbyPlayerState player)
        {
            player = default;
            if (_disposed || !_state.IsVisible)
            {
                return false;
            }

            foreach (LobbyPlayerState candidate in _state.Players)
            {
                if (candidate.IsLocal && candidate.IsInputAvailable && candidate.LocalIndex == localIndex)
                {
                    player = candidate;
                    return true;
                }
            }

            return false;
        }

        private void CharacterClickedHandler(int characterId)
        {
            Refresh();
            if (characterId < 0 || characterId >= 3 || !CanSelect(_state.MouseLocalIndex, out LobbyPlayerState player))
            {
                return;
            }

            if (player.IsReady)
            {
                if (player.CharacterId == characterId)
                {
                    _session.ToggleReady(player.LocalIndex);
                }
            }
            else
            {
                _backSelections.Remove(player.LocalIndex);
                _selectedCharacters[player.LocalIndex] = characterId;
                _session.SelectCharacter(player.LocalIndex, characterId);
            }

            Refresh();
        }

        private void LeaveLobbyClickedHandler()
        {
            _session.LeaveLobby();
        }
    }
}
