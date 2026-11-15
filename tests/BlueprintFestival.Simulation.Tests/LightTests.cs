using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>Path lights, floodlights and fairy lights on poles, and what the dark does to people.</summary>
public class LightTests
{
    [Fact]
    public void A_path_light_and_a_floodlight_go_up_on_the_grass_and_are_paid_for()
    {
        var game = new FestivalGame(seed: 4);
        var f = game.Field.Field;
        var money = game.Money;
        var post = new TilePoint(f.X + 20, f.Y + 20);
        var tower = new TilePoint(f.X + 60, f.Y + 40);

        Assert.Equal(8, game.CheckLight(LightKind.PathLight, post).Cost);
        Assert.True(game.BuildLight(LightKind.PathLight, post).Ok);
        Assert.True(game.BuildLight(LightKind.Floodlight, tower).Ok);
        Assert.Equal(money - 98, game.Money);
        Assert.Equal(98, game.Spending.Single(l => l.What == FestivalGame.LightsLine).Amount);
        Assert.Equal(2, game.Site.Lights.Count);

        // People step past a post but have to walk round a tower.
        Assert.True(game.Field.IsWalkable(post));
        Assert.False(game.Field.IsWalkable(tower));

        // Nothing else can be built on either, and a light cannot go on top of something or off the field.
        Assert.False(game.CheckLight(LightKind.PathLight, post).Ok);
        Assert.False(game.CheckFence(FenceKind.Rope, new TilePoint(post.X - 2, post.Y), new TilePoint(post.X + 2, post.Y)).Ok);
        Assert.False(game.CheckStall(StallKind.Bar, tower.X - 2, tower.Y - 2, Facing.South).Ok);
        Assert.False(game.CheckLight(LightKind.Floodlight, new TilePoint(0, 0)).Ok);
        Assert.False(game.CheckLight(LightKind.FairyLights, new TilePoint(f.X + 5, f.Y + 5)).Ok);

        // Taking one down gives the money back.
        Assert.Equal(90, game.Remove(tower));
        Assert.Equal(money - 8, game.Money);
        Assert.True(game.Field.IsWalkable(tower));
        Assert.Single(game.Site.Lights);
    }

    [Fact]
    public void Dragging_fairy_lights_puts_poles_in_along_the_way()
    {
        var game = new FestivalGame(seed: 4);
        var f = game.Field.Field;
        var money = game.Money;
        var from = new TilePoint(f.X + 20, f.Y + 20);
        var to = new TilePoint(f.X + 50, f.Y + 20);

        // 15 m at £3 a metre, with a pole every 5 m.
        var check = game.CheckFairyLights(from, to);
        Assert.True(check.Ok);
        Assert.Equal(45, check.Cost);
        Assert.True(game.BuildFairyLights(from, to).Ok);
        Assert.Equal(money - 45, game.Money);
        var fairy = Assert.Single(game.Site.FairyLights);
        Assert.Equal(new[] { from, new TilePoint(f.X + 30, f.Y + 20), new TilePoint(f.X + 40, f.Y + 20), to }, fairy.Poles());
        Assert.All(fairy.Poles(), p => Assert.True(game.Site.IsFairyPole(p)));

        // People walk under the string and past the poles; nothing else is built on a pole.
        Assert.All(fairy.Poles(), p => Assert.True(game.Field.IsWalkable(p)));
        Assert.False(game.CheckLight(LightKind.PathLight, to).Ok);
        Assert.True(game.CheckLight(LightKind.PathLight, new TilePoint(f.X + 35, f.Y + 20)).Ok);

        // Too short, the same again, or a pole on top of something: refused.
        Assert.False(game.CheckFairyLights(new TilePoint(f.X + 5, f.Y + 5), new TilePoint(f.X + 7, f.Y + 5)).Ok);
        Assert.False(game.CheckFairyLights(to, from).Ok);
        Assert.True(game.BuildStall(StallKind.Fries, f.X + 60, f.Y + 40, Facing.South).Ok);
        Assert.False(game.CheckFairyLights(new TilePoint(f.X + 40, f.Y + 42), new TilePoint(f.X + 62, f.Y + 42)).Ok);

        // But the string can cross over a stall, as long as its poles stand clear.
        Assert.True(game.CheckFairyLights(new TilePoint(f.X + 58, f.Y + 43), new TilePoint(f.X + 68, f.Y + 43)).Ok);
    }

