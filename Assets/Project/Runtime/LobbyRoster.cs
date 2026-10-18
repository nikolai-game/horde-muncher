using System.Collections.Generic;

namespace HordeMuncher
{
    public static class LobbyRoster
    {
        private const int SERIALIZED_FIELD_COUNT = 9;
        private const int LOCAL_INDEX_FIELD = 2;
        private const int COLOR_INDEX_FIELD = 5;
        private const int CHARACTER_ID_FIELD = 6;
        private const int READY_FIELD = 3;
        private const int INPUT_NAME_FIELD = 4;
        private const int SELECTION_JOINED_FIELD = 7;
        private const int DISPLAY_NAME_FIELD = 8;

        public static List<LobbyParticipant> Parse(string roster)
        {
            var players = new List<LobbyParticipant>();
            foreach (string row in roster.Split(';'))
            {
                string[] fields = row.Split(',');
                if (fields.Length != SERIALIZED_FIELD_COUNT || !int.TryParse(fields[0], out int playerNumber)
                    || !int.TryParse(fields[1], out int localPlayerGroupId) || !int.TryParse(fields[LOCAL_INDEX_FIELD], out int localIndex)
                    || !int.TryParse(fields[COLOR_INDEX_FIELD], out int colorIndex)
                    || !int.TryParse(fields[CHARACTER_ID_FIELD], out int characterId) || !CharacterArchetypes.IsValid(characterId))
                {
                    continue;
                }

                players.Add(new LobbyParticipant
                {
                    PlayerNumber = playerNumber, LocalPlayerGroupId = localPlayerGroupId, LocalIndex = localIndex,
                    Ready = fields[READY_FIELD] == "1", InputName = System.Uri.UnescapeDataString(fields[INPUT_NAME_FIELD]),
                    ColorIndex = colorIndex, CharacterId = characterId,
                    SelectionJoined = fields[SELECTION_JOINED_FIELD] == "1", DisplayName = System.Uri.UnescapeDataString(fields[DISPLAY_NAME_FIELD])
                });
            }

            players.Sort((first, second) => first.PlayerNumber.CompareTo(second.PlayerNumber));
            return players;
        }
    }
}
