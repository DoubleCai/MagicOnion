using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using Grpc.Net.Client;
using MagicOnion.Client;

var app = ConsoleApp.Create(args);
app.AddRootCommand(Main);
app.Run();

async Task Main(
    [Option("s")]ScenarioType scenario,
    [Option("u")]string url,
    [Option("w")]int warmup = 10,
    [Option("d")]int duration = 10,
    [Option("t")]int streams = 10,
    [Option("c")]int channels = 10,
    [Option("r")]string? report = null,
    uint rounds = 1,
    [Option("v")]bool verbose = false
)
{
    var config = new ScenarioConfiguration(url, warmup, duration, streams, channels, verbose);

    PrintStartupInformation();

    WriteLog($"Scenario: {scenario}");
    WriteLog($"Url: {config.Url}");
    WriteLog($"Warmup: {config.Warmup} s");
    WriteLog($"Duration: {config.Duration} s");
    WriteLog($"Streams: {config.Streams}");
    WriteLog($"Channels: {config.Channels}");
    WriteLog($"Rounds: {rounds}");

    var resultsByScenario = new Dictionary<ScenarioType, List<PerformanceResult>>();
    var runScenarios = Enum.GetValues<ScenarioType>().Where(x => (scenario == ScenarioType.All) ? x != ScenarioType.All : x == scenario);
    for (var i = 1; i <= rounds; i++)
    {
        WriteLog($"Round: {i}");
        foreach (var scenario2 in runScenarios)
        {
            if (!resultsByScenario.TryGetValue(scenario2, out var results))
            {
                results = new List<PerformanceResult>();
                resultsByScenario[scenario2] = results;
            }
            results.Add(await RunScenarioAsync(scenario2, config));
        }
    }

    if (!string.IsNullOrWhiteSpace(report))
    {
        WriteLog($"Write report to '{report}'");
        using var writer = File.CreateText(report);
        writer.WriteLine($"Created at {DateTime.Now}");
        writer.WriteLine($"========================================");
        PrintStartupInformation(writer);
        writer.WriteLine($"========================================");
        writer.WriteLine($"Scenario: {scenario}");
        writer.WriteLine($"Url     : {config.Url}");
        writer.WriteLine($"Warmup  : {config.Warmup} s");
        writer.WriteLine($"Duration: {config.Duration} s");
        writer.WriteLine($"Streams : {config.Streams}");
        writer.WriteLine($"Channels: {config.Channels}");
        writer.WriteLine($"========================================");
        foreach (var (s, results) in resultsByScenario)
        {
            writer.WriteLine($"Scenario           : {s}");
            foreach (var (result, round) in results.Select((x, i) => (x, i)))
            {
                writer.WriteLine($"Round              : {round}");
                writer.WriteLine($"Requests per Second: {result.RequestsPerSecond:0.000} rps");
                writer.WriteLine($"Duration           : {result.Duration.TotalSeconds} s");
                writer.WriteLine($"Total Requests     : {result.TotalRequests} requests");
                writer.WriteLine($"========================================");
            }
        }

        writer.WriteLine($"Scenario\t{string.Join("\t", Enumerable.Range(1, (int)rounds).Select(x => $"Requests/s ({x})"))}\tRequests/s (Avg)");
        foreach (var (s, results) in resultsByScenario)
        {
            writer.WriteLine($"{s}\t{string.Join("\t", results.Select(x => x.RequestsPerSecond.ToString("0.000")))}\t{results.Average(x => x.RequestsPerSecond):0.000}");
        }
    }
}

