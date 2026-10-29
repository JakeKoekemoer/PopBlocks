using System.Diagnostics;
using PopBlocks.Engine;
using PopBlocks.Engine.Logic;
using PopBlocks.Engine.Models;
using Xunit;
using Xunit.Abstractions;

namespace PopBlocks.Tests;

/// <summary>
/// Engine timings. Skipped unless POPBLOCKS_BENCH is set (it plays tens of thousands of moves and prints numbers rather than
/// checking them tightly). Run it with <c>POPBLOCKS_BENCH=1 dotnet test --filter EngineBenchmark -v n</c> and record the
/// results in docs/overnight/PERFORMANCE.md. The only hard check is a generous ceiling, so a real regression still fails.
/// </summary>
public class EngineBenchmark
{
    private readonly ITestOutputHelper _out;
    public EngineBenchmark(ITestOutputHelper output) => _out = output;

    private static bool Enabled => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("POPBLOCKS_BENCH"));

    private sealed record Result(int Moves, double MeanMicros, double P99Micros, double MaxMicros, int Games, int OrbsSeen, int Breaks);

    private static Result Play(GameRules rules, int moves, int seed)
    {
        var random = new Random(seed);
        var times = new List<double>(moves);
        int games = 0, orbs = 0, breaks = 0;
        var engine = new GameEngine(new PieceGenerator(new Random(seed)));
        engine.EventRaised += e => { if (e == GameEvent.OrbSpawned) orbs++; };
        engine.WaveResolved += w => breaks += w.Broken.Count;
        engine.Start(GameSetup.ClassicRun(seed, chosen: false) with { Rules = rules });
        games++;

        double tick = Stopwatch.Frequency / 1_000_000.0;
        for (int i = 0; i < moves; i++)
        {
            if (engine.State == GameState.GameOver)
            {
                engine.Start(GameSetup.ClassicRun(seed + games, chosen: false) with { Rules = rules });
                games++;
            }
            // A random slide and turn, then a hard drop; the time is the drop and everything that settles after it.
            for (int s = random.Next(0, 6); s > 0; s--) engine.MoveRight();
            for (int s = random.Next(0, 6); s > 0; s--) engine.MoveLeft();
            if (random.Next(3) == 0) engine.Rotate();

            long start = Stopwatch.GetTimestamp();
            engine.HardDrop();
            for (int guard = 0; guard < 60 && engine.State == GameState.ResolvingBursts; guard++) engine.Tick();
            times.Add((Stopwatch.GetTimestamp() - start) / tick);
        }

        times.Sort();
        return new Result(moves, times.Average(), times[(int)(times.Count * 0.99)], times[^1], games, orbs, breaks);
    }

    [Fact]
    public void Ten_thousand_random_moves_with_the_default_rules_then_a_tall_board_with_cracks_and_orbs()
    {
        if (!Enabled) return;

        Play(GameRules.Default, 500, 1);   // warm up the JIT
        // The old game for comparison: no cracks, no Orbs (the pre-v1.1 settle).
        var baseline = Play(GameRules.Default with { Fracture = false, Orbs = OrbMode.Never }, 10_000, 10);
        var normal = Play(GameRules.Default, 10_000, 11);
        var tall = Play(GameRules.Default with { BoardRows = 24, Fracture = true, Orbs = OrbMode.Always }, 10_000, 12);

        _out.WriteLine($"no cracks/orbs: mean {baseline.MeanMicros:0.0} us, p99 {baseline.P99Micros:0.0} us, max {baseline.MaxMicros:0.0} us over {baseline.Moves} moves in {baseline.Games} games");
        _out.WriteLine($"default rules : mean {normal.MeanMicros:0.0} us, p99 {normal.P99Micros:0.0} us, max {normal.MaxMicros:0.0} us over {normal.Moves} moves in {normal.Games} games, {normal.OrbsSeen} orbs, {normal.Breaks} breaks");
        _out.WriteLine($"24 rows+cracks+orbs: mean {tall.MeanMicros:0.0} us, p99 {tall.P99Micros:0.0} us, max {tall.MaxMicros:0.0} us over {tall.Moves} moves in {tall.Games} games, {tall.OrbsSeen} orbs, {tall.Breaks} breaks");

        // Budget: a settle (cracks + orb + camera) is at most 2 ms. The ceiling here is looser: it is for catching a regression on a loaded machine.
        Assert.True(normal.P99Micros < 10_000, $"default p99 {normal.P99Micros:0} us");
        Assert.True(tall.P99Micros < 10_000, $"tall p99 {tall.P99Micros:0} us");
    }

    [Fact]
    public void Pips_Insight_stays_inside_its_four_milliseconds()
    {
        if (!Enabled) return;

        var random = new Random(5);
        var engine = new GameEngine(new PieceGenerator(new Random(5)));
        engine.Start(GameSetup.ClassicRun(5, chosen: false));
        var times = new List<double>();
        double tick = Stopwatch.Frequency / 1_000_000.0;
        PlacementAdvisor.Best(engine.CreateAdviceRequest()!);   // warm up
        for (int i = 0; i < 600; i++)
        {
            if (engine.State == GameState.GameOver) engine.Start(GameSetup.ClassicRun(5 + i, chosen: false));
            var request = engine.CreateAdviceRequest();
            if (request is not null)
            {
                long start = Stopwatch.GetTimestamp();
                PlacementAdvisor.Best(request);
                times.Add((Stopwatch.GetTimestamp() - start) / tick);
            }
            for (int s = random.Next(0, 6); s > 0; s--) engine.MoveRight();
            engine.HardDrop();
            for (int guard = 0; guard < 60 && engine.State == GameState.ResolvingBursts; guard++) engine.Tick();
        }
        times.Sort();
        _out.WriteLine($"insight : mean {times.Average():0.0} us, p99 {times[(int)(times.Count * 0.99)]:0.0} us, max {times[^1]:0.0} us over {times.Count} pieces");
        Assert.True(times[(int)(times.Count * 0.99)] < 4_000, "the advisor should stay inside 4 ms");
    }

    [Fact]
    public void The_benchmark_does_nothing_unless_asked()
    {
        // (It is a cheap fact so that the default run shows the switch exists.)
        Assert.Equal(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("POPBLOCKS_BENCH")), Enabled);
    }
}
