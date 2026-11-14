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
    public void NobodyDancesLikeTheyAreAtTheWrongGig()
    {
        // Nobody head-bangs to country or jumps to hip hop.
        var country = Enumerable.Range(0, 2000).Select(p => Dancing.MoveFor(Genre.Country, p)).Distinct().ToHashSet();
        Assert.DoesNotContain(DanceMove.HeadBang, country);
        Assert.DoesNotContain(DanceMove.Horns, country);
        Assert.DoesNotContain(DanceMove.Jump, country);
        Assert.DoesNotContain(DanceMove.Jump, Enumerable.Range(0, 2000).Select(p => Dancing.MoveFor(Genre.HipHop, p)));
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
        var person = Enumerable.Range(0, 1000).First(p => Dancing.Lateness(p) == 0);

        Assert.Equal(0, Dancing.Through(8, DanceMove.HeadBang, person), 6);
        Assert.Equal(0.5, Dancing.Through(8.5, DanceMove.HeadBang, person), 6);

        // A sway takes two beats: halfway through it on the next beat, and back to the start on the one after.
        Assert.Equal(0, Dancing.Through(8, DanceMove.Sway, person), 6);
        Assert.Equal(0.5, Dancing.Through(9, DanceMove.Sway, person), 6);
        Assert.Equal(0, Dancing.Through(10, DanceMove.Sway, person), 6);

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
