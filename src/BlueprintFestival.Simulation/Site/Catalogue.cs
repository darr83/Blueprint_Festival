namespace BlueprintFestival.Simulation.Site;

/// <summary>The kinds of small stage a village festival can put up.</summary>
public enum StageKind
{
    /// <summary>A farm trailer with straw bales: nearly free, room for a duo.</summary>
    FarmTrailer,

    /// <summary>A wooden deck under a striped canvas roof: the village fete bandstand.</summary>
    Bandstand,

    /// <summary>A hired stage with a steel frame, a roof, lights and proper speakers.</summary>
    HiredStage,

    MainStage,
}

/// <summary>The kinds of fence.</summary>
public enum FenceKind
{
    /// <summary>Wooden stakes and rope.</summary>
    Rope,

    /// <summary>Chestnut paling: waist-high slats on wire.</summary>
    Paling,

    /// <summary>Low steel crowd barriers that hook together.</summary>
    CrowdBarrier,

    /// <summary>Tall steel mesh panels in blocks.</summary>
    SteelMesh,
}

/// <summary>The kinds of way in.</summary>
public enum GateKind
{
    /// <summary>Two posts, a painted board and a table to check tickets at.</summary>
    VillageGate,

    /// <summary>A steel arch with a banner, two lanes and a ticket hut.</summary>
    ArchGate,

    TurnstileRow,
}

/// <summary>The stalls and services people use. New kinds go on the end, so old saves still load.</summary>
public enum StallKind
{
    /// <summary>A little wooden hut with a fryer: fries in a cone.</summary>
    Fries,

    /// <summary>A catering van with a hatch: burgers.</summary>
    Burgers,

    /// <summary>A striped marquee with a wooden bar across the front.</summary>
    Bar,

    /// <summary>Three portable cabins in a row.</summary>
    Toilets,

    /// <summary>A standpipe with two taps. Free.</summary>
    WaterPoint,

    /// <summary>A white tent with a green cross, where people who are unwell can lie down.</summary>
    FirstAidTent,

    /// <summary>Nine toilet cabins in a row.</summary>
    ToiletBlock,

    /// <summary>A big white tent with a green cross and six beds, for a big festival.</summary>
    MedicalTent,

    /// <summary>A litter bin. People passing with rubbish in their hands mostly use it.</summary>
    Bin,

    /// <summary>A cart under a big umbrella with a giant hot dog on top.</summary>
    HotDogs,

    /// <summary>A gazebo with a domed brick oven.</summary>
    Pizza,

    /// <summary>A long counter under a red roof hung with paper lanterns.</summary>
    Noodles,

    /// <summary>A pastel van with a giant cone on the roof.</summary>
    IceCream,

    /// <summary>A little cart with a coffee machine and a big cup on its roof.</summary>
    Coffee,

    /// <summary>A bright stand with two churning slush tanks.</summary>
    Slush,

    /// <summary>A thatched beach bar.</summary>
    Cocktails,

    /// <summary>A small stall hung with rain ponchos.</summary>
    Ponchos,

    /// <summary>A stall with bucket hats on pegs.</summary>
    BucketHats,

    /// <summary>A tent selling the festival's own T-shirts, posters and bags.</summary>
    Merchandise,

    /// <summary>A little tent with a wall of lockers, where a phone can be charged for a pound or two.</summary>
    PhoneCharging,
}

/// <summary>Which shelf of the build bar a stall is on.</summary>
public enum StallGroup
{
    Food,
    Drink,
    Other,
    Services,
}

/// <summary>What someone has in their hand after buying something.</summary>
public enum Carried
{
    None,
    HotDog,
    Burger,
    Fries,
    Pizza,
    Noodles,
    IceCream,
    Pint,
    Cup,
    Coffee,
    Slush,
    Cocktail,

    /// <summary>A carrier bag of merchandise. It is kept all day.</summary>
    Bag,
}

/// <summary>What buying something puts on the buyer, if anything.</summary>
public enum Worn
{
    None,
    Poncho,
    BucketHat,
}