    [Fact]
    public void A_drag_from_beside_a_pole_carries_on_from_it_at_any_angle()
    {
        var game = new FestivalGame(seed: 4);
        var f = game.Field.Field;
        var from = new TilePoint(f.X + 20, f.Y + 20);
        var corner = new TilePoint(f.X + 40, f.Y + 20);
        Assert.True(game.BuildFairyLights(from, corner).Ok);

        // Starting one tile off the end pole ties on to that pole, and the new string runs off on the slant.
        var end = new TilePoint(f.X + 52, f.Y + 36);
        Assert.True(game.BuildFairyLights(new TilePoint(corner.X + 1, corner.Y + 1), end).Ok);
        Assert.Equal(2, game.Site.FairyLights.Count);
        Assert.Equal(corner, game.Site.FairyLights[1].From);
        Assert.Equal(10, game.Site.FairyLights[1].Metres, 3);
        Assert.Equal(30, Lighting.FairyCost(10));

        // Five poles in all: the corner pole is shared.
        Assert.Equal(5, game.Site.FairyLights.SelectMany(s => s.Poles()).Distinct().Count());

        // Taking down at the shared pole takes both strings; at an end pole, just the one.
        Assert.Equal(60, game.Site.LightRefundAt(corner));
        Assert.Equal(30, game.Site.LightRefundAt(from));
        var money = game.Money;
        Assert.Equal(30, game.Remove(end));
        Assert.Equal(money + 30, game.Money);
        Assert.Single(game.Site.FairyLights);
        Assert.False(game.Site.IsFairyPole(end));
        Assert.True(game.Site.IsFairyPole(corner));
    }

    [Fact]
    public void Lights_light_the_ground_round_them_and_fade_at_the_edge()
    {
        var game = new FestivalGame(seed: 4);
        var f = game.Field.Field;
        var site = game.Site;
        var post = new TilePoint(f.X + 20, f.Y + 60);
        Assert.Equal(0, site.LitAt(post));
        Assert.True(game.BuildLight(LightKind.PathLight, post).Ok);

        // A path light reaches 5 m: full to 2.5 m, fading after, nothing past 5 m.
        Assert.Equal(1, site.LitAt(post));
        Assert.Equal(1, site.LitAt(new TilePoint(post.X + 4, post.Y)));
        Assert.InRange(site.LitAt(new TilePoint(post.X + 8, post.Y)), 0.2, 0.6);
        Assert.Equal(0, site.LitAt(new TilePoint(post.X + 11, post.Y)));

        // A floodlight reaches 18 m.
        var tower = new TilePoint(f.X + 80, f.Y + 60);
        Assert.True(game.BuildLight(LightKind.Floodlight, tower).Ok);
        Assert.Equal(1, site.LitAt(new TilePoint(tower.X + 16, tower.Y)));
        Assert.True(site.LitAt(new TilePoint(tower.X, tower.Y + 30)) > 0);
        Assert.Equal(0, site.LitAt(new TilePoint(tower.X + 37, tower.Y)));

        // Fairy lights light a strip 4 m either side, all the way along.
        var (a, b) = (new TilePoint(f.X + 20, f.Y + 20), new TilePoint(f.X + 60, f.Y + 20));
        Assert.True(game.BuildFairyLights(a, b).Ok);
        Assert.Equal(1, site.LitAt(new TilePoint(f.X + 45, f.Y + 22)));
        Assert.True(site.LitAt(new TilePoint(f.X + 45, f.Y + 26)) is > 0 and < 1);
        Assert.Equal(0, site.LitAt(new TilePoint(f.X + 45, f.Y + 29)));

        // A stall's own lamps light a little ground round it; taking a light down puts its patch back in the dark.
        Assert.True(game.BuildStall(StallKind.Burgers, f.X + 100, f.Y + 20, Facing.South).Ok);
        Assert.True(site.LitAt(new TilePoint(f.X + 106, f.Y + 27)) > 0.9);
        game.Remove(post);
        Assert.Equal(0, site.LitAt(post));
    }

    [Fact]
    public void Darkness_is_nothing_by_day_and_only_where_there_is_no_light_at_night()
    {
        var game = new FestivalGame(seed: 4);
        var f = game.Field.Field;
        var post = new TilePoint(f.X + 20, f.Y + 60);
        Assert.True(game.BuildLight(LightKind.PathLight, post).Ok);
        var (litX, litY) = ((post.X + 0.5) * FieldMap.TileMetres, (post.Y + 0.5) * FieldMap.TileMetres);
        var (darkX, darkY) = (litX + 20, litY);

        // Eight in the morning: no darkness anywhere.
        Assert.Equal(0, game.DarknessAt(darkX, darkY));
        Assert.Equal(0, game.InTheDark);

        while (game.Now.TimeOfDay < TimeSpan.FromHours(22.5))
        {
            game.Clock.Step();
        }

        Assert.Equal(1, game.DarknessAt(darkX, darkY), 3);
        Assert.Equal(0, game.DarknessAt(litX, litY), 3);
    }

