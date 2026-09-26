using ZProtocol;

namespace ZamboniGameServerProvider;

public abstract class GameInstance
{
    public ulong GameId { get; init; }
    public Guid Guid { get; init; }
    public ZamboniTopology Topology { get; init; }
    public string? GameProtocolVersion { get; init; }
    public ushort Port { get; init; }
    protected string MatchmakerIpAddress { get; init; }
    protected ushort MatchmakerZProtocolPort { get; init; }
    public abstract void Start();
    public abstract void Stop();
    public abstract bool AddJoiningPlayer(byte slot, string ipAddress);
    public abstract void InformPlayerLeaving(byte slot);
}