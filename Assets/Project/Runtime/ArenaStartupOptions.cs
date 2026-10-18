using System;
using HordeMuncher.UI.Contracts;

namespace HordeMuncher
{
    public sealed class ArenaStartupOptions
    {
        public string Mode { get; }
        public string ServerAddress { get; }
        public string Port { get; }
        public string RoomCode { get; }

        public ArenaStartupOptions(string[] arguments, string defaultMode, string serverAddress, string port)
        {
            Mode = ReadOption(arguments, "-arenaMode", defaultMode);
            ServerAddress = ReadOption(arguments, "-arenaAddress", Normalize(serverAddress, ArenaConnectionDefaults.LOOPBACK_ADDRESS));
            Port = ReadOption(arguments, "-arenaPort", Normalize(port, ArenaConnectionDefaults.PORT));
            RoomCode = ReadOption(arguments, "-arenaRoomCode", string.Empty);
        }

        public void StartSession(IArenaSessionCommands commands)
        {
            switch (Mode)
            {
                case "host":
                    commands.StartHostSession(ServerAddress, Port);
                    break;
                case "client":
                    commands.JoinRoom(RoomCode);
                    break;
                case "local":
                    commands.StartLocalSession(ServerAddress, Port);
                    break;
                case "server":
                    commands.StartDedicatedServer(ServerAddress, Port);
                    break;
            }
        }

        private static string ReadOption(string[] arguments, string name, string fallback)
        {
            int index = Array.IndexOf(arguments, name);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : fallback;
        }

        private static string Normalize(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
