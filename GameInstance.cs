using ZProtocol;

namespace ZamboniGameServerProvider;

public abstract class GameInstance
{
    public ulong GameId { get; init; }
    public Guid Guid { get; init; }
    public ZamboniTopology Topology { get; init; }
    public string? GameProtocolVersion { get; init; }
    public ushort Port { get; init; }
    public abstract void Start();
    public abstract void Stop();
}