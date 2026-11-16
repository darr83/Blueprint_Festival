namespace BlueprintFestival.Simulation.Sales;

/// <summary>
/// Something done once to get the festival talked about. Cost is at a village festival and grows
/// with the licence. EveryMonths is how long before it can be done again; 0 is once a year.
/// </summary>
public sealed record Stunt(string Id, string Name, int Cost, int Buzz, int EveryMonths, string About, int FreeTickets = 0);

/// <summary>One batch of tickets: what it is called and its share of the ordinary tickets.</summary>
public sealed record TicketRelease(string Name, double Share);

/// <summary>One answer to a piece of news, and what it does.</summary>
public sealed record NewsChoice(string Label, string Outcome, int Buzz = 0, int PriceCut = 0, bool UsesJob = false, double Sales = 1);

/// <summary>A piece of news that turns up at the start of a month and wants an answer. The last choice is what happens if it is ignored.</summary>
public sealed record NewsItem(string Id, string Headline, string About, IReadOnlyList<NewsChoice> Choices);

/// <summary>One thing that happened in the campaign, for the chart and the list beside it.</summary>
public sealed record CampaignNote(int Turn, string What);

/// <summary>The rules of the ticket campaign: jobs, buzz, ticket releases, stunts and news.</summary>
public static class Campaign
{
    /// <summary>How many jobs there is time for in a month.</summary>
    public const int JobsAMonth = 3;

    /// <summary>How much buzz fades in a month if nothing feeds it.</summary>
    public const double BuzzFade = 8;

    /// <summary>The most buzz that adverts running through a month keep alive.</summary>
    public const int MostAdvertBuzz = 5;

    /// <summary>Buzz from a ticket release selling out.</summary>
    public const double SoldOutBuzz = 6;

    /// <summary>Buzz from announcing an act, for each of its stars.</summary>
    public const double BuzzAStar = 3;

    /// <summary>The share of an announced act's fans still without a ticket who buy in each later month.</summary>
    public const double LaterMonths = 0.45;

    /// <summary>The share who buy on each day of build week.</summary>
    public const double EachBuildDay = 0.07;

    public static readonly IReadOnlyList<TicketRelease> Releases = new[]
    {
        new TicketRelease("Early bird", 0.2),
        new TicketRelease("Second release", 0.35),
        new TicketRelease("Final release", 0.45),
    };

    public static readonly IReadOnlyList<Stunt> Stunts = new[]
    {
        new Stunt("interview", "Local radio interview", 0, 8, 2, "Free. Talk the festival up on the breakfast show."),
        new Stunt("press", "Story in the local paper", 0, 5, 0, "Free, but it is only news once."),
        new Stunt("competition", "Ticket competition", 0, 6, 3, "Give away ten tickets. The winners tell their friends.", 10),
        new Stunt("pubnight", "Free night at the pub", 15, 7, 2, "Put a local act on at the pub. Everyone there hears about the festival."),
        new Stunt("launch", "Launch party", 60, 14, 0, "A night in the village hall with a band, and the press invited."),
    };

    public static Stunt? FindStunt(string id) => Stunts.FirstOrDefault(s => s.Id == id);

    public static readonly IReadOnlyList<NewsItem> News = new[]
    {
        new NewsItem("rival", "Another festival has picked your weekend", "People will choose between you unless you answer it.", new[]
        {
            new NewsChoice("Cut £2 off the price", "Tickets are £2 cheaper from now on.", PriceCut: 2),
            new NewsChoice("Shout louder", "Uses one of this month's jobs.", UsesJob: true),
            new NewsChoice("Ignore it", "Buzz falls by 10.", Buzz: -10),
        }),
        new NewsItem("paper", "The county paper wants an interview", "A whole page, if you can find the time.", new[]
        {
            new NewsChoice("Give the interview", "Uses a job. Buzz up by 9.", Buzz: 9, UsesJob: true),
            new NewsChoice("Too busy", "Nothing happens."),
        }),
        new NewsItem("petition", "Neighbours have started a petition about the noise", "It will be in the paper if nobody talks to them.", new[]
        {
            new NewsChoice("Hold a meeting in the village hall", "Uses a job. The petition is dropped.", UsesJob: true),
            new NewsChoice("Ignore it", "Buzz falls by 7.", Buzz: -7),
        }),
        new NewsItem("touts", "Touts are buying tickets to sell on", "Fans are cross about it.", new[]
        {
            new NewsChoice("Limit tickets to four each", "Fans are pleased: buzz up by 4. Sales a little slower this month.", Buzz: 4, Sales: 0.92),
            new NewsChoice("Leave it", "Sales a little quicker this month. Buzz falls by 5.", Buzz: -5, Sales: 1.06),
        }),
        new NewsItem("clip", "A video about the festival is going round", "Thousands have watched it already.", new[]
        {
            new NewsChoice("Share it everywhere", "Uses a job. Buzz up by 12.", Buzz: 12, UsesJob: true),
            new NewsChoice("Enjoy it", "Buzz up by 5.", Buzz: 5),
        }),
    };

    public static NewsItem? FindNews(string id) => News.FirstOrDefault(n => n.Id == id);

    /// <summary>How buzz changes general sales: under half with none, one and a half times and more when everyone is talking.</summary>
    public static double BuzzPull(double buzz) => 0.4 + (0.012 * Math.Clamp(buzz, 0, 100));

    /// <summary>How much of the whole bill's worth people will pay before any act is announced, after an ordinary year.</summary>
    public const double OnTrust = 0.85;

    /// <summary>How buzz hurries an act's fans along: three quarters as fast with none, a quarter faster when everyone is talking.</summary>
    public static double BuzzHurry(double buzz) => 0.75 + (0.005 * Math.Clamp(buzz, 0, 100));

    /// <summary>The share of the tickets people buy in a month on the festival's name alone, before buzz, bill and price are counted.</summary>
    public const double OnTheName = 0.06;

    /// <summary>
    /// The share of the tickets an act's own fans would buy if the act were announced: next to
    /// nothing for a local turn, most of the field for a household name.
    /// </summary>
    public static double FanShare(double pull) => 0.55 * Math.Pow(Math.Clamp(pull, 0, 1), 1.5);

    /// <summary>The share of an act's fans who buy in the month it is announced: the bigger the name, the faster they go.</summary>
    public static double FirstMonth(double pull) => 0.35 + (0.5 * Math.Clamp(pull, 0, 1));

    /// <summary>What buzz is, in a few words.</summary>
    public static string BuzzWords(double buzz) => buzz switch
    {
        < 15 => "Nobody has heard of it",
        < 35 => "A few people know",
        < 55 => "People are talking",
        < 80 => "The talk of the county",
        _ => "Everyone is talking",
    };
}

/// <summary>The campaign as written to a save file.</summary>
public sealed record CampaignSave(double Buzz, int JobsDone, List<string> Announced, int ReleaseOpen, Dictionary<string, double> Fans, List<string> Fresh,
    Dictionary<string, int> Stunts, List<string> NewsSeen, string? News, bool NewsAnswered, double SalesFactor, int Given, List<CampaignNote> Notes, List<int> SoldOut);
