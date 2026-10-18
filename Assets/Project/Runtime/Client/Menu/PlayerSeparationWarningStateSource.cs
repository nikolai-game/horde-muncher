using HordeMuncher.UI.Contracts;
using UnityEngine;

namespace HordeMuncher.Client.Menu
{
    public sealed class PlayerSeparationWarningStateSource : IPlayerSeparationWarningStateSource
    {
        private readonly ArenaSession _session;
        public bool IsWarningVisible
        {
            get
            {
                if (!_session.IsRunning || Camera.main == null)
                {
                    return false;
                }

                ArenaCameraFollow camera = Camera.main.GetComponent<ArenaCameraFollow>();
                return camera != null && camera.NearSharedLimit;
            }
        }

        public PlayerSeparationWarningStateSource(ArenaSession session)
        {
            _session = session;
        }
    }
}
