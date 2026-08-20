using ZamboniDedicated;
using ZProtocol;

namespace ZamboniGameServerProvider;

internal sealed class DedicatedInstance : GameInstance
{
    private Dedicated? _dedicated;
    public ulong GameId { get; }
    public Guid Guid { get; }
    public ZamboniTopology Topology { get; }
    public string GameProtocolVersion { get; }
    public ushort Port { get; }
    public int MaxPlayers { get; }

    public DedicatedInstance(ushort port, ReserveRequest request)
    {
        GameId = request.GameId;
        Guid = request.Guid;
        Topology = request.Topology;
        GameProtocolVersion = request.GameProtocolVersion;
        Port = port;
        MaxPlayers = request.MaxPlayers;
    }

    public override void Start()
    {
        _dedicated = new Dedicated(ticksPerSecond: 30, maxClients: MaxPlayers, port: Port);
        _dedicated.Run();
    }

    public override void Stop()
    {
        _dedicated?.Shutdown();
    }
}