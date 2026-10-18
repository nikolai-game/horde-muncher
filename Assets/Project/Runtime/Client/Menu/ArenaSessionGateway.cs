using HordeMuncher.UI.Contracts;

namespace HordeMuncher.Client.Menu
{
    public sealed class ArenaSessionGateway : IArenaSessionStateSource, IArenaSessionCommands
    {
        private readonly ArenaSession _session;

        public ArenaSessionGateway(ArenaSession session)
        {
            _session = session;
        }

        public ArenaSessionState Read()
        {
            return new ArenaSessionState
            {
                IsRunning = _session.IsRunning,
                IsConnecting = _session.IsConnecting,
                RoomCode = _session.RoomCode,
                IsInLobby = _session.IsRunning && _session.HasJoinedLobby && !_session.HasStarted && _session.Mode != ArenaMode.Server,
                IsPaused = _session.IsPaused,
                Status = _session.Status
            };
        }

        public void StartHostSession(string serverAddress, string port)
        {
            _ = _session.StartRelaySessionAsync(true);
        }

        public void JoinServer(string serverAddress, string port)
        {
            _session.StartSession(ArenaMode.Client, serverAddress, port);
        }

        public void JoinRoom(string roomCode)
        {
            _ = _session.StartRelaySessionAsync(false, roomCode);
        }

        public void StartLocalSession(string serverAddress, string port)
        {
            _session.StartSession(ArenaMode.Local, serverAddress, port);
        }

        public void StartDedicatedServer(string serverAddress, string port)
        {
            _session.StartSession(ArenaMode.Server, serverAddress, port);
        }

        public void ResumeGame()
        {
            _session.SetPaused(false);
        }

        public void QuitGame()
        {
            _session.StopSession();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }

        public void StopSession()
        {
            _session.StopSession();
        }
    }
}
