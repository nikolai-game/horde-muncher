using System.Collections.Generic;

namespace HordeMuncher
{
    public static class LobbyRoster
    {
        public static List<LobbyParticipant> Parse(string roster)
        {
            var players = new List<LobbyParticipant>();
            foreach (string row in roster.Split(';'))
            {
                string[] fields = row.Split(',');
                if (fields.Length != 9 || !int.TryParse(fields[0], out int playerNumber)
                    || !int.TryParse(fields[1], out int localPlayerGroupId) || !int.TryParse(fields[2], out int localIndex)
                    || !int.TryParse(fields[5], out int colorIndex)
                    || !int.TryParse(fields[6], out int characterId) || !CharacterArchetypes.IsValid(characterId))
                {
                    continue;
                }

                players.Add(new LobbyParticipant
                {
                    PlayerNumber = playerNumber, LocalPlayerGroupId = localPlayerGroupId, LocalIndex = localIndex,
                    Ready = fields[3] == "1", InputName = System.Uri.UnescapeDataString(fields[4]),
                    ColorIndex = colorIndex, CharacterId = characterId,
                    SelectionJoined = fields[7] == "1", DisplayName = System.Uri.UnescapeDataString(fields[8])
                });
            }

            players.Sort((first, second) => first.PlayerNumber.CompareTo(second.PlayerNumber));
            return players;
        }
    }
}
