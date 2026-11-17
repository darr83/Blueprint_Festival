using BlueprintFestival.Simulation.Show;
using Xunit;

namespace BlueprintFestival.Simulation.Tests;

[Collection(SingingCollection.Name)]
public class SingAlongTests
{
    [Fact]
    public void Marked_choruses_are_saved_and_read_back_from_the_chorus_file()
    {
        var choruses = Choruses.None.With(45.2, 75.8).With(150, 181.5);

        var text = choruses.Write();
        var back = Choruses.Read(text);

        Assert.Contains("chorus = 0:45.2 - 1:15.8", text);
        Assert.Equal(2, back.All.Count);
        Assert.Equal(45.2, back.All[0].Start, 1);
        Assert.Equal(181.5, back.All[1].End, 1);
        Assert.Equal("01 Hay Fever.mp3.chorus", Choruses.FileFor("01 Hay Fever.mp3"));
    }

    [Fact]
    public void A_chorus_file_written_by_hand_is_read_too()
    {
        var choruses = Choruses.Read("# mine\nchorus = 30 - 50\n1:30 to 1:55\nchorus: nonsense\n\n2:10.5-2:40\n");

        Assert.Equal(new[] { (30.0, 50.0), (90.0, 115.0), (130.5, 160.0) }, choruses.All.Select(c => (c.Start, c.End)));
        Assert.Null(Choruses.Seconds("1:75"));
        Assert.Null(Choruses.Seconds("soon"));
        Assert.Equal(65.5, Choruses.Seconds("1:05.5"));
        Assert.False(Choruses.Read("").Any);
    }

    [Fact]
    public void Overlapping_choruses_join_and_slips_are_left_out()
    {
        var choruses = Choruses.None.With(10, 30).With(25, 40).With(60, 61).With(80, 70);

        Assert.Equal(new[] { (10.0, 40.0), (70.0, 80.0) }, choruses.All.Select(c => (c.Start, c.End)));
        Assert.Equal(new[] { (70.0, 80.0) }, choruses.Without(0).All.Select(c => (c.Start, c.End)));
    }

    [Fact]
    public void The_crowd_comes_in_at_the_start_of_a_chorus_and_dies_away_after_it()
    {
        var choruses = Choruses.None.With(60, 90);

        Assert.Equal(0, choruses.SingingAt(59));
        Assert.InRange(choruses.SingingAt(60.6), 0.3, 0.7);
        Assert.Equal(1, choruses.SingingAt(75));
        Assert.Equal(1, choruses.SingingAt(90));
        Assert.InRange(choruses.SingingAt(91), 0.2, 0.8);
        Assert.Equal(0, choruses.SingingAt(95));
        Assert.Equal(15, choruses.SecondsInto(75), 6);
        Assert.Equal(-1, choruses.SecondsInto(50));
        Assert.Equal(0, Choruses.None.SingingAt(75));
    }

    [Fact]
    public void Tapping_marks_a_chorus_start_then_end_and_undo_takes_a_tap_back()
    {
        var marking = new ChorusMarking(Choruses.None);

        Assert.False(marking.Tap(40));
        Assert.Equal(40, marking.Started);
        Assert.False(marking.Tap(41), "Two taps a second apart are a slip, not a chorus.");
        Assert.True(marking.Tap(70));
        Assert.Null(marking.Started);
        Assert.Single(marking.Marks.All);

        marking.Tap(120);
        Assert.False(marking.Undo(), "The first undo takes back the start that was tapped.");
        Assert.Single(marking.Marks.All);
        Assert.True(marking.Undo());
        Assert.False(marking.Marks.Any);
        Assert.False(marking.CanUndo);

        marking.Tap(10);
        marking.Tap(20);
        marking.Clear();
        Assert.False(marking.Marks.Any);
        Assert.True(marking.Undo());
        Assert.Single(marking.Marks.All);
    }

