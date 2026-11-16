using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Sales;
using BlueprintFestival.Simulation.Show;

namespace BlueprintFestival.Simulation;

/// <summary>
/// The ticket campaign. Tickets no longer sell by themselves: each month there is time for three
/// jobs, and it is the jobs that sell them. Announcing an act brings a rush of its fans, the bigger
/// the name the bigger and faster the rush. Tickets come in three releases the festival opens when
/// it chooses. Buzz lifts every sale and fades unless it is fed. Now and then the news wants an answer.
/// </summary>
public sealed partial class FestivalGame
{
    private readonly HashSet<string> _announced = new();
    private readonly Dictionary<string, double> _fans = new();
    private readonly HashSet<string> _fresh = new();
    private readonly Dictionary<string, int> _stunts = new();
    private readonly List<string> _newsSeen = new();
    private readonly List<CampaignNote> _campaignNotes = new();
    private readonly List<int> _soldOut = new();
    private int _jobsDone;
    private string? _news;
    private bool _newsAnswered;
    private double _salesFactor = 1;
    private int _given;

    /// <summary>True for a year whose tickets are sold by a campaign of jobs.</summary>
    public bool Campaigning { get; private set; }

    /// <summary>How much the festival is being talked about, from 0 to 100.</summary>
    public double Buzz { get; private set; }

    /// <summary>Which ticket release is on sale, counting from 0. Earlier ones stay on sale until they are gone.</summary>
    public int ReleaseOpen { get; private set; }

    /// <summary>The jobs there is still time for this month.</summary>
    public int JobsLeft => Campaigning && Phase == Phase.Sell ? Math.Max(0, Campaign.JobsAMonth - _jobsDone) : 0;

    /// <summary>The acts that have been announced.</summary>
    public IReadOnlyCollection<string> ActsAnnounced => _announced;

    /// <summary>What has happened in the campaign so far, oldest first.</summary>
    public IReadOnlyList<CampaignNote> CampaignNotes => _campaignNotes;

    /// <summary>Tickets given away in competitions. They are counted as sold.</summary>
    public int TicketsGiven => _given;

    private int OrdinaryOnSale => Math.Max(0, OnSale - VipOnSale);

    private int OrdinarySold => TicketsSold - VipTicketsSold;

    /// <summary>How many tickets a release holds.</summary>
    public int ReleaseSize(int release)
    {
        var sizes = Campaign.Releases.Select(r => (int)Math.Round(OrdinaryOnSale * r.Share)).ToArray();
        sizes[^1] = OrdinaryOnSale - sizes[..^1].Sum();
        return sizes[Math.Clamp(release, 0, sizes.Length - 1)];
    }

    /// <summary>How many of a release's tickets are sold: the releases fill in order.</summary>
    public int ReleaseSold(int release)
    {
        var before = Enumerable.Range(0, release).Sum(ReleaseSize);
        return Math.Clamp(OrdinarySold - before, 0, ReleaseSize(release));
    }

    /// <summary>Ordinary tickets that can be bought now: what is left of the releases opened so far.</summary>
    public int ReleaseLeft => !Campaigning ? OrdinaryLeft : Math.Max(0, Math.Min(OrdinaryLeft, Enumerable.Range(0, ReleaseOpen + 1).Sum(ReleaseSize) - OrdinarySold));

    /// <summary>True if there is another release still to open.</summary>
    public bool CanOpenRelease => JobsLeft > 0 && ReleaseOpen < Campaign.Releases.Count - 1;

    /// <summary>The acts booked that nobody has been told about yet.</summary>
    public IEnumerable<Slot> Secret => LineUp.Where(s => !_announced.Contains(s.Act.Name));

    private IEnumerable<Slot> Known => LineUp.Where(s => _announced.Contains(s.Act.Name));

    /// <summary>How strongly the acts announced so far sell tickets to people who are not their fans.</summary>
    public double AnnouncedPull => PullOf(Known.ToList());

    /// <summary>
    /// The price people think a ticket is worth: what the acts announced so far are worth, and never
    /// much less than the whole bill, because some buy on the festival's name alone, more of them after a good year.
    /// </summary>
    public double PerceivedFairPrice
    {
        get
        {
            if (!Campaigning)
            {
                return FairPrice;
            }

            var trust = LastHappiness is { } h ? Math.Clamp(0.6 + (0.5 * h / 100.0), 0.7, 1.05) : 0.85;
            return Math.Max(FairOf(Known.ToList()), Campaign.OnTrust * FairPrice * trust);
        }
    }

