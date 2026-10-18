using Unity.Entities;

namespace HordeMuncher
{
    public struct IncomingRpcBudget : IComponentData
    {
        public const int BURST_CAPACITY = 32;
        public const int REQUESTS_PER_SECOND = 16;

        public double UpdatedAt;
        public double Tokens;
    }
}
