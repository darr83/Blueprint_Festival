using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;

namespace BlueprintFestival.Simulation;

/// <summary>
/// Ways to camp: bring your own tent, a tent ready pitched, a campervan on hardstanding, or
/// glamping in a bell tent. Each dearer way is its own ticket with its own price and its own
/// buyers, and needs its own ground marked in the camping field. Whatever is marked is set up on
/// the day (tents put up, gravel laid, bell tents hired), sold or not. Glamping guests pay a lot
/// and get cross fast if the showers queue in the morning.
/// </summary>
public sealed partial class FestivalGame
{
    /// <summary>What glamping guests lose when the morning shower queue is longer than ten minutes.</summary>
    public const double GlampersKeptWaiting = 0.08;

    private readonly int[] _kindPrices = { CampingFee, CampLayout.UsualPrice(CampKind.ReadyPitched), CampLayout.UsualPrice(CampKind.Campervan), CampLayout.UsualPrice(CampKind.Glamping) };

    /// <summary>What a party pays for one of the dearer ways to camp. (Bring your own tent is <see cref="CampingPrice"/>, for each person.)</summary>
    public int CampKindPrice(CampKind kind) => kind == CampKind.OwnTent ? CampingPrice : _kindPrices[(int)kind];

    /// <summary>Sets the price of a dearer way to camp, any time before the gates open.</summary>
    public void SetCampKindPrice(CampKind kind, int pounds)
    {
        if (kind == CampKind.OwnTent)
        {
            SetCampingPrice(pounds);
            return;
        }

        if (Phase is not (Phase.Plan or Phase.Sell or Phase.Build))
        {
            throw new InvalidOperationException("Camping prices are set before the gates open.");
        }

        _kindPrices[(int)kind] = Math.Clamp(pounds, 0, CampLayout.DearestPrice(kind));
        Crowd.SetKindShare(kind, CampLayout.ShareAt(kind, _kindPrices[(int)kind]));
    }

    /// <summary>How many pitches of a dearer kind the marked ground holds just now.</summary>
    public int ExtraPitches(CampKind kind) => Crowd.OpenPitches().Count(p => p.Zone == CampLayout.ZoneFor(kind));

    /// <summary>How many parties have booked each way to camp, going by who would pitch where today.</summary>
    public int Booked(CampKind kind) => Crowd.PlanPitches().Count(p => CampLayout.KindOn(Site.CampZoneAt(p.Pitch)) == kind);

    /// <summary>How many parties want a dearer way to camp at today's price, room or not.</summary>
    public int Wanting(CampKind kind) => Crowd.Parties.Count(p => Crowd.WouldCamp(p) && Crowd.BoughtKind(p) == kind);

    /// <summary>A camper has come through the gate: their pitch is paid for, by the person for their own tent and by the party for the rest.</summary>
    private void PayForPitch(Party party)
    {
        var kind = Crowd.KindOf(party);
        if (kind == CampKind.OwnTent)
        {
            Earn("Camping", party.Members.Count * CampingPrice);
            return;
        }

        Earn(TakingsFor(kind), CampKindPrice(kind));
    }

    /// <summary>The money line a dearer way to camp's takings go under.</summary>
    public static string TakingsFor(CampKind kind) => kind switch
    {
        CampKind.ReadyPitched => "Ready-pitched tents",
        CampKind.Campervan => "Campervan pitches",
        CampKind.Glamping => "Glamping",
        _ => "Camping",
    };

    /// <summary>On the morning of the festival the marked ground is set up: tents put up, gravel laid and bell tents made up.</summary>
    private void SetUpCampExtras()
    {
        foreach (var kind in CampLayout.Extras)
        {
            var pitches = ExtraPitches(kind);
            if (pitches > 0)
            {
                Spend(kind switch
                {
                    CampKind.ReadyPitched => "Tents put up for ready pitched",
                    CampKind.Campervan => "Hardstanding for campervans",
                    _ => "Bell tents for glamping",
                }, pitches * CampLayout.SetUpCost(kind));
            }
        }
    }

    /// <summary>The camp checks' lines for the dearer ways to camp: how many booked, the room, and what setting up costs.</summary>
    private IEnumerable<CampCheck> ExtraChecks(List<(Party Party, World.TilePoint Pitch)> plan)
    {
        foreach (var kind in CampLayout.Extras)
        {
            var room = ExtraPitches(kind);
            var wanting = Wanting(kind);
            if (room == 0 && wanting == 0)
            {
                continue;
            }

            var booked = plan.Count(p => CampLayout.KindOn(Site.CampZoneAt(p.Pitch)) == kind);
            var cost = room * CampLayout.SetUpCost(kind);
            var text = room == 0
                ? $"{CampLayout.KindName(kind)}: {wanting:N0} would book at £{CampKindPrice(kind)}. Mark ground for it"
                : $"{CampLayout.KindName(kind)}: {booked:N0} booked of {room:N0} pitches at £{CampKindPrice(kind)} (£{cost:N0} to set up)";
            var state = room == 0 || wanting > room ? CampCheckState.Short : CampCheckState.Met;
            yield return new CampCheck(state, text);
        }
    }

    private List<int>? SaveCampKindPrices() => _kindPrices.Skip(1).SequenceEqual(CampLayout.Extras.Select(CampLayout.UsualPrice)) ? null : _kindPrices.Skip(1).ToList();

    private void RestoreCampKindPrices(List<int>? prices)
    {
        for (var i = 0; i < CampLayout.Extras.Length; i++)
        {
            var kind = CampLayout.Extras[i];
            _kindPrices[(int)kind] = Math.Clamp(prices is { } set && i < set.Count ? set[i] : CampLayout.UsualPrice(kind), 0, CampLayout.DearestPrice(kind));
            Crowd.SetKindShare(kind, CampLayout.ShareAt(kind, _kindPrices[(int)kind]));
        }
    }
}