/// <summary>
/// One thing on a stall's menu: its name, what people usually expect to pay for it, what the buyer
/// then carries or wears, how much of a hunger or thirst it answers (1 is a whole meal or a long
/// drink), and who it is for.
/// </summary>
public sealed record StallItem(string Name, int UsualPrice, Carried Gives, double Fills = 1, Worn Wears = Worn.None, bool AdultsOnly = false, bool ForChildren = false);

/// <summary>What a person can need during the day.</summary>
public enum Need
{
    Food,
    Drink,
    Toilet,
}

/// <summary>Which way something faces. A stage faces its audience.</summary>
public enum Facing
{
    South,
    West,
    North,
    East,
}

/// <summary>What there is to know about a kind of stage. Sizes are in half-metre tiles.</summary>
public sealed record StageInfo(StageKind Kind, string Name, int Width, int Depth, int Cost, int Performers, string About);

/// <summary>What there is to know about a kind of fence. The cost is for each metre.</summary>
public sealed record FenceInfo(FenceKind Kind, string Name, int CostPerMetre, string About);

/// <summary>What there is to know about a kind of gate: how wide, how many lanes, how fast each lane checks one person.</summary>
public sealed record GateInfo(GateKind Kind, string Name, int Width, int Lanes, int SecondsPerPerson, int Cost, string About);

/// <summary>
/// What there is to know about a kind of stall: its size in tiles, what it costs to hire, the need
/// it answers, how long each person takes to serve and how many are served at once. Price is no
/// longer used: what a stall charges is on its menu.
/// </summary>
public sealed record StallInfo(StallKind Kind, string Name, int Width, int Depth, int Cost, Need? Serves, int Price, int SecondsPerPerson, int Servers, string About);

/// <summary>Everything the player can build, with its size and what it costs to hire for the day.</summary>
/// <summary>The kinds of staff a festival hires for the day.</summary>
public enum StaffKind
{
    TicketStaff,
    Security,
    FirstAider,

    /// <summary>Picks up litter and empties the bins.</summary>
    Cleaner,
}

/// <summary>One kind of staff: what they are called, what they cost for the day, and what they do.</summary>
public sealed record StaffInfo(StaffKind Kind, string Name, int Wage, string About);

public static class Catalogue
{
    public static readonly IReadOnlyList<StageInfo> Stages = new[]
    {
        new StageInfo(StageKind.FarmTrailer, "Farm trailer", 16, 6, 50, 2, "Lent by the farmer for the price of the diesel. Room for a duo."),
        new StageInfo(StageKind.Bandstand, "Bandstand", 12, 11, 400, 4, "A deck under a striped roof. Room for a small band."),
        new StageInfo(StageKind.HiredStage, "Hired stage", 24, 12, 1500, 5, "A proper stage with a roof, lights and big speakers."),
        new StageInfo(StageKind.MainStage, "Main stage", 40, 20, 6000, 6, "A festival main stage: a big roof, a wall of speakers and room for any band."),
    };

    public static readonly IReadOnlyList<FenceInfo> Fences = new[]
    {
        new FenceInfo(FenceKind.Rope, "Rope and stakes", 1, "Marks a line. Cheap and friendly."),
        new FenceInfo(FenceKind.Paling, "Chestnut paling", 3, "Waist-high wooden slats on wire."),
        new FenceInfo(FenceKind.CrowdBarrier, "Crowd barrier", 5, "Steel barriers for queues and the front of a stage."),
        new FenceInfo(FenceKind.SteelMesh, "Steel mesh fence", 8, "Tall panels: the proper festival fence."),
    };

    public static readonly IReadOnlyList<GateInfo> Gates = new[]
    {
        new GateInfo(GateKind.VillageGate, "Village gate", 8, 1, 30, 40, "Two posts and a ticket table. One queue."),
        new GateInfo(GateKind.ArchGate, "Arch gate", 12, 2, 10, 600, "A steel arch with two lanes and a ticket hut."),
        new GateInfo(GateKind.TurnstileRow, "Turnstile row", 24, 4, 6, 2000, "Four fast lanes under one long arch, for when hundreds arrive at once."),
    };

