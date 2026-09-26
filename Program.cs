using NLog;
using NLog.Layouts;

namespace ZamboniGameServerProvider;

class Program
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static LogLevel _currentLogLevel = LogLevel.Debug;

    public static GameServerProvider? GameServerProvider;

    static async Task Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        var startupLevel = ParseLogLevel(args.Length > 0 ? args[0] : null) ?? LogLevel.Debug;
        StartLogger(startupLevel);
        GameServerProvider = new GameServerProvider();
        _ = GameServerProvider.Start();
        await CommandLoop();
    }

    private static void StartLogger(LogLevel startupLevel)
    {
        var logLevel = startupLevel;
        var layout = new SimpleLayout("[${longdate}][${callsite-filename:includeSourcePath=false}(${callsite-linenumber})][${level:uppercase=true}]: ${message:withexception=true}");
        LogManager.Setup().LoadConfiguration(builder =>
        {
            builder.ForLogger().FilterMinLevel(logLevel)
                .WriteToConsole(layout)
                .WriteToFile("logs/server-${shortdate}.log", layout);
        });
    }

    private static LogLevel? ParseLogLevel(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        try
        {
            return LogLevel.FromString(name.Trim());
        }
        catch (Exception)
        {
            Console.WriteLine($"Unknown log level '{name}'. Valid options: Trace, Debug, Info, Warn, Error, Fatal, Off");
            return null;
        }
    }

    private static void SetLogLevel(LogLevel level)
    {
        _currentLogLevel = level;
        if (LogManager.Configuration != null)
        {
            foreach (var rule in LogManager.Configuration.LoggingRules)
            {
                rule.SetLoggingLevels(level, LogLevel.Fatal);
            }
        }

        LogManager.ReconfigExistingLoggers();
    }

    private static Task CommandLoop()
    {
        Console.WriteLine("Commands: 'status', 'loglevel [level]', 'exit'");
        while (true)
        {
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input)) continue;
            var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

            switch (parts[0].ToLowerInvariant())
            {
                case "status": ShowStatus(); break;

                case "loglevel":
                    if (parts.Length == 1)
                    {
                        Console.WriteLine($"Current log level: {_currentLogLevel.Name}");
                    }
                    else if (ParseLogLevel(parts[1]) is { } newLevel)
                    {
                        SetLogLevel(newLevel);
                    }

                    break;

                case "exit":
                    Logger.Info("Exiting...");
                    Environment.Exit(0);
                    break;
            }
        }
    }

    private static void ShowStatus()
    {
        Console.WriteLine("\n--- ZamboniGameServerProvider ---");
        Console.WriteLine($"Active instances: {GameServerProvider!.Servers.Count}");
        foreach (var server in GameServerProvider.Servers.Values)
        {
            Console.WriteLine($"{server.Guid} {server.GameId} {server.GameProtocolVersion} {server.Topology} {server.Port}");
        }

        Console.WriteLine("-----------------------------\n");
    }
}