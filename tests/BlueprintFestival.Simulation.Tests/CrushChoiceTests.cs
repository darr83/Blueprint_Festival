using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>The four things the player can do when a crush builds, and what each costs.</summary>
public class CrushChoiceTests
{
    /// <summary>Opens the gates and runs the day until the crush warning, or fails if it never comes.</summary>
    private static void RunToTheWarning(FestivalGame game)
    {
        var warned = false;
        game.CrushWarning += (_, _) => warned = true;
        game.OpenGates();
        while (!warned)
        {
            game.Clock.Step();
            Assert.True(game.Phase == Phase.Day, "No crush built all day.");
        }
    }

    private static void RunMinutes(FestivalGame game, int minutes)
    {
        for (var i = 0; i < minutes * 60 && game.Phase == Phase.Day; i++)
        {
            game.Clock.Step();
        }
    }

    private static void RunToTheEnd(FestivalGame game)
    {
        var guard = 0;
        while (game.Phase == Phase.Day)
        {
            game.Clock.Step();
            Assert.True(++guard < 70000, "The day never ended.");
        }
    }

    private static double MoodOf(FestivalGame game, IEnumerable<int> parties) =>
        game.Crowd.Parties.Where(p => parties.Contains(p.Id)).Average(p => p.Mood);

    /// <summary>Hires security at the gate end of the field, well away from the stage. There is money for three.</summary>
    private static void SecurityByTheGate(FestivalGame game, int how)
    {
        var gap = game.Field.Gap;
        for (var i = 0; i < how; i++)
        {
            var hired = game.HireStaff(StaffKind.Security, new TilePoint(gap.X + (i * 2), gap.Y - 6));
            Assert.True(hired.Ok, $"{hired.Problem} (money {game.Money})");
        }
    }

    [Fact]
    public void Stopping_for_an_announcement_moves_the_front_back_and_works_less_each_time()
    {
        var game = SafetyTests.BuildDay(bigName: true);
        var said = new List<int>();
        game.Announced += said.Add;
        RunToTheWarning(game);
        var watching = game.Crowd.Parties.Where(p => p.State == PartyState.Watching).Select(p => p.Id).ToList();
        var before = MoodOf(game, watching);

        Assert.Equal(1, game.AnnouncementHeard);
        Assert.True(game.Choose(CrushChoice.Announce));
        Assert.True(game.ActPaused);
        Assert.True(game.CrushHandled);
        Assert.True(MoodOf(game, watching) < before, "Stopping the music bores the crowd a little.");
        Assert.True(before - MoodOf(game, watching) < 0.05, "But only a little.");
        Assert.Equal(0.67, game.AnnouncementHeard);
        Assert.False(game.Announce(), "The music is already off.");

        RunMinutes(game, 3);
        Assert.True(game.Density.Worst <= DensityMap.Crush, $"The front should have eased, but is {game.Density.Worst:0.0} per m².");
        Assert.Equal(new[] { 0 }, said);

        RunMinutes(game, 2);
        Assert.False(game.ActPaused);
        Assert.True(game.Announce());
        Assert.Equal(0.34, game.AnnouncementHeard);
        Assert.Equal(new[] { 0, 1 }, said);
    }

    [Fact]
    public void Fewer_people_listen_to_each_announcement_and_always_the_same_ones()
    {
        var game = SafetyTests.BuildDay(bigName: true);
        var parties = game.Crowd.Parties.ToList();
        var all = parties.Count(p => Crowd.Listens(p, 1));
        var most = parties.Count(p => Crowd.Listens(p, 0.67));
        var some = parties.Count(p => Crowd.Listens(p, 0.34));

        Assert.Equal(parties.Count, all);
        Assert.InRange(most, parties.Count * 0.55, parties.Count * 0.8);
        Assert.InRange(some, parties.Count * 0.22, parties.Count * 0.46);
        Assert.All(parties.Where(p => Crowd.Listens(p, 0.34)), p => Assert.True(Crowd.Listens(p, 0.67)));
    }

    [Fact]
    public void Security_sent_in_cost_overtime_hold_the_spot_then_go_back_to_their_posts()
    {
        var game = SafetyTests.BuildDay(bigName: true);
        SecurityByTheGate(game, 3);
        RunToTheWarning(game);
        var money = game.Money;
        var spot = game.CrushSpot;

        Assert.Equal(FestivalGame.SecurityCallOut, game.SecurityFree);
        Assert.Equal(3, game.SendSecurity());
        Assert.Equal(money - (3 * FestivalGame.Overtime), game.Money);
        Assert.Equal(1, game.SecuritySent);
        Assert.True(game.CrushHandled);
        Assert.Equal(3, game.Staff.Count(s => s.State == StaffState.Sent));
        Assert.Equal(0, game.SecurityFree);
        Assert.False(game.ActPaused, "The music plays on.");

        var guard = 0;
        while (!game.Staff.Any(s => s.State == StaffState.Holding))
        {
            game.Clock.Step();
            Assert.True(++guard < 60 * 20, "Security never got there.");
        }

        Assert.True(game.Stewarded(spot.X, spot.Y));
        RunMinutes(game, 3);
        Assert.True(game.PulledOut > 0, "Security lift people out of the crush.");

        RunMinutes(game, 12);
        Assert.DoesNotContain(game.Staff, s => s.State is StaffState.Sent or StaffState.Holding);
        RunMinutes(game, 15);
        Assert.All(game.Staff.Where(s => s.Kind == StaffKind.Security), s => Assert.True(s.State is StaffState.AtPost or StaffState.Returning));
        RunToTheEnd(game);
        Assert.False(game.SeriousIncident);
    }

