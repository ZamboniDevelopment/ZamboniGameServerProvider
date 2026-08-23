using ZamboniDedicated;
using ZProtocol;

namespace ZamboniGameServerProvider;

internal sealed class DedicatedInstance : GameInstance
{
    private Dedicated? Dedicated { get; set; }
    private int MaxPlayers { get; }

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
        Dedicated = new Dedicated(ticksPerSecond: 30, maxClients: MaxPlayers, port: Port);
        Dedicated.Start();
    }

    public override void Stop()
    {
        Dedicated?.Stop();
    }
}