    private double CampaignPricePull => PricePullAt(PerceivedFairPrice / TicketPrice);

    /// <summary>How many of an act's fans would buy if it were announced: its share of the ordinary tickets.</summary>
    public int FansOf(Act act)
    {
        ArgumentNullException.ThrowIfNull(act);
        var second = Shows.Skip(1).Any(show => show.Slots.Any(s => s.Act.Name == act.Name));
        return (int)Math.Round(OrdinaryOnSale * Campaign.FanShare(FanPull.Of(act)) * (second ? 0.7 : 1));
    }

    /// <summary>About how many tickets announcing an act would sell in its first month, at today's price.</summary>
    public int AnnounceWorth(string artist) =>
        LineUp.FirstOrDefault(s => s.Act.Name == artist)?.Act is { } act
            ? (int)Math.Round(FansOf(act) * Math.Min(1, Campaign.FirstMonth(FanPull.Of(act)) * Campaign.BuzzHurry(Buzz) * PricePullAfter(act)))
            : 0;

    private double PricePullAfter(Act act)
    {
        var known = Known.ToList();
        if (LineUp.FirstOrDefault(s => s.Act.Name == act.Name) is { } slot && !known.Contains(slot))
        {
            known.Add(slot);
        }

        return PricePullAt(Math.Max(FairOf(known), PerceivedFairPrice) / TicketPrice);
    }

    private bool UseJob()
    {
        if (JobsLeft <= 0)
        {
            return false;
        }

        _jobsDone++;
        return true;
    }

    private void Lift(double buzz) => Buzz = Math.Clamp(Buzz + buzz, 0, 100);

    private void Jot(string what) => _campaignNotes.Add(new CampaignNote(SalesTurn, what));

    /// <summary>Announces an act: its fans start buying, buzz goes up, and a job is used. False with no job left or nobody of that name still secret.</summary>
    public bool Announce(string artist)
    {
        if (LineUp.FirstOrDefault(s => s.Act.Name == artist)?.Act is not { } act || _announced.Contains(artist) || !UseJob())
        {
            return false;
        }

        Reveal(act);
        Lift((Campaign.BuzzAStar * act.Rating) + (Show.Slots.LastOrDefault()?.Act.Name == artist ? 4 : 0));
        Jot($"{artist} announced");
        return true;
    }

    private void Reveal(Act act)
    {
        _announced.Add(act.Name);
        _fans[act.Name] = FansOf(act);
        _fresh.Add(act.Name);
    }

    /// <summary>Opens the next ticket release, which uses a job.</summary>
    public bool OpenRelease()
    {
        if (!CanOpenRelease || !UseJob())
        {
            return false;
        }

        ReleaseOpen++;
        Jot($"{Campaign.Releases[ReleaseOpen].Name} opened at £{TicketPrice}");
        return true;
    }

    /// <summary>What a stunt costs at this size of festival.</summary>
    public int StuntCost(Stunt stunt)
    {
        ArgumentNullException.ThrowIfNull(stunt);
        return Scaled(stunt.Cost);
    }

    /// <summary>True if a stunt can be done this month: it has not been done too lately and there is a job and the money for it.</summary>
    public bool CanDoStunt(string id) =>
        Campaign.FindStunt(id) is { } stunt && JobsLeft > 0 && StuntCost(stunt) <= Math.Max(0, Money) && StuntReadyIn(id) == 0;

    /// <summary>How many months until a stunt can be done again: 0 if it can be done now, -1 if not again this year.</summary>
    public int StuntReadyIn(string id)
    {
        if (Campaign.FindStunt(id) is not { } stunt || !_stunts.TryGetValue(id, out var last))
        {
            return 0;
        }

        return stunt.EveryMonths == 0 ? -1 : Math.Max(0, last + stunt.EveryMonths - SalesTurn);
    }

