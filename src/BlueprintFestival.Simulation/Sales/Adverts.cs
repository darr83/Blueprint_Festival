namespace BlueprintFestival.Simulation.Sales;

/// <summary>One way of advertising the festival: what it costs each month it runs and how many people it reaches.</summary>
public sealed record Advert(string Id, string Name, int Cost, int Reach, string Reaches);

/// <summary>Every way there is to advertise. All of them are there from the first year; the dear ones only pay at a big festival.</summary>
public static class Adverts
{
    public static readonly IReadOnlyList<Advert> All = new[]
    {
        new Advert("posters", "Posters in shop windows", 5, 150, "Village"),
        new Advert("newsletter", "Parish newsletter", 4, 100, "Village"),
        new Advert("email", "Email to last year's crowd", 5, 400, "People who came before"),
        new Advert("flyers", "Flyers through doors", 10, 250, "Village and nearby"),
        new Advert("banners", "Roadside banners", 15, 300, "Passing drivers"),
        new Advert("website", "Festival website", 25, 350, "Anyone searching"),
        new Advert("paper", "Local paper advert", 40, 600, "Town"),
        new Advert("social", "Social media adverts", 60, 900, "Town and county"),
        new Advert("street", "Street team at gigs", 80, 800, "Music fans"),
        new Advert("bus", "Bus adverts", 150, 1500, "Town"),
        new Advert("localradio", "Local radio", 200, 2200, "County"),
        new Advert("magazine", "Music magazine", 300, 3000, "Music fans"),
        new Advert("regionaltv", "Regional TV", 900, 12000, "Region"),
        new Advert("nationalradio", "National radio", 1600, 25000, "Country"),
        new Advert("nationaltv", "National TV", 3500, 60000, "Country"),
    };

    public static Advert? Find(string id) => All.FirstOrDefault(a => a.Id == id);
}

/// <summary>What one month, or one day of build week, sold.</summary>
public sealed record SalesLine(string When, int Tickets, int Price, int AdCost, int Vips = 0, int VipPrice = 0)
{
    /// <summary>What the ordinary tickets and the VIP tickets took between them.</summary>
    public int Taken => (Tickets * Price) + (Vips * VipPrice);
}

/// <summary>Ticket selling as written to a save file.</summary>
public sealed record SalesSave(int Turn, int Sold, List<string> Adverts, List<SalesLine> Log, int VipSold = 0, int? VipPrice = null);
