using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using NLog;
using NLog.Layouts;
using RelayProtocol;
using Protocol = RelayProtocol.RelayProtocol;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Relay;

class Program
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static RelayConfig _relayConfig;
    private static ConcurrentDictionary<ushort, RelayServer> _activeRelays = new();
    public static bool ShowTraffic;

    static async Task Main(string[] args)
    {
        InitConfig();
        StartLogger();
        Logger.Debug("Hello World!");

        _ = Listener();
        await CommandLoop();
    }

    private static void StartLogger()
    {
        var logLevel = LogLevel.Debug;
        var layout = new SimpleLayout("[${longdate}][${callsite-filename:includeSourcePath=false}(${callsite-linenumber})][${level:uppercase=true}]: ${message:withexception=true}");
        LogManager.Setup().LoadConfiguration(builder =>
        {
            builder.ForLogger().FilterMinLevel(logLevel)
                .WriteToConsole(layout)
                .WriteToFile("logs/server-${shortdate}.log", layout);
        });
    }

    private static void InitConfig()
    {
        const string configFile = "relay-config.yml";
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .Build();
        var serializer = new SerializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .Build();

        if (!File.Exists(configFile))
        {
            _relayConfig = new RelayConfig();
            var yaml = serializer.Serialize(_relayConfig);

            File.WriteAllText(configFile, yaml);
            Logger.Warn("Config file created: " + configFile);
            return;
        }

        var yamlText = File.ReadAllText(configFile);
        _relayConfig = deserializer.Deserialize<RelayConfig>(yamlText);
    }

    static async Task Listener()
    {
        var listener = new TcpListener(IPAddress.Any, 4000);
        listener.Start();

        while (true)
        {
            var client = await listener.AcceptTcpClientAsync();

            _ = Task.Run(async () =>
            {
                using (client)
                {
                    var remoteIp = ((IPEndPoint)client.Client.RemoteEndPoint!).Address;

                    if (!_relayConfig.MatchmakingServerAddresses.Contains(remoteIp))
                    {
                        Logger.Warn("Blocked packet from: " + remoteIp);
                        return;
                    }

                    try
                    {
                        await using var stream = client.GetStream();

                        var packet = await Protocol.ReadCommandAsync(stream);

                        if (packet == null)
                        {
                            Logger.Warn("Packet is null");
                            return;
                        }

                        if (packet.Version != Protocol.ProtocolVersion)
                        {
                            Logger.Warn($"Version mismatch: {packet.Version}");
                            return;
                        }

                        switch (packet)
                        {
                            case ReserveInstanceCommand reserve:
                                ushort assignedPort = GetAvailablePort();
                                if (assignedPort == 0)
                                {
                                    await Protocol.SendResponseAsync(stream, new ReserveInstanceResponse(0)
                                    {
                                        Status = Protocol.RelayStatus.Error
                                    });
                                    break;
                                }

                                var createdRelay = new RelayServer(assignedPort, reserve.GameProtocolVersion);
                                _activeRelays[assignedPort] = createdRelay;
                                createdRelay.Start();
                                await Protocol.SendResponseAsync(stream, new ReserveInstanceResponse(assignedPort)
                                {
                                    Status = Protocol.RelayStatus.Ok
                                });
                                break;
                            case DestroyInstanceCommand destroy:
                                if (_activeRelays.TryRemove(destroy.Port, out var destroyedRelay))
                                {
                                    destroyedRelay.Stop();
                                    Logger.Debug($"Destroyed relay on port {destroy.Port}");
                                    await Protocol.SendResponseAsync(stream, new GenericResponse
                                    {
                                        Status = Protocol.RelayStatus.Ok
                                    });
                                }
                                else
                                {
                                    await Protocol.SendResponseAsync(stream, new GenericResponse
                                    {
                                        Status = Protocol.RelayStatus.Error
                                    });
                                }

                                break;
                            case AllowFromCommand allow:
                                if (_activeRelays.TryGetValue(allow.RelayPort, out var modifiedRelay))
                                {
                                    modifiedRelay.AllowFrom.TryAdd(allow.AllowedIp, 0);
                                    await Protocol.SendResponseAsync(stream, new GenericResponse
                                    {
                                        Status = Protocol.RelayStatus.Ok
                                    });
                                }
                                else
                                {
                                    await Protocol.SendResponseAsync(stream, new GenericResponse
                                    {
                                        Status = Protocol.RelayStatus.Error
                                    });
                                }

                                break;
                            case ResetAllInstancesCommand resetAllInstancesCommand:
                                var versions = resetAllInstancesCommand.GameProtocolVersions.ToList();
                                foreach (var activeRelay in _activeRelays.Values.ToList().Where(activeRelay => versions.Contains(activeRelay.GameProtocolVersion)))
                                {
                                    if (_activeRelays.TryRemove(activeRelay.Port, out var removed))
                                    {
                                        removed.Stop();
                                    }
                                }

                                await Protocol.SendResponseAsync(stream, new GenericResponse
                                {
                                    Status = Protocol.RelayStatus.Ok
                                });
                                break;
                            default:
                                Logger.Warn($"Unknown command type: {packet.GetType().Name}");
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

    private static async Task CommandLoop()
    {
        Console.WriteLine("Commands: 'status', 'exit', 'traffic'");

        while (true)
        {
            string? input = Console.ReadLine()?.Trim().ToLower();

            if (input == "status")
            {
                ShowStatus();
            }

            if (input == "traffic")
            {
                ShowTraffic = !ShowTraffic;
            }
            else if (input == "exit")
            {
                Logger.Info("Exiting...");
                Environment.Exit(0);
            }
        }
    }

    private static void ShowStatus()
    {
        Console.WriteLine("\n--- Relay Server Status ---");
        Console.WriteLine($"Total Active Relays: {_activeRelays.Count}");

        if (_activeRelays.IsEmpty)
        {
            Console.WriteLine("No relays currently active.");
        }
        else
        {
            Console.WriteLine("Port\tClients\tStatus\tProtocol");
            foreach (var kvp in _activeRelays)
            {
                var port = kvp.Key;
                var server = kvp.Value;

                string playerCount = server != null ? server.Players.Count.ToString() : "Empty";
                string status = server != null ? "Running" : "Reserved";
                string protocol = server != null ? server.GameProtocolVersion : "Empty";

                Console.WriteLine($"{port}\t{playerCount}\t{status}\t{protocol}");
            }
        }

        Console.WriteLine("---------------------------\n");
    }

    private static ushort GetAvailablePort()
    {
        for (int port = _relayConfig.PortRange.Start.Value; port <= _relayConfig.PortRange.End.Value; port++)
        {
            var p = (ushort)port;
            if (!_activeRelays.ContainsKey(p))
                return p;
        }

        return 0;
    }
}