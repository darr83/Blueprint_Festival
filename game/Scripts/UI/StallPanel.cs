using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Audio;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.Sound;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>
/// A stall's menu board. Click a food, drink or merchandise stall on the field (with nothing
/// picked on the build bar) and this opens down the left: everything it sells, with a minus and a
/// plus to set each price until the gates open, what people make of the prices, and what it has
/// sold today.
/// </summary>
public partial class StallPanel : PanelContainer
{
    private FestivalGame _game = null!;
    private BuildTool _tool = null!;
    private VBoxContainer _box = null!;
    private double _sinceRefresh;
    private string _shown = "";
    private readonly List<Button> _minus = new();
    private readonly List<Button> _plus = new();

    /// <summary>Which stall's menu is showing, as its place in the site's list, or -1.</summary>
    public int Stall { get; private set; } = -1;

    /// <summary>The words that say what people make of the prices.</summary>
    public string Verdict { get; private set; } = "";

    /// <summary>How many things are listed on the menu showing.</summary>
    public int Rows { get; private set; }

    public void Initialise(FestivalGame game, BuildTool tool)
    {
        _game = game;
        _tool = tool;
        Theme = UiStyle.Theme;
        CustomMinimumSize = new Vector2(400, 0);
        AddThemeStyleboxOverride("panel", UiStyle.Card(margin: 20, radius: 12));
        _box = new VBoxContainer();
        _box.AddThemeConstantOverride("separation", 8);
        AddChild(_box);
        Visible = false;
        SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft, LayoutPresetMode.Minsize, 22);
        Position = new Vector2(22, UiStyle.TopClear + 8);
        game.Site.Changed += OnSiteChanged;
    }

    public override void _ExitTree() => _game.Site.Changed -= OnSiteChanged;

    private void OnSiteChanged()
    {
        // Stalls are numbered in the order they stand in the list, so taking one down can move the rest.
        if (Visible)
        {
            Close();
        }
    }

    /// <summary>Opens the menu board of a stall, if it is one that sells things. True if it opened.</summary>
    public bool Open(int stall)
    {
        if (stall < 0 || stall >= _game.Site.Stalls.Count || !HasBoard(_game.Site.Stalls[stall].Kind))
        {
            return false;
        }

        Stall = stall;
        Visible = true;
        _shown = "";
        Refresh();
        return true;
    }

    /// <summary>True for a stall that opens a board when clicked: anything that sells, and the toilets and taps, which can be kept for VIPs.</summary>
    public static bool HasBoard(StallKind kind) => Catalogue.Sells(kind) || Catalogue.HasQueue(kind);

    /// <summary>The "VIP guests only" switch on the board showing, or null.</summary>
    public CheckButton? VipSwitch { get; private set; }

    /// <summary>Keeps the stall showing for VIP guests only, or opens it to everyone, as the switch on the board does.</summary>
    public bool SetVipOnly(bool on)
    {
        var stall = Stall;
        if (stall < 0 || !_game.SetVipOnly(stall, on))
        {
            SoundBoard.Cue(Sounds.Refuse);
            if (stall >= 0)
            {
                _shown = "";
                Refresh();
            }

            return false;
        }

        SoundBoard.Cue(Sounds.Click);

        // Changing who may use a stall changes the site, which closes the board: open it again.
        Open(stall);
        return true;
    }

    public void Close()
    {
        Stall = -1;
        Visible = false;
    }

    /// <summary>Puts one price up or down by a pound, as a click on its plus or minus would.</summary>
    public bool Nudge(int item, int pounds)
    {
        if (Stall < 0 || !_game.CanSetPrices)
        {
            SoundBoard.Cue(Sounds.Refuse);
            return false;
        }

        var placed = _game.Site.Stalls[Stall];
        var changed = _game.SetPrice(Stall, item, placed.PriceOf(item) + pounds) is { } now && now != placed.PriceOf(item);
        SoundBoard.Cue(changed ? Sounds.Click : Sounds.Refuse);
        Refresh();
        return changed;
    }

    public void UsualPrices()
    {
        if (Stall >= 0 && _game.ResetPrices(Stall))
        {
            SoundBoard.Cue(Sounds.Click);
            Refresh();
        }
    }

    public override void _Process(double delta)
    {
        _sinceRefresh += delta;
        if (Visible && _sinceRefresh > 0.5)
        {
            Refresh();
        }
    }

    /// <summary>What people make of a set of prices, from how dear the menu is on average against the usual prices.</summary>
    public static string VerdictFor(double dearness, int wouldBuy) =>
        dearness <= 0.8 ? $"A bargain. People will be pleased, and more of them will stop for a treat. About {wouldBuy} in 100 would buy here."
        : dearness <= 1.1 ? $"Fair prices. About {wouldBuy} in 100 would buy here."
        : dearness <= 1.5 ? $"Dear. About {wouldBuy} in 100 would still buy here, but they will grumble, and fewer will stop for a treat."
        : $"Far too dear. Only about {wouldBuy} in 100 would buy here, and the rest go without and mind it.";

    public void Refresh()
    {
        _sinceRefresh = 0;
        if (Stall < 0 || Stall >= _game.Site.Stalls.Count)
        {
            Close();
            return;
        }

        var placed = _game.Site.Stalls[Stall];
        var menu = Catalogue.MenuOf(placed.Kind);
        var sales = _game.SalesOf(Stall);
        var open = _game.CanSetPrices;
        var key = $"{Stall}|{placed.Kind}|{placed.VipOnly}|{string.Join(",", Enumerable.Range(0, menu.Count).Select(placed.PriceOf))}|{string.Join(",", sales.Sold)}|{sales.Takings}|{open}|{_game.Phase}";
        if (key == _shown)
        {
            return;
        }

        _shown = key;
        foreach (var child in _box.GetChildren())
        {
            _box.RemoveChild(child);
            child.QueueFree();
        }

        _minus.Clear();
        _plus.Clear();
        var info = Catalogue.Of(placed.Kind);
        var top = new HBoxContainer();
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddThemeConstantOverride("separation", 0);
        titles.AddChild(UiStyle.Caps(menu.Count > 0 ? "Menu board" : "Stall", pink: true));
        titles.AddChild(UiStyle.Heading(info.Name, 26));
        top.AddChild(titles);
        var close = UiStyle.SecondaryButton("Close", Close);
        close.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        top.AddChild(close);
        _box.AddChild(top);

        // Who may use it: everyone, or VIP guests only, with a sign outside saying so.
        VipSwitch = null;
        if (Catalogue.AlwaysVip(placed.Kind))
        {
            _box.AddChild(UiStyle.Body("Only VIP guests come in here.", size: 14, bold: true));
        }
        else
        {
            var vip = new CheckButton { Text = "VIP guests only", ButtonPressed = placed.VipOnly, Disabled = !open, FocusMode = FocusModeEnum.None };
            vip.AddThemeFontSizeOverride("font_size", 16);
            vip.Toggled += on => Callable.From(() => SetVipOnly(on)).CallDeferred();
            VipSwitch = vip;
            _box.AddChild(vip);
            var why = UiStyle.Body(placed.VipOnly ? "A VIP sign stands outside, and nobody without a lanyard is served."
                : "Keep it for VIP guests: a VIP sign goes up outside and only people with a lanyard use it.", muted: true, size: 13);
            why.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            why.CustomMinimumSize = new Vector2(360, 0);
            _box.AddChild(why);
        }

        if (menu.Count == 0)
        {
            Rows = 0;
            Verdict = "";
            ResetSize();
            return;
        }

        _box.AddChild(new HSeparator());
        var hint = UiStyle.Body(open ? "Set the prices before the gates open. Cheaper sells more and pleases people; dearer earns more on each sale but puts people off."
            : _game.Phase == Phase.Day ? "The gates are open, so today's prices are fixed." : "Prices can be set on the build day, and on the morning until the gates open.", muted: true, size: 13);
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hint.CustomMinimumSize = new Vector2(360, 0);
        _box.AddChild(hint);
        _box.AddChild(new HSeparator());

        var dearness = 0.0;
        for (var i = 0; i < menu.Count; i++)
        {
            var item = i;
            var price = placed.PriceOf(i);
            dearness += placed.Dearness(i);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var names = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            names.AddThemeConstantOverride("separation", 0);
            names.AddChild(UiStyle.Body(menu[i].Name, size: 16, bold: true));
            var sold = _game.Phase == Phase.Build ? "" : $"  ·  {sales.Sold.ElementAtOrDefault(i)} sold";
            names.AddChild(UiStyle.Body($"Usually £{menu[i].UsualPrice}{sold}", muted: true, size: 13));
            row.AddChild(names);

            var minus = UiStyle.RoundButton("minus", () => Nudge(item, -1), 34);
            minus.Disabled = !open || price <= Catalogue.LowestPrice(menu[i]);
            minus.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            row.AddChild(minus);
            _minus.Add(minus);
            var tag = UiStyle.Heading($"£{price}", 22);
            tag.CustomMinimumSize = new Vector2(52, 0);
            tag.HorizontalAlignment = HorizontalAlignment.Center;
            tag.AddThemeColorOverride("font_color", price > menu[i].UsualPrice * 1.5 ? new Color("#D9483B") : price > menu[i].UsualPrice ? new Color("#B8860B") : price < menu[i].UsualPrice ? UiStyle.FirstAidGreen : UiStyle.TextDark);
            row.AddChild(tag);
            var plus = UiStyle.RoundButton("plus", () => Nudge(item, 1), 34);
            plus.Disabled = !open || price >= Catalogue.HighestPrice(menu[i]);
            plus.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            row.AddChild(plus);
            _plus.Add(plus);
            _box.AddChild(row);
        }

        Rows = menu.Count;
        _box.AddChild(new HSeparator());
        Verdict = VerdictFor(dearness / System.Math.Max(1, menu.Count), _game.WouldBuy(Stall));
        var verdict = UiStyle.Body(Verdict, size: 14);
        verdict.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        verdict.CustomMinimumSize = new Vector2(360, 0);
        _box.AddChild(verdict);

        if (_game.Phase != Phase.Build)
        {
            var share = ((sales.Takings * Catalogue.PitchShare) + 50) / 100;
            var today = UiStyle.Body($"Today: {sales.Items:N0} sold, £{sales.Takings:N0} taken. Your share for the pitch is £{share:N0}.", muted: true, size: 13);
            today.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            today.CustomMinimumSize = new Vector2(360, 0);
            _box.AddChild(today);
        }
        else
        {
            _box.AddChild(UiStyle.Body($"The stallholder keeps most of the takings. You get {Catalogue.PitchShare}p in the pound for the pitch.", muted: true, size: 13));
        }

        if (open)
        {
            var usual = UiStyle.SecondaryButton("Back to the usual prices", UsualPrices);
            usual.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            usual.Disabled = placed.Prices is null || Enumerable.Range(0, menu.Count).All(i => placed.PriceOf(i) == menu[i].UsualPrice);
            _box.AddChild(usual);
        }

        ResetSize();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } && Visible)
        {
            Close();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } || _tool.Mode != BuildMode.None
            || _game.Phase is not (Phase.Build or Phase.Day))
        {
            return;
        }

        if (_tool.TileUnderMouse() is { } tile && ClickOn(tile))
        {
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>
    /// What a click on the field does when nothing is picked on the build bar: on a stall that
    /// sells things it opens that stall's menu board, anywhere else it closes the one showing.
    /// True if a board opened.
    /// </summary>
    public bool ClickOn(TilePoint tile)
    {
        var site = _game.Site;
        if (site.StallAt(tile) is { } stall)
        {
            for (var i = 0; i < site.Stalls.Count; i++)
            {
                if (ReferenceEquals(site.Stalls[i], stall) && Open(i))
                {
                    SoundBoard.Cue(Sounds.Tab);
                    return true;
                }
            }
        }

        if (Visible)
        {
            Close();
        }

        return false;
    }
}
