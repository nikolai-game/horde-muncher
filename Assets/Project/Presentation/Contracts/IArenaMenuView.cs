namespace HordeMuncher.UI.Contracts
{
    public interface IArenaMenuView
    {
        event System.Action OnHostSessionClicked;
        event System.Action<string> OnJoinRoomClicked;
        event System.Action OnOpenSettingsClicked;
        event System.Action OnCloseSettingsClicked;
        event System.Action OnExitToMenuClicked;
        event System.Action OnResumeGameClicked;
        void Render(ArenaSessionState state, bool isSettingsVisible);
        void FocusHostButton();
    }
}