    public static readonly IReadOnlyList<StallInfo> Stalls = new[]
    {
        new StallInfo(StallKind.Fries, "Fries hut", 6, 6, 70, Need.Food, 0, 35, 1, "Fries in a cone from a little wooden hut. A snack, not a meal."),
        new StallInfo(StallKind.Burgers, "Burger van", 12, 5, 120, Need.Food, 0, 45, 2, "Burgers from a hatch. Serves two at a time."),
        new StallInfo(StallKind.Bar, "Bar tent", 12, 8, 200, Need.Drink, 0, 25, 3, "Beer, cider, wine and soft drinks from a long bar. Serves three at a time."),
        new StallInfo(StallKind.Toilets, "Toilets", 8, 3, 40, Need.Toilet, 0, 75, 3, "Three cabins. People will not wait for ever."),
        new StallInfo(StallKind.WaterPoint, "Water point", 3, 2, 15, Need.Drink, 0, 20, 2, "Two taps. Free, so it earns nothing."),
        new StallInfo(StallKind.FirstAidTent, "First aid tent", 8, 6, 80, null, 0, 1200, 2, "Two beds under a green cross. First-aiders bring people to the nearest free bed to rest."),
        new StallInfo(StallKind.ToiletBlock, "Toilet block", 24, 3, 110, Need.Toilet, 0, 75, 9, "Nine cabins in a row, for a bigger crowd."),
        new StallInfo(StallKind.MedicalTent, "Medical tent", 16, 10, 450, null, 0, 600, 6, "Six beds under a green cross. People are back on their feet in half the time."),
        new StallInfo(StallKind.Bin, "Litter bin", 2, 2, 10, null, 0, 0, 0, "Most people with rubbish in their hands use a bin if one is within a dozen steps. It holds 40 pieces, then a cleaner has to empty it."),
        new StallInfo(StallKind.HotDogs, "Hot dog cart", 6, 5, 60, Need.Food, 0, 35, 1, "Hot dogs from a cart under a big umbrella. Quick and cheap."),
        new StallInfo(StallKind.Pizza, "Pizza oven", 8, 8, 140, Need.Food, 0, 50, 2, "Slices straight from a wood-fired oven. Serves two at a time."),
        new StallInfo(StallKind.Noodles, "Noodle bar", 10, 6, 130, Need.Food, 0, 45, 2, "Boxes of noodles under paper lanterns. Serves two at a time."),
        new StallInfo(StallKind.IceCream, "Ice cream van", 10, 5, 90, Need.Food, 0, 25, 1, "Cones and lollies. A treat rather than a meal, and busiest on a hot day."),
        new StallInfo(StallKind.Coffee, "Coffee cart", 6, 5, 70, Need.Drink, 0, 40, 1, "Coffee, tea and hot chocolate. Busiest in the morning and in the rain."),
        new StallInfo(StallKind.Slush, "Slush stand", 6, 5, 60, Need.Drink, 0, 20, 1, "Iced slush and lemonade. Children love it, and so does everyone on a hot day."),
        new StallInfo(StallKind.Cocktails, "Cocktail bar", 10, 7, 180, Need.Drink, 0, 40, 2, "A thatched beach bar. Grown-ups' drinks, and a fruit punch for everyone else."),
        new StallInfo(StallKind.Ponchos, "Poncho stall", 6, 5, 40, null, 0, 12, 1, "Rain ponchos. Nobody looks until it rains, then everybody wants one."),
        new StallInfo(StallKind.BucketHats, "Hat stall", 6, 6, 50, null, 0, 30, 1, "Bucket hats. They sell best in hot sun."),
        new StallInfo(StallKind.Merchandise, "Merchandise tent", 10, 8, 150, null, 0, 40, 2, "The festival's own T-shirts, posters and bags. People who are having a good day take something home."),
        new StallInfo(StallKind.PhoneCharging, "Phone charging tent", 6, 6, 120, null, 0, 30, 2, "Lockers to charge a phone in. More people want one as the day goes on."),
    };

