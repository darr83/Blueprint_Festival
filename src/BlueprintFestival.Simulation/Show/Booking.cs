using BlueprintFestival.Simulation.Site;

namespace BlueprintFestival.Simulation.Show;

/// <summary>Whether an artist can be booked, and if not why not.</summary>
public enum Availability
{
    /// <summary>Free to book.</summary>
    Free,

    /// <summary>Already playing this festival.</summary>
    Booked,

    /// <summary>Signed for another festival that weekend.</summary>
    Gone,

    /// <summary>Turned the festival down twice and will not talk again this year.</summary>
    WalkedAway,
}

/// <summary>What came of an offer to an artist.</summary>
public enum OfferOutcome
{
    /// <summary>They said yes and are booked at the fee offered.</summary>
    Accepted,

    /// <summary>They said no. One more offer can be made.</summary>
    Refused,

    /// <summary>They said no for the second time and have signed for somebody else.</summary>
    WalkedAway,

    /// <summary>They are not free to book.</summary>
    NotFree,

    /// <summary>A four or five star act only plays one of the last two sets on a stage.</summary>
    WantsLaterSet,

    /// <summary>There is not the money to pay them.</summary>
    NoMoney,

    /// <summary>No such artist, or the set cannot be booked just now.</summary>
    NotPossible,
}

/// <summary>
/// The rules of booking an artist: how big a stage they want, how likely they are to take less
/// than they ask, how many other festivals are after them and when they will be gone.
/// </summary>
public static class Booking
{
    /// <summary>An offer below this share of what an artist asks is an insult: it is always turned down.</summary>
    public const double Insulting = 0.6;

    /// <summary>How many times an artist says no before they sign for somebody else.</summary>
    public const int Patience = 2;

    /// <summary>How much dearer an artist is for each month the festival waits after January.</summary>
    public const double DearerAMonth = 0.08;

    /// <summary>The first set of a stage, counting from 0, that a four or five star act will play: one of the last two.</summary>
    public static int EarliestSetForABigName => RunningOrder.SetsAStage - 2;

    /// <summary>The smallest stage an act with so many stars will play.</summary>
    public static StageSize StageFor(int stars) => stars switch { <= 2 => StageSize.Small, <= 4 => StageSize.Medium, _ => StageSize.Large };

    /// <summary>True if an act with so many stars will play a kind of stage.</summary>
    public static bool WillPlay(int stars, StageKind stage) => stars <= Catalogue.Of(stage).TopRating;

    /// <summary>The cheapest stage of a size, for saying what one costs.</summary>
    public static StageInfo CheapestOf(StageSize size) => Catalogue.StagesOf(size).First();

    /// <summary>
    /// The chance an artist takes an offer, from the share of their asking fee it is: certain at the
    /// full fee, nothing at an insult, and a straight line between. An artist nobody else is asking
    /// for is a little easier to talk down.
    /// </summary>
    public static double ChanceOfYes(int offer, int ask, int rivals)
    {
        if (ask <= 0 || offer >= ask)
        {
            return 1;
        }

        var share = (double)offer / ask;
        if (share < Insulting)
        {
            return 0;
        }

        return Math.Clamp(((share - Insulting) / (1 - Insulting)) + (rivals == 0 ? 0.15 : 0), 0.05, 0.95);
    }

    /// <summary>A chance in plain words: "About 6 in 10".</summary>
    public static string ChanceWords(double chance) => chance >= 1 ? "Certain" : chance <= 0 ? "No chance: that is an insult" : $"About {Math.Clamp((int)Math.Round(chance * 10), 1, 9)} in 10";

    /// <summary>How many other festivals are asking after an artist this year: the bigger the name, the more.</summary>
    public static int Rivals(Artist artist, int seed)
    {
        ArgumentNullException.ThrowIfNull(artist);
        var roll = Roll(artist.Name, seed, 1);
        return artist.Rating switch
        {
            >= 5 => 4 + (int)(roll * 2),
            4 => 2 + (int)(roll * 2),
            3 => (int)(roll * 3),
            _ => 0,
        };
    }

    /// <summary>
    /// The last month an artist can still be booked, counting January as 0: after that many months
    /// of ticket sales they have signed elsewhere. Null means they are free all year.
    /// Everyone is free in January, and five star acts go first.
    /// </summary>
    public static int? GoneAfter(Artist artist, int seed)
    {
        ArgumentNullException.ThrowIfNull(artist);
        if (artist.Rating <= 2 || Rivals(artist, seed) == 0)
        {
            return null;
        }

        var spread = Roll(artist.Name, seed, 2);
        return artist.Rating switch
        {
            >= 5 => (int)(spread * 2),
            4 => 1 + (int)(spread * 3),
            _ => 2 + (int)(spread * 4),
        };
    }

    /// <summary>A number from 0 to 1 that is always the same for the same artist, festival and purpose.</summary>
    public static double Roll(string name, int seed, int purpose)
    {
        ArgumentNullException.ThrowIfNull(name);
        var hash = 2166136261u ^ (uint)seed;
        foreach (var c in name)
        {
            hash = (hash ^ c) * 16777619u;
        }

        hash = (hash ^ (uint)purpose) * 16777619u;
        hash ^= hash >> 15;
        hash *= 0x2C1B3C6Du;
        hash ^= hash >> 12;
        return (hash % 100000) / 100000.0;
    }
}

/// <summary>Booking as written to a save file: the fees agreed and who has said no how often.</summary>
public sealed record BookingSave(Dictionary<string, int> Agreed, Dictionary<string, int> Refusals, List<string>? NoShows = null);
