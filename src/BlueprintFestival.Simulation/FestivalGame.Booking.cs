using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;

namespace BlueprintFestival.Simulation;

/// <summary>
/// Booking the acts. Every artist asks a fee; the festival can pay it or offer less and risk a
/// no. The bigger names have other festivals after them and are gone as the months pass, they
/// cost more the later they are booked, and they will only play a stage that is big enough.
/// </summary>
public sealed partial class FestivalGame
{
    private readonly Dictionary<string, int> _agreed = new();
    private readonly Dictionary<string, int> _refusals = new();
    private readonly List<string> _noShows = new();

    /// <summary>The acts that would not play on the day because their stage was too small, in the order they were due on.</summary>
    public IReadOnlyList<string> NoShows => _noShows;

    /// <summary>How many months of ticket sales have gone by, for what an artist asks and who is still free.</summary>
    private int BookingMonth => SoldAhead ? Math.Min(SalesTurn, SalesMonths.Count) : 0;

    /// <summary>What an artist asks to play: more for more stars, more at a bigger festival, and more for every month after January.</summary>
    public int AskOf(Artist artist)
    {
        ArgumentNullException.ThrowIfNull(artist);
        return (int)Math.Round(FeeOf(artist.Rating) * (1 + (Booking.DearerAMonth * BookingMonth)));
    }

    /// <summary>What an artist is paid: the fee agreed with them, or what they ask if nothing has been agreed.</summary>
    public int FeeFor(string artist) =>
        _agreed.TryGetValue(artist, out var fee) ? fee : Roster.Named(artist) is { } found ? AskOf(found) : 0;

    /// <summary>What an act booked for a set is paid.</summary>
    public int FeeFor(Act act)
    {
        ArgumentNullException.ThrowIfNull(act);
        return _agreed.TryGetValue(act.Name, out var fee) ? fee : FeeOf(act.Rating);
    }

    /// <summary>How many other festivals are asking after an artist.</summary>
    public int RivalsFor(Artist artist) => Booking.Rivals(artist, Seed + Year);

    /// <summary>The last month an artist can still be booked (January is 0), or null if they are free all year.</summary>
    public int? GoneAfter(Artist artist)
    {
        ArgumentNullException.ThrowIfNull(artist);
        return Booking.GoneAfter(artist, Seed + Year);
    }

    /// <summary>Whether an artist can be booked now.</summary>
    public Availability AvailabilityOf(Artist artist)
    {
        ArgumentNullException.ThrowIfNull(artist);
        if (SetOf(artist.Name) is not null)
        {
            return Availability.Booked;
        }

        if (_refusals.GetValueOrDefault(artist.Name) >= Booking.Patience)
        {
            return Availability.WalkedAway;
        }

        return GoneAfter(artist) is { } last && BookingMonth > last ? Availability.Gone : Availability.Free;
    }

    /// <summary>How many times an artist has turned the festival down this year.</summary>
    public int RefusalsFrom(string artist) => _refusals.GetValueOrDefault(artist);

    /// <summary>The chance an artist would say yes to an offer.</summary>
    public double ChanceOfYes(Artist artist, int offer) =>
        _agreed.TryGetValue(artist.Name, out var agreed) && offer >= agreed ? 1 : Booking.ChanceOfYes(offer, AskOf(artist), RivalsFor(artist));

    /// <summary>True if an act with so many stars would take a set: a four or five star act only plays one of the last two on a stage.</summary>
    public static bool WouldPlaySet(int stars, int set) =>
        stars < 4 || (set >= RunningOrder.SecondBase ? set - RunningOrder.SecondBase : set) >= Booking.EarliestSetForABigName;

    /// <summary>True while acts can be booked: in January, and into empty sets while tickets are on sale.</summary>
    public bool CanBookNow => Phase == Phase.Plan || (Phase == Phase.Sell && SoldAhead);

    /// <summary>
    /// Offers an artist a fee to play a set. At what they ask or more it is a deal. For less they may
    /// say no, and a second no sends them to another festival. Once tickets are on sale only an
    /// empty set can be filled, and the fee is paid there and then.
    /// </summary>
    public OfferOutcome Offer(int set, string artist, int pounds)
    {
        var place = set >= RunningOrder.SecondBase ? set - RunningOrder.SecondBase : set;
        if (!CanBookNow || Roster.Named(artist) is not { } found || place < 0 || place >= RunningOrder.SetsAStage
            || (set >= RunningOrder.SecondBase && !SecondShow.Booked) || pounds < 0)
        {
            return OfferOutcome.NotPossible;
        }

        var late = Phase != Phase.Plan;
        if (late && (StageOf(set).At(set) is not null || SetOf(artist) is not null))
        {
            return OfferOutcome.NotPossible;
        }

        var free = AvailabilityOf(found);
        if (free is Availability.Gone or Availability.WalkedAway)
        {
            return OfferOutcome.NotFree;
        }

        if (!WouldPlaySet(found.Rating, set))
        {
            return OfferOutcome.WantsLaterSet;
        }

        var ask = AskOf(found);
        var fee = Math.Min(pounds, _agreed.TryGetValue(artist, out var before) ? before : ask);
        if (late && fee > Math.Max(0, Money))
        {
            return OfferOutcome.NoMoney;
        }

        var tries = _refusals.GetValueOrDefault(artist);
        var chance = ChanceOfYes(found, fee);
        if (chance < 1 && GameRandom.FromSeed(Seed + Year, 700 + (tries * 31) + (int)(Booking.Roll(artist, Seed, 3) * 10000)).NextDouble() >= chance)
        {
            _refusals[artist] = tries + 1;
            return tries + 1 >= Booking.Patience ? OfferOutcome.WalkedAway : OfferOutcome.Refused;
        }

        _agreed[artist] = fee;
        if (late)
        {
            Place(set, found);
            Spend("Act fees", fee);
        }
        else if (!BookAct(set, artist))
        {
            return OfferOutcome.NotPossible;
        }

        return OfferOutcome.Accepted;
    }

