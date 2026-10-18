using Unity.NetCode;
using UnityEngine.Scripting;

namespace HordeMuncher
{
    [Preserve]
    public sealed class ArenaBootstrap : ClientServerBootstrap
    {
        public override bool Initialize(string defaultWorldName)
        {
            if (!DetermineIfBootstrappingEnabled())
            {
                return false;
            }

            AutoConnectPort = 0;
            CreateLocalWorld("ArenaMenu");
            return true;
        }
    }
}
