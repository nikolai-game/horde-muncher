using System.Collections.Generic;
using HordeMuncher.UI.Contracts;

namespace HordeMuncher.Tests.Menu
{
    public sealed class FakeArenaMenuSession : IArenaSessionStateSource, IArenaSessionCommands, IPlayerSeparationWarningStateSource
    {
        public ArenaSessionState State = new ArenaSessionState
        {
            Status = "Выберите режим игры"
        };
        public readonly List<string> Commands = new List<string>();
        public bool IsWarningVisible { get; set; }

        public ArenaSessionState Read()
        {
            return State;
        }

        public void StartHostSession(string serverAddress, string port)
        {
            Commands.Add("Host " + serverAddress + ":" + port);
        }

        public void JoinServer(string serverAddress, string port)
        {
            Commands.Add("Join " + serverAddress + ":" + port);
        }

        public void JoinRoom(string roomCode)
        {
            Commands.Add("Join room " + roomCode);
        }

        public void StartLocalSession(string serverAddress, string port)
        {
            Commands.Add("Local " + serverAddress + ":" + port);
        }

        public void StartDedicatedServer(string serverAddress, string port)
        {
            Commands.Add("Server " + serverAddress + ":" + port);
        }

        public void ResumeGame()
        {
            Commands.Add("Resume");
            State.IsPaused = false;
        }

        public void StopSession()
        {
            Commands.Add("Stop");
            State.IsRunning = false;
        }
    }
}
