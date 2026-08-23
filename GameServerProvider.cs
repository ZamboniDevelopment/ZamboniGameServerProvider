using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using NLog;
using ZProtocol;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Protocol = ZProtocol.ZProtocol;

namespace ZamboniGameServerProvider;

public class GameServerProvider
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public static readonly string PublicIp = new HttpClient().GetStringAsync("https://checkip.amazonaws.com/").GetAwaiter().GetResult().Trim();

    private static GameServerProviderConfig _config = null!;

    public readonly ConcurrentDictionary<Guid, GameInstance> Servers = new();
    private readonly ConcurrentQueue<ushort> _freePorts = new();

    public GameServerProvider()
    {
        InitConfig();
        for (ushort p = _config.PortRangeStart; p < _config.PortRangeEnd; p++)
        {
            _freePorts.Enqueue(p);
        }
    }

    public Task Start()
    {
        _ = Listener();
        return Task.CompletedTask;
    }

    private static void InitConfig()
    {
        const string configFile = "game-server-provider-config.yml";
        var serializer = new SerializerBuilder().WithNamingConvention(PascalCaseNamingConvention.Instance).Build();

        if (!File.Exists(configFile))
        {
            _config = new GameServerProviderConfig();
            File.WriteAllText(configFile, serializer.Serialize(_config));
            Logger.Warn("Config file created: " + configFile);
            return;
        }

        var deserializer = new DeserializerBuilder().WithNamingConvention(PascalCaseNamingConvention.Instance).Build();
        _config = deserializer.Deserialize<GameServerProviderConfig>(File.ReadAllText(configFile));
    }

    private async Task Listener()
    {
        var listener = new TcpListener(IPAddress.Any, _config.ZProtocolPort);
        listener.Start();

        while (true)
        {
            var client = await listener.AcceptTcpClientAsync();

            _ = Task.Run(async () =>
            {
                using (client)
                {
                    var remoteIp = ((IPEndPoint)client.Client.RemoteEndPoint!).Address;

                    if (!_config.MatchmakingServerAddresses.Contains(remoteIp))
                    {
                        Logger.Warn("Blocked packet from: " + remoteIp);
                        return;
                    }

                    try
                    {
                        await using var stream = client.GetStream();

                        var command = await Protocol.ReadCommandAsync(stream);

                        if (command == null)
                        {
                            Logger.Warn("Packet is null");
                            return;
                        }

                        if (command.Version != Protocol.ProtocolVersion)
                        {
                            Logger.Warn($"Version mismatch: {command.Version}");
                            return;
                        }

                        switch (command)
                        {
                            case ReserveInstanceCommand reserve:

                                if (!_freePorts.TryDequeue(out var port))
                                {
                                    await Protocol.SendResponseAsync(stream, new GenericResponse
                                    {
                                        Status = Status.NoCapacity
                                    });
                                    break;
                                }

                                GameInstance gameInstance;
                                if (reserve.Request.Topology == ZamboniTopology.Dedicated)
                                {
                                    gameInstance = new DedicatedInstance(port, reserve.Request);
                                }
                                else
                                {
                                    gameInstance = new H2HRelayInstance(port, reserve.Request);
                                }

                                Servers.TryAdd(reserve.Request.Guid, gameInstance);
                                gameInstance.Start();
                                await Protocol.SendResponseAsync(stream, new ReserveInstanceResponse(new GameInstanceInfo(_config.PublicIp.ToLower().Equals("auto") ? PublicIp : _config.PublicIp, port))
                                {
                                    Status = Status.Ok
                                });
                                break;
                            case DestroyInstanceCommand destroy:
                                if (Servers.TryRemove(destroy.Guid, out var destroyed))
                                {
                                    destroyed.Stop();
                                    _freePorts.Enqueue(destroyed.Port);
                                    Logger.Debug($"Destroyed server on port {destroyed.Port}");
                                    await Protocol.SendResponseAsync(stream, new GenericResponse
                                    {
                                        Status = Status.Ok
                                    });
                                }
                                else
                                {
                                    await Protocol.SendResponseAsync(stream, new GenericResponse
                                    {
                                        Status = Status.Error
                                    });
                                }

                                break;
                            case ResetAllInstancesCommand resetAllInstancesCommand:
                                var versions = resetAllInstancesCommand.GameProtocolVersions;
                                foreach (var server in Servers.Values.ToList().Where(server => versions.Contains(server.GameProtocolVersion)))
                                {
                                    if (Servers.TryRemove(server.Guid, out var removed))
                                    {
                                        removed.Stop();
                                        _freePorts.Enqueue(server.Port);
                                    }
                                }

                                await Protocol.SendResponseAsync(stream, new GenericResponse
                                {
                                    Status = Status.Ok
                                });
                                break;
                            default:
                                Logger.Warn($"Unknown command type: {command.GetType().Name}");
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Error processing request");
                    }
                }
            });
        }
    }
}