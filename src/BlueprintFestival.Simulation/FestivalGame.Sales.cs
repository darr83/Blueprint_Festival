using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Sales;

namespace BlueprintFestival.Simulation;

/// <summary>
/// Selling tickets ahead. The acts are booked in January and the tickets go on sale. Each month
/// to June is one turn, then each of the seven days of build week. How many sell in a turn comes
/// from the adverts running (how many people hear), the line-up and the price (how many of them
/// buy), last year's word of mouth, and how close the day is.
/// </summary>
public sealed partial class FestivalGame
{
    /// <summary>The months tickets are on sale before build week.</summary>
    public static readonly IReadOnlyList<string> SalesMonths = new[] { "January", "February", "March", "April", "May", "June" };

    /// <summary>The days of build week, ending on the morning of the festival.</summary>
    public static readonly IReadOnlyList<string> BuildDays = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday morning" };

    /// <summary>How keen people are to buy in each month: it builds as July comes.</summary>
    private static readonly double[] Urgency = { 0.7, 0.7, 0.8, 0.9, 1.1, 1.3 };

    /// <summary>How keen people are to buy on one day of build week.</summary>
    private const double DayUrgency = 0.15;

    /// <summary>The dearest a ticket can be.</summary>
    public const int MaxTicketPrice = 60;

    /// <summary>Every turn of selling there is: six months and seven days.</summary>
    public static int SalesTurns => SalesMonths.Count + BuildDays.Count;

    private readonly HashSet<string> _adverts = new();
    private readonly List<SalesLine> _salesLog = new();

    /// <summary>True for a year that sells its tickets ahead, month by month.</summary>
    public bool SoldAhead { get; private set; }

    /// <summary>How many turns of selling are done: 0 in January before anything is sold, 13 when the gates are about to open.</summary>
    public int SalesTurn { get; private set; }

    /// <summary>Tickets sold so far.</summary>
    public int TicketsSold { get; private set; }

    /// <summary>What each turn sold, oldest first.</summary>
    public IReadOnlyList<SalesLine> SalesLog => _salesLog;

    /// <summary>What the next turn is called, or null when selling is over.</summary>
    public string? NextSalesTurn => SalesTurn < SalesMonths.Count ? SalesMonths[SalesTurn] : SalesTurn < SalesTurns ? BuildDays[SalesTurn - SalesMonths.Count] : null;

    public bool SoldOut => TicketsSold >= Licence;

    public bool AdvertOn(string id) => _adverts.Contains(id);

