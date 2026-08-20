using ZProtocol;

namespace ZamboniGameServerProvider;

internal sealed class H2HRelayInstance : GameInstance
{
    private H2HRelay? _h2HRelay;
    public ulong GameId { get; }
    public Guid Guid { get; }
    public ZamboniTopology Topology { get; }
    public string GameProtocolVersion { get; }
    public ushort Port { get; }

    public H2HRelayInstance(ushort port, ReserveRequest request)
    {
        GameId = request.GameId;
        Guid = request.Guid;
        Topology = request.Topology;
        GameProtocolVersion = request.GameProtocolVersion;
        Port = port;
    }

    public override void Start()
    {
        _h2HRelay = new H2HRelay(port: Port);
        _h2HRelay.Start();
    }

    public override void Stop()
    {
        _h2HRelay?.Stop();
    }
}