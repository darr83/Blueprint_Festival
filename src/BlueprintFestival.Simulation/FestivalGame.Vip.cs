namespace BlueprintFestival.Simulation;

/// <summary>
/// VIP tickets: up to one ticket in ten can be a VIP ticket, sold alongside the ordinary ones at
/// its own price. VIP guests wear a lanyard and may use VIP ground, the VIP lounge, the viewing
/// platform and any stall kept for VIPs.
/// </summary>
public sealed partial class FestivalGame
{
    /// <summary>The share of all the tickets on sale that are VIP tickets.</summary>
    public const double VipShare = 0.1;

    /// <summary>A VIP ticket starts at this many times the ordinary price.</summary>
    public const int VipTimes = 3;

    /// <summary>The dearest a VIP ticket can be.</summary>
    public const int MaxVipTicketPrice = 250;

    private int? _vipPrice;

    /// <summary>VIP tickets sold so far. They are counted in <see cref="TicketsSold"/> too.</summary>
    public int VipTicketsSold { get; private set; }

    /// <summary>How many VIP tickets there are to sell: a tenth of all the tickets, when tickets are sold ahead.</summary>
    public int VipOnSale => SoldAhead ? (int)Math.Floor(OnSale * VipShare) : 0;

    /// <summary>VIP tickets still to sell.</summary>
    public int VipLeft => Math.Max(0, VipOnSale - VipTicketsSold);

    /// <summary>Ordinary tickets still to sell: everything on sale that is not kept for VIPs.</summary>
    public int OrdinaryLeft => Math.Max(0, OnSale - VipOnSale - (TicketsSold - VipTicketsSold));

    /// <summary>What a VIP ticket costs: three times the ordinary price until the player sets it.</summary>
    public int VipTicketPrice => _vipPrice ?? Math.Min(MaxVipTicketPrice, TicketPrice * VipTimes);

    /// <summary>True once the player has set the VIP price themselves, so it no longer follows the ordinary price.</summary>
    public bool VipPriceSet => _vipPrice is not null;

    /// <summary>The VIP price this line-up is worth: three times the fair ordinary price.</summary>
    public double VipFairPrice => FairPrice * VipTimes;

    /// <summary>How the VIP price changes VIP sales: 1 at the fair VIP price, more below it, falling away above it.</summary>
    public double VipPricePull => PricePullAt(VipFairPrice / VipTicketPrice);

    /// <summary>About how many VIP tickets the next turn would sell, before luck.</summary>
    public int VipForecast => NextSalesTurn is null ? 0 : Math.Min(VipLeft, (int)Math.Round(VipDemand(SalesTurn)));

    /// <summary>How many VIP tickets a turn would sell: about one buyer in nine, at the VIP price.</summary>
    private double VipDemand(int turn) => VipOnSale == 0 ? 0 : Interest(turn) * VipShare * 1.1 * VipPricePull;

    /// <summary>Sets the VIP ticket price in whole pounds. VIP tickets already sold keep what was paid for them.</summary>
    public void SetVipTicketPrice(int pounds)
    {
        if (Phase is not (Phase.Plan or Phase.Sell or Phase.Build))
        {
            throw new InvalidOperationException("The VIP price is set before the gates open.");
        }

        _vipPrice = Math.Clamp(pounds, 1, MaxVipTicketPrice);
    }

    /// <summary>How many VIP guests came today and how happy they are, out of 100, or null with no VIPs.</summary>
    public (int Guests, int Happiness)? VipDay
    {
        get
        {
            var vips = Crowd.Parties.Where(p => p.Vip && p.Ticketed is not null).ToList();
            if (vips.Count == 0)
            {
                return null;
            }

            var people = vips.Sum(p => p.Members.Count);
            return (people, (int)Math.Round(100 * vips.Sum(p => p.Mood * p.Members.Count) / people));
        }
    }
}
