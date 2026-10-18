using System;
using HordeMuncher.UI.Contracts;

namespace HordeMuncher.Tests.Menu
{
    public sealed class FakeArenaMenuView : IArenaMenuView
    {
        public event Action OnHostSessionClicked;
        public event Action<string> OnJoinRoomClicked;
        public event Action OnOpenSettingsClicked;
        public event Action OnCloseSettingsClicked;
        public event Action OnExitToMenuClicked;
        public event Action OnResumeGameClicked;
        public event Action OnQuitGameClicked;
        public ArenaSessionState State;
        public int HostFocusCount;
        public bool IsSettingsVisible;

        public void Render(ArenaSessionState state, bool isSettingsVisible)
        {
            State = state;
            IsSettingsVisible = isSettingsVisible;
        }

        public void FocusHostButton()
        {
            HostFocusCount++;
        }

        public void ClickHostSession() => OnHostSessionClicked?.Invoke();

        public void ClickJoinServer() => OnJoinRoomClicked?.Invoke("ABC123");

        public void ClickOpenSettings() => OnOpenSettingsClicked?.Invoke();

        public void ClickCloseSettings() => OnCloseSettingsClicked?.Invoke();

        public void ClickExitToMenu() => OnExitToMenuClicked?.Invoke();
        public void ClickResumeGame() => OnResumeGameClicked?.Invoke();

        public void ClickQuitGame() => OnQuitGameClicked?.Invoke();
    }
}
