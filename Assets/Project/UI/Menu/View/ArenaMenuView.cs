using System;
using HordeMuncher.UI.Contracts;
using UnityEngine.UIElements;

namespace HordeMuncher.UI.Menu
{
    public sealed class ArenaMenuView : IArenaMenuView, IDisposable
    {
        public event Action OnHostSessionClicked;
        public event Action<string> OnJoinRoomClicked;
        public event Action OnOpenSettingsClicked;
        public event Action OnCloseSettingsClicked;
        public event Action OnExitToMenuClicked;
        public event Action OnResumeGameClicked;
        private readonly VisualElement _startScreen;
        private readonly VisualElement _sessionHud;
        private readonly VisualElement _settingsScreen;
        private readonly VisualElement _pauseScreen;
        private readonly Label _menuStatus;
        private readonly Label _sessionStatus;
        private readonly Button _hostButton;
        private readonly Button _joinServerButton;
        private readonly TextField _roomCodeInput;
        private readonly TextField _activeRoomCode;
        private readonly Button _openSettingsButton;
        private readonly Button _closeSettingsButton;
        private readonly Button _exitToMenuButton;
        private readonly Button _pauseExitToMenuButton;
        private readonly Button _resumeGameButton;
        private IVisualElementScheduledItem _focusHostButton;

        public ArenaMenuView(VisualElement root)
        {
            _startScreen = root.Q("startScreen");
            _sessionHud = root.Q("sessionHud");
            _settingsScreen = root.Q("settingsScreen");
            _pauseScreen = root.Q("pauseScreen");
            root.Add(_pauseScreen);
            _menuStatus = root.Q<Label>("menuStatus");
            _sessionStatus = root.Q<Label>("sessionStatus");
            _hostButton = root.Q<Button>("hostButton");
            _joinServerButton = root.Q<Button>("clientButton");
            _roomCodeInput = root.Q<TextField>("roomCodeInput");
            _activeRoomCode = root.Q<TextField>("activeRoomCode");
            _openSettingsButton = root.Q<Button>("settingsButton");
            _closeSettingsButton = root.Q<Button>("closeSettingsButton");
            _exitToMenuButton = root.Q<Button>("backButton");
            _pauseExitToMenuButton = root.Q<Button>("pauseExitToMenuButton");
            _resumeGameButton = root.Q<Button>("resumeGameButton");
            _resumeGameButton.clicked += ResumeGameClickedHandler;
            _hostButton.clicked += HostSessionClickedHandler;
            _joinServerButton.clicked += JoinRoomClickedHandler;
            _openSettingsButton.clicked += OpenSettingsClickedHandler;
            _closeSettingsButton.clicked += CloseSettingsClickedHandler;
            _exitToMenuButton.clicked += ExitToMenuClickedHandler;
            _pauseExitToMenuButton.clicked += ExitToMenuClickedHandler;
        }

        public void Render(ArenaSessionState state, bool isSettingsVisible)
        {
            _hostButton.SetEnabled(!state.IsConnecting);
            _joinServerButton.SetEnabled(!state.IsConnecting);
            _roomCodeInput.SetEnabled(!state.IsConnecting);
            _activeRoomCode.SetValueWithoutNotify(state.RoomCode ?? string.Empty);
            bool wasSettingsVisible = !_settingsScreen.ClassListContains("hidden");
            _startScreen.EnableInClassList("hidden", state.IsRunning || isSettingsVisible);
            _settingsScreen.EnableInClassList("hidden", state.IsRunning || !isSettingsVisible);
            if (isSettingsVisible && !wasSettingsVisible)
            {
                _focusHostButton?.Pause();
                _closeSettingsButton.Focus();
            }

            bool isPauseVisible = state.IsRunning && !state.IsInLobby && state.IsPaused;
            bool wasPauseVisible = !_pauseScreen.ClassListContains("hidden");
            _pauseScreen.EnableInClassList("hidden", !isPauseVisible);
            if (isPauseVisible)
            {
                _pauseScreen.BringToFront();
                if (!wasPauseVisible)
                {
                    _focusHostButton?.Pause();
                    _resumeGameButton.Focus();
                }
            }

            _sessionHud.EnableInClassList("hidden", !state.IsRunning || state.IsInLobby || isPauseVisible);
            _menuStatus.text = state.Status;
            _sessionStatus.text = state.Status;
        }

        public void FocusHostButton()
        {
            _focusHostButton?.Pause();
            _focusHostButton = _hostButton.schedule.Execute(_hostButton.Focus);
        }

        public void Dispose()
        {
            _focusHostButton?.Pause();
            _hostButton.clicked -= HostSessionClickedHandler;
            _joinServerButton.clicked -= JoinRoomClickedHandler;
            _openSettingsButton.clicked -= OpenSettingsClickedHandler;
            _closeSettingsButton.clicked -= CloseSettingsClickedHandler;
            _exitToMenuButton.clicked -= ExitToMenuClickedHandler;
            _pauseExitToMenuButton.clicked -= ExitToMenuClickedHandler;
            _resumeGameButton.clicked -= ResumeGameClickedHandler;
        }

        private void HostSessionClickedHandler()
        {
            OnHostSessionClicked?.Invoke();
        }

        private void JoinRoomClickedHandler()
        {
            OnJoinRoomClicked?.Invoke(_roomCodeInput.value);
        }

        private void OpenSettingsClickedHandler()
        {
            OnOpenSettingsClicked?.Invoke();
        }

        private void CloseSettingsClickedHandler()
        {
            OnCloseSettingsClicked?.Invoke();
        }

        private void ResumeGameClickedHandler()
        {
            OnResumeGameClicked?.Invoke();
        }

        private void ExitToMenuClickedHandler()
        {
            OnExitToMenuClicked?.Invoke();
        }
    }
}