    [Fact]
    public void A_songs_singing_on_its_own_is_not_a_song_in_itself()
    {
        var songs = SongFile.InOrder(new[] { "01 Hay Fever.mp3", "01 Hay Fever.vocals.mp3", "01 Hay Fever.crowd.ogg", "01 Hay Fever.mp3.chorus", "02 Barn Dance.ogg" });

        Assert.Equal(new[] { "Hay Fever", "Barn Dance" }, songs.Select(s => s.Title));
        Assert.Equal("01 Hay Fever.crowd.mp3", songs[0].VocalsFiles().First());
        Assert.Contains("01 Hay Fever.crowd.ogg", songs[0].VocalsFiles());
        Assert.Contains("01 Hay Fever.vocals.mp3", songs[0].VocalsFiles());
    }

    [Fact]
    public void Now_and_then_the_artist_stops_singing_and_leaves_a_chorus_to_the_crowd()
    {
        var chorus = new Chorus(60, 90);
        var left = Enumerable.Range(0, 10_000).Select(roll => SingAlong.LeftToTheCrowd(roll, chorus)).ToList();
        var taken = left.Where(l => l is not null).Select(l => l!.Value).ToList();

        Assert.InRange(taken.Count, 1000, 2000);
        Assert.All(taken, l => Assert.InRange(l.Length, SingAlong.LeftShortest, SingAlong.LeftLongest));
        Assert.All(taken, l => Assert.True(l.Start >= 63 && l.End <= 89, "Once the crowd is all in, and done before the chorus ends."));
        Assert.Equal(left[123], SingAlong.LeftToTheCrowd(123, chorus));
        Assert.All(Enumerable.Range(0, 2000), roll => Assert.Null(SingAlong.LeftToTheCrowd(roll, new Chorus(60, 67))));
    }

    [Fact]
    public void The_artists_voice_fades_out_for_the_crowd_and_back_in()
    {
        var left = new[] { new Chorus(70, 77) };

        Assert.Equal(0, SingAlong.LeftLevel(left, 65));
        Assert.InRange(SingAlong.LeftLevel(left, 70.3), 0.3, 0.7);
        Assert.Equal(1, SingAlong.LeftLevel(left, 74));
        Assert.Equal(0, SingAlong.LeftLevel(left, 78));
        Assert.Equal(0, SingAlong.LeftLevel(Array.Empty<Chorus>(), 74));
    }

    [Fact]
    public void A_happy_crowd_sings_and_a_fed_up_one_does_not()
    {
        Assert.Equal(0, SingAlong.Share(30, 3_000_000));
        Assert.True(SingAlong.Share(80, 500) > 0.3);
        Assert.True(SingAlong.Share(80, 2_600_000) > SingAlong.Share(80, 5_000), "A big following gets more of the field singing.");
        Assert.True(SingAlong.Share(90, 50_000) > SingAlong.Share(55, 50_000));
        Assert.InRange(SingAlong.Share(100, 10_000_000), 0.99, 1);
    }

    [Fact]
    public void The_front_sings_first_and_the_singing_spreads_back()
    {
        const double share = 0.5;
        int Singing(double fromFront, double seconds) => Enumerable.Range(0, 2000).Count(p => SingAlong.Sings(p, fromFront, seconds, share));

        Assert.Equal(0, Singing(2, -1));
        Assert.True(Singing(2, 1) > 1500, "Nearly everyone at the front sings once the crowd is this keen.");
        Assert.Equal(0, Singing(45, 1));
        Assert.True(Singing(45, 5) is > 600 and < 1400, "About half the back sings, once it has spread to them.");
        Assert.True(Singing(2, 10) > Singing(45, 10));
        Assert.Equal(0, Enumerable.Range(0, 500).Count(p => SingAlong.Sings(p, 2, 5, 0)));
    }

    [Fact]
    public void Most_singers_put_their_arms_up_and_the_singing_gets_louder_with_more_singers()
    {
        var armsUp = Enumerable.Range(0, 1000).Count(SingAlong.ArmsUp);
        Assert.InRange(armsUp, 600, 800);
        Assert.Equal(0, SingAlong.Loudness(0));
        Assert.True(SingAlong.Loudness(50) < SingAlong.Loudness(300));
        Assert.Equal(1, SingAlong.Loudness(5000));
        Assert.All(Enumerable.Range(0, 200), b => Assert.InRange(SingAlong.Mouth(b, b * 0.13), 0.45, 0.95));
        Assert.Equal(DanceTiming.OnTheHit, Dancing.TimingOf(DanceMove.SingAlong, 12345));
    }
}
