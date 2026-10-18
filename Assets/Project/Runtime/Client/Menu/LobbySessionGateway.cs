using System.Collections.Generic;
using HordeMuncher.UI.Contracts;
using HordeMuncher.UI.Menu;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HordeMuncher.Client.Menu
{
    public sealed class LobbySessionGateway : ILobbySession
    {

        private readonly ArenaSession _session;
        private readonly PlayerColorSettings _playerColors;
        private readonly LobbyJoinHoldTracker _joinHolds = new LobbyJoinHoldTracker();
        private readonly HashSet<int> _unavailablePlayers = new HashSet<int>();
        private readonly HashSet<int> _pendingSelectionJoins = new HashSet<int>();
        private readonly HashSet<int> _waitForJoinRelease = new HashSet<int>();
        private readonly Dictionary<int, float> _nextNavigationTimes = new Dictionary<int, float>();
        private readonly List<LobbyJoinHoldState> _activeJoinHolds = new List<LobbyJoinHoldState>();
        private bool _wasLobbyVisible;

        public LobbySessionGateway(ArenaSession session, PlayerColorSettings playerColors)
        {
            _session = session;
            _playerColors = playerColors;
        }

        public LobbyState ReadLobby()
        {
            List<LobbyParticipant> roster = _session.ReadLobbyPlayers();
            List<LocalPlayerInput> inputs = _session.ReadLocalInputs();
            var players = new List<LobbyPlayerState>();
            int mouseLocalIndex = -1;
            int selectingCount = 0;
            int readyCount = 0;
            foreach (LobbyParticipant player in roster)
            {
                bool isLocal = player.LocalPlayerGroupId == _session.LocalPlayerGroupId;
                LocalPlayerInput input = inputs.Find(candidate => candidate.LocalIndex == player.LocalIndex);
                bool available = !isLocal || ArenaLocalInput.IsAvailable(input);
                Color playerColor = _playerColors != null ? _playerColors.GetColor(player.ColorIndex) : Color.white;
                if (isLocal && player.SelectionJoined && input.KeyboardLayout != 0 && available && mouseLocalIndex < 0)
                {
                    mouseLocalIndex = player.LocalIndex;
                }

                selectingCount += player.SelectionJoined ? 1 : 0;
                readyCount += player.Ready ? 1 : 0;
                players.Add(new LobbyPlayerState
                {
                    LocalIndex = player.LocalIndex,
                    CharacterId = player.CharacterId,
                    ColorRed = playerColor.r,
                    ColorGreen = playerColor.g,
                    ColorBlue = playerColor.b,
                    IsLocal = isLocal,
                    IsReady = player.Ready,
                    IsInputAvailable = available,
                    SelectionJoined = player.SelectionJoined,
                    DisplayName = player.DisplayName
                });
            }

            return new LobbyState
            {
                IsVisible = _session.IsRunning && _session.HasJoinedLobby && !_session.HasStarted && _session.Mode != ArenaMode.Server,
                MouseLocalIndex = mouseLocalIndex,
                Status = "В лобби: " + roster.Count + "/4 · Присоединились к выбору: " + selectingCount + " · Готовы: " + readyCount,
                Players = players.ToArray(),
                JoinHolds = _activeJoinHolds.ToArray(),
                Characters = new[]
                {
                    ReadCharacter(CharacterArchetypes.BALANCED),
                    ReadCharacter(CharacterArchetypes.STRONG),
                    ReadCharacter(CharacterArchetypes.FAST)
                }
            };
        }

        public void UpdateInput(LobbyPresenter presenter, float currentTime)
        {
            _activeJoinHolds.Clear();
            if (!_session.IsRunning || !_session.HasJoinedLobby || _session.HasStarted)
            {
                ResetInput();
                return;
            }

            if (!_wasLobbyVisible || presenter == null)
            {
                _wasLobbyVisible = true;
                return;
            }

            List<LocalPlayerInput> inputs = _session.ReadLocalInputs();
            List<LobbyParticipant> roster = _session.ReadLobbyPlayers();
            foreach (LocalPlayerInput input in inputs)
            {
                bool held = IsJoinHeld(input);
                if (!held)
                {
                    _waitForJoinRelease.Remove(input.LocalIndex);
                }

                LobbyParticipant participant = roster.Find(player => player.LocalPlayerGroupId == _session.LocalPlayerGroupId
                    && player.LocalIndex == input.LocalIndex);
                bool registered = roster.Exists(player => player.LocalPlayerGroupId == _session.LocalPlayerGroupId
                    && player.LocalIndex == input.LocalIndex);
                if (registered && _pendingSelectionJoins.Remove(input.LocalIndex))
                {
                    presenter.JoinSelection(input.LocalIndex);
                }

                bool completedHold = _joinHolds.UpdateHold(input.KeyboardLayout, input.DeviceId, held, currentTime);
                if (held && !completedHold && !_waitForJoinRelease.Contains(input.LocalIndex)
                    && (!registered || !participant.SelectionJoined))
                {
                    Color playerColor = registered && _playerColors != null ? _playerColors.GetColor(participant.ColorIndex) : Color.white;
                    AddJoinHold(input, registered ? participant.DisplayName : ReadInputName(input), playerColor, currentTime);
                }

                if (!registered && completedHold)
                {
                    _pendingSelectionJoins.Add(input.LocalIndex);
                    _waitForJoinRelease.Add(input.LocalIndex);
                }

                if (registered && !participant.SelectionJoined && completedHold)
                {
                    presenter.JoinSelection(input.LocalIndex);
                    _waitForJoinRelease.Add(input.LocalIndex);
                }

                if (!ArenaLocalInput.IsAvailable(input))
                {
                    if (_unavailablePlayers.Add(input.LocalIndex))
                    {
                        _session.SetLobbyReady(input.LocalIndex, false);
                    }

                    continue;
                }

                _unavailablePlayers.Remove(input.LocalIndex);
                if (!registered || !participant.SelectionJoined || _waitForJoinRelease.Contains(input.LocalIndex))
                {
                    continue;
                }

                bool cancel = Application.isFocused && (input.KeyboardLayout != 0
                    ? (input.KeyboardLayout == LocalInputLayout.PRIMARY_KEYBOARD ? Keyboard.current.escapeKey.wasPressedThisFrame
                        : Keyboard.current.backspaceKey.wasPressedThisFrame)
                    : InputSystem.GetDeviceById(input.DeviceId) is Gamepad gamepad && gamepad.buttonEast.wasPressedThisFrame);
                if (cancel)
                {
                    presenter.CancelSelection(input.LocalIndex);
                    continue;
                }

                Vector2 direction = ArenaLocalInput.ReadNavigation(input, Application.isFocused);
                if (direction.sqrMagnitude < NavigationInputTuning.THRESHOLD_SQUARED)
                {
                    _nextNavigationTimes.Remove(input.LocalIndex);
                }
                else if (!_nextNavigationTimes.TryGetValue(input.LocalIndex, out float nextMoveTime) || currentTime >= nextMoveTime)
                {
                    presenter.MoveSelection(input.LocalIndex, direction.x, direction.y);
                    _nextNavigationTimes[input.LocalIndex] = currentTime + NavigationInputTuning.REPEAT_SECONDS;
                }

                if (ArenaLocalInput.WasSubmitPressed(input))
                {
                    presenter.SubmitSelection(input.LocalIndex);
                    if (!_session.IsRunning || !_session.HasJoinedLobby || _session.HasStarted)
                    {
                        return;
                    }
                }
            }

            for (int keyboardLayout = 1; keyboardLayout <= ArenaLimits.KEYBOARD_LAYOUT_COUNT; keyboardLayout++)
            {
                int candidateLayout = keyboardLayout;
                if (!inputs.Exists(input => input.KeyboardLayout == candidateLayout))
                {
                    TryJoinUnusedInput(new LocalPlayerInput { KeyboardLayout = candidateLayout }, currentTime);
                }
            }

            foreach (InputDevice device in InputSystem.devices)
            {
                if ((device is Gamepad || device is Joystick) && !inputs.Exists(input => input.DeviceId == device.deviceId))
                {
                    TryJoinUnusedInput(new LocalPlayerInput { DeviceId = device.deviceId }, currentTime);
                }
            }
        }

        public void ResetInput()
        {
            _activeJoinHolds.Clear();
            _wasLobbyVisible = false;
            _joinHolds.Clear();
            _nextNavigationTimes.Clear();
            _pendingSelectionJoins.Clear();
            _waitForJoinRelease.Clear();
            _unavailablePlayers.Clear();
        }

        public void JoinCharacterSelection(int localIndex)
        {
            _session.SetLobbySelection(localIndex, true);
        }

        public void PreviewCharacter(int localIndex, int characterId)
        {
            _session.SetLobbySelection(localIndex, true, characterId);
        }

        public void LeaveCharacterSelection(int localIndex)
        {
            _session.SetLobbySelection(localIndex, false);
        }

        public void ToggleReady(int localIndex)
        {
            foreach (LobbyParticipant player in _session.ReadLobbyPlayers())
            {
                if (player.LocalPlayerGroupId == _session.LocalPlayerGroupId && player.LocalIndex == localIndex)
                {
                    _session.SetLobbyReady(localIndex, !player.Ready, player.CharacterId);
                    return;
                }
            }
        }

        public void SelectCharacter(int localIndex, int characterId)
        {
            if (CharacterArchetypes.IsValid(characterId))
            {
                _session.SetLobbyReady(localIndex, true, characterId);
            }
        }

        public void LeaveLobby()
        {
            _session.StopSession();
        }

        private void TryJoinUnusedInput(LocalPlayerInput input, float currentTime)
        {
            bool held = IsJoinHeld(input);
            bool completedHold = _joinHolds.UpdateHold(input.KeyboardLayout, input.DeviceId, held, currentTime);
            if (!completedHold)
            {
                if (held)
                {
                    AddJoinHold(input, ReadInputName(input), Color.white, currentTime);
                }

                return;
            }

            if (!_session.AddLocalPlayer(input.KeyboardLayout, input.DeviceId))
            {
                return;
            }

            LocalPlayerInput joinedInput = _session.ReadLocalInputs().Find(candidate => candidate.KeyboardLayout == input.KeyboardLayout
                && candidate.DeviceId == input.DeviceId);
            _pendingSelectionJoins.Add(joinedInput.LocalIndex);
            _waitForJoinRelease.Add(joinedInput.LocalIndex);
        }

        private void AddJoinHold(LocalPlayerInput input, string displayName, Color playerColor, float currentTime)
        {
            float progress = _joinHolds.ReadProgress(input.KeyboardLayout, input.DeviceId, currentTime);
            if (progress >= 1)
            {
                return;
            }

            _activeJoinHolds.Add(new LobbyJoinHoldState
            {
                DisplayName = displayName,
                Progress = progress,
                ColorRed = playerColor.r,
                ColorGreen = playerColor.g,
                ColorBlue = playerColor.b
            });
        }

        private static string ReadInputName(LocalPlayerInput input)
        {
            if (input.KeyboardLayout != 0)
            {
                return input.KeyboardLayout == LocalInputLayout.PRIMARY_KEYBOARD ? "Клавиатура · Пробел" : "Клавиатура · Enter";
            }

            return "Контроллер " + input.DeviceId;
        }

        private static bool IsJoinHeld(LocalPlayerInput input)
        {
            if (!Application.isFocused || !ArenaLocalInput.IsAvailable(input))
            {
                return false;
            }

            if (input.KeyboardLayout != 0)
            {
                return input.KeyboardLayout == LocalInputLayout.PRIMARY_KEYBOARD
                    ? Keyboard.current.spaceKey.isPressed : Keyboard.current.enterKey.isPressed;
            }

            InputDevice device = InputSystem.GetDeviceById(input.DeviceId);
            return device is Gamepad gamepad ? gamepad.buttonSouth.isPressed
                : device is Joystick joystick && joystick.trigger.isPressed;
        }

        private static LobbyCharacterState ReadCharacter(int characterId)
        {
            var positive = new List<string>();
            var negative = new List<string>();
            var neutral = new List<string>();
            foreach (string line in CharacterArchetypes.GetDescription(characterId).Split('\n'))
            {
                if (line.StartsWith("+"))
                {
                    positive.Add(line);
                }
                else if (line.StartsWith("−") || line.StartsWith("-"))
                {
                    negative.Add(line);
                }
                else
                {
                    neutral.Add(line);
                }
            }

            return new LobbyCharacterState
            {
                CharacterId = characterId,
                Name = CharacterArchetypes.GetName(characterId),
                Description = string.Join("\n", neutral),
                PositiveModifiers = string.Join("\n", positive),
                NegativeModifiers = string.Join("\n", negative)
            };
        }
    }
}
