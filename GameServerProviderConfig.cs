using System.Net;
using YamlDotNet.Serialization;

namespace ZamboniGameServerProvider;

public class GameServerProviderConfig
{
    public List<string> MatchmakingServers { get; set; } = new()
    {
        "95.217.209.57", "127.0.0.1"
    };

    public string PublicIp { get; set; } = "auto";
    public ushort ZProtocolPort { get; set; } = 3737;
    public ushort PortRangeStart { get; set; } = 17600;
    public ushort PortRangeEnd { get; set; } = 17700;

    [YamlIgnore] public Range PortRange => new(PortRangeStart, PortRangeEnd);

    [YamlIgnore]
    public List<IPAddress> MatchmakingServerAddresses =>
        MatchmakingServers.Select(IPAddress.Parse).ToList();
}