    /// <summary>Switches an advert on or off for the turns to come.</summary>
    public void SetAdvert(string id, bool on)
    {
        if (Adverts.Find(id) is null)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "No such advert.");
        }

        if (on)
        {
            _adverts.Add(id);
        }
        else
        {
            _adverts.Remove(id);
        }
    }

    /// <summary>How many people the adverts switched on reach in a month.</summary>
    public int AdvertReach => Adverts.All.Where(a => _adverts.Contains(a.Id)).Sum(a => a.Reach);

    /// <summary>What the adverts switched on cost for a month.</summary>
    public int AdvertCostAMonth => Adverts.All.Where(a => _adverts.Contains(a.Id)).Sum(a => a.Cost);

    /// <summary>What the adverts cost for the next turn: a month, or a day of build week at a sixth of that. Once every ticket is sold they stop.</summary>
    public int AdvertCost => SoldOut ? 0 : SalesTurn < SalesMonths.Count ? AdvertCostAMonth : (AdvertCostAMonth + 5) / 6;

    /// <summary>
    /// How strongly the line-up sells tickets, where 1 is an ordinary bill. The last act on the
    /// main stage counts most, by its stars and its following; every other act adds a little; and
    /// four or more kinds of music bring a wider crowd.
    /// </summary>
    public double LineUpPull
    {
        get
        {
            var head = Show.Slots.LastOrDefault()?.Act;
            var others = LineUp.Where(s => s.Act != head).Sum(s => s.Act.Rating);
            var genres = LineUp.Select(s => s.Act.Genre).Distinct().Count();
            var fame = head is null ? 0 : Math.Min(0.25, Math.Log10(Math.Max(100, head.Fans)) / 28);
            return 0.3 + (0.15 * (head?.Rating ?? 0)) + fame + Math.Min(0.35, 0.03 * others) + (genres >= 4 ? 0.05 : 0);
        }
    }

    /// <summary>The ticket price this line-up is worth. Dearer than this sells slowly; cheaper sells fast.</summary>
    public double FairPrice
    {
        get
        {
            var head = Show.Slots.LastOrDefault()?.Act;
            var others = LineUp.Where(s => s.Act != head).Sum(s => s.Act.Rating);
            return 2.5 + (1.2 * (head?.Rating ?? 0)) + (0.2 * Math.Min(12, others));
        }
    }

    /// <summary>How the price changes sales: 1 at the fair price, more below it, and falling away quickly above it.</summary>
    public double PricePull => PricePullAt(FairPrice / TicketPrice);

    private static double PricePullAt(double value) => value >= 1 ? Math.Min(1.8, Math.Pow(value, 1.5)) : Math.Max(0.03, Math.Pow(value, 2.5));

    /// <summary>
    /// How keen the people still without a ticket are, from 1 in January down to <see cref="LastToBuy"/>
    /// for the very last ticket: the keenest buy first, so the last few hundred take the most selling.
    /// </summary>
    public double Keenness => 1 - ((1 - LastToBuy) * Math.Min(1.0, (double)TicketsSold / Licence));

    /// <summary>How keen the last buyer is, next to the first.</summary>
    public const double LastToBuy = 0.45;

    /// <summary>What has been taken for the tickets sold ahead.</summary>
    public int TicketTakings => _salesLog.Sum(l => l.Tickets * l.Price);

    /// <summary>
    /// The mood people arrive in, from what they paid against what the line-up is worth: a bargain
    /// starts the day well and a dear ticket has them expecting a lot.
    /// </summary>
    public double ArrivalMood
    {
        get
        {
            if (!SoldAhead || TicketsSold == 0)
            {
                return People.Crowd.StartingMood;
            }

            var paid = (double)TicketTakings / TicketsSold;
            return People.Crowd.StartingMood + Math.Clamp(0.45 * ((FairPrice / Math.Max(1, paid)) - 1), -0.25, 0.08);
        }
    }

    /// <summary>Sets the ticket price in whole pounds. Tickets already sold keep what was paid for them.</summary>
    public void SetTicketPrice(int pounds)
    {
        if (Phase is not (Phase.Plan or Phase.Sell or Phase.Build))
        {
            throw new InvalidOperationException("The ticket price is set before the gates open.");
        }

        TicketPrice = Math.Clamp(pounds, 1, MaxTicketPrice);
        if (Phase == Phase.Plan)
        {
            Crowd.Replan(ExpectedAt(TicketPrice));
        }
    }

    /// <summary>About how many tickets the next turn would sell as things stand, before luck.</summary>
    public int SalesForecast => NextSalesTurn is null ? 0 : Math.Min(Licence - TicketsSold, (int)Math.Round(Demand(SalesTurn)));

    private double Demand(int turn)
    {
        var week = turn >= SalesMonths.Count;
        var word = LastHappiness is { } h ? 0.6 + (0.5 * h / 100.0) : 1.0;
        var heard = (Licence * 0.035 * word) + (Math.Pow(AdvertReach, 0.85) * 0.12);
        return heard * LineUpPull * PricePull * Keenness * (week ? DayUrgency * ForecastPull : Urgency[turn]);
    }

    /// <summary>
    /// Books the acts and puts the tickets on sale in January. False if the bank cannot cover
    /// what has to be paid now.
    /// </summary>
    public bool BookEvent()
    {
        Need(Phase.Plan);
        if (!PayForPlan())
        {
            return false;
        }

        SoldAhead = true;
        SalesTurn = 0;
        TicketsSold = 0;
        Crowd.Replan(10);
        Phase = Phase.Sell;
        return true;
    }

    /// <summary>
    /// Runs the next month, or the next day of build week: pays for the adverts and sells the
    /// tickets. Null if there is not the money for the adverts switched on, or nothing left to run.
    /// After June the build week begins.
    /// </summary>
    public SalesLine? RunSales()
    {
        if (!SoldAhead || NextSalesTurn is not { } when || Phase != (SalesTurn < SalesMonths.Count ? Phase.Sell : Phase.Build))
        {
            return null;
        }

        var cost = AdvertCost;
        if (cost > Math.Max(0, Money))
        {
            return null;
        }

        var luck = GameRandom.FromSeed(Seed, 40 + SalesTurn).Between(0.93, 1.07);
        var tickets = Math.Clamp((int)Math.Round(Demand(SalesTurn) * luck), 0, Licence - TicketsSold);
        if (cost > 0)
        {
            Spend("Advertising", cost);
        }

        Earn("Tickets", tickets * TicketPrice);
        TicketsSold += tickets;
        var line = new SalesLine(when, tickets, TicketPrice, cost);
        _salesLog.Add(line);
        SalesTurn++;
        if (SalesTurn >= SalesMonths.Count)
        {
            // From build week on, the crowd that will come is the crowd that has bought.
            ApplyPlan(Math.Max(10, TicketsSold));
            Phase = Phase.Build;
        }

        return line;
    }

    /// <summary>Sells whatever days of build week are left, without adverts if there is not the money for them.</summary>
    private void FinishSales()
    {
        while (SoldAhead && NextSalesTurn is not null)
        {
            if (RunSales() is null)
            {
                _adverts.Clear();
            }
        }
    }

    private SalesSave? SaveSales() => SoldAhead ? new SalesSave(SalesTurn, TicketsSold, _adverts.OrderBy(a => a).ToList(), _salesLog.ToList()) : null;

    private void RestoreSales(SalesSave? sales)
    {
        if (sales is null)
        {
            return;
        }

        SoldAhead = true;
        SalesTurn = Math.Clamp(sales.Turn, 0, SalesTurns);
        TicketsSold = Math.Clamp(sales.Sold, 0, Licence);
        _adverts.Clear();
        _adverts.UnionWith((sales.Adverts ?? new()).Where(a => Adverts.Find(a) is not null));
        _salesLog.Clear();
        _salesLog.AddRange(sales.Log ?? new());
    }
}
