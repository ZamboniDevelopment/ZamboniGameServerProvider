using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NLog;

namespace Relay;

public class RelayServer(ushort port, string gameProtocolVersion)
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    public readonly ConcurrentDictionary<IPEndPoint, byte> Players = new();
    public readonly ConcurrentDictionary<string, byte> AllowFrom = new();
    private readonly UdpClient _udpClient = new(port);
    public ushort Port { get; } = port;
    public string GameProtocolVersion { get; } = gameProtocolVersion;
    private readonly CancellationTokenSource _cts = new();

    public void Start()
    {
        Logger.Debug("Starting RelayServer on port: " + port);
        _ = RunAsync();
    }

    public void Stop()
    {
        _cts.Cancel();
        _udpClient.Close();
    }

    private async Task RunAsync()
    {
        while (true)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync();
                _ = ProcessPacketAsync(result);
            }
            catch (Exception)
            {
                break;
            }
        }
    }

    private async Task ProcessPacketAsync(UdpReceiveResult result)
    {
        string remoteIp = result.RemoteEndPoint.Address.ToString();

        if (!Players.ContainsKey(result.RemoteEndPoint))
        {
            if (AllowFrom.TryRemove(remoteIp, out _))
            {
                Players.TryAdd(result.RemoteEndPoint, 0);
            }
            else
            {
                Logger.Warn($"Blocked udp traffic from unknown address: {result.RemoteEndPoint.Address}");
                return;
            }
        }

        if (Program.ShowTraffic) Logger.Debug("Received: " + ByteArrayToString(result.Buffer) + " from:" + result.RemoteEndPoint);

        var sendTasks = Players.Keys
            .Where(ep => !ep.Equals(result.RemoteEndPoint))
            .Select(ep => _udpClient.SendAsync(result.Buffer, result.Buffer.Length, ep));

        await Task.WhenAll(sendTasks);
    }

    private static string ByteArrayToString(byte[] ba)
    {
        StringBuilder hex = new StringBuilder(ba.Length * 2);
        foreach (byte b in ba)
            hex.AppendFormat("{0:x2}", b);
        return hex.ToString();
    }
}