using BlueprintFestival.Simulation.Core;

namespace BlueprintFestival.Simulation.Tests;

public class GameClockTests
{
    [Fact]
    public void A_new_game_opens_on_a_Saturday_morning_in_July()
    {
        var clock = new GameClock();
        Assert.Equal(DayOfWeek.Saturday, clock.Now.DayOfWeek);
        Assert.Equal(7, clock.Now.Month);
        Assert.Equal(8, clock.Now.Hour);
        Assert.Equal(GameSpeed.Normal, clock.Speed);
    }

    [Fact]
    public void An_hour_takes_eight_real_minutes_at_normal_speed()
    {
        var clock = new GameClock();
        var start = clock.Now;
        for (var i = 0; i < 8 * 60 * 60; i++)
        {
            clock.Advance(1 / 60.0);
        }

        Assert.InRange((clock.Now - start).TotalMinutes, 59.9, 60.1);
    }

    [Theory]
    [InlineData(GameSpeed.Normal, 15)]
    [InlineData(GameSpeed.Fast, 30)]
    [InlineData(GameSpeed.VeryFast, 60)]
    [InlineData(GameSpeed.Fastest, 120)]
    public void Each_speed_runs_its_multiple_of_ticks(GameSpeed speed, int expectedTicks)
    {
        var clock = new GameClock();
        clock.SetSpeed(speed);
        Assert.Equal(expectedTicks, clock.Advance(2));
        Assert.Equal(expectedTicks, clock.TickCount);
    }

    [Fact]
    public void Nothing_moves_while_paused()
    {
        var clock = new GameClock();
        var start = clock.Now;
        clock.SetSpeed(GameSpeed.Paused);
        Assert.Equal(0, clock.Advance(100));
        Assert.Equal(start, clock.Now);
        Assert.True(clock.IsPaused);
    }

    [Fact]
    public void Small_frames_add_up_to_the_same_time_as_one_long_one()
    {
        var smooth = new GameClock();
        var jerky = new GameClock();
        for (var i = 0; i < 48; i++)
        {
            smooth.Advance(0.125);
        }

        for (var i = 0; i < 6; i++)
        {
            jerky.Advance(1);
        }

        Assert.Equal(jerky.Now, smooth.Now);
    }

    [Fact]
    public void A_long_stall_is_capped_and_the_backlog_dropped()
    {
        var clock = new GameClock();
        Assert.Equal(GameClock.MaxTicksPerAdvance, clock.Advance(3600));
        Assert.Equal(0, clock.Advance(0));
    }

    [Fact]
    public void Ticked_is_raised_once_per_tick()
    {
        var clock = new GameClock();
        var seen = 0;
        clock.Ticked += _ => seen++;
        var ran = clock.Advance(1);
        Assert.Equal(ran, seen);
        Assert.True(seen > 0);
    }

    [Fact]
    public void Bad_input_is_refused()
    {
        var clock = new GameClock();
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.SetSpeed((GameSpeed)3));
    }

    [Fact]
    public void State_can_be_captured_and_restored()
    {
        var clock = new GameClock();
        clock.SetSpeed(GameSpeed.VeryFast);
        clock.Advance(10);
        var state = clock.CaptureState();

        var other = new GameClock();
        other.RestoreState(state);
        Assert.Equal(clock.Now, other.Now);
        Assert.Equal(clock.TickCount, other.TickCount);
        Assert.Equal(GameSpeed.VeryFast, other.Speed);
    }
}
