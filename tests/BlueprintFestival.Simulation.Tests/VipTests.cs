using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>VIP tickets, lanyards, VIP ground, the lounge, the viewing platform and stalls kept for VIPs.</summary>
public class VipTests
{
    private static readonly DateTime Day = GameClock.DefaultStart.Date;

    private static FestivalGame Year(int licence = 1500) =>
        FestivalGame.StartYear("Larkmoor Festival", new YearSetup(4, licence, 60000, false, 80));

    /// <summary>What a VIP day saw, sampled every game minute.</summary>
    private sealed record Seen(FestivalGame Game, int OutsidersOnVipGround, int OutsidersAtVipBar, int VipsAtVipBar, int VipsInLounge, int VipsOnDeck, int VipsWatchingFromDeck, double DeckHeight);

    /// <summary>
    /// A village day with a stage, a VIP area (ground, a platform looking at the stage and a lounge
    /// behind it), a bar kept for VIPs, an ordinary bar and toilets, and thirty VIP guests.
    /// </summary>
    private static Seen VipDay(bool lounge = true, bool platform = true, bool vipBar = true, bool ground = true, int vips = 30, double untilHour = 19, int seed = 11)
    {
        var game = new FestivalGame("Larkmoor VIP", seed: seed);
        game.SetWeather(DayWeather.Of(WeatherKind.Fine, 3));
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.Bandstand, f.X + 34, f.Y + 14, Facing.South).Ok);
        if (ground)
        {
            Assert.True(game.VipGround(new TilePoint(f.X + 4, f.Y + 30), new TilePoint(f.X + 27, f.Y + 61), true).Ok);
        }

        if (platform)
        {
            Assert.True(game.BuildStall(StallKind.VipPlatform, f.X + 10, f.Y + 32, Facing.North).Ok);
        }

        if (lounge)
        {
            Assert.True(game.BuildStall(StallKind.VipLounge, f.X + 8, f.Y + 48, Facing.North).Ok);
        }

        Assert.True(game.BuildStall(StallKind.Bar, f.X + 36, f.Y + 50, Facing.South).Ok);
        var bar = game.Site.Stalls.Count - 1;
        Assert.True(game.BuildStall(StallKind.Bar, f.X + 54, f.Y + 50, Facing.South).Ok);
        Assert.True(game.BuildStall(StallKind.Toilets, f.X + 70, f.Y + 40, Facing.West).Ok);
        Assert.True(game.BuildStall(StallKind.Toilets, f.X + 70, f.Y + 52, Facing.West).Ok);
        if (vipBar)
        {
            Assert.True(game.Site.SetVipOnly(bar, true));
        }

        game.Crowd.VipTickets = vips;
        var deck = game.Site.Stalls.FirstOrDefault(s => s.Kind == StallKind.VipPlatform) is { } p ? game.Site.InsideOf(p).ToHashSet() : new HashSet<TilePoint>();
        var loungeFloor = game.Site.Stalls.FirstOrDefault(s => s.Kind == StallKind.VipLounge) is { } l ? game.Site.InsideOf(l).ToHashSet() : new HashSet<TilePoint>();
        int outsiders = 0, outsidersAtBar = 0, vipsAtBar = 0, inLounge = 0, onDeck = 0, watching = 0;
        var height = 0.0;
        var until = Day.AddHours(untilHour);
        var tick = 0;
        while (game.Now < until)
        {
            game.Clock.Step();
            if (++tick % 60 != 0)
            {
                continue;
            }

            foreach (var party in game.Crowd.Parties.Where(q => q.IsOnSite))
            {
                var tile = FieldMap.TileAt(party.X, party.Y);
                if (!party.Vip)
                {
                    outsiders += game.Field.IsVipOnly(tile) ? 1 : 0;
                    outsidersAtBar += vipBar && party.Stall == bar && party.State is PartyState.Queuing or PartyState.AtStall ? 1 : 0;
                    continue;
                }

                vipsAtBar += party.Stall == bar && party.State is PartyState.Queuing or PartyState.AtStall ? 1 : 0;
                inLounge += loungeFloor.Contains(tile) ? 1 : 0;
                if (deck.Contains(tile))
                {
                    onDeck++;
                    height = Math.Max(height, game.Site.HeightAt(tile));
                    watching += party.State == PartyState.Watching ? 1 : 0;
                }
            }
        }

        return new Seen(game, outsiders, outsidersAtBar, vipsAtBar, inLounge, onDeck, watching, height);
    }

    private static readonly Lazy<Seen> Full = new(() => VipDay());

    [Fact]
    public void One_ticket_in_ten_is_a_VIP_ticket_sold_alongside_the_rest_at_its_own_price()
    {
        var game = Year();
        game.SetTicketPrice(12);
        Assert.Equal(36, game.VipTicketPrice);
        Assert.False(game.VipPriceSet);
        Assert.True(game.BookEvent());
        Assert.Equal((int)(game.OnSale * 0.1), game.VipOnSale);
        game.SetAdvert("flyers", true);
        game.SetVipTicketPrice(40);
        Assert.Equal(40, game.VipTicketPrice);
        Assert.True(game.VipPriceSet);

        var money = game.Money;
        while (game.NextSalesTurn is not null)
        {
            Assert.NotNull(game.RunSales());
        }

        Assert.InRange(game.VipTicketsSold, 1, game.VipOnSale);
        Assert.True(game.TicketsSold - game.VipTicketsSold <= game.OnSale - game.VipOnSale);
        Assert.Equal(game.VipTicketsSold * 40, game.VipTakings);
        Assert.Equal(game.VipTakings, game.Income.Where(l => l.What == "VIP tickets").Sum(l => l.Amount));
        Assert.Equal(game.SalesLog.Sum(l => l.Vips), game.VipTicketsSold);

        // Build week: the crowd coming is the tickets sold, with the VIPs among them in lanyards.
        Assert.Equal(game.VipTicketsSold, game.Crowd.Vips);
        Assert.All(game.Crowd.Parties, p => Assert.All(p.Members, m => Assert.Equal(p.Vip, m.Look.Lanyard is not null)));
        Assert.Equal(game.VipTicketsSold, game.Crowd.Parties.Where(p => p.Vip).Sum(p => p.Members.Count));
    }

    [Fact]
    public void A_dear_VIP_ticket_sells_fewer_and_a_fair_one_sells_more()
    {
        int Sold(int vipPrice)
        {
            var game = Year();
            Assert.True(game.BookEvent());
            game.SetAdvert("flyers", true);
            game.SetVipTicketPrice(vipPrice);
            for (var m = 0; m < 3; m++)
            {
                game.RunSales();
            }

            return game.VipTicketsSold;
        }

        var fair = (int)Math.Round(Year().VipFairPrice);
        Assert.True(Sold(fair) > Sold(fair * 3));
        Assert.True(Sold(Math.Max(1, fair / 2)) >= Sold(fair));
    }

    [Fact]
    public void A_year_paid_for_at_the_gate_has_no_VIP_tickets()
    {
        var game = FestivalGame.NewFestival("Larkmoor Festival");
        Assert.Equal(0, game.VipOnSale);
        Assert.Equal(0, game.Crowd.Vips);
        Assert.All(game.Crowd.Parties, p => Assert.False(p.Vip));
    }

    [Fact]
    public void VIP_ground_keeps_everyone_but_VIPs_and_staff_off_and_takes_over_no_customers_marks()
    {
        var game = new FestivalGame("Larkmoor Festival", seed: 3);
        var f = game.Field.Field;
        var a = new TilePoint(f.X + 10, f.Y + 10);
        var b = new TilePoint(f.X + 13, f.Y + 12);
        Assert.True(game.KeepOut(a, b, true).Ok);
        Assert.True(game.VipGround(a, b, true).Ok);
        Assert.Equal(12, game.Site.VipGround.Count);
        Assert.Empty(game.Site.KeepOut);
        Assert.False(game.Field.IsWalkable(a.X, a.Y));
        Assert.True(game.Field.IsWalkableForVip(a));
        Assert.True(game.Field.IsWalkableForStaff(a));

        // Marking "no customers" over it takes the VIP mark off again.
        Assert.True(game.KeepOut(a, a, true).Ok);
        Assert.False(game.Site.IsVipGround(a));
        Assert.Equal(11, game.Site.VipGround.Count);

        // It is kept in the save.
        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal(game.Site.VipGround.OrderBy(p => p.Y).ThenBy(p => p.X), loaded.Site.VipGround.OrderBy(p => p.Y).ThenBy(p => p.X));
        Assert.False(loaded.Field.IsWalkable(b.X, b.Y));

        // Clearing it opens it to everyone.
        Assert.True(game.VipGround(b, b, false).Ok);
        Assert.True(game.Field.IsWalkable(b.X, b.Y));
        Assert.False(game.VipGround(b, b, false).Ok);
    }

    [Fact]
    public void Any_stall_but_a_bin_can_be_kept_for_VIPs_and_the_VIP_buildings_always_are()
    {
        var game = new FestivalGame("Larkmoor Festival", seed: 3);
        var f = game.Field.Field;
        Assert.True(game.BuildStall(StallKind.Burgers, f.X + 10, f.Y + 10, Facing.South).Ok);
        Assert.True(game.BuildStall(StallKind.Toilets, f.X + 30, f.Y + 10, Facing.South).Ok);
        Assert.True(game.BuildStall(StallKind.Bin, f.X + 40, f.Y + 10, Facing.South).Ok);
        Assert.True(game.BuildStall(StallKind.VipLounge, f.X + 10, f.Y + 30, Facing.South).Ok);
        Assert.True(game.Site.SetVipOnly(0, true));
        Assert.True(game.Site.SetVipOnly(1, true));
        Assert.False(game.Site.SetVipOnly(2, true));
        Assert.False(game.Site.SetVipOnly(3, false));
        Assert.True(game.Site.Stalls[0].ForVips && game.Site.Stalls[1].ForVips && game.Site.Stalls[3].ForVips);
        Assert.False(game.Site.Stalls[2].ForVips);
        Assert.Equal(StallGroup.Vip, Catalogue.GroupOf(StallKind.VipLounge));
        Assert.Equal(new[] { StallKind.VipLounge, StallKind.VipPlatform }, Catalogue.StallsIn(StallGroup.Vip).Select(s => s.Kind));

        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.True(loaded.Site.Stalls[0].VipOnly);
        Assert.False(loaded.Site.Stalls[2].VipOnly);
    }

    [Fact]
    public void People_walk_into_the_lounge_and_up_onto_the_platform_but_not_through_their_walls()
    {
        var game = new FestivalGame("Larkmoor Festival", seed: 3);
        var f = game.Field.Field;
        Assert.True(game.BuildStall(StallKind.VipLounge, f.X + 10, f.Y + 30, Facing.South).Ok);
        Assert.True(game.BuildStall(StallKind.VipPlatform, f.X + 40, f.Y + 30, Facing.East).Ok);
        var lounge = game.Site.Stalls[0];
        var platform = game.Site.Stalls[1];

        // The bar across the back and the walls are solid; the floor is for VIPs.
        Assert.False(game.Field.IsWalkableForVip(SiteLayout.Local(lounge, 8, 1)));
        Assert.False(game.Field.IsWalkableForVip(SiteLayout.Local(lounge, 0, 6)));
        Assert.True(game.Field.IsWalkableForVip(SiteLayout.Local(lounge, 8, 6)));
        Assert.False(game.Field.IsWalkable(SiteLayout.Local(lounge, 8, 6).X, SiteLayout.Local(lounge, 8, 6).Y));
        Assert.True(game.Field.IsWalkableForVip(SiteLayout.Local(lounge, 7, 11)));

        // The lounge is served across its bar, inside the marquee.
        var (fx, fy, _, _) = SiteLayout.Front(lounge.ServeRect, lounge.Facing);
        Assert.Equal((f.Y + 30 + SiteLayout.LoungeBarDepth) * FieldMap.TileMetres, fy, 3);
        Assert.Equal((f.X + 18) * FieldMap.TileMetres, fx, 3);

        // The platform: steps at the back, a deck up high with a rail round it.
        Assert.True(game.Field.IsWalkableForVip(SiteLayout.Local(platform, 5, 0)));
        Assert.True(game.Site.HeightAt(SiteLayout.Local(platform, 5, 0)) is > 0 and < SiteLayout.PlatformHeight);
        Assert.Equal(SiteLayout.PlatformHeight, game.Site.HeightAt(SiteLayout.Local(platform, 5, 4)));
        Assert.False(game.Field.IsWalkableForVip(SiteLayout.Local(platform, 5, 7)));
        Assert.False(game.Field.IsWalkableForVip(SiteLayout.Local(platform, 0, 4)));
        Assert.True(game.Field.IsWalkable(SiteLayout.Local(platform, 1, 0).X, SiteLayout.Local(platform, 1, 0).Y));
        Assert.Equal(40, game.Site.InsideOf(platform).Count());
        Assert.Equal(0, game.Site.HeightAt(new TilePoint(f.X + 2, f.Y + 2)));

        var vipRoute = new PathFinder(game.Field, vip: true).FindPath(new TilePoint(f.X + 60, f.Y + 60), SiteLayout.Local(platform, 5, 4));
        Assert.NotNull(vipRoute);
        Assert.Contains(vipRoute!, t => t == SiteLayout.Local(platform, 5, 0) || t == SiteLayout.Local(platform, 6, 0));
        Assert.Null(new PathFinder(game.Field).FindPath(new TilePoint(f.X + 60, f.Y + 60), SiteLayout.Local(platform, 5, 4)));
    }

    [Fact]
    public void Nobody_without_a_lanyard_goes_on_VIP_ground_or_uses_a_VIP_stall()
    {
        var seen = Full.Value;
        Assert.Equal(0, seen.OutsidersOnVipGround);
        Assert.Equal(0, seen.OutsidersAtVipBar);
        Assert.True(seen.VipsAtVipBar > 0, "VIP guests should use the bar kept for them.");
        Assert.Equal(30, seen.Game.Crowd.Vips);
    }

    [Fact]
    public void VIP_guests_spend_time_in_the_lounge_and_watch_from_the_platform()
    {
        var seen = Full.Value;
        Assert.True(seen.VipsInLounge > 0, "VIP guests should go into the lounge.");
        Assert.True(seen.VipsWatchingFromDeck > 0, "VIP guests should watch the stage from the platform.");
        Assert.Equal(SiteLayout.PlatformHeight, seen.DeckHeight);
        var lounge = seen.Game.Site.Stalls.Select((s, i) => (s, i)).First(t => t.s.Kind == StallKind.VipLounge).i;
        Assert.True(seen.Game.SalesOf(lounge).Items > 0, "The lounge bar should sell to VIPs.");
    }

    [Fact]
    public void VIP_guests_are_happier_with_VIP_things_than_with_nothing_for_them()
    {
        var with = Full.Value.Game.VipDay!.Value;
        var without = VipDay(lounge: false, platform: false, vipBar: false, ground: false).Game.VipDay!.Value;
        Assert.Equal(30, with.Guests);
        Assert.True(with.Happiness > without.Happiness + 5, $"With VIP things {with.Happiness}, without {without.Happiness}.");
    }
}
