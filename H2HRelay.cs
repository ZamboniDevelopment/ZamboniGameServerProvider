using System.Net;
using System.Net.Sockets;

namespace ZamboniGameServerProvider;

public sealed class H2HRelay(ushort port)
{
    private IPEndPoint? _playerA;
    private IPEndPoint? _playerB;
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

        if (_playerA is null)
        {
            _playerA = sender;
        }
        else if (_playerB is null && !sender.Equals(_playerA))
        {
            _playerB = sender;
        }

        var target = sender.Equals(_playerA) ? _playerB : sender.Equals(_playerB) ? _playerA : null;

        if (target is not null) await _relayUdpClient.SendAsync(result.Buffer, target);
    }
}