    /// <summary>Does a stunt: pays for it, lifts the buzz, and uses a job.</summary>
    public bool DoStunt(string id)
    {
        if (!CanDoStunt(id) || Campaign.FindStunt(id) is not { } stunt || !UseJob())
        {
            return false;
        }

        if (StuntCost(stunt) > 0)
        {
            Spend("Publicity", StuntCost(stunt));
        }

        var free = Math.Min(stunt.FreeTickets, ReleaseLeft);
        TicketsSold += free;
        _given += free;
        _stunts[id] = SalesTurn;
        Lift(stunt.Buzz);
        Jot(stunt.Name);
        return true;
    }

    /// <summary>Switches an advert on, which uses a job in a campaign. Switching one off is free with <see cref="SetAdvert"/>.</summary>
    public bool StartAdvert(string id)
    {
        if (Adverts.Find(id) is null || _adverts.Contains(id))
        {
            return false;
        }

        if (Campaigning && Phase == Phase.Sell && !UseJob())
        {
            return false;
        }

        _adverts.Add(id);
        return true;
    }

    /// <summary>
    /// Books another act into an empty set while tickets are on sale, which uses a job if they say
    /// yes. See <see cref="Offer"/> for what can come of it.
    /// </summary>
    public OfferOutcome OfferLate(int set, string artist, int pounds)
    {
        if (Campaigning && JobsLeft <= 0)
        {
            return OfferOutcome.NotPossible;
        }

        var outcome = Offer(set, artist, pounds);
        if (outcome == OfferOutcome.Accepted && Campaigning)
        {
            UseJob();
            Jot($"{artist} booked");
        }

        return outcome;
    }

    /// <summary>The news waiting for an answer this month, or null.</summary>
    public NewsItem? PendingNews => Campaigning && Phase == Phase.Sell && !_newsAnswered && _news is { } id ? Campaign.FindNews(id) : null;

    /// <summary>True if a choice can be taken: one that uses a job needs a job left.</summary>
    public bool CanAnswerNews(int choice) =>
        PendingNews is { } news && choice >= 0 && choice < news.Choices.Count && (!news.Choices[choice].UsesJob || JobsLeft > 0);

    /// <summary>Answers this month's news with one of its choices.</summary>
    public bool AnswerNews(int choice)
    {
        if (!CanAnswerNews(choice) || PendingNews is not { } news)
        {
            return false;
        }

        var picked = news.Choices[choice];
        if (picked.UsesJob)
        {
            UseJob();
        }

        if (picked.PriceCut > 0)
        {
            TicketPrice = Math.Max(1, TicketPrice - picked.PriceCut);
        }

        Lift(picked.Buzz);
        _salesFactor = picked.Sales;
        _newsAnswered = true;
        Jot($"{news.Headline}: {picked.Label.ToLowerInvariant()}");
        return true;
    }

    private void StartCampaign()
    {
        Campaigning = true;
        Buzz = Math.Clamp(15 + (LastHappiness is { } h ? (h - 50) * 0.4 : 0), 5, 40);
        ReleaseOpen = 0;
        _jobsDone = 0;
        _announced.Clear();
        _fans.Clear();
        _fresh.Clear();
        _stunts.Clear();
        _newsSeen.Clear();
        _campaignNotes.Clear();
        _soldOut.Clear();
        _news = null;
        _newsAnswered = false;
        _salesFactor = 1;
        _given = 0;
    }

    /// <summary>The fans who would buy this turn, act by act, before the price is counted.</summary>
    private Dictionary<string, double> FanDemand(int turn)
    {
        var week = turn >= SalesMonths.Count;
        var demand = new Dictionary<string, double>();
        foreach (var (name, left) in _fans)
        {
            var pull = LineUp.FirstOrDefault(s => s.Act.Name == name)?.Act is { } act ? FanPull.Of(act) : 0;
            demand[name] = left * Math.Min(1, (week ? Campaign.EachBuildDay : _fresh.Contains(name) ? Campaign.FirstMonth(pull) : Campaign.LaterMonths) * Campaign.BuzzHurry(Buzz));
        }

        return demand;
    }

    private double GeneralDemand(int turn)
    {
        var week = turn >= SalesMonths.Count;
        var word = LastHappiness is { } h ? 0.6 + (0.5 * h / 100.0) : 1.0;
        var heard = (OnSale * Campaign.OnTheName * word) + (Math.Pow(AdvertReach, 0.85) * 0.12);
        return heard * (0.5 + (0.5 * AnnouncedPull)) * Campaign.BuzzPull(Buzz) * Keenness * (week ? DayUrgency * ForecastPull : Urgency[turn]);
    }

