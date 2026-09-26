using ZProtocol;

namespace ZamboniGameServerProvider;

internal sealed class H2HRelayInstance : GameInstance
{
    private H2HRelay? H2HRelay { get; set; }

    public H2HRelayInstance(ushort port, ReserveRequest request, string matchmakerIpAddress)
    {
        GameId = request.GameId;
        Guid = request.Guid;
        Topology = request.Topology;
        GameProtocolVersion = request.GameProtocolVersion;
        Port = port;
        MatchmakerIpAddress = matchmakerIpAddress;
    }

    public override void Start()
    {
        H2HRelay = new H2HRelay(port: Port);
        H2HRelay.Start();
    }

    public override void Stop()
    {
        H2HRelay?.Stop();
    }

    public override bool AddJoiningPlayer(byte slot, string ipAddress)
    {
        return H2HRelay != null && (H2HRelay.PlayerA == null || H2HRelay.PlayerB == null);
    }

    public override void InformPlayerLeaving(byte slot)
    {
        return;
    }
}