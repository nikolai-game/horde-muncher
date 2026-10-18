namespace HordeMuncher.UI.Contracts
{
    public interface IArenaSessionCommands
    {
        void StartHostSession(string serverAddress, string port);
        void JoinServer(string serverAddress, string port);
        void JoinRoom(string roomCode);
        void StartLocalSession(string serverAddress, string port);
        void StartDedicatedServer(string serverAddress, string port);
        void StopSession();
        void ResumeGame();
    }
}