    /// <summary>Empties every set on both stages, so the whole line-up is the player's to book. Only in January.</summary>
    public void ClearBill()
    {
        Need(Phase.Plan);
        foreach (var show in new[] { Show, SecondShow })
        {
            for (var i = 0; i < RunningOrder.SetsAStage; i++)
            {
                show.Book(show.Base + i, null);
            }
        }

        SecondShow.Booked = false;
        Crowd.Replan(ExpectedAt(TicketPrice));
    }

    /// <summary>The size of stage a stage's line-up needs: the size its biggest name will play. Null with nobody booked.</summary>
    public StageSize? StageNeeded(bool second)
    {
        var show = second ? SecondShow : Show;
        return !show.Booked || show.Slots.Count == 0 ? null : Booking.StageFor(show.Slots.Max(s => s.Act.Rating));
    }

    /// <summary>
    /// The acts who will not play as things stand in build week, with why: their stage is too small
    /// for them, or there is no stage for them at all.
    /// </summary>
    public IReadOnlyList<string> StageProblems
    {
        get
        {
            var problems = new List<string>();
            if (!SoldAhead)
            {
                return problems;
            }

            foreach (var show in new[] { Show, SecondShow })
            {
                if (!show.Booked || show.Slots.Count == 0)
                {
                    continue;
                }

                var which = show.Second ? "second stage" : "main stage";
                if (show.Stage is not { } stage)
                {
                    problems.Add(show.Second && Site.Stages.Count == 1
                        ? $"The second stage's acts have no stage. They play the next biggest stage you put up."
                        : $"The {which}'s acts have no stage yet.");
                    continue;
                }

                var info = Catalogue.Of(stage.Kind);
                foreach (var slot in show.Slots.Where(s => !Booking.WillPlay(s.Act.Rating, stage.Kind)))
                {
                    var wants = Booking.StageFor(slot.Act.Rating);
                    problems.Add($"{slot.Act.Name} will not play the {info.Name.ToLowerInvariant()}. They want a {Catalogue.Name(wants).ToLowerInvariant()} stage (from £{Booking.CheapestOf(wants).Cost:N0}).");
                }
            }

            return problems;
        }
    }

    /// <summary>On the morning of the festival, any act whose stage is too small for them goes home. Their fee is not given back.</summary>
    private void SendHomeActsWithNoStage()
    {
        // A year sold in one go at the gate (the quick way) books whoever it likes for any stage.
        if (!SoldAhead)
        {
            return;
        }

        foreach (var show in new[] { Show, SecondShow })
        {
            if (!show.Booked || show.Stage is not { } stage)
            {
                continue;
            }

            foreach (var slot in show.Slots.Where(s => !Booking.WillPlay(s.Act.Rating, stage.Kind)).ToList())
            {
                _noShows.Add(slot.Act.Name);
                show.Book(slot.Index, null);
                Note(DayNoteKind.CalledOff, $"{slot.Act.Name} would not play", $"The {Catalogue.Of(stage.Kind).Name.ToLowerInvariant()} was too small for them", DayTone.Bad);
            }
        }
    }

    /// <summary>The mood lost by everyone arriving for each star of an act that will not be playing after all.</summary>
    public const double NoShowMoodAStar = 0.012;

    /// <summary>How much the mood people arrive in is down because acts they paid to see are not playing.</summary>
    public double NoShowMood => _noShows.Sum(n => (Roster.Named(n)?.Rating ?? 1) * NoShowMoodAStar);

    private BookingSave? SaveBooking() =>
        _agreed.Count == 0 && _refusals.Count == 0 && _noShows.Count == 0 ? null : new BookingSave(new(_agreed), new(_refusals), _noShows.ToList());

    private void RestoreBooking(BookingSave? booking)
    {
        _agreed.Clear();
        _refusals.Clear();
        _noShows.Clear();
        if (booking is null)
        {
            return;
        }

        foreach (var (name, fee) in booking.Agreed ?? new())
        {
            _agreed[name] = Math.Max(0, fee);
        }

        foreach (var (name, times) in booking.Refusals ?? new())
        {
            _refusals[name] = Math.Max(0, times);
        }

        _noShows.AddRange(booking.NoShows ?? new());
    }
}
