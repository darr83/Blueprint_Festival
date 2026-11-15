using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>Firework shows: the mixer's rules, the launch pad and its safe zone, and what a show does to the crowd.</summary>
[Collection(BigDays.Name)]
public class FireworkTests
{
    [Fact]
    public void There_are_eight_kinds_in_three_rows_and_the_big_ones_need_the_big_pad()
    {
        Assert.Equal(8, Fireworks.All.Count);
        Assert.All(Enum.GetValues<FireworkHeight>(), h => Assert.Contains(Fireworks.All, f => f.Height == h));
        Assert.Equal(new[] { FireworkKind.PeonyShell, FireworkKind.GoldWillow, FireworkKind.FinaleBarrage }, Fireworks.All.Where(f => f.BigPadOnly).Select(f => f.Kind));
        Assert.Equal(3, Fireworks.AtOnce(bigPad: false));
        Assert.Equal(5, Fireworks.AtOnce(bigPad: true));
    }

    [Fact]
    public void The_example_shows_get_five_stars_from_the_pad_they_are_for()
    {
        Assert.Equal(5, Fireworks.Rate(Fireworks.Example(), bigPad: true).Stars);
        Assert.Equal(5, Fireworks.Rate(Fireworks.SmallExample(), bigPad: false).Stars);
        Assert.Equal(5, Fireworks.Rate(Fireworks.SmallExample(), bigPad: true).Stars);

        // The big show on a small pad: its big shells stay in the box, and too much goes off at once.
        var small = Fireworks.Rate(Fireworks.Example(), bigPad: false);
        Assert.True(small.Stars <= 3);
        Assert.Contains(small.Notes, n => !n.Good && n.Text.Contains("big launch pad"));
        Assert.Equal(0.1, Fireworks.Rate(Fireworks.Example(), bigPad: true).Mood, 6);
    }

    [Fact]
    public void A_samey_show_with_a_gap_and_no_finish_loses_stars()
    {
        Assert.Equal(0, Fireworks.Rate(Array.Empty<FireworkCue>(), true).Stars);

        // Two rockets at the start and nothing else: one kind, a long gap, no finale. Never fewer than one star.
        var dull = new[] { new FireworkCue(FireworkKind.Rocket, 0), new FireworkCue(FireworkKind.Rocket, 2) };
        var rating = Fireworks.Rate(dull, true);
        Assert.Equal(1, rating.Stars);
        Assert.Equal(26, Fireworks.Gap(dull));
        Assert.Contains(rating.Notes, n => n.Text.Contains("samey"));
        Assert.Contains(rating.Notes, n => n.Text.Contains("gap"));
        Assert.Contains(rating.Notes, n => n.Text.Contains("finale"));

        // Four fountains at once is too many for a small pad.
        var busy = Enumerable.Range(0, 4).Select(_ => new FireworkCue(FireworkKind.Fountain, 0)).ToList();
        Assert.Equal(4, Fireworks.TooMany(busy, bigPad: false).Count);
        Assert.Empty(Fireworks.TooMany(busy, bigPad: true));
    }

    [Fact]
    public void Fireworks_go_off_on_the_half_second_and_never_run_past_the_end()
    {
        Assert.Equal(3.5, Fireworks.Clamp(FireworkKind.Rocket, 3.4));
        Assert.Equal(0, Fireworks.Clamp(FireworkKind.Rocket, -5));
        Assert.Equal(28, Fireworks.Clamp(FireworkKind.FinaleBarrage, 40));
        Assert.Equal(28.5, Fireworks.Clamp(FireworkKind.GroundMine, 40));
    }

    [Fact]
    public void A_pad_needs_its_whole_safe_zone_clear_and_its_fence_blocks_the_way_round_it()
    {
        var game = new FestivalGame(seed: 4);
        var f = game.Field.Field;
        var money = game.Money;
        var (x, y) = (f.X + 40, f.Y + 30);
        var pad = new PlacedLaunchPad(LaunchPadKind.Small, x, y, false);
        Assert.Equal(new TileRect(x - 12, y - 12, 30, 30), pad.Zone);
        Assert.Equal(120, pad.FenceCost);

        // Too near the hedge for its safe zone.
        Assert.False(game.CheckLaunchPad(LaunchPadKind.Small, f.X + 2, f.Y + 2).Ok);
        // A stall in the way.
        Assert.True(game.BuildStall(StallKind.Bar, x + 14, y, Facing.South).Ok);
        Assert.Contains("bar", game.CheckLaunchPad(LaunchPadKind.Small, x, y).Problem);
        Assert.True(game.Remove(new TilePoint(x + 15, y + 1)) > 0);
        money = game.Money;

        Assert.True(game.BuildLaunchPad(LaunchPadKind.Small, x, y).Ok);
        Assert.Equal(money - 150, game.Money);
        Assert.False(game.Field.IsWalkable(new TilePoint(x + 2, y + 2)));
        Assert.False(game.CheckLaunchPad(LaunchPadKind.Small, x + 40, y).Ok);

        // Fencing the zone off costs £2 a metre and closes its edge to people.
        var edge = new TilePoint(x - 12, y + 3);
        Assert.True(game.Field.IsWalkable(edge));
        Assert.True(game.FenceSafeZone(true).Ok);
        Assert.Equal(money - 150 - 120, game.Money);
        Assert.False(game.Field.IsWalkable(edge));
        Assert.True(game.Site.LaunchPad!.Fenced);
        Assert.False(game.CheckLight(LightKind.PathLight, edge).Ok);

        // Taking the pad down gives back the pad and the fence.
        Assert.Equal(270, game.Remove(edge));
        Assert.Equal(money, game.Money);
        Assert.Null(game.Site.LaunchPad);
        Assert.True(game.Field.IsWalkable(edge));
    }

