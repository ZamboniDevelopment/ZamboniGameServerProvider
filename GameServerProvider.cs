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

    public static GameServerProviderConfig Config = null!;

    public readonly ConcurrentDictionary<Guid, GameInstance> Servers = new();
    private readonly ConcurrentQueue<ushort> _freePorts = new();

    public GameServerProvider()
    {
        InitConfig();
        for (ushort p = Config.PortRangeStart; p < Config.PortRangeEnd; p++)
        {
            _freePorts.Enqueue(p);
        }
    }

    public Task Start()
    {
        _ = Listen();
        return Task.CompletedTask;
    }

    private static void InitConfig()
    {
        const string configFile = "game-server-provider-config.yml";
        var serializer = new SerializerBuilder().WithNamingConvention(PascalCaseNamingConvention.Instance).Build();

        if (!File.Exists(configFile))
        {
            Config = new GameServerProviderConfig();
            File.WriteAllText(configFile, serializer.Serialize(Config));
            Logger.Warn("Config file created: " + configFile);
            return;
        }

        var deserializer = new DeserializerBuilder().WithNamingConvention(PascalCaseNamingConvention.Instance).Build();
        Config = deserializer.Deserialize<GameServerProviderConfig>(File.ReadAllText(configFile));
    }

    private async Task Listen()
    {
        var listener = new TcpListener(IPAddress.Any, Config.ZProtocolPort);
        listener.Start();

        while (true)
        {
            var client = await listener.AcceptTcpClientAsync();

            _ = Task.Run(async () =>
            {
                using (client)
                {
                    var remoteIp = ((IPEndPoint)client.Client.RemoteEndPoint!).Address;

                    if (Config.MatchmakingServerAddresses.Any(ipAddress => ipAddress.Equals(remoteIp)))
                    {
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
                                        Logger.Debug($"GameServerProvider creating a dedicated server on port {port}");
                                        gameInstance = new DedicatedInstance(port, reserve.Request, remoteIp.ToString());
                                    }
                                    else
                                    {
                                        Logger.Debug($"GameServerProvider creating a relay server on port {port}");
                                        gameInstance = new H2HRelayInstance(port, reserve.Request, remoteIp.ToString());
                                    }

                                    Servers.TryAdd(reserve.Request.Guid, gameInstance);
                                    gameInstance.Start();
                                    await Protocol.SendResponseAsync(stream, new ReserveInstanceResponse(new GameInstanceInfo(Config.PublicIp.ToLower().Equals("auto") ? PublicIp : Config.PublicIp, port))
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
                                case PlayerJoiningCommand playerJoining:
                                    bool success = false;
                                    if (Servers.TryGetValue(playerJoining.Guid, out var targetServer))
                                    {
                                        success = targetServer.AddJoiningPlayer(playerJoining.Slot, playerJoining.IpAddress);
                                    }

                                    if (success)
                                    {
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
                    else
                    {
                        Logger.Warn("Blocked packet from: " + remoteIp);
                    }
                }
            });
        }
    }

    public static async Task<ResponsePacket?> SendAsync(string ip, ushort port, CommandPacket command)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(ip, port);
            await using var stream = client.GetStream();
            await Protocol.SendCommandAsync(stream, command);
            return await Protocol.ReadResponseAsync(stream);
        }
        catch (Exception e)
        {
            Logger.Warn(e);
            Logger.Warn("Failed to contact server " + ip);
        }

        return null;
    }
}