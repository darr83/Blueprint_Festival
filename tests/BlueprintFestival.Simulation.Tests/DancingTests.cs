using BlueprintFestival.Simulation.Show;

namespace BlueprintFestival.Simulation.Tests;

public class DancingTests
{
    [Theory]
    [InlineData(Genre.Pop, DanceMove.Bounce)]
    [InlineData(Genre.Rock, DanceMove.HeadBang)]
    [InlineData(Genre.Metal, DanceMove.Horns)]
    [InlineData(Genre.Electronic, DanceMove.Jump)]
    [InlineData(Genre.Country, DanceMove.Sway)]
    [InlineData(Genre.HipHop, DanceMove.Nod)]
    [InlineData(Genre.Indie, DanceMove.Shuffle)]
    [InlineData(Genre.Punk, DanceMove.Pogo)]
    [InlineData(Genre.Folk, DanceMove.Jig)]
    [InlineData(Genre.Reggae, DanceMove.Skank)]
    [InlineData(Genre.Soul, DanceMove.Shimmy)]
    [InlineData(Genre.Disco, DanceMove.Disco)]
    public void EachSortOfMusicHasItsOwnMove(Genre genre, DanceMove move) => Assert.Equal(move, Dancing.MoveFor(genre));

    [Fact]
    public void MostOfTheCrowdDanceTheMoveThatSuitsTheMusicAndSomeJustNod()
    {
        foreach (var genre in Enum.GetValues<Genre>())
        {
            var moves = Enumerable.Range(0, 2000).Select(p => Dancing.MoveFor(genre, p)).ToList();
            var suited = moves.Count(m => m == Dancing.MoveFor(genre)) / 2000.0;
            var nodding = moves.Count(m => m == DanceMove.Tap) / 2000.0;

            Assert.InRange(suited, 0.6, 0.8);
            Assert.InRange(nodding, 0.06, 0.3);
        }
    }

    [Fact]
    public void AboutOneInSevenAtEachStageDanceADifferentStyle()
    {
        foreach (var genre in Enum.GetValues<Genre>())
        {
            var styles = Dancing.OtherStyles(genre);
            Assert.DoesNotContain(Dancing.MoveFor(genre), styles);
            Assert.True(styles.Count >= 3);

            var moves = Enumerable.Range(0, 4000).Select(p => Dancing.MoveFor(genre, p)).ToList();
            Assert.True(moves.Distinct().Count() >= 5, $"{genre} has only {moves.Distinct().Count()} moves");

            // Each of the other styles turns up, so the crowd is a mix and not one odd move out.
            foreach (var style in styles)
            {
                Assert.InRange(moves.Count(m => m == style) / 4000.0, 0.02, 0.3);
            }
        }
    }

    [Fact]
    public void SomeDanceOnTheOtherBeatAndSomeTwiceAsFast()
    {
        var timings = Enumerable.Range(0, 4000).Select(p => Dancing.TimingOf(DanceMove.HeadBang, p)).ToList();
        Assert.InRange(timings.Count(t => t == DanceTiming.Offbeat) / 4000.0, 0.08, 0.16);
        Assert.InRange(timings.Count(t => t == DanceTiming.DoubleTime) / 4000.0, 0.05, 0.11);
        Assert.InRange(timings.Count(t => t == DanceTiming.OnTheHit) / 4000.0, 0.75, 0.85);

        // The clapping heard follows those swaying, so they always clap on the hit; and nobody jumps twice as fast.
        Assert.All(Enumerable.Range(0, 2000), p => Assert.Equal(DanceTiming.OnTheHit, Dancing.TimingOf(DanceMove.Sway, p)));
        Assert.DoesNotContain(DanceTiming.DoubleTime, Enumerable.Range(0, 2000).Select(p => Dancing.TimingOf(DanceMove.Jump, p)));
    }

    [Fact]
    public void OffbeatDancersLandOnTheBeatInBetweenAndDoubleTimeOnEveryBeat()
    {
        var offbeat = Enumerable.Range(0, 5000).First(p => Dancing.Lateness(p) == 0 && Dancing.TimingOf(DanceMove.HeadBang, p) == DanceTiming.Offbeat);
        Assert.Equal(0.5, Dancing.Through(8, DanceMove.HeadBang, offbeat), 6);
        Assert.Equal(0, Dancing.Through(9, DanceMove.HeadBang, offbeat), 6);
        Assert.Equal(0.5, Dancing.Through(10, DanceMove.HeadBang, offbeat), 6);

        var fast = Enumerable.Range(0, 5000).First(p => Dancing.Lateness(p) == 0 && Dancing.TimingOf(DanceMove.HeadBang, p) == DanceTiming.DoubleTime);
        Assert.Equal(0, Dancing.Through(8, DanceMove.HeadBang, fast), 6);
        Assert.Equal(0.5, Dancing.Through(8.5, DanceMove.HeadBang, fast), 6);
        Assert.Equal(0, Dancing.Through(9, DanceMove.HeadBang, fast), 6);

        // Still always somewhere in the move, whatever the timing.
        foreach (var move in Enum.GetValues<DanceMove>())
        {
            Assert.All(Enumerable.Range(0, 300), b => Assert.InRange(Dancing.Through(b * 0.37, move, b), 0, 1 - 1e-12));
        }
    }

