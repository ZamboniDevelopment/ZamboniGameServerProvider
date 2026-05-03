using System.Net;
using YamlDotNet.Serialization;

namespace Relay;

public class RelayConfig
{
    public List<string> MatchmakingServers { get; set; } = new()
    {
        "95.217.209.57",
    };
    public int PortRangeStart { get; set; } = 17600;
    public int PortRangeEnd { get; set; } = 17700;
    
    [YamlIgnore]
    public Range PortRange => new(PortRangeStart, PortRangeEnd);
    
    [YamlIgnore]
    public List<IPAddress> MatchmakingServerAddresses => 
        MatchmakingServers.Select(IPAddress.Parse).ToList();
}