    /// <summary>The same village festival day run up to a time in the evening, with or without floodlights all over the field.</summary>
    private static FestivalGame Evening(bool lit, double untilHour)
    {
        var game = FestivalGame.NewFestival("Lamplight Fair");
        // A small name tops the bill, so the day is not decided by a crush at the front.
        game.ChooseHeadliner(game.Headliners.ToList().FindIndex(h => h.Stars == game.Headliners.Min(o => o.Stars)));
        Assert.True(game.ConfirmPlan());
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        Assert.True(game.BuildFence(FenceKind.Rope, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y)).Ok);
        Assert.True(game.BuildGate(GateKind.VillageGate, gap.X, y, false).Ok);
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.Bandstand, f.X + 50, f.Y + 6, Facing.South).Ok);
        Amenities.Add(game);
        Assert.True(game.HireStaff(StaffKind.TicketStaff, new TilePoint(gap.X + 1, y)).Ok);
        // First aid on hand, so the evening is not decided by someone left lying ill in the crowd.
        Assert.True(game.BuildStall(StallKind.FirstAidTent, f.X + 20, f.Y + 6, Facing.South).Ok);
        Assert.True(game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 30, f.Y + 30)).Ok);
        Assert.True(game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 70, f.Y + 30)).Ok);
        if (lit)
        {
            // Nine floodlights in a grid: nowhere on the field is more than 13 m from one.
            foreach (var x in new[] { 20, 60, 100 })
            {
                foreach (var row in new[] { 14, 40, 66 })
                {
                    var at = new TilePoint(f.X + x, f.Y + row);
                    // Lent for the test, so the money is the same both times.
                    for (var tries = 0; tries < 20 && !game.Site.AddLight(LightKind.Floodlight, at).Ok; tries++)
                    {
                        at = new TilePoint(at.X + 1, at.Y);
                    }
                }
            }

            Assert.Equal(9, game.Site.Lights.Count);
        }

        game.OpenGates();
        while (game.Phase == Phase.Day && game.Now.TimeOfDay < TimeSpan.FromHours(untilHour))
        {
            game.Clock.Step();
        }

        return game;
    }

    [Fact]
    public void After_dark_people_are_happier_where_it_is_lit()
    {
        var dark = Evening(false, 22.5);
        var lit = Evening(true, 22.5);

        // In the unlit field the dark has been getting people down since dusk.
        double Mood(FestivalGame g) => g.Crowd.Parties.Where(p => p.State != PartyState.Expected).Sum(p => p.Mood * p.Members.Count);
        Assert.True(Mood(lit) > Mood(dark), $"lit {Mood(lit):0.00}, dark {Mood(dark):0.00}");

        // Nobody on the lit field is in the dark; on the unlit one everyone not watching the act is.
        Assert.True(dark.InTheDark >= 10, $"{dark.InTheDark} in the dark");
        Assert.Equal(0, lit.InTheDark);
    }

    [Fact]
    public void People_about_on_an_unlit_field_at_night_count_as_in_the_dark()
    {
        var crowd = new Crowd(FieldMap.Village(), 3, GameClock.DefaultStart.Date)
        {
            Darkness = (_, _) => 1,
        };
        Assert.True(Crowd.DarkPerHour > 0);
        Assert.NotNull(crowd.Darkness);

        // Watching an act, nobody minds; strolling about or queuing, they do.
        var game = Evening(false, 21.5);
        var about = game.Crowd.Parties.Where(Crowd.MindsTheDark).ToList();
        Assert.All(game.Crowd.Parties.Where(p => p.State == PartyState.Watching), p => Assert.False(Crowd.MindsTheDark(p)));
        if (about.Count > 0)
        {
            Assert.True(game.InTheDark > 0);
        }
    }

    [Fact]
    public void Lights_are_saved_and_come_back()
    {
        var game = new FestivalGame(seed: 4);
        var f = game.Field.Field;
        Assert.True(game.BuildLight(LightKind.Floodlight, new TilePoint(f.X + 60, f.Y + 40)).Ok);
        Assert.True(game.BuildLight(LightKind.PathLight, new TilePoint(f.X + 10, f.Y + 10)).Ok);
        Assert.True(game.BuildFairyLights(new TilePoint(f.X + 20, f.Y + 20), new TilePoint(f.X + 40, f.Y + 20)).Ok);
        Assert.True(game.BuildFairyLights(new TilePoint(f.X + 40, f.Y + 20), new TilePoint(f.X + 40, f.Y + 44)).Ok);

        var json = System.Text.Json.JsonSerializer.Serialize(game.Save());
        var loaded = FestivalGame.Load(System.Text.Json.JsonSerializer.Deserialize<SaveData>(json)!);
        Assert.Equal(game.Site.Lights, loaded.Site.Lights);
        Assert.Equal(game.Site.FairyLights, loaded.Site.FairyLights);
        Assert.False(loaded.Field.IsWalkable(new TilePoint(f.X + 60, f.Y + 40)));
        Assert.Equal(1, loaded.Site.LitAt(new TilePoint(f.X + 40, f.Y + 30)));
        Assert.NotNull(loaded.Crowd.Darkness);

        // An old save with no lights in it still loads.
        var old = game.Save() with { Site = game.Site.Save() with { Lights = null, FairyLights = null } };
        Assert.Empty(FestivalGame.Load(old).Site.Lights);
    }
}