async Task<PerformanceResult> RunScenarioAsync(ScenarioType scenario, ScenarioConfiguration config)
{
    Func<IScenario> scenarioFactory = scenario switch
    {
        ScenarioType.Unary => () => new UnaryScenario(),
        ScenarioType.UnaryLargePayload1K => () => new UnaryLargePayload1KScenario(),
        ScenarioType.UnaryLargePayload2K => () => new UnaryLargePayload2KScenario(),
        ScenarioType.UnaryLargePayload4K => () => new UnaryLargePayload4KScenario(),
        ScenarioType.UnaryLargePayload8K => () => new UnaryLargePayload8KScenario(),
        ScenarioType.UnaryLargePayload16K => () => new UnaryLargePayload16KScenario(),
        ScenarioType.UnaryLargePayload32K => () => new UnaryLargePayload32KScenario(),
        ScenarioType.UnaryLargePayload64K => () => new UnaryLargePayload64KScenario(),
        ScenarioType.StreamingHub => () => new StreamingHubScenario(),
        ScenarioType.StreamingHubLargePayload1K => () => new StreamingHubLargePayload1KScenario(),
        ScenarioType.StreamingHubLargePayload2K => () => new StreamingHubLargePayload2KScenario(),
        ScenarioType.StreamingHubLargePayload4K => () => new StreamingHubLargePayload4KScenario(),
        ScenarioType.StreamingHubLargePayload8K => () => new StreamingHubLargePayload8KScenario(),
        ScenarioType.StreamingHubLargePayload16K => () => new StreamingHubLargePayload16KScenario(),
        ScenarioType.StreamingHubLargePayload32K => () => new StreamingHubLargePayload32KScenario(),
        ScenarioType.StreamingHubLargePayload64K => () => new StreamingHubLargePayload64KScenario(),
        _ => throw new Exception($"Unknown Scenario: {scenario}"),
    };

    var ctx = new PerformanceTestRunningContext();
    var cts = new CancellationTokenSource();

    WriteLog($"Starting scenario '{scenario}'...");
    var tasks = new List<Task>();
    for (var i = 0; i < config.Channels; i++)
    {
        var channel = GrpcChannel.ForAddress(config.Url);
        for (var j = 0; j < config.Streams; j++)
        {
            if (config.Verbose) WriteLog($"Channel[{i}] - Stream[{j}]: Run");
            tasks.Add(Task.Run(async () =>
            {
                var scenarioRunner = scenarioFactory();
                await scenarioRunner.PrepareAsync(channel);
                await scenarioRunner.RunAsync(ctx, cts.Token);
            }));
        }
    }
    WriteLog("Warming up...");
    await Task.Delay(TimeSpan.FromSeconds(config.Warmup));
    ctx.Ready();
    WriteLog("Warmup completed");
    
    WriteLog("Running...");
    cts.CancelAfter(TimeSpan.FromSeconds(config.Duration));
    await Task.WhenAll(tasks);
    ctx.Complete();
    WriteLog("Completed.");

    var result = ctx.GetResult();
    WriteLog($"Requests per Second: {result.RequestsPerSecond:0.000} rps");
    WriteLog($"Duration: {result.Duration.TotalSeconds} s");
    WriteLog($"Total Requests: {result.TotalRequests} requests");

    return result;
}

void PrintStartupInformation(TextWriter? writer = null)
{
    writer ??= Console.Out;

    writer.WriteLine($"MagicOnion {typeof(MagicOnionClient).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}");
    writer.WriteLine($"grpc-dotnet {typeof(Grpc.Net.Client.GrpcChannel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}");
    writer.WriteLine();

    writer.WriteLine("Configurations:");
#if RELEASE
    writer.WriteLine($"Build Configuration: Release");
#else
    writer.WriteLine($"Build Configuration: Debug");
#endif
    writer.WriteLine($"{nameof(RuntimeInformation.FrameworkDescription)}: {RuntimeInformation.FrameworkDescription}");
    writer.WriteLine($"{nameof(RuntimeInformation.OSDescription)}: {RuntimeInformation.OSDescription}");
    writer.WriteLine($"{nameof(RuntimeInformation.OSArchitecture)}: {RuntimeInformation.OSArchitecture}");
    writer.WriteLine($"{nameof(RuntimeInformation.ProcessArchitecture)}: {RuntimeInformation.ProcessArchitecture}");
    writer.WriteLine($"{nameof(GCSettings.IsServerGC)}: {GCSettings.IsServerGC}");
    writer.WriteLine($"{nameof(Environment.ProcessorCount)}: {Environment.ProcessorCount}");
    writer.WriteLine($"{nameof(Debugger)}.{nameof(Debugger.IsAttached)}: {Debugger.IsAttached}");
    writer.WriteLine();
}

void WriteLog(string value)
{
    Console.WriteLine($"[{DateTime.Now:s}] {value}");
}

public record ScenarioConfiguration(string Url, int Warmup, int Duration, int Streams, int Channels, bool Verbose);