    /// <summary>The fans who buy this turn, act by act, once the price is counted: never more than are still looking.</summary>
    private Dictionary<string, double> FanBuyers(int turn) =>
        FanDemand(turn).ToDictionary(f => f.Key, f => Math.Min(_fans[f.Key], f.Value * CampaignPricePull * _salesFactor));

    private double CampaignDemand(int turn) =>
        (GeneralDemand(turn) * CampaignPricePull * _salesFactor) + FanBuyers(turn).Values.Sum();

    /// <summary>What happens to the campaign once a turn's tickets are sold.</summary>
    private void AfterCampaignTurn(int turn, int tickets, double wanted)
    {
        // The fans who got a ticket are fans no longer looking for one. Those a sold-out release turned away are still looking.
        var served = wanted <= 0 ? 0 : Math.Min(1.0, tickets / wanted);
        foreach (var (name, buyers) in FanBuyers(turn))
        {
            _fans[name] = Math.Max(0, _fans[name] - (buyers * served));
        }

        _fresh.Clear();
        for (var release = 0; release <= ReleaseOpen; release++)
        {
            if (!_soldOut.Contains(release) && ReleaseSold(release) >= ReleaseSize(release) && ReleaseSize(release) > 0)
            {
                _soldOut.Add(release);
                Lift(Campaign.SoldOutBuzz);
                _campaignNotes.Add(new CampaignNote(turn, $"{Campaign.Releases[release].Name} sold out"));
            }
        }

        if (turn < SalesMonths.Count)
        {
            Buzz = Math.Clamp(Buzz - Campaign.BuzzFade + Math.Min(Campaign.MostAdvertBuzz, _adverts.Count), 0, 100);
        }

        _jobsDone = 0;
        _salesFactor = 1;
        _news = null;
        _newsAnswered = false;
        var next = turn + 1;
        if (next < SalesMonths.Count)
        {
            // Some months bring news that wants an answer.
            var random = GameRandom.FromSeed(Seed + Year, 800 + next);
            var unseen = Campaign.News.Where(n => !_newsSeen.Contains(n.Id)).ToList();
            if (unseen.Count > 0 && random.NextDouble() < 0.55)
            {
                _news = unseen[random.Next(unseen.Count)].Id;
                _newsSeen.Add(_news);
            }
        }
        else if (next == SalesMonths.Count)
        {
            // Build week: the posters are up with every name on them, and every ticket left is on sale.
            ReleaseOpen = Campaign.Releases.Count - 1;
            foreach (var slot in Secret.ToList())
            {
                Reveal(slot.Act);
            }

            _fresh.Clear();
        }
    }

    private CampaignSave? SaveCampaign() => !Campaigning ? null
        : new CampaignSave(Buzz, _jobsDone, _announced.OrderBy(a => a).ToList(), ReleaseOpen, new(_fans), _fresh.OrderBy(a => a).ToList(), new(_stunts),
            _newsSeen.ToList(), _news, _newsAnswered, _salesFactor, _given, _campaignNotes.ToList(), _soldOut.ToList());

    private void RestoreCampaign(CampaignSave? save)
    {
        if (save is null)
        {
            return;
        }

        StartCampaign();
        Buzz = Math.Clamp(save.Buzz, 0, 100);
        _jobsDone = Math.Clamp(save.JobsDone, 0, Campaign.JobsAMonth);
        _announced.UnionWith(save.Announced ?? new());
        ReleaseOpen = Math.Clamp(save.ReleaseOpen, 0, Campaign.Releases.Count - 1);
        foreach (var (name, left) in save.Fans ?? new())
        {
            _fans[name] = Math.Max(0, left);
        }

        _fresh.UnionWith(save.Fresh ?? new());
        foreach (var (id, turn) in save.Stunts ?? new())
        {
            _stunts[id] = turn;
        }

        _newsSeen.AddRange(save.NewsSeen ?? new());
        _news = save.News;
        _newsAnswered = save.NewsAnswered;
        _salesFactor = save.SalesFactor <= 0 ? 1 : save.SalesFactor;
        _given = Math.Max(0, save.Given);
        _campaignNotes.AddRange(save.Notes ?? new());
        _soldOut.AddRange(save.SoldOut ?? new());
    }
}
