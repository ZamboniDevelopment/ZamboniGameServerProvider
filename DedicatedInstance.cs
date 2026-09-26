using ZamboniDedicated;
using ZProtocol;

namespace ZamboniGameServerProvider;

internal sealed class DedicatedInstance : GameInstance
{
    private Dedicated? Dedicated { get; set; }
    private int MaxPlayers { get; }

    public DedicatedInstance(ushort port, ReserveRequest request, string matchmakerIpAddress)
    {
        GameId = request.GameId;
        Guid = request.Guid;
        Topology = request.Topology;
        GameProtocolVersion = request.GameProtocolVersion;
        Port = port;
        MaxPlayers = request.MaxPlayers;
        MatchmakerIpAddress = matchmakerIpAddress;
    }

    public override void Start()
    {
        Dedicated = new Dedicated(ticksPerSecond: 30, maxClients: MaxPlayers, port: Port, standalone: false);
        Dedicated.Start();
        Dedicated.PlayerLeavingAction += InformPlayerLeaving;
    }

    public override void Stop()
    {
        Dedicated?.Stop();
        Dedicated!.PlayerLeavingAction -= InformPlayerLeaving;
    }

    public override bool AddJoiningPlayer(byte slot, string ipAddress)
    {
        return Dedicated != null && Dedicated.AddJoiningPlayer(slot, ipAddress);
    }

    public override async void InformPlayerLeaving(byte slot)
    {
        await GameServerProvider.SendAsync(MatchmakerIpAddress, MatchmakerZProtocolPort, new PlayerLeavingCommand(slot, Guid));
    }
}