    [Fact]
    public void Shows_are_planned_at_quarter_hours_in_the_evening_and_paid_for_by_the_firework()
    {
        var game = new FestivalGame(seed: 4);
        var money = game.Money;
        Assert.Equal(-1, game.AddFireworkShow(19 * 60));
        Assert.Equal(-1, game.AddFireworkShow((21 * 60) + 10));
        Assert.Equal(0, game.AddFireworkShow(22 * 60));
        Assert.Equal(-1, game.AddFireworkShow(22 * 60));
        Assert.Equal(0, game.AddFireworkShow((21 * 60) + 30));
        Assert.Equal(2, game.AddFireworkShow(23 * 60));
        Assert.Equal(0, game.AddFireworkShow(20 * 60));
        Assert.Equal(-1, game.AddFireworkShow((23 * 60) + 15));
        Assert.Null(game.NextShowMinute());

        Assert.True(game.AddFirework(1, FireworkKind.Rocket, 3.3).Ok);
        Assert.True(game.AddFirework(1, FireworkKind.FinaleBarrage, 26).Ok);
        Assert.Equal(money - 135, game.Money);
        Assert.Equal(3.5, game.FireworkShows[1].Cues[0].At);
        Assert.True(game.MoveFirework(1, 0, 10));
        Assert.Equal(10, game.FireworkShows[1].Cues[0].At);
        Assert.True(game.RemoveFirework(1, 0));
        Assert.Equal(money - 120, game.Money);
        Assert.Equal(2, game.MoveFireworkShow(1, (22 * 60) + 45));
        Assert.True(game.RemoveFireworkShow(2));
        Assert.Equal(money, game.Money);
    }

    [Fact]
    public void A_show_cheers_everyone_up_without_a_fence_it_upsets_them_and_with_a_stall_in_the_zone_it_is_called_off()
    {
        // A village festival, run on to just before a show at half past nine.
        var (game, x, y) = Evening("Larkmoor Festival", (21 * 60) + 30, 21.4);

        Assert.True(game.Crowd.OnSite > 20, $"There should be a crowd on site, not {game.Crowd.OnSite}.");
        var json = game.Save().ToJson();
        double Average(FestivalGame g) => g.Crowd.Parties.Where(p => p.IsOnSite).Average(p => p.Mood);

        FestivalGame Run(Action<FestivalGame> setUp, out double before)
        {
            var copy = FestivalGame.Load(SaveData.FromJson(json));
            Assert.Single(copy.FireworkShows);
            Assert.NotNull(copy.Site.LaunchPad);
            setUp(copy);
            before = Average(copy);
            var seen = 0;
            copy.FireworksStarted += (_, _) => seen++;
            while (copy.Now < copy.Day.AddHours(21.5).AddSeconds(2))
            {
                copy.Clock.Step();
            }

            Assert.Equal(1, seen);
            return copy;
        }

        var safe = Run(g => Assert.True(g.FenceSafeZone(true).Ok), out var safeBefore);
        var result = Assert.Single(safe.FireworkResults);
        Assert.Equal(FireworkOutcome.Safe, result.Outcome);
        Assert.Equal(5, result.Stars);
        Assert.True(Average(safe) > safeBefore + 0.05, $"A five-star show in the dark should lift the mood, from {safeBefore:0.000} to {Average(safe):0.000}.");
        Assert.False(safe.UseExampleShow(0).Ok);

        var open = Run(_ => { }, out var openBefore);
        Assert.Equal(FireworkOutcome.NoFence, open.FireworkResults[0].Outcome);
        Assert.True(Average(open) < openBefore - 0.03);

        var blocked = Run(g => Assert.True(g.BuildStall(StallKind.Bar, x - 10, y - 10, Facing.South).Ok, g.BuildStall(StallKind.Bar, x - 10, y - 10, Facing.South).Problem), out var blockedBefore);
        Assert.Equal(FireworkOutcome.CalledOff, blocked.FireworkResults[0].Outcome);
        Assert.Contains("bar", blocked.FireworkResults[0].Why);
        Assert.True(Average(blocked) < blockedBefore - 0.05);
    }

    /// <summary>A planned village festival with a small launch pad and a five-star show, run on to an hour of the evening.</summary>
    private static (FestivalGame Game, int X, int Y) Evening(string name, int showMinute, double untilHour)
    {
        var game = SafetyTests.BuildDay(bigName: false, name);
        var f = game.Field.Field;
        var (x, y) = (f.X + 90, f.Y + 50);
        while (!game.CheckLaunchPad(LaunchPadKind.Small, x, y).Ok)
        {
            Assert.True(--x > f.X + 12, game.CheckLaunchPad(LaunchPadKind.Small, x + 1, y).Problem);
        }

        Assert.True(game.BuildLaunchPad(LaunchPadKind.Small, x, y).Ok);
        Assert.True(game.UseExampleShow(game.AddFireworkShow(showMinute)).Ok);
        game.OpenGates();
        while (game.Now < game.Day.AddHours(untilHour))
        {
            game.Clock.Step();
        }

        return (game, x, y);
    }

    [Fact]
    public void Without_a_fence_people_are_burned_at_some_shows()
    {
        var hurt = 0;
        for (var seed = 1; seed <= 6; seed++)
        {
            var (game, _, _) = Evening($"Fireworks Fair {seed}", 21 * 60, 20.9);
            while (game.FireworkResults.Count == 0)
            {
                game.Clock.Step();
            }

            hurt += game.FireworkInjuries;
            if (game.FireworkInjuries > 0)
            {
                Assert.Contains("burned", game.FireworkResults[0].Why);
            }
        }

        Assert.InRange(hurt, 1, 5);
    }
}
