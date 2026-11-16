using BlueprintFestival.Simulation.Site;

namespace BlueprintFestival.Simulation;

/// <summary>What one stall has sold today: how many of each thing on its menu, and what it has taken in pounds.</summary>
public sealed record StallSales(IReadOnlyList<int> Sold, int Takings)
{
    public int Items => Sold.Sum();
}

public sealed partial class FestivalGame
{
    private int _sharePence;

    private readonly Dictionary<(int X, int Y), (int[] Sold, int Takings)> _stallSales = new();

    /// <summary>
    /// True while the prices on the stalls can still be changed: on the build day, and on the day
    /// itself until the gates open. After that the menu boards are up and the prices stay.
    /// </summary>
    public bool CanSetPrices => Phase == Phase.Build || (Phase == Phase.Day && GatesShut);

    /// <summary>How many people have turned away from the stalls today because of the prices.</summary>
    public int PricedOut => Crowd.PricedOut;

    /// <summary>What the stalls have taken today, before the festival's share is worked out.</summary>
    public int StallTakings => _stallSales.Values.Sum(s => s.Takings);

    /// <summary>How many things the stalls have sold today.</summary>
    public int ItemsSold => _stallSales.Values.Sum(s => s.Sold.Sum());

    /// <summary>
    /// Sets what a stall charges for one thing on its menu, in whole pounds. The price is kept
    /// between £1 and three times the usual price. Returns the price it ended up at, or null if
    /// the prices can no longer be changed or there is no such stall or thing.
    /// </summary>
    public int? SetPrice(int stall, int item, int price) => CanSetPrices ? Site.SetPrice(stall, item, price) : null;

    /// <summary>Keeps a stall for VIP guests only, or opens it to everyone, while the prices can still be changed.</summary>
    public bool SetVipOnly(int stall, bool vipOnly) => CanSetPrices && Site.SetVipOnly(stall, vipOnly);

    /// <summary>
    /// Tells a bar not to serve anyone who is drunk, or to serve everyone again. Unlike the prices,
    /// this can be changed at any time, in the build or during the day.
    /// </summary>
    public bool SetNoDrunks(int stall, bool noDrunks) => Phase is Phase.Build or Phase.Day && Site.SetNoDrunks(stall, noDrunks);

    /// <summary>Puts everything on a stall's menu back to its usual price.</summary>
    public bool ResetPrices(int stall)
    {
        if (!CanSetPrices || stall < 0 || stall >= Site.Stalls.Count)
        {
            return false;
        }

        var menu = Catalogue.MenuOf(Site.Stalls[stall].Kind);
        for (var i = 0; i < menu.Count; i++)
        {
            Site.SetPrice(stall, i, menu[i].UsualPrice);
        }

        return menu.Count > 0;
    }

    /// <summary>What a stall has sold so far today.</summary>
    public StallSales SalesOf(int stall)
    {
        if (stall < 0 || stall >= Site.Stalls.Count)
        {
            return new StallSales(Array.Empty<int>(), 0);
        }

        var placed = Site.Stalls[stall];
        return _stallSales.TryGetValue((placed.X, placed.Y), out var sales)
            ? new StallSales(sales.Sold.ToArray(), sales.Takings)
            : new StallSales(new int[Catalogue.MenuOf(placed.Kind).Count], 0);
    }

    /// <summary>
    /// What the customers make of a stall's prices, in a few plain words: how many of the people
    /// expected would pay for at least one thing on its menu.
    /// </summary>
    public int WouldBuy(int stall)
    {
        if (stall < 0 || stall >= Site.Stalls.Count || Crowd.Parties.Count == 0)
        {
            return 100;
        }

        var placed = Site.Stalls[stall];
        var people = 0;
        var willing = 0;
        foreach (var party in Crowd.Parties)
        {
            people += party.Members.Count;
            willing += Crowd.WorthIt(party, placed) ? party.Members.Count : 0;
        }

        return people == 0 ? 100 : (int)Math.Round(100.0 * willing / people);
    }

    private void NoteSale(PlacedStall stall, int item, int price)
    {
        var count = Catalogue.MenuOf(stall.Kind).Count;
        if (!_stallSales.TryGetValue((stall.X, stall.Y), out var sales) || sales.Sold.Length != count)
        {
            sales = (new int[count], 0);
        }

        if (item >= 0 && item < count)
        {
            sales.Sold[item]++;
        }

        _stallSales[(stall.X, stall.Y)] = (sales.Sold, sales.Takings + price);
    }

    /// <summary>The line of the day's takings a stall's pitch money goes on.</summary>
    public static string TakingsLine(StallKind kind) => Catalogue.GroupOf(kind) switch
    {
        StallGroup.Food => "Food stalls",
        StallGroup.Drink => "Drink stalls",
        StallGroup.Other => "Ponchos, hats and merchandise",
        StallGroup.Fair => "Fairground",
        _ => Catalogue.Of(kind).Name,
    };

    private List<StallSalesSave> SaveStallSales() => _stallSales.Select(s => new StallSalesSave(s.Key.X, s.Key.Y, s.Value.Sold.ToList(), s.Value.Takings)).ToList();

    private void RestoreStallSales(List<StallSalesSave>? saved, int pricedOut)
    {
        _stallSales.Clear();
        foreach (var s in saved ?? new())
        {
            _stallSales[(s.X, s.Y)] = ((s.Sold ?? new()).ToArray(), s.Takings);
        }

        Crowd.PricedOut = Math.Max(0, pricedOut);
    }
}

/// <summary>One stall's sales as written to a save file.</summary>
public sealed record StallSalesSave(int X, int Y, List<int>? Sold, int Takings);
