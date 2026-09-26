using System.Net;
using System.Net.Sockets;

namespace ZamboniGameServerProvider;

public sealed class H2HRelay(ushort port)
{
    public IPEndPoint? PlayerA;
    public IPEndPoint? PlayerB;
    private readonly UdpClient _relayUdpClient = new(port);
    private readonly CancellationTokenSource _cts = new();

    public void Start()
    {
        _ = RunAsync();
    }

    public void Stop()
    {
        _cts.Cancel();
        _relayUdpClient.Close();
    }

    private async Task RunAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var result = await _relayUdpClient.ReceiveAsync(_cts.Token);
                await ProcessPacketAsync(result);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }

    private async Task ProcessPacketAsync(UdpReceiveResult result)
    {
        var sender = result.RemoteEndPoint;

        if (PlayerA is null)
        {
            PlayerA = sender;
        }
        else if (PlayerB is null && !sender.Equals(PlayerA))
        {
            PlayerB = sender;
        }

        var target = sender.Equals(PlayerA) ? PlayerB : sender.Equals(PlayerB) ? PlayerA : null;

        if (target is not null) await _relayUdpClient.SendAsync(result.Buffer, target);
    }
}