    private static readonly IReadOnlyList<StallItem> NothingToSell = Array.Empty<StallItem>();

    private static readonly Dictionary<StallKind, IReadOnlyList<StallItem>> Menus = new()
    {
        [StallKind.HotDogs] = new[]
        {
            new StallItem("Hot dog", 4, Carried.HotDog),
            new StallItem("Chilli dog", 5, Carried.HotDog),
            new StallItem("Veggie dog", 4, Carried.HotDog),
        },
        [StallKind.Burgers] = new[]
        {
            new StallItem("Burger", 6, Carried.Burger),
            new StallItem("Cheeseburger", 7, Carried.Burger),
            new StallItem("Veggie burger", 6, Carried.Burger),
        },
        [StallKind.Fries] = new[]
        {
            new StallItem("Fries", 3, Carried.Fries, 0.55),
            new StallItem("Cheesy fries", 4, Carried.Fries, 0.65),
            new StallItem("Loaded fries", 5, Carried.Fries, 0.8),
        },
        [StallKind.Pizza] = new[]
        {
            new StallItem("Margherita slice", 4, Carried.Pizza, 0.9),
            new StallItem("Pepperoni slice", 5, Carried.Pizza, 0.9),
            new StallItem("Veggie slice", 4, Carried.Pizza, 0.9),
        },
        [StallKind.Noodles] = new[]
        {
            new StallItem("Vegetable noodles", 6, Carried.Noodles),
            new StallItem("Chicken noodles", 7, Carried.Noodles),
            new StallItem("Spicy noodles", 7, Carried.Noodles),
        },
        [StallKind.IceCream] = new[]
        {
            new StallItem("Ice cream cone", 3, Carried.IceCream, 0.3),
            new StallItem("Double cone", 4, Carried.IceCream, 0.4),
            new StallItem("Ice lolly", 2, Carried.IceCream, 0.2),
        },
        [StallKind.Bar] = new[]
        {
            new StallItem("Pint of beer", 4, Carried.Pint, AdultsOnly: true),
            new StallItem("Pint of cider", 4, Carried.Pint, AdultsOnly: true),
            new StallItem("Glass of wine", 5, Carried.Cup, AdultsOnly: true),
            new StallItem("Soft drink", 2, Carried.Cup),
        },
        [StallKind.Coffee] = new[]
        {
            new StallItem("Coffee", 3, Carried.Coffee, 0.8),
            new StallItem("Tea", 2, Carried.Coffee, 0.8),
            new StallItem("Hot chocolate", 3, Carried.Coffee, 0.8),
        },
        [StallKind.Slush] = new[]
        {
            new StallItem("Small slush", 2, Carried.Slush, 0.7),
            new StallItem("Large slush", 3, Carried.Slush),
            new StallItem("Lemonade", 2, Carried.Cup),
        },
        [StallKind.Cocktails] = new[]
        {
            new StallItem("Sunset spritz", 7, Carried.Cocktail, AdultsOnly: true),
            new StallItem("Minty cooler", 7, Carried.Cocktail, AdultsOnly: true),
            new StallItem("Fruit punch", 4, Carried.Cocktail),
        },
        [StallKind.Ponchos] = new[]
        {
            new StallItem("Rain poncho", 3, Carried.None, 0, Worn.Poncho),
            new StallItem("Heavy-duty poncho", 5, Carried.None, 0, Worn.Poncho),
            new StallItem("Child's poncho", 2, Carried.None, 0, Worn.Poncho, ForChildren: true),
        },
        [StallKind.BucketHats] = new[]
        {
            new StallItem("Bucket hat", 6, Carried.None, 0, Worn.BucketHat),
            new StallItem("Patterned bucket hat", 8, Carried.None, 0, Worn.BucketHat),
            new StallItem("Child's bucket hat", 4, Carried.None, 0, Worn.BucketHat, ForChildren: true),
        },
        [StallKind.PhoneCharging] = new[]
        {
            new StallItem("Quick top-up", 2, Carried.None, 0),
            new StallItem("Full charge", 4, Carried.None, 0, AdultsOnly: true),
            new StallItem("Power bank to borrow", 6, Carried.None, 0, AdultsOnly: true),
        },
        [StallKind.Merchandise] = new[]
        {
            new StallItem("Festival T-shirt", 15, Carried.Bag, 0),
            new StallItem("Tote bag", 8, Carried.Bag, 0),
            new StallItem("Poster", 6, Carried.Bag, 0),
            new StallItem("Programme", 4, Carried.Bag, 0),
        },
    };

