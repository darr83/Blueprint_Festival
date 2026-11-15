using BlueprintFestival.Simulation.Show;

namespace BlueprintFestival.Simulation.Tests;

public class RosterTests
{
    private static string Root()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !Directory.Exists(Path.Combine(folder.FullName, "game", "Music")))
        {
            folder = folder.Parent;
        }

        Assert.NotNull(folder);
        return folder.FullName;
    }

    [Fact]
    public void There_are_twenty_five_artists_of_every_kind_and_genre()
    {
        var artists = Roster.Artists;

        Assert.Equal(25, artists.Count);
        Assert.Equal(25, artists.Select(a => a.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.True(artists.Count(a => a.Kind == ActKind.Solo) >= 4);
        Assert.True(artists.Count(a => a.Kind == ActKind.Duo) >= 4);
        Assert.True(artists.Count(a => a.Kind is ActKind.Band or ActKind.FolkBand) >= 8);
        Assert.True(artists.Count(a => a.Kind == ActKind.Dj) >= 4);
        foreach (var genre in Enum.GetValues<Genre>())
        {
            Assert.True(artists.Count(a => a.Genre == genre) >= 3, $"Too few {genre} artists.");
        }

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, artists.Select(a => a.Rating).Distinct().Order());

        // A folder name has to be something Windows will take.
        Assert.All(artists, a => Assert.True(a.Name.IndexOfAny(Path.GetInvalidFileNameChars().Concat("&<>:\"/\\|?*").ToArray()) < 0, a.Name));
    }

    [Fact]
    public void A_better_rating_means_a_bigger_following()
    {
        for (var rating = 1; rating < 5; rating++)
        {
            var most = Roster.Artists.Where(a => a.Rating == rating).Max(a => a.Fans);
            var fewest = Roster.Artists.Where(a => a.Rating == rating + 1).Min(a => a.Fans);
            Assert.True(fewest > most, $"A {rating + 1} star artist has fewer fans than a {rating} star one.");
        }

        Assert.Equal("780", Roster.FansText(780));
        Assert.Equal("7.4k", Roster.FansText(7400));
        Assert.Equal("44k", Roster.FansText(44000));
        Assert.Equal("2.6m", Roster.FansText(2600000));
        Assert.Equal("Hip hop", Roster.NameOf(Genre.HipHop));
    }

    [Fact]
    public void There_is_a_headline_band_and_a_headline_dj_at_every_rating_offered()
    {
        for (var stars = 2; stars <= 5; stars++)
        {
            Assert.Single(Roster.Artists, a => a.Headline && a.Rating == stars && a.Kind == ActKind.Band);
            Assert.Single(Roster.Artists, a => a.Headline && a.Rating == stars && a.Kind == ActKind.Dj);
        }
    }

    [Fact]
    public void A_festival_books_artists_from_the_roster_and_nobody_twice()
    {
        for (var year = 1; year <= 6; year++)
        {
            foreach (var name in new[] { "Larkmoor Festival", "Top Field Fair", "Midsummer on the Green" })
            {
                var game = FestivalGame.StartYear(name, new YearSetup(year, FestivalGame.Licences[year - 1], 50000));
                var acts = game.Show.Slots.Take(3).Concat(game.SecondShow.Slots).Select(s => s.Act).Concat(game.Headliners.Select(h => h.Act)).ToList();

                Assert.Equal(10, acts.Count);
                Assert.Equal(10, acts.Select(a => a.Name).Distinct().Count());
                Assert.All(acts, act =>
                {
                    var artist = Roster.Named(act.Name);
                    Assert.NotNull(artist);
                    Assert.Equal((artist.Kind, artist.Genre, artist.Rating, artist.Fans), (act.Kind, act.Genre, act.Rating, act.Fans));
                });
                Assert.All(game.Headliners, offer =>
                {
                    Assert.Equal(offer.Stars, offer.Act.Rating);
                    Assert.Contains(offer.Act.Kind, new[] { ActKind.Band, ActKind.Dj });
                });
                Assert.All(game.Show.Slots.Take(3), slot => Assert.InRange(slot.Act.Rating, 1, 3));
                Assert.All(game.SecondShow.Slots, slot => Assert.InRange(slot.Act.Rating, 1, 3));
            }
        }
    }

    [Fact]
    public void The_same_festival_books_the_same_artists_every_time()
    {
        var one = FestivalGame.NewFestival("Larkmoor Festival");
        var two = FestivalGame.NewFestival("Larkmoor Festival");
        var other = FestivalGame.NewFestival("Top Field Fair");

        Assert.Equal(one.Show.Slots.Select(s => s.Act.Name), two.Show.Slots.Select(s => s.Act.Name));
        Assert.Equal(one.Headliners.Select(h => h.Act.Name), two.Headliners.Select(h => h.Act.Name));
        Assert.NotEqual(one.Show.Slots.Select(s => s.Act.Name), other.Show.Slots.Select(s => s.Act.Name));
    }

    [Theory]
    [InlineData("01 Long Way Home.mp3", 1, "Long Way Home")]
    [InlineData("02_last_bus_to_market_square.ogg", 2, "last bus to market square")]
    [InlineData("3 - Rain On The Top Field.WAV", 3, "Rain On The Top Field")]
    [InlineData("10. Harvest Moon.mp3", 10, "Harvest Moon")]
    [InlineData("Encore.mp3", int.MaxValue, "Encore")]
    public void A_song_file_is_named_with_a_number_and_its_title(string file, int order, string title)
    {
        var song = SongFile.Read(file);

        Assert.NotNull(song);
        Assert.Equal(order, song.Order);
        Assert.Equal(title, song.Title);
    }

    [Fact]
    public void Songs_play_in_number_order_and_other_files_are_left_alone()
    {
        var songs = SongFile.InOrder(new[] { "03 Third.mp3", "PUT SONGS HERE.txt", "01 First.ogg", "01 First.ogg.import", "Encore.wav", "02 Second.mp3", "cover.png" });

        Assert.Equal(new[] { "First", "Second", "Third", "Encore" }, songs.Select(s => s.Title));
        Assert.Null(SongFile.Read("notes.txt"));
    }

    [Fact]
    public void Every_artist_has_a_music_folder_of_their_own_name()
    {
        var music = Path.Combine(Root(), "game", "Music");
        var folders = Directory.GetDirectories(music).Select(Path.GetFileName).ToList();

        Assert.Equal(Roster.Artists.Select(a => a.Name).Append("Entertainers").Order(), folders.Order());
        Assert.All(Roster.Artists, a => Assert.True(File.Exists(Path.Combine(music, a.Name, "PUT SONGS HERE.txt")), a.Name));
        Assert.True(File.Exists(Path.Combine(music, "README.txt")));
    }

    [Fact]
    public void Every_entertainer_has_a_music_folder_of_their_own()
    {
        var music = Path.Combine(Root(), "game", "Music");
        foreach (var kind in Busking.Kinds)
        {
            var folder = Path.Combine(music, Busking.MusicFolder(kind));
            Assert.True(File.Exists(Path.Combine(folder, "PUT SONGS HERE.txt")), folder);
        }

        Assert.True(File.Exists(Path.Combine(music, "Entertainers", "README.txt")));
    }
}
