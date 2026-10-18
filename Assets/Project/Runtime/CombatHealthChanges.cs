using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace HordeMuncher
{
    public static class CombatHealthChanges
    {
        public static void Send(EntityCommandBuffer commands, float3 position, int healthChange, bool isPlayer, bool isCritical = false)
        {
            if (healthChange == 0)
            {
                return;
            }

            Entity message = commands.CreateEntity();
            commands.AddComponent(message, new CombatHealthChangeRpc
            {
                Position = position,
                HealthChange = healthChange,
                IsPlayer = isPlayer,
                IsCritical = isCritical
            });
            commands.AddComponent(message, new SendRpcCommandRequest());
        }
    }
}
