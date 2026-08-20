using NLog;
using NLog.Layouts;

namespace ZamboniGameServerProvider;

class Program
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static GameServerProvider? _gameServerProvider;

    static async Task Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        StartLogger();
        _gameServerProvider = new GameServerProvider();
        _ = _gameServerProvider.Start();
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

    private static Task CommandLoop()
    {
        try
        {
            Console.WriteLine("Commands: 'status', 'exit'");

            while (true)
            {
                switch (Console.ReadLine()?.Trim().ToLowerInvariant())
                {
                    case "status": ShowStatus(); break;
                    case "exit":
                        Logger.Info("Exiting...");
                        Environment.Exit(0);
                        break;
                }
            }
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    private static void ShowStatus()
    {
        Console.WriteLine("\n--- ZamboniGameServerProvider ---");
        Console.WriteLine($"Active instances: {_gameServerProvider!.Servers.Count}");
        foreach (var server in _gameServerProvider.Servers.Values)
        {
            Console.WriteLine($"{server.Guid} {server.GameId} {server.GameProtocolVersion} {server.Topology} {server.Port}");
        }

        Console.WriteLine("-----------------------------\n");
    }
}