    [Fact]
    public void EverySortOfMusicHasAMoveNoOtherSortUses()
    {
        var moves = Enum.GetValues<Genre>().Select(g => Dancing.MoveFor(g)).ToList();
        Assert.Equal(moves.Count, moves.Distinct().Count());

        // Nobody pogos at a folk or soul gig, or jigs to punk.
        Assert.DoesNotContain(DanceMove.Pogo, Enumerable.Range(0, 2000).Select(p => Dancing.MoveFor(Genre.Folk, p)));
        Assert.DoesNotContain(DanceMove.Pogo, Enumerable.Range(0, 2000).Select(p => Dancing.MoveFor(Genre.Soul, p)));
        Assert.DoesNotContain(DanceMove.Jig, Enumerable.Range(0, 2000).Select(p => Dancing.MoveFor(Genre.Punk, p)));

        // Nobody pogos or jigs twice as fast.
        Assert.All(Enumerable.Range(0, 2000), p => Assert.NotEqual(DanceTiming.DoubleTime, Dancing.TimingOf(DanceMove.Pogo, p)));
        Assert.All(Enumerable.Range(0, 2000), p => Assert.NotEqual(DanceTiming.DoubleTime, Dancing.TimingOf(DanceMove.Jig, p)));
    }

    [Fact]
    public void NobodyDancesLikeTheyAreAtTheWrongGig()
    {
        // Nobody head-bangs to country or jumps to hip hop.
        var country = Enumerable.Range(0, 2000).Select(p => Dancing.MoveFor(Genre.Country, p)).Distinct().ToHashSet();
        Assert.DoesNotContain(DanceMove.HeadBang, country);
        Assert.DoesNotContain(DanceMove.Horns, country);
        Assert.DoesNotContain(DanceMove.Jump, country);
        Assert.DoesNotContain(DanceMove.Jump, Enumerable.Range(0, 2000).Select(p => Dancing.MoveFor(Genre.HipHop, p)));
        Assert.DoesNotContain(DanceMove.Horns, Enumerable.Range(0, 2000).Select(p => Dancing.MoveFor(Genre.Pop, p)));
    }

    [Fact]
    public void EveryoneIsCloseToTheBeatButNotAllExactlyOnIt()
    {
        var lateness = Enumerable.Range(0, 1000).Select(Dancing.Lateness).ToList();
        Assert.All(lateness, l => Assert.InRange(l, -0.05, 0.05));
        Assert.True(lateness.Distinct().Count() > 50);

        var energy = Enumerable.Range(0, 1000).Select(Dancing.Energy).ToList();
        Assert.All(energy, e => Assert.InRange(e, 0.65, 1.15));
        Assert.True(energy.Max() - energy.Min() > 0.4);
    }

    [Fact]
    public void ThePersonDancesTheSameWayEveryTime()
    {
        Assert.Equal(Dancing.MoveFor(Genre.Rock, 41), Dancing.MoveFor(Genre.Rock, 41));
        Assert.Equal(Dancing.Lateness(41), Dancing.Lateness(41));
        Assert.Equal(Dancing.Energy(41), Dancing.Energy(41));
    }

    [Fact]
    public void AMoveStartsAsTheBeatLands()
    {
        var person = Enumerable.Range(0, 5000).First(p => Dancing.Lateness(p) == 0 && Dancing.TimingOf(DanceMove.HeadBang, p) == DanceTiming.OnTheHit);

        // A head-bang lands on every other beat, and is halfway round on the beat between.
        Assert.Equal(2, Dancing.BeatsAHit);
        Assert.Equal(0, Dancing.Through(8, DanceMove.HeadBang, person), 6);
        Assert.Equal(0.5, Dancing.Through(9, DanceMove.HeadBang, person), 6);
        Assert.Equal(0, Dancing.Through(10, DanceMove.HeadBang, person), 6);

        // A sway goes one way on one hit and back on the next: four beats there and back.
        Assert.Equal(0, Dancing.Through(8, DanceMove.Sway, person), 6);
        Assert.Equal(0.5, Dancing.Through(10, DanceMove.Sway, person), 6);
        Assert.Equal(0, Dancing.Through(12, DanceMove.Sway, person), 6);

        Assert.All(Enumerable.Range(0, 200), b => Assert.InRange(Dancing.Through(b * 0.37, DanceMove.Jump, b), 0, 1 - 1e-12));
    }

    [Fact]
    public void TheSongsOwnBeatIsUsedWhenItIsKnownAndASteadyOneWhenItIsNot()
    {
        var song = new BeatInfo(100, 0.4);
        Assert.Equal(1, Dancing.BeatNow(song, 1.0, Genre.Rock, 99), 6);

        // With no beat, a rock act's steady 126 a minute from the start of the set.
        Assert.Equal(126, Dancing.BeatNow(null, 0, Genre.Rock, 60), 6);
    }
}