    [Fact]
    public void Security_cannot_be_sent_if_none_are_hired()
    {
        var game = SafetyTests.BuildDay(bigName: true);
        RunToTheWarning(game);
        var money = game.Money;

        Assert.Equal(0, game.SecurityFree);
        Assert.False(game.Choose(CrushChoice.SendSecurity));
        Assert.Equal(money, game.Money);
        Assert.False(game.CrushHandled);
    }

    [Fact]
    public void Cancelling_the_act_clears_the_crush_at_once_but_upsets_people_and_word_gets_round()
    {
        var game = SafetyTests.BuildDay(bigName: true);
        Show.Slot? cancelled = null;
        game.ActCancelled += slot => cancelled = slot;
        RunToTheWarning(game);
        var slot = game.ActAtCrush!;
        var fans = game.Crowd.Parties.Where(p => p.IsOnSite && p.Watch == slot.Index).Select(p => p.Id).ToList();
        var others = game.Crowd.Parties.Where(p => p.IsOnSite && p.Watch != slot.Index).Select(p => p.Id).ToList();
        var fansBefore = MoodOf(game, fans);
        var othersBefore = MoodOf(game, others);

        Assert.True(game.Choose(CrushChoice.CancelAct));
        Assert.Equal(slot.Index, cancelled!.Index);
        Assert.Null(game.Show.Playing(game.Now));
        Assert.True(game.Show.Slots.Single(s => s.Index == slot.Index).CutShort);
        Assert.Equal(1, game.ActsCancelled);
        Assert.True(fansBefore - MoodOf(game, fans) >= FestivalGame.CancelMoodWatching - 0.01, "The act's fans are let down.");
        Assert.True(othersBefore - MoodOf(game, others) >= FestivalGame.CancelMoodEveryone - 0.01, "Everyone else a little.");
        Assert.Contains(game.Crowd.Parties, p => fans.Contains(p.Id) && p.State == PartyState.Leaving);
        Assert.False(game.CancelAct(), "There is nothing left on to cancel.");

        RunMinutes(game, 3);
        Assert.Equal(0, game.Density.RedSquares);
        RunToTheEnd(game);
        Assert.False(game.SeriousIncident);

        game.PayScouts();
        var result = game.HandBack();
        Assert.Equal(1, result.ActsCancelled);
        Assert.Equal(result.Happy - 5, FestivalGame.WordOfMouth(result));
    }

    [Fact]
    public void Leaving_a_crush_alone_keeps_the_music_on_but_anyone_hurt_stops_the_licence_growing()
    {
        var game = SafetyTests.BuildDay(bigName: true, "Otterbourne Live");
        RunToTheWarning(game);

        Assert.True(game.Choose(CrushChoice.LeaveIt));
        Assert.True(game.CrushLeftAlone);
        Assert.False(game.CrushHandled);
        Assert.Equal(1, game.TimesLeftAlone);
        Assert.False(game.LeaveIt(), "It is already being left alone.");
        Assert.True(game.MusicOn);

        RunToTheEnd(game);
        Assert.True(game.CrushInjuries > 0);
        Assert.Equal(game.CrushInjuries, game.HurtLeftAlone);

        game.PayScouts();
        var result = game.HandBack();
        Assert.Equal(result.LicenceNow, result.LicenceNext);
        Assert.Equal(game.HurtLeftAlone, result.HurtLeftAlone);
        Assert.Equal(Math.Max(0, result.Happy - 10), FestivalGame.WordOfMouth(result));
    }

    [Fact]
    public void Choices_made_during_a_crush_are_saved_and_carry_on_after_loading()
    {
        var game = SafetyTests.BuildDay(bigName: true);
        SecurityByTheGate(game, 3);
        RunToTheWarning(game);
        Assert.Equal(3, game.SendSecurity());
        RunMinutes(game, 1);
        Assert.True(game.CancelAct());

        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal(1, loaded.SecuritySent);
        Assert.Equal(1, loaded.ActsCancelled);
        Assert.True(loaded.CrushHandled);
        Assert.Null(loaded.Show.Playing(loaded.Now));
        Assert.Equal(
            game.Staff.Select(s => (s.State, s.HoldX, s.HoldY, s.HoldLeft)),
            loaded.Staff.Select(s => (s.State, s.HoldX, s.HoldY, s.HoldLeft)));

        RunToTheEnd(game);
        RunToTheEnd(loaded);
        Assert.Equal(game.CrushInjuries, loaded.CrushInjuries);
        Assert.Equal(game.PulledOut, loaded.PulledOut);
        Assert.Equal(game.Crowd.Happiness, loaded.Crowd.Happiness);
    }
}
