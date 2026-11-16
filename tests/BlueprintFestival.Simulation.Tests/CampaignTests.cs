using BlueprintFestival.Simulation.Sales;
using BlueprintFestival.Simulation.Show;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>The ticket campaign: jobs, announcing acts, ticket releases, buzz, stunts and news.</summary>
public class CampaignTests
{
    private static FestivalGame OnSale(int licence = 1500, int bank = 40000)
    {
        var game = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(4, licence, bank, false, 80));
        game.SetTicketPrice((int)Math.Round(game.FairPrice));
        Assert.True(game.BookEvent(campaign: true));
        return game;
    }

    private static void RunToTheDay(FestivalGame game)
    {
        while (game.NextSalesTurn is not null && game.RunSales() is not null)
        {
        }
    }

    private static string Biggest(FestivalGame game) => game.Secret.OrderByDescending(s => FanPull.Of(s.Act)).First().Act.Name;

    [Fact]
    public void A_campaign_starts_with_three_jobs_one_release_open_and_every_act_a_secret()
    {
        var game = OnSale();
        Assert.True(game.Campaigning);
        Assert.Equal(3, game.JobsLeft);
        Assert.Equal(0, game.ReleaseOpen);
        Assert.Empty(game.ActsAnnounced);
        Assert.Equal(game.LineUp.Count(), game.Secret.ToList().Count);
        Assert.Equal(game.OnSale - game.VipOnSale, Enumerable.Range(0, 3).Sum(r => game.ReleaseSize(r)));
        Assert.Equal(game.ReleaseSize(0), game.ReleaseLeft);
    }

    [Fact]
    public void A_year_booked_the_old_way_has_no_campaign()
    {
        var game = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(4, 1500, 40000, false, 80));
        Assert.True(game.BookEvent());
        Assert.False(game.Campaigning);
        Assert.Equal(0, game.JobsLeft);
        Assert.False(game.Announce(game.LineUp.First().Act.Name));
    }

    [Fact]
    public void Tickets_hardly_sell_unless_the_festival_works_for_them()
    {
        var lazy = OnSale();
        RunToTheDay(lazy);

        var busy = OnSale();
        while (busy.Phase == Phase.Sell)
        {
            if (busy.PendingNews is not null)
            {
                busy.AnswerNews(0);
            }

            if (busy.ReleaseLeft < busy.ReleaseSize(0) / 2)
            {
                busy.OpenRelease();
            }

            while (busy.JobsLeft > 0 && busy.Secret.Any())
            {
                busy.Announce(Biggest(busy));
            }

            foreach (var stunt in Campaign.Stunts)
            {
                busy.DoStunt(stunt.Id);
            }

            Assert.NotNull(busy.RunSales());
        }

        RunToTheDay(busy);
        Assert.True(lazy.TicketsSold < lazy.OnSale * 0.4, $"A festival that does nothing sold {lazy.TicketsSold} of {lazy.OnSale}.");
        Assert.True(busy.TicketsSold > busy.OnSale * 0.7, $"A busy campaign sold only {busy.TicketsSold} of {busy.OnSale}.");
        Assert.True(busy.TicketsSold > lazy.TicketsSold * 2);
    }

    [Fact]
    public void Announcing_an_act_uses_a_job_lifts_the_buzz_and_brings_its_fans()
    {
        var quiet = OnSale();
        var loud = OnSale();
        var name = Biggest(loud);
        var buzz = loud.Buzz;
        Assert.True(loud.AnnounceWorth(name) > 0);
        Assert.True(loud.Announce(name));
        Assert.Equal(2, loud.JobsLeft);
        Assert.True(loud.Buzz > buzz);
        Assert.Contains(name, loud.ActsAnnounced);
        Assert.False(loud.Announce(name));
        Assert.Equal(2, loud.JobsLeft);
        Assert.True(loud.SalesForecast > quiet.SalesForecast * 3, $"{loud.SalesForecast} against {quiet.SalesForecast}");
        Assert.Contains(loud.CampaignNotes, n => n.What.Contains(name));
    }

    [Fact]
    public void The_bigger_the_name_the_more_fans_and_the_faster_they_buy()
    {
        var star = FanPull.Of(5, 2_000_000);
        var local = FanPull.Of(1, 300);
        Assert.True(Campaign.FanShare(star) > Campaign.FanShare(local) * 5);
        Assert.True(Campaign.FirstMonth(star) > Campaign.FirstMonth(local));
        var game = OnSale(5000);
        var acts = game.LineUp.Where(s => game.Show.Slots.Contains(s)).Select(s => s.Act).OrderBy(FanPull.Of).ToList();
        Assert.True(game.FansOf(acts[^1]) >= game.FansOf(acts[0]));
        Assert.True(Campaign.FanShare(1) > 0.5);
        Assert.True(Campaign.FanShare(0.1) < 0.05);
    }

    [Fact]
    public void Only_three_jobs_fit_in_a_month_and_the_next_month_brings_three_more()
    {
        var game = OnSale();
        Assert.True(game.DoStunt("interview"));
        Assert.True(game.DoStunt("press"));
        Assert.True(game.DoStunt("competition"));
        Assert.Equal(0, game.JobsLeft);
        Assert.False(game.Announce(Biggest(game)));
        Assert.False(game.OpenRelease());
        Assert.False(game.StartAdvert("posters"));
        Assert.NotNull(game.RunSales());
        Assert.Equal(3, game.JobsLeft);
    }

    [Fact]
    public void A_release_caps_what_can_sell_until_the_next_is_opened()
    {
        var game = OnSale();
        foreach (var slot in game.Secret.Take(3).ToList())
        {
            game.Announce(slot.Act.Name);
        }

        var months = 0;
        while (game.ReleaseLeft > 0 && months++ < 5)
        {
            game.RunSales();
            foreach (var slot in game.Secret.Take(3).ToList())
            {
                game.Announce(slot.Act.Name);
            }
        }

        Assert.Equal(0, game.ReleaseLeft);
        Assert.Equal(game.ReleaseSize(0), game.ReleaseSold(0));
        Assert.True(game.NothingOnSale || game.VipLeft > 0);
        Assert.False(game.SoldOut);
        Assert.Equal(0, game.SalesForecast);
        Assert.Contains(game.CampaignNotes, n => n.What == "Early bird sold out");

        game.RunSales();
        Assert.True(game.OpenRelease());
        Assert.Equal(1, game.ReleaseOpen);
        Assert.Equal(game.ReleaseSize(1), game.ReleaseLeft);
        Assert.True(game.SalesForecast > 0);
    }

    [Fact]
    public void Build_week_puts_every_ticket_on_sale_and_every_name_on_the_poster()
    {
        var game = OnSale();
        while (game.Phase == Phase.Sell)
        {
            game.RunSales();
        }

        Assert.Equal(Campaign.Releases.Count - 1, game.ReleaseOpen);
        Assert.Empty(game.Secret);
        Assert.Equal(0, game.JobsLeft);
    }

    [Fact]
    public void Buzz_fades_each_month_unless_it_is_fed()
    {
        var game = OnSale();
        game.DoStunt("launch");
        var high = game.Buzz;
        game.RunSales();
        Assert.Equal(high - Campaign.BuzzFade, game.Buzz, 6);
        Assert.True(Campaign.BuzzPull(80) > Campaign.BuzzPull(20));
        Assert.True(Campaign.BuzzHurry(80) > Campaign.BuzzHurry(20));

        var advertised = OnSale();
        advertised.DoStunt("launch");
        advertised.StartAdvert("posters");
        advertised.StartAdvert("newsletter");
        advertised.RunSales();
        Assert.Equal(high - Campaign.BuzzFade + 2, advertised.Buzz, 6);
    }

    [Fact]
    public void A_stunt_costs_money_and_cannot_be_done_again_straight_away()
    {
        var game = OnSale();
        var money = game.Money;
        var night = Campaign.FindStunt("pubnight")!;
        Assert.True(game.DoStunt("pubnight"));
        Assert.Equal(money - game.StuntCost(night), game.Money);
        Assert.False(game.CanDoStunt("pubnight"));
        Assert.Equal(2, game.StuntReadyIn("pubnight"));
        Assert.True(game.DoStunt("press"));
        Assert.Equal(-1, game.StuntReadyIn("press"));
        game.RunSales();
        game.RunSales();
        Assert.Equal(0, game.StuntReadyIn("pubnight"));
        Assert.True(game.CanDoStunt("pubnight"));
        Assert.False(game.CanDoStunt("press"));
    }

    [Fact]
    public void A_competition_gives_tickets_away()
    {
        var game = OnSale();
        var sold = game.TicketsSold;
        var takings = game.TicketTakings;
        Assert.True(game.DoStunt("competition"));
        Assert.Equal(sold + 10, game.TicketsSold);
        Assert.Equal(10, game.TicketsGiven);
        Assert.Equal(takings, game.TicketTakings);
    }

    [Fact]
    public void An_advert_takes_a_job_to_start_and_none_to_stop()
    {
        var game = OnSale();
        Assert.True(game.StartAdvert("posters"));
        Assert.Equal(2, game.JobsLeft);
        Assert.False(game.StartAdvert("posters"));
        game.SetAdvert("posters", false);
        Assert.Equal(2, game.JobsLeft);
        Assert.False(game.AdvertOn("posters"));
    }

    [Fact]
    public void News_turns_up_some_months_and_each_answer_does_what_it_says()
    {
        // Somewhere in the first few seeds' years there is news to answer.
        var game = OnSale();
        while (game.Phase == Phase.Sell && game.PendingNews is null)
        {
            game.RunSales();
        }

        var news = game.PendingNews;
        Assert.NotNull(news);
        var buzz = game.Buzz;
        var price = game.TicketPrice;
        var jobs = game.JobsLeft;
        var last = news!.Choices.Count - 1;
        Assert.True(game.AnswerNews(last));
        Assert.Null(game.PendingNews);
        Assert.False(game.AnswerNews(0));
        Assert.Equal(Math.Clamp(buzz + news.Choices[last].Buzz, 0, 100), game.Buzz, 6);
        Assert.Equal(price - news.Choices[last].PriceCut, game.TicketPrice);
        Assert.Equal(jobs - (news.Choices[last].UsesJob ? 1 : 0), game.JobsLeft);
        Assert.All(Campaign.News, n => Assert.InRange(n.Choices.Count, 2, 3));
    }

    [Fact]
    public void People_judge_the_price_by_the_acts_they_know_about()
    {
        var game = OnSale();
        var before = game.PerceivedFairPrice;
        Assert.True(before < game.FairPrice);
        foreach (var slot in game.Secret.ToList().Take(3))
        {
            game.Announce(slot.Act.Name);
        }

        game.RunSales();
        foreach (var slot in game.Secret.ToList())
        {
            game.Announce(slot.Act.Name);
        }

        Assert.True(game.PerceivedFairPrice > before);
        Assert.Equal(game.FairPrice, game.PerceivedFairPrice, 6);
    }

    [Fact]
    public void A_campaign_comes_back_from_a_save_as_it_was()
    {
        var game = OnSale();
        game.Announce(Biggest(game));
        game.DoStunt("interview");
        game.RunSales();
        game.OpenRelease();
        game.StartAdvert("posters");

        var loaded = FestivalGame.Load(System.Text.Json.JsonSerializer.Deserialize<SaveData>(System.Text.Json.JsonSerializer.Serialize(game.Save()))!);
        Assert.True(loaded.Campaigning);
        Assert.Equal(game.Buzz, loaded.Buzz, 6);
        Assert.Equal(game.JobsLeft, loaded.JobsLeft);
        Assert.Equal(game.ReleaseOpen, loaded.ReleaseOpen);
        Assert.Equal(game.ActsAnnounced.OrderBy(a => a), loaded.ActsAnnounced.OrderBy(a => a));
        Assert.Equal(game.StuntReadyIn("interview"), loaded.StuntReadyIn("interview"));
        Assert.Equal(game.SalesForecast, loaded.SalesForecast);
        Assert.Equal(game.CampaignNotes.Select(n => n.What), loaded.CampaignNotes.Select(n => n.What));
        Assert.Equal(game.PendingNews?.Id, loaded.PendingNews?.Id);
        Assert.Equal(game.RunSales()!.Tickets, loaded.RunSales()!.Tickets);
    }
}