    /// <summary>What a kind of stall sells. Empty for toilets, taps, tents and bins.</summary>
    public static IReadOnlyList<StallItem> MenuOf(StallKind kind) => Menus.TryGetValue(kind, out var menu) ? menu : NothingToSell;

    /// <summary>True for a stall that sells things and so has prices to set.</summary>
    public static bool Sells(StallKind kind) => Menus.ContainsKey(kind);

    /// <summary>True for a stall people queue at: anything that sells, the toilets and the taps.</summary>
    public static bool HasQueue(StallKind kind) => Of(kind).Serves is not null || Sells(kind);

    /// <summary>The lowest and the highest price the player can put on something, from its usual price.</summary>
    public static int LowestPrice(StallItem item) => 1;

    public static int HighestPrice(StallItem item) => item.UsualPrice * 3;

    /// <summary>Which shelf of the build bar a stall is on.</summary>
    public static StallGroup GroupOf(StallKind kind) => kind switch
    {
        StallKind.HotDogs or StallKind.Burgers or StallKind.Fries or StallKind.Pizza or StallKind.Noodles or StallKind.IceCream => StallGroup.Food,
        StallKind.Bar or StallKind.Coffee or StallKind.Slush or StallKind.Cocktails => StallGroup.Drink,
        StallKind.Ponchos or StallKind.BucketHats or StallKind.Merchandise or StallKind.PhoneCharging => StallGroup.Other,
        _ => StallGroup.Services,
    };

    /// <summary>The stalls on one shelf, in the order they are offered.</summary>
    public static IEnumerable<StallInfo> StallsIn(StallGroup group) => ShelfOrder.Where(k => GroupOf(k) == group).Select(Of);

    private static readonly StallKind[] ShelfOrder =
    {
        StallKind.HotDogs, StallKind.Burgers, StallKind.Fries, StallKind.Pizza, StallKind.Noodles, StallKind.IceCream,
        StallKind.Bar, StallKind.Coffee, StallKind.Slush, StallKind.Cocktails,
        StallKind.Ponchos, StallKind.BucketHats, StallKind.Merchandise, StallKind.PhoneCharging,
        StallKind.Toilets, StallKind.ToiletBlock, StallKind.WaterPoint, StallKind.Bin,
    };

    /// <summary>The festival's share of what a stall takes, in pounds out of every hundred.</summary>
    public const int PitchShare = 25;

    /// <summary>What one person pays at the gate.</summary>
    public const int TicketPrice = 5;

    public static StallInfo Of(StallKind kind) => Stalls[(int)kind];

    public static readonly IReadOnlyList<StaffInfo> Staff = new[]
    {
        new StaffInfo(StaffKind.TicketStaff, "Ticket staff", 40, "Checks tickets at a gate. One for each lane."),
        new StaffInfo(StaffKind.Security, "Security", 60, "Walks a beat and deals with trouble. Behind a front barrier each one holds another 8 m of crowd."),
        new StaffInfo(StaffKind.FirstAider, "First-aider", 50, "Goes to anyone taken ill or hurt and looks after them."),
        new StaffInfo(StaffKind.Cleaner, "Cleaner", 35, "Walks the field picking up litter, and empties the bins when they fill."),
    };

    public static StaffInfo Of(StaffKind kind) => Staff[(int)kind];

    public static StageInfo Of(StageKind kind) => Stages[(int)kind];

    public static FenceInfo Of(FenceKind kind) => Fences[(int)kind];

    public static GateInfo Of(GateKind kind) => Gates[(int)kind];
}
