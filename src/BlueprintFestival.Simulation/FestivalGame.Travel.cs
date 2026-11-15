using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation;

/// <summary>How the crowd is planned to get to the festival, in people.</summary>
public readonly record struct TravelSplit(int OnFoot, int ByCar, int ByBus, int Cars);

/// <summary>
/// Getting there. A couple of hundred villagers walk. Everyone else needs a space in a car park
/// field or a seat on a shuttle bus from the station, and someone with no way to get there does
/// not buy a ticket. The festival sets the parking charge and hires the buses.
/// </summary>
public sealed partial class FestivalGame
{
    /// <summary>How many live near enough to walk.</summary>
    public const int Walkers = 250;

    /// <summary>People to a car, on average.</summary>
    public const double PeopleACar = Crowd.PeopleACar;

    /// <summary>How many one shuttle bus brings over the day, going back and forth to the station.</summary>
    public const int BusSeats = 150;

    /// <summary>What a shuttle bus costs for the day.</summary>
    public const int BusCost = 120;

    public const int MostBuses = 40;

    /// <summary>Drivers pay up to this without a murmur. Each pound over it sends some looking for a bus.</summary>
    public const int FairParking = 5;

    public const int DearestParking = 20;

    /// <summary>What a car pays to park, in pounds.</summary>
    public int ParkingCharge { get; private set; } = 3;

    /// <summary>Shuttle buses hired for the day.</summary>
    public int BusesHired { get; private set; }

    /// <summary>The share of people with a car who will pay the parking charge.</summary>
    public double DriversWilling => Math.Clamp(1 - (0.06 * Math.Max(0, ParkingCharge - FairParking)), 0.2, 1);

    /// <summary>How many people the car parks can bring at this charge.</summary>
    public int CanDrive => Plan is null ? 0 : (int)(Plan.ParkingSpaces * PeopleACar * DriversWilling);

    /// <summary>How many people have a way to get to the festival: on foot, by car or by bus.</summary>
    public int CanGetHere => Plan is null ? int.MaxValue : Walkers + CanDrive + (BusesHired * BusSeats);

    /// <summary>Sets the parking charge, any time before the gates open.</summary>
    public void SetParkingCharge(int pounds)
    {
        NeedBeforeGates();
        ParkingCharge = Math.Clamp(pounds, 0, DearestParking);
        ApplyTravel();
    }

    /// <summary>Hires so many shuttle buses, any time before the gates open. They are paid for on the day.</summary>
    public void HireBuses(int buses)
    {
        NeedBeforeGates();
        BusesHired = Math.Clamp(buses, 0, MostBuses);
        ApplyTravel();
    }

    /// <summary>Keeps the parking charge and the buses of another year, so they need not be set again each January.</summary>
    private FestivalGame TravellingLike(FestivalGame other)
    {
        ParkingCharge = other.ParkingCharge;
        BusesHired = other.BusesHired;
        CampingPrice = other.CampingPrice;
        Crowd.CamperShare = CamperShare;
        ApplyTravel();
        return this;
    }

    private void NeedBeforeGates()
    {
        if (Phase is not (Phase.Plan or Phase.Sell or Phase.Build))
        {
            throw new InvalidOperationException("Travel is arranged before the gates open.");
        }
    }

    /// <summary>How a crowd of so many would travel as things stand: walkers first, then cars, then buses, and anyone left walks a long way.</summary>
    public TravelSplit TravelFor(int people)
    {
        if (Plan is null)
        {
            return new TravelSplit(people, 0, 0, 0);
        }

        var onFoot = Math.Min(people, Walkers);
        var byCar = Math.Min(people - onFoot, CanDrive);
        var byBus = Math.Min(people - onFoot - byCar, BusesHired * BusSeats);
        return new TravelSplit(people - byCar - byBus, byCar, byBus, (int)Math.Ceiling(byCar / PeopleACar));
    }

    /// <summary>Tells the crowd how each party travels. Called whenever the crowd, the charge or the buses change.</summary>
    private void ApplyTravel()
    {
        if (Plan is null)
        {
            return;
        }

        var split = TravelFor(Crowd.Expected);
        Crowd.SetTravel(Math.Min(split.OnFoot, Walkers), split.ByCar, split.ByBus, Field.ParkingBays());
    }

    /// <summary>Pays for the buses on the morning of the day. Buses the bank cannot cover are sent back.</summary>
    private void PayForBuses()
    {
        if (Plan is null || BusesHired == 0)
        {
            return;
        }

        BusesHired = Math.Min(BusesHired, Math.Max(0, Money) / BusCost);
        ApplyTravel();
        if (BusesHired > 0)
        {
            Spend("Shuttle buses", BusesHired * BusCost);
        }
    }
}
