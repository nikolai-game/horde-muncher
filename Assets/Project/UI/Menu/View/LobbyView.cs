using System;
using HordeMuncher.UI.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace HordeMuncher.UI.Menu
{
    public sealed class LobbyView : ILobbyView, IDisposable
    {
        private const int FAST_CHARACTER_ID = 2;

        public event Action<int> OnCharacterClicked;
        public event Action OnLeaveLobbyClicked;

        private readonly VisualElement _root;
        private readonly Label _status;
        private readonly Button _leaveLobby;
        private readonly VisualElement[] _playerSeats = new VisualElement[ArenaLimits.MAX_PLAYERS];
        private readonly VisualElement[] _characters = new VisualElement[ArenaLimits.MAX_PLAYERS];
        private readonly VisualElement[] _emptySeats = new VisualElement[ArenaLimits.MAX_PLAYERS];
        private readonly Label[] _participants = new Label[ArenaLimits.MAX_PLAYERS];
        private readonly Label[] _characterNames = new Label[ArenaLimits.MAX_PLAYERS];
        private readonly Label[] _descriptions = new Label[ArenaLimits.MAX_PLAYERS];
        private readonly Label[] _positiveModifiers = new Label[ArenaLimits.MAX_PLAYERS];
        private readonly Label[] _negativeModifiers = new Label[ArenaLimits.MAX_PLAYERS];
        private readonly Label[] _readyChecks = new Label[ArenaLimits.MAX_PLAYERS];
        private readonly Button[] _characterButtons = new Button[ArenaLimits.CHARACTER_COUNT];
        private readonly Action[] _characterHandlers = new Action[ArenaLimits.CHARACTER_COUNT];
        private readonly VisualElement[] _joinHoldContainers = new VisualElement[ArenaLimits.MAX_PLAYERS];
        private readonly HoldProgressRing[] _joinHoldRings = new HoldProgressRing[ArenaLimits.MAX_PLAYERS];
        private readonly Label[] _joinHoldLabels = new Label[ArenaLimits.MAX_PLAYERS];
        private readonly VisualElement[] _selectionHighlights = new VisualElement[ArenaLimits.MAX_PLAYERS];

        public LobbyView(VisualElement root)
        {
            _root = root.Q("lobbyScreen");
            _status = root.Q<Label>("lobbyStatus");
            _leaveLobby = root.Q<Button>("leaveLobby");
            for (int index = 0; index < ArenaLimits.MAX_PLAYERS; index++)
            {
                _playerSeats[index] = root.Q("lobbySeat" + index);
                _characters[index] = root.Q("lobbyCharacter" + index);
                _emptySeats[index] = root.Q("lobbyEmpty" + index);
                _joinHoldContainers[index] = new VisualElement { name = "lobbyJoinHold" + index, pickingMode = PickingMode.Ignore };
                _joinHoldContainers[index].AddToClassList("lobby-join-hold");
                _joinHoldContainers[index].AddToClassList("hidden");
                _joinHoldRings[index] = new HoldProgressRing();
                _joinHoldLabels[index] = new Label { enableRichText = false };
                _joinHoldLabels[index].AddToClassList("lobby-empty-hint");
                _joinHoldContainers[index].Add(_joinHoldRings[index]);
                _joinHoldContainers[index].Add(_joinHoldLabels[index]);
                _emptySeats[index].Add(_joinHoldContainers[index]);
                _participants[index] = root.Q<Label>("lobbyParticipant" + index);
                _participants[index].enableRichText = false;
                _characterNames[index] = _characters[index].Q<Label>(className: "lobby-character-name");
                _descriptions[index] = _characters[index].Q<Label>(className: "lobby-character-description");
                _positiveModifiers[index] = _characters[index].Q<Label>(className: "lobby-positive-stats");
                _negativeModifiers[index] = _characters[index].Q<Label>(className: "lobby-negative-stats");
                _readyChecks[index] = _characters[index].Q<Label>(className: "lobby-ready-check");
                _selectionHighlights[index] = new VisualElement { pickingMode = PickingMode.Ignore };
                _selectionHighlights[index].AddToClassList("lobby-selection");
            }

            for (int characterId = 0; characterId < ArenaLimits.CHARACTER_COUNT; characterId++)
            {
                int selectedCharacterId = characterId;
                _characterButtons[characterId] = root.Q<Button>("lobbyChoice" + characterId);
                _characterHandlers[characterId] = () => OnCharacterClicked?.Invoke(selectedCharacterId);
                _characterButtons[characterId].clicked += _characterHandlers[characterId];
            }

            _leaveLobby.clicked += LeaveLobbyClickedHandler;
            _root.Query<Button>().ForEach(button => button.focusable = false);
        }

        public void Render(LobbyState state)
        {
            _root.EnableInClassList("hidden", !state.IsVisible);
            _status.text = state.Status;
            LobbyPlayerState[] players = state.Players ?? Array.Empty<LobbyPlayerState>();
            int occupiedSeats = 0;
            for (int index = 0; index < ArenaLimits.MAX_PLAYERS; index++)
            {
                _participants[index].EnableInClassList("hidden", index >= players.Length);
                _selectionHighlights[index].RemoveFromHierarchy();
                if (index >= players.Length)
                {
                    continue;
                }

                LobbyPlayerState player = players[index];
                string status = player.IsReady ? "готов ✓" : player.SelectionJoined ? "выбирает" : "ожидает участия";
                _participants[index].text = player.DisplayName + " · " + status;
                _participants[index].style.color = GetPlayerColor(player);
                if (player.SelectionJoined && occupiedSeats < ArenaLimits.MAX_PLAYERS)
                {
                    RenderCharacter(occupiedSeats++, player, state.Characters);
                }
            }

            LobbyJoinHoldState[] joinHolds = state.JoinHolds ?? Array.Empty<LobbyJoinHoldState>();
            for (int seatIndex = 0; seatIndex < ArenaLimits.MAX_PLAYERS; seatIndex++)
            {
                bool occupied = seatIndex < occupiedSeats;
                int holdIndex = seatIndex - occupiedSeats;
                bool showHold = state.IsVisible && !occupied && holdIndex < joinHolds.Length;
                _joinHoldContainers[seatIndex].EnableInClassList("hidden", !showHold);
                if (showHold)
                {
                    LobbyJoinHoldState hold = joinHolds[holdIndex];
                    var holdColor = new Color(hold.ColorRed, hold.ColorGreen, hold.ColorBlue);
                    _joinHoldRings[seatIndex].Render(hold.Progress, holdColor);
                    _joinHoldLabels[seatIndex].text = hold.DisplayName + " · " + Mathf.RoundToInt(hold.Progress * StatUnits.PERCENT_SCALE) + "%";
                    _joinHoldLabels[seatIndex].style.color = holdColor;
                }

                _characters[seatIndex].EnableInClassList("hidden", !occupied);
                _emptySeats[seatIndex].EnableInClassList("hidden", occupied);
                if (!occupied)
                {
                    _playerSeats[seatIndex].RemoveFromClassList("lobby-player-ready");
                    SetBorderColor(_playerSeats[seatIndex], new StyleColor(StyleKeyword.Null));
                }
            }

            for (int index = 0; index < players.Length && index < ArenaLimits.MAX_PLAYERS; index++)
            {
                LobbyPlayerState player = players[index];
                if (!state.IsVisible || !player.SelectionJoined || player.CharacterId < 0 || player.CharacterId >= ArenaLimits.CHARACTER_COUNT)
                {
                    continue;
                }

                Button selectedButton = player.IsBackSelected ? _leaveLobby : _characterButtons[player.CharacterId];
                int overlappingSelections = 0;
                foreach (VisualElement highlight in _selectionHighlights)
                {
                    if (highlight.parent == selectedButton)
                    {
                        overlappingSelections++;
                    }
                }

                VisualElement selection = _selectionHighlights[index];
                for (int inset = 0; inset < ArenaLimits.MAX_PLAYERS; inset++)
                {
                    selection.EnableInClassList("lobby-selection-inset-" + inset, inset == overlappingSelections);
                }

                SetBorderColor(selection, GetPlayerColor(player));
                selectedButton.Add(selection);
            }
        }

        public void Dispose()
        {
            for (int characterId = 0; characterId < ArenaLimits.CHARACTER_COUNT; characterId++)
            {
                _characterButtons[characterId].clicked -= _characterHandlers[characterId];
            }

            _leaveLobby.clicked -= LeaveLobbyClickedHandler;
        }

        private void RenderCharacter(int seatIndex, LobbyPlayerState player, LobbyCharacterState[] characters)
        {
            if (characters == null || player.CharacterId < 0 || player.CharacterId >= characters.Length)
            {
                return;
            }

            LobbyCharacterState character = characters[player.CharacterId];
            Color playerColor = GetPlayerColor(player);
            _characterNames[seatIndex].text = character.Name;
            _characterNames[seatIndex].style.color = playerColor;
            SetText(_descriptions[seatIndex], character.Description);
            SetText(_positiveModifiers[seatIndex], character.PositiveModifiers);
            SetText(_negativeModifiers[seatIndex], character.NegativeModifiers);
            _readyChecks[seatIndex].EnableInClassList("hidden", !player.IsReady);
            _characters[seatIndex].EnableInClassList("lobby-strong", player.CharacterId == 1);
            _characters[seatIndex].EnableInClassList("lobby-fast", player.CharacterId == FAST_CHARACTER_ID);
            _playerSeats[seatIndex].EnableInClassList("lobby-player-ready", player.IsReady);
            SetBorderColor(_playerSeats[seatIndex], playerColor);
        }

        private void LeaveLobbyClickedHandler()
        {
            OnLeaveLobbyClicked?.Invoke();
        }

        private static Color GetPlayerColor(LobbyPlayerState player)
        {
            return new Color(player.ColorRed, player.ColorGreen, player.ColorBlue);
        }

        private static void SetText(Label label, string text)
        {
            label.text = text;
            label.EnableInClassList("hidden", string.IsNullOrEmpty(text));
        }

        private static void SetBorderColor(VisualElement element, StyleColor color)
        {
            element.style.borderTopColor = color;
            element.style.borderRightColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftColor = color;
        }
    }
}
