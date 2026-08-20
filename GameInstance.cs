using ZProtocol;

namespace ZamboniGameServerProvider;

public abstract class GameInstance
{
    public ulong GameId { get; }
    public Guid Guid { get; }
    public ZamboniTopology Topology { get; }
    public string GameProtocolVersion { get; }
    public ushort Port { get; }
    public abstract void Start();
    public abstract void Stop();
}