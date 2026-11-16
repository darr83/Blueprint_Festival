using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class TroubleTests
{
    /// <summary>A village festival with a gate, a stage, food, a bar, toilets and first aid, and whichever staff are asked for.</summary>
    private static FestivalGame Day(int security = 2, int cleaners = 1, int firstAiders = 2)
    {
        var game = FestivalGame.NewFestival("Larkmoor Festival");
        game.ChooseHeadliner(0);
        Assert.True(game.ConfirmPlan());
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        Assert.True(game.BuildFence(FenceKind.Rope, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y)).Ok);
        Assert.True(game.BuildGate(GateKind.VillageGate, gap.X, y, false).Ok);
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.FarmTrailer, f.X + 50, f.Y + 6, Facing.South).Ok);
        Amenities.Add(game);
        Assert.True(game.HireStaff(StaffKind.TicketStaff, new TilePoint(gap.X + 1, y)).Ok);
        Assert.True(game.BuildStall(StallKind.FirstAidTent, f.X + 30, f.Y + 6, Facing.South).Ok);
        for (var i = 0; i < firstAiders; i++)
        {
            Assert.True(game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 34 + (i * 36), f.Y + 14 + (i * 26))).Ok);
        }

        for (var i = 0; i < security; i++)
        {
            Assert.True(game.HireStaff(StaffKind.Security, new TilePoint(f.X + 56 + (i * 34), f.Y + 20 + (i * 10))).Ok);
        }

        for (var i = 0; i < cleaners; i++)
        {
            Assert.True(game.HireStaff(StaffKind.Cleaner, new TilePoint(f.X + 60 + (i * 20), f.Y + 34)).Ok);
        }

        game.StartDay();
        game.OpenGates();
        return game;
    }

    private static void RunUntil(FestivalGame game, double hour)
    {
        var when = game.Day.AddHours(hour);
        while (game.Now < when && game.Phase == Phase.Day)
        {
            game.Clock.Step();
        }
    }

    private static void RunMinutes(FestivalGame game, double minutes)
    {
        var until = game.Now + TimeSpan.FromMinutes(minutes);
        while (game.Now < until && game.Phase == Phase.Day)
        {
            game.Clock.Step();
        }
    }

    /// <summary>Somewhere in the open with people about: the middle of whoever is standing in the field.</summary>
    private static (double X, double Y) Busy(FestivalGame game)
    {
        var about = game.Crowd.Parties.Where(game.Crowd.CouldFight).ToList();
        Assert.True(about.Count >= 2, "Nobody about to get into bother.");
        return (about.Average(p => p.X), about.Average(p => p.Y));
    }

    [Fact]
    public void Drink_goes_from_merry_to_tipsy_to_drunk_and_makes_people_unsteady()
    {
        Assert.Equal(Tipple.Sober, Crowd.LevelOf(0.5));
        Assert.Equal(Tipple.Merry, Crowd.LevelOf(Crowd.MerryAt));
        Assert.Equal(Tipple.Tipsy, Crowd.LevelOf(Crowd.TipsyAt));
        Assert.Equal(Tipple.Drunk, Crowd.LevelOf(Crowd.DrunkAt));
        Assert.Equal(0, Crowd.WobbleOf(Crowd.MerryAt));
        Assert.InRange(Crowd.WobbleOf(Crowd.TipsyAt), 0.1, 0.5);
        Assert.InRange(Crowd.WobbleOf(Crowd.DrunkAt), 0.5, 1);
        Assert.Equal(1, Crowd.WobbleOf(8));
        Assert.True(Catalogue.SellsAlcohol(StallKind.Bar) && Catalogue.SellsAlcohol(StallKind.Cocktails));
        Assert.False(Catalogue.SellsAlcohol(StallKind.Coffee));
        Assert.Contains(Catalogue.MenuOf(StallKind.Bar), i => Catalogue.IsAlcohol(StallKind.Bar, i));
        Assert.Contains(Catalogue.MenuOf(StallKind.Bar), i => !Catalogue.IsAlcohol(StallKind.Bar, i));
    }

    [Fact]
    public void People_who_buy_at_the_bar_have_had_a_drink_and_it_wears_off_at_one_an_hour()
    {
        var game = Day();
        RunUntil(game, 19);
        var drinkers = game.Crowd.Parties.Where(p => p.Drink > 0).ToList();
        Assert.NotEmpty(drinkers);
        Assert.DoesNotContain(drinkers, p => p.Members.All(m => m.Look.Age is AgeGroup.Child or AgeGroup.Teenager));
        var (merry, tipsy, drunk) = game.Drinkers;
        Assert.True(merry + tipsy + drunk > 0, "Nobody is even merry by seven in the evening.");

        // Someone standing about, given six drinks, is staggering now and merely tipsy three hours on (if they keep off it).
        var party = game.Crowd.Parties.First(p => game.Crowd.CouldFight(p) && Crowd.Thirstiness(p) == 0);
        game.Crowd.Pour(party, 6 - party.Drink);
        Assert.Contains(game.Crowd.People, p => p.Id == party.Members[0].Id && p.Wobble > 0.9);
        var before = party.Drink;
        RunMinutes(game, 60);
        Assert.True(party.Drink < before - 0.9, $"An hour on they had still had {party.Drink:0.00}.");
    }

    [Fact]
    public void A_bar_told_not_to_serve_drunk_people_refuses_them_and_fewer_end_up_drunk()
    {
        static (int DrunkMinutes, double Worst, int Refused) Evening(bool refuse)
        {
            var game = GrowthTests.Built(1500);
            for (var i = 0; i < game.Site.Stalls.Count; i++)
            {
                Assert.Equal(game.Site.Stalls[i].Kind == StallKind.Bar, game.SetNoDrunks(i, refuse));
            }

            Assert.All(game.Site.Stalls.Where(s => s.Kind == StallKind.Bar), s => Assert.Equal(refuse, s.NoDrunks));
            game.StartDay();
            game.OpenGates();
            var minutes = 0;
            var worst = 0.0;
            while (game.Phase == Phase.Day && game.Now < game.Day.AddHours(22.5))
            {
                game.Clock.Step();
                if (game.Clock.TickCount % 60 == 0)
                {
                    minutes += game.Drinkers.Drunk;
                    worst = Math.Max(worst, game.Crowd.Parties.Max(p => p.Drink));
                }
            }

            return (minutes, worst, game.DrinksRefused);
        }

        var open = Evening(refuse: false);
        var strict = Evening(refuse: true);
        Assert.Equal(0, open.Refused);
        Assert.True(open.DrunkMinutes >= 600, $"Only {open.DrunkMinutes} minutes of anyone being drunk with every bar serving everyone.");
        Assert.True(strict.Refused > 0, "Nobody was refused.");

        // Nobody gets more than one round past drunk when the bars stop serving them, and people are drunk for less of the day.
        Assert.True(strict.Worst < Crowd.HadEnoughAt + 2.3, $"Someone had {strict.Worst:0.0} drinks in them with the bars refusing.");
        Assert.True(open.Worst > strict.Worst, $"worst {open.Worst:0.0} serving everyone, {strict.Worst:0.0} refusing");
        Assert.True(strict.DrunkMinutes < open.DrunkMinutes * 0.7, $"{strict.DrunkMinutes} drunk minutes with the bars refusing, {open.DrunkMinutes} without (refused {strict.Refused}, worst {strict.Worst:0.0} against {open.Worst:0.0}).");
    }

    [Fact]
    public void Security_break_up_a_fight_and_throw_both_out()
    {
        var game = Day();
        RunUntil(game, 15);
        var (x, y) = Busy(game);
        Assert.True(game.StartFightAt(x, y));
        Assert.Equal(1, game.Fights);
        var fight = Assert.Single(game.Incidents, i => i.Kind == IncidentKind.Fight);
        var one = game.Crowd.PartyById(fight.Party)!;
        var other = game.Crowd.PartyById(fight.Other)!;
        Assert.True(one.Fighting && other.Fighting);
        Assert.Equal(2, game.Crowd.People.Count(p => p.Fighting));
        Assert.Contains(game.DayLog, n => n.Kind == DayNoteKind.Trouble && n.What.StartsWith("A fight") && n.Tone == DayTone.Open);

        // Nobody else is left standing within arm's reach of it.
        Assert.DoesNotContain(game.Crowd.Parties, p => p != one && p != other && p.State == PartyState.Standing
            && Math.Sqrt(((p.X - fight.X) * (p.X - fight.X)) + ((p.Y - fight.Y) * (p.Y - fight.Y))) < 2.5);

        var sawSeeingOut = false;
        var guard = 0;
        while (game.Staff.Any(s => s.Kind == StaffKind.Security && s.State != StaffState.AtPost && !s.IsFree) || game.Incidents.Any(i => i.Kind == IncidentKind.Fight))
        {
            game.Clock.Step();
            sawSeeingOut |= game.Staff.Any(s => s.State == StaffState.SeeingOut);
            Assert.True(++guard < 3000, "The fight was never dealt with.");
        }

        Assert.True(sawSeeingOut, "Nobody was walked to the gate.");
        Assert.Equal(2, game.ThrownOut);
        Assert.Equal(0, game.FightInjuries);
        Assert.False(one.Fighting || other.Fighting);
        Assert.All(new[] { one, other }, p => Assert.True(p.State is PartyState.Leaving or PartyState.Gone, $"{p.State}"));
        Assert.Contains(game.DayLog, n => n.Outcome == "Broken up: both thrown out" && n.Tone == DayTone.Good);
        var report = DayReport.Of(game);
        Assert.Equal(1, report.Fights);
        Assert.Equal(2, report.ThrownOut);
    }

    [Fact]
    public void A_fight_nobody_stops_ends_with_someone_hurt()
    {
        var game = Day(security: 0);
        RunUntil(game, 15);
        var (x, y) = Busy(game);
        var hurtBefore = game.DayLog.Count(n => n.Kind == DayNoteKind.Hurt);
        Assert.True(game.StartFightAt(x, y));
        var fight = Assert.Single(game.Incidents, i => i.Kind == IncidentKind.Fight);
        var near = game.Crowd.Parties.Where(p => p.IsOnSite && !p.Fighting && Math.Abs(p.X - fight.X) < 6 && Math.Abs(p.Y - fight.Y) < 6).ToList();
        var moodBefore = near.Sum(p => p.Mood);

        RunMinutes(game, FestivalGame.FightGraceMinutes - 1);
        Assert.Equal(0, game.FightInjuries);
        RunMinutes(game, 40);
        Assert.Equal(1, game.FightInjuries);
        Assert.Equal(0, game.ThrownOut);
        Assert.DoesNotContain(game.Incidents, i => i.Kind == IncidentKind.Fight);
        Assert.DoesNotContain(game.Crowd.Parties, p => p.Fighting);
        Assert.Contains(game.DayLog, n => n.Outcome == "Nobody stopped it: someone was hurt" && n.Tone == DayTone.Bad);
        Assert.Equal(hurtBefore + 1, game.DayLog.Count(n => n.Kind == DayNoteKind.Hurt));
        Assert.Contains(game.DayLog, n => n.What.StartsWith("Hurt in a fight"));
        Assert.Equal(1, DayReport.Of(game).HurtInFights);
        if (near.Count > 0)
        {
            Assert.True(near.Sum(p => p.Mood) < moodBefore, "Nobody near minded.");
        }
    }

    [Fact]
    public void Sick_stays_on_the_ground_putting_people_off_until_a_cleaner_mops_it_up()
    {
        var game = Day(cleaners: 0);
        RunUntil(game, 15);
        var (x, y) = Busy(game);
        Assert.True(game.BeSickAt(x, y));
        Assert.Equal(1, game.TimesSick);
        var patch = Assert.Single(game.Sick);
        Assert.Single(game.Crowd.People, p => p.BeingSick);
        Assert.Contains(game.DayLog, n => n.Kind == DayNoteKind.Sick && n.Outcome == "Waiting for a cleaner");
        Assert.Contains(DayReport.Of(game).WorthALook, w => w.Contains("no cleaner"));

        // They straighten up again after a minute or two, but the mess is still there an hour later.
        RunMinutes(game, 5);
        Assert.DoesNotContain(game.Crowd.People, p => p.BeingSick);
        RunMinutes(game, 60);
        Assert.Single(game.Sick);
        Assert.Equal(0, game.SickMopped);

        // A cleaner hired now goes straight to it with a mop.
        var f = game.Field.Field;
        var tile = FieldMap.TileAt(patch.X, patch.Y);
        var post = Enumerable.Range(4, 12).Select(d => new TilePoint(tile.X + d, tile.Y)).First(game.Field.IsWalkableForStaff);
        Assert.True(game.HireStaff(StaffKind.Cleaner, post).Ok);
        var sawMopping = false;
        var guard = 0;
        while (game.Sick.Count > 0)
        {
            game.Clock.Step();
            sawMopping |= game.Staff.Any(s => s.State == StaffState.Mopping);
            Assert.True(++guard < 3000, "Nobody mopped it up.");
        }

        Assert.True(sawMopping);
        Assert.Equal(1, game.SickMopped);
        Assert.Contains(game.DayLog, n => n.Kind == DayNoteKind.Sick && n.Outcome == "Mopped up" && n.Tone == DayTone.Good);
        Assert.DoesNotContain(DayReport.Of(game).WorthALook, w => w.Contains("sick"));
        _ = f;
    }

    [Fact]
    public void People_standing_by_sick_lose_heart_and_people_elsewhere_do_not()
    {
        var game = Day(cleaners: 0);
        RunUntil(game, 15);
        var twin = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        var (x, y) = Busy(game);
        Assert.True(game.BeSickAt(x, y));
        var patch = game.Sick[0];
        foreach (var g in new[] { game, twin })
        {
            RunMinutes(g, 1);
        }

        var near = game.Crowd.Parties.Where(p => p.IsOnSite && Math.Sqrt(((p.X - patch.X) * (p.X - patch.X)) + ((p.Y - patch.Y) * (p.Y - patch.Y))) < FestivalGame.SickReach - 0.5).Select(p => p.Id).ToList();
        Assert.NotEmpty(near);
        Assert.All(near, id => Assert.True(game.Crowd.PartyById(id)!.Mood < twin.Crowd.PartyById(id)!.Mood, $"Party {id} did not mind."));
    }

    [Fact]
    public void A_fight_and_sick_on_the_ground_are_saved_and_carry_on_the_same_after_loading()
    {
        var game = Day();
        RunUntil(game, 16);
        var (x, y) = Busy(game);
        Assert.True(game.BeSickAt(x, y));
        Assert.True(game.StartFightAt(x + 6, y + 4));
        foreach (var party in game.Crowd.Parties.Where(game.Crowd.CouldFight).Take(12))
        {
            game.Crowd.Pour(party, 5);
        }

        RunMinutes(game, 0.5);
        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal(game.Sick, loaded.Sick);
        Assert.Equal(game.Fights, loaded.Fights);
        Assert.Equal(game.Crowd.People.Count(p => p.Fighting), loaded.Crowd.People.Count(p => p.Fighting));
        Assert.Equal(game.Crowd.People.Count(p => p.BeingSick), loaded.Crowd.People.Count(p => p.BeingSick));
        Assert.Equal(game.Drinkers, loaded.Drinkers);

        foreach (var g in new[] { game, loaded })
        {
            var guard = 0;
            while (g.Phase == Phase.Day)
            {
                g.Clock.Step();
                Assert.True(++guard < 70000);
            }
        }

        Assert.Equal((game.Fights, game.ThrownOut, game.FightInjuries, game.TimesSick, game.SickMopped, game.PassedOut), (loaded.Fights, loaded.ThrownOut, loaded.FightInjuries, loaded.TimesSick, loaded.SickMopped, loaded.PassedOut));
        Assert.Equal(game.Crowd.Happiness, loaded.Crowd.Happiness);
        Assert.Equal(game.DayLog.Select(n => (n.What, n.Outcome)), loaded.DayLog.Select(n => (n.What, n.Outcome)));
        Assert.DoesNotContain(game.Crowd.Parties, p => p.Fighting);
    }

    [Fact]
    public void A_big_festival_has_a_few_fights_and_some_sick_but_not_a_riot()
    {
        var game = GrowthTests.Built(1500);
        var f = game.Field.Field;
        Assert.True(game.HireStaff(StaffKind.Cleaner, new TilePoint(f.X + 60, f.Y + 80)).Ok);
        Assert.True(game.HireStaff(StaffKind.Cleaner, new TilePoint(f.X + 140, f.Y + 90)).Ok);
        game.StartDay();
        game.OpenGates();
        var guard = 0;
        while (game.Phase == Phase.Day)
        {
            game.Clock.Step();
            Assert.True(++guard < 70000);
        }

        Assert.InRange(game.Fights, 1, 12);
        Assert.InRange(game.TimesSick, 2, 25);
        Assert.InRange(game.PassedOut, 0, 10);
        Assert.Equal(game.Fights * 2, game.ThrownOut + (game.FightInjuries * 2));
        Assert.Equal(game.TimesSick, game.SickMopped + game.Sick.Count);
        Assert.Empty(game.Incidents);
        Assert.DoesNotContain(game.Crowd.Parties, p => p.Fighting);
    }
}
