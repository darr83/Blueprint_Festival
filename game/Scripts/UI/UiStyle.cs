using System.Collections.Generic;
using BlueprintFestival.Audio;
using BlueprintFestival.Simulation.Sound;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>
/// The shared look of every screen: a festival programme. White paper, pink and sunflower bunting,
/// poster headings, and ink outlines. Red, amber and green are kept for safety status and never
/// used as decoration.
/// </summary>
public static class UiStyle
{
    public static readonly Color StagePink = new("#EC4F7F");
    public static readonly Color PinkHover = new("#F0668D");
    public static readonly Color PinkPressed = new("#C73A64");
    public static readonly Color PinkTint = new("#FCE8EE");
    public static readonly Color Sunflower = new("#F5B72A");
    public static readonly Color SunTint = new("#FFF3C9");
    public static readonly Color NightInk = new("#26232B");
    public static readonly Color Meadow = new("#5E9C4E");
    public static readonly Color Mud = new("#7A6646");
    public static readonly Color FirstAidGreen = new("#1E9E52");

    public static readonly Color Surface = Colors.White;
    public static readonly Color Ground = new("#FAF8F4");
    public static readonly Color Border = new("#E8E4EA");
    public static readonly Color TextDark = NightInk;
    public static readonly Color TextMuted = new("#7C7783");

    /// <summary>How far down the screen the top badges reach: side panels start below this.</summary>
    public const int TopClear = 104;

    /// <summary>Theme type variation for white buttons with an ink outline.</summary>
    public const string Secondary = "SecondaryButton";

    /// <summary>Theme type variation for round buttons that sit inside a white pill.</summary>
    public const string Round = "RoundButton";

    /// <summary>Theme type variation for round buttons that float over the field by themselves.</summary>
    public const string RoundFloating = "RoundFloatingButton";

    /// <summary>Theme type variation for small pills that switch something on and off.</summary>
    public const string Chip = "ChipButton";

    /// <summary>Theme type variation for the cards of things to build.</summary>
    public const string ChoiceCard = "ChoiceCard";

    private static readonly Dictionary<string, ImageTexture> IconCache = new();
    private static Theme? _theme;
    private static Font? _serif;
    private static Font? _serifHeavy;
    private static Font? _sans;
    private static Font? _sansSemi;
    private static Font? _sansBold;
    private static Font? _caps;

    public static Theme Theme => _theme ??= BuildTheme();

    /// <summary>The poster font, for names and headings.</summary>
    public static Font Serif => _serif ??= Load("Fraunces-700.ttf");

    /// <summary>The poster font at its heaviest, for big headings and big numbers.</summary>
    public static Font SerifHeavy => _serifHeavy ??= Load("Fraunces-800.ttf");

    public static Font Sans => _sans ??= Load("Inter-Regular.otf");

    public static Font SansSemi => _sansSemi ??= Load("Inter-SemiBold.otf");

    public static Font SansBold => _sansBold ??= Load("Inter-Bold.otf");

    /// <summary>Bold and widely spaced, for the small capital labels.</summary>
    public static Font CapsFont => _caps ??= new FontVariation { BaseFont = SansBold, SpacingGlyph = 1 };

    private static Font Load(string file)
    {
        var path = $"res://Fonts/{file}";
        return ResourceLoader.Exists(path) && GD.Load<Font>(path) is { } font ? font : ThemeDB.FallbackFont;
    }

    private static readonly Dictionary<string, string> IconPaths = new()
    {
        ["stage"] = "<path d='M3 20h18M5 20V9h14v11M3 9l9-5 9 5'/><path d='M9 20v-5h6v5'/>",
        ["fence"] = "<path d='M5 21V6l2-3 2 3v15M15 21V6l2-3 2 3v15M3 10h18M3 16h18'/>",
        ["gate"] = "<path d='M4 21V5a8 8 0 0 1 16 0v16'/><path d='M9 21v-9h6v9'/>",
        ["stall"] = "<path d='M3 9l2-5h14l2 5'/><path d='M3 9a3 3 0 0 0 6 0 3 3 0 0 0 6 0 3 3 0 0 0 6 0'/><path d='M5 12v9h14v-9'/>",
        ["staff"] = "<circle cx='12' cy='7' r='4'/><path d='M4 21v-2a6 6 0 0 1 6-6h4a6 6 0 0 1 6 6v2'/>",
        ["ground"] = "<path d='M3 19c3-2 5-2 9 0s6 2 9 0M3 14c3-2 5-2 9 0s6 2 9 0M3 9c3-2 5-2 9 0s6 2 9 0'/>",
        ["remove"] = "<path d='M4 7h16M10 11v6M14 11v6M5 7l1 13h12l1-13M9 7V4h6v3'/>",
        ["pause"] = "<path d='M9 5v14M15 5v14'/>",
        ["play"] = "<path d='M7 4l12 8-12 8z'/>",
        ["crowd"] = "<circle cx='8' cy='8' r='3'/><circle cx='16' cy='8' r='3'/><path d='M2 20a6 6 0 0 1 12 0M10 20a6 6 0 0 1 12 0'/>",
        ["camband"] = "<path d='M2 10h10v8H2zM12 13l4-2.5v7L12 15'/><path d='M21 3v7'/><circle cx='19.3' cy='10.2' r='1.7'/><path d='M21 3l-3 1'/>",
        ["camcrowd"] = "<path d='M2 3h10v7H2zM12 5.5l4-2.5v7l-4-2.5'/><circle cx='6' cy='16' r='2'/><circle cx='12' cy='16' r='2'/><circle cx='18' cy='16' r='2'/><path d='M3 22a3 3 0 0 1 6 0M9 22a3 3 0 0 1 6 0M15 22a3 3 0 0 1 6 0'/>",
        ["left"] = "<path d='M3 12a9 9 0 1 0 3-6.7L3 8'/><path d='M3 3v5h5'/>",
        ["right"] = "<path d='M21 12a9 9 0 1 1-3-6.7L21 8'/><path d='M21 3v5h-5'/>",
        ["plus"] = "<path d='M12 5v14M5 12h14'/>",
        ["minus"] = "<path d='M5 12h14'/>",
        ["menu"] = "<path d='M4 6h16M4 12h16M4 18h16'/>",
        ["whole"] = "<path d='M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5'/>",
        ["cross"] = "<path d='M9 3h6v6h6v6h-6v6H9v-6H3V9h6z'/>",
        ["drop"] = "<path d='M12 3c4 5 6 8 6 11a6 6 0 0 1-12 0c0-3 2-6 6-11z'/>",
        ["door"] = "<path d='M6 21V4h12v17M3 21h18'/><path d='M14.5 12.5h.01'/>",
        ["bin"] = "<path d='M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13M10 11v6M14 11v6'/>",
        ["broom"] = "<path d='M19 3l-7 9M9.5 10.5l4 3.5-3 7H3.5c2-2 3.5-5 3-8z'/><path d='M7 20l1.5-3M10.5 20.5l1-2.5'/>",
        ["shield"] = "<path d='M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6z'/>",
        ["ticket"] = "<path d='M3 8a2 2 0 0 0 0 4v4h18v-4a2 2 0 0 0 0-4V4H3z'/><path d='M14 5v2M14 9v2M14 13v2'/>",
        ["cup"] = "<path d='M5 8h11v5a5 5 0 0 1-5 5h-1a5 5 0 0 1-5-5z'/><path d='M16 9h2a2 2 0 0 1 0 5h-2M4 21h13'/>",
        ["glass"] = "<path d='M6 3h12l-1.5 9a4.5 4.5 0 0 1-9 0zM12 16.5V21M8 21h8'/>",
    };

    /// <summary>
    /// A line icon drawn in white, to be tinted where it is used. Drawn at twice the size it is
    /// shown, so it stays sharp on a big screen.
    /// </summary>
    public static ImageTexture Icon(string name)
    {
        if (IconCache.TryGetValue(name, out var made))
        {
            return made;
        }

        var paths = IconPaths.TryGetValue(name, out var found) ? found : IconPaths["stall"];
        var svg = $"<svg xmlns='http://www.w3.org/2000/svg' width='24' height='24' viewBox='0 0 24 24' fill='none' stroke='white' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'>{paths}</svg>";
        var image = new Image();
        image.LoadSvgFromString(svg, 3f);
        var texture = ImageTexture.CreateFromImage(image);
        IconCache[name] = texture;
        return texture;
    }

    /// <summary>An icon as a picture of a set size and colour, which lets the mouse through.</summary>
    public static TextureRect IconView(string name, int size, Color? colour = null) => new()
    {
        Texture = Icon(name),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        CustomMinimumSize = new Vector2(size, size),
        SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        Modulate = colour ?? NightInk,
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    public static StyleBoxFlat Box(Color background, int radius = 12, Color? border = null, int margin = 0, int borderWidth = 1)
    {
        var box = new StyleBoxFlat
        {
            BgColor = background,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ContentMarginLeft = margin, ContentMarginRight = margin, ContentMarginTop = margin, ContentMarginBottom = margin,
            AntiAliasing = true,
        };
        if (border is { } colour)
        {
            box.BorderColor = colour;
            box.BorderWidthBottom = box.BorderWidthTop = box.BorderWidthLeft = box.BorderWidthRight = borderWidth;
        }

        return box;
    }

    /// <summary>A box with an ink outline, as the tickets and cards of the programme have.</summary>
    public static StyleBoxFlat Outlined(Color background, int radius = 10, int margin = 12, Color? outline = null) => Box(background, radius, outline ?? NightInk, margin, 2);

    /// <summary>A white badge with a soft shadow, used for everything that floats over the field.</summary>
    public static StyleBoxFlat Card(int margin = 16, int radius = 14)
    {
        var box = Box(Surface, radius, null, margin);
        box.ShadowColor = new Color(0.08f, 0.1f, 0.04f, 0.22f);
        box.ShadowSize = 14;
        box.ShadowOffset = new Vector2(0, 5);
        return box;
    }

    private static StyleBoxFlat Pill(Color background, Color? border = null, int sides = 24, int ends = 11)
    {
        var box = Box(background, 999, border, 0, 2);
        box.ContentMarginLeft = box.ContentMarginRight = sides;
        box.ContentMarginTop = box.ContentMarginBottom = ends;
        return box;
    }

    private static void Colours(Theme theme, string type, Color normal, Color pressed, Color? disabled = null)
    {
        foreach (var state in new[] { "font_color", "font_hover_color", "font_focus_color", "icon_normal_color", "icon_hover_color", "icon_focus_color" })
        {
            theme.SetColor(state, type, normal);
        }

        foreach (var state in new[] { "font_pressed_color", "font_hover_pressed_color", "icon_pressed_color", "icon_hover_pressed_color" })
        {
            theme.SetColor(state, type, pressed);
        }

        theme.SetColor("font_disabled_color", type, disabled ?? normal);
        theme.SetColor("icon_disabled_color", type, disabled ?? normal);
    }

    private static void States(Theme theme, string type, StyleBox normal, StyleBox hover, StyleBox pressed, StyleBox? disabled = null)
    {
        theme.SetStylebox("normal", type, normal);
        theme.SetStylebox("hover", type, hover);
        theme.SetStylebox("pressed", type, pressed);
        theme.SetStylebox("hover_pressed", type, pressed);
        theme.SetStylebox("focus", type, new StyleBoxEmpty());
        theme.SetStylebox("disabled", type, disabled ?? normal);
    }

    private static Theme BuildTheme()
    {
        var theme = new Theme { DefaultFont = Sans, DefaultFontSize = 16 };
        theme.SetStylebox("panel", "PanelContainer", Card(24));

        // The main button: a pink pill.
        States(theme, "Button", Pill(StagePink), Pill(PinkHover), Pill(PinkPressed), Pill(new Color("#D8D3DA")));
        Colours(theme, "Button", Colors.White, Colors.White);
        theme.SetFont("font", "Button", SansBold);
        theme.SetFontSize("font_size", "Button", 15);

        // Everything else you can press: a white pill with an ink outline, which fills with ink when it is on.
        theme.SetTypeVariation(Secondary, "Button");
        var off = Pill(Surface, new Color("#CFC9D3"));
        States(theme, Secondary, Pill(Surface, NightInk), Pill(SunTint, NightInk), Pill(NightInk, NightInk), off);
        Colours(theme, Secondary, TextDark, Colors.White, new Color("#B5AFBA"));

        theme.SetTypeVariation(Chip, "Button");
        States(theme, Chip, Pill(Surface, NightInk, 13, 6), Pill(SunTint, NightInk, 13, 6), Pill(NightInk, NightInk, 13, 6));
        Colours(theme, Chip, TextDark, Colors.White);
        theme.SetFont("font", Chip, SansSemi);
        theme.SetFontSize("font_size", Chip, 13);

        theme.SetTypeVariation(Round, "Button");
        States(theme, Round, Box(new Color(1, 1, 1, 0), 999), Box(SunTint, 999), Box(NightInk, 999));
        Colours(theme, Round, TextDark, Colors.White);

        theme.SetTypeVariation(RoundFloating, "Button");
        States(theme, RoundFloating, Card(0, 999), Shadowed(SunTint), Shadowed(NightInk));
        Colours(theme, RoundFloating, TextDark, Colors.White);

        theme.SetTypeVariation(ChoiceCard, "Button");
        States(theme, ChoiceCard, Outlined(Surface, 10, 6, Border), Outlined(SunTint, 10, 6, Border), Outlined(SunTint, 10, 6));

        theme.SetColor("font_color", "Label", TextDark);

        var field = Outlined(Surface, 10, 0);
        field.ContentMarginLeft = field.ContentMarginRight = 14;
        field.ContentMarginTop = field.ContentMarginBottom = 10;
        var fieldFocus = (StyleBoxFlat)field.Duplicate();
        fieldFocus.BorderColor = StagePink;
        theme.SetStylebox("normal", "LineEdit", field);
        theme.SetStylebox("focus", "LineEdit", fieldFocus);
        theme.SetColor("font_color", "LineEdit", TextDark);
        theme.SetColor("font_placeholder_color", "LineEdit", new Color("#A8A1AD"));
        theme.SetColor("caret_color", "LineEdit", StagePink);

        theme.SetStylebox("separator", "HSeparator", new StyleBoxLine { Color = Border, Thickness = 1 });

        var tip = Outlined(Surface, 8, 0);
        tip.ContentMarginLeft = tip.ContentMarginRight = 12;
        tip.ContentMarginTop = tip.ContentMarginBottom = 8;
        theme.SetStylebox("panel", "TooltipPanel", tip);
        theme.SetColor("font_color", "TooltipLabel", TextDark);
        theme.SetFontSize("font_size", "TooltipLabel", 14);
        return theme;
    }

    private static StyleBoxFlat Shadowed(Color background)
    {
        var box = Card(0, 999);
        box.BgColor = background;
        return box;
    }

    /// <summary>A heading in the poster font.</summary>
    public static Label Heading(string text, int size = 28)
    {
        var label = new Label { Text = text };
        label.AddThemeFontOverride("font", size >= 26 ? SerifHeavy : Serif);
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }

    public static Label Body(string text, bool muted = false, int size = 16, bool bold = false)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        if (bold)
        {
            label.AddThemeFontOverride("font", SansSemi);
        }

        if (muted)
        {
            label.AddThemeColorOverride("font_color", TextMuted);
        }

        return label;
    }

    /// <summary>A small label in spaced capitals: grey over a section, pink over a heading.</summary>
    public static Label Caps(string text, bool pink = false, int size = 12)
    {
        var label = new Label { Text = text, Uppercase = true };
        label.AddThemeFontOverride("font", CapsFont);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", pink ? StagePink : TextMuted);
        return label;
    }

    /// <summary>The ink line under a heading.</summary>
    public static ColorRect Rule(int thickness = 2) => new() { Color = thickness > 1 ? NightInk : Border, CustomMinimumSize = new Vector2(0, thickness), MouseFilter = Control.MouseFilterEnum.Ignore };

    public static Button Button(string text, System.Action pressed, string sound = Sounds.Click)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        button.Pressed += () => SoundBoard.Cue(sound);
        button.Pressed += pressed;
        return button;
    }

    public static Button SecondaryButton(string text, System.Action pressed, string sound = Sounds.Click)
    {
        var button = Button(text, pressed, sound);
        button.ThemeTypeVariation = Secondary;
        return button;
    }

    /// <summary>A small pill that is filled with ink while it is on.</summary>
    public static Button ChipButton(string text, System.Action pressed, string sound = Sounds.Click)
    {
        var button = Button(text, pressed, sound);
        button.ThemeTypeVariation = Chip;
        return button;
    }

    /// <summary>A round button with an icon, or with a word or two if the icon name is not one.</summary>
    public static Button RoundButton(string iconOrText, System.Action pressed, int size = 46, bool floating = false, string sound = Sounds.Click)
    {
        var button = Button("", pressed, sound);
        button.ThemeTypeVariation = floating ? RoundFloating : Round;
        button.CustomMinimumSize = new Vector2(size, size);
        if (IconPaths.ContainsKey(iconOrText))
        {
            button.Icon = Icon(iconOrText);
            button.IconAlignment = HorizontalAlignment.Center;
            button.ExpandIcon = true;
            button.AddThemeConstantOverride("icon_max_width", size * 9 / 20);
        }
        else
        {
            button.Text = iconOrText;
        }

        return button;
    }
}

/// <summary>A string of pink and sunflower flags, hung along the top of whatever it is put on.</summary>
public partial class Bunting : Control
{
    /// <summary>How wide one flag is, in pixels.</summary>
    public float FlagWidth { get; set; } = 36;

    public Bunting()
    {
        CustomMinimumSize = new Vector2(0, 22);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw() => Flags(this, Size.X, FlagWidth, Size.Y);

    /// <summary>Draws flags across the top of a control, pink and sunflower in turn.</summary>
    public static void Flags(CanvasItem on, float width, float flagWidth, float drop)
    {
        var count = Mathf.Max(1, Mathf.RoundToInt(width / flagWidth));
        var each = width / count;
        for (var i = 0; i < count; i++)
        {
            var x = i * each;
            on.DrawColoredPolygon(new[] { new Vector2(x, 0), new Vector2(x + each, 0), new Vector2(x + (each / 2), drop) }, i % 2 == 0 ? UiStyle.StagePink : UiStyle.Sunflower);
        }
    }
}

/// <summary>A sheet of white paper with bunting along its top edge: the panel every screen is printed on.</summary>
public partial class Paper : PanelContainer
{
    private readonly float _flag;
    private readonly float _drop;

    public Paper() : this(28)
    {
    }

    public Paper(int margin, bool small = false)
    {
        _flag = small ? 26 : 36;
        _drop = small ? 16 : 22;
        Theme = UiStyle.Theme;
        var box = UiStyle.Card(margin, 6);
        box.ContentMarginTop = margin + _drop - (small ? 6 : 10);
        box.ShadowSize = 22;
        box.ShadowOffset = new Vector2(0, 10);
        AddThemeStyleboxOverride("panel", box);
    }

    public override void _Draw() => Bunting.Flags(this, Size.X, _flag, _drop);
}

/// <summary>
/// A ticket: an ink outline with a tear line down it and a notch at each end of the tear. The
/// stub to the right of the tear is as wide as <see cref="Stub"/>.
/// </summary>
public partial class Ticket : PanelContainer
{
    /// <summary>How wide the stub to the right of the tear is, in pixels.</summary>
    public float Stub { get; set; } = 130;

    public Ticket()
    {
        AddThemeStyleboxOverride("panel", UiStyle.Outlined(UiStyle.Surface, 10, 0));
    }

    /// <summary>Lays out the ticket: what goes on the main part, and what goes on the stub.</summary>
    public static Ticket With(Control main, Control stub, float stubWidth = 130)
    {
        var ticket = new Ticket { Stub = stubWidth };
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 0);
        ticket.AddChild(row);
        var left = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var right = new MarginContainer { CustomMinimumSize = new Vector2(stubWidth, 0) };
        foreach (var (side, value) in new[] { ("margin_left", 16), ("margin_right", 16), ("margin_top", 11), ("margin_bottom", 11) })
        {
            left.AddThemeConstantOverride(side, value);
            right.AddThemeConstantOverride(side, value);
        }

        left.AddChild(main);
        right.AddChild(stub);
        row.AddChild(left);
        row.AddChild(right);
        return ticket;
    }

    private static Vector2[] HalfDisc(Vector2 centre, float radius, bool lower)
    {
        var points = new Vector2[13];
        for (var i = 0; i <= 12; i++)
        {
            var angle = (lower ? 0 : Mathf.Pi) + (Mathf.Pi * i / 12);
            points[i] = centre + (new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }

        return points;
    }

    public override void _Draw()
    {
        var x = Size.X - Stub;
        for (var y = 12f; y < Size.Y - 12; y += 9)
        {
            DrawLine(new Vector2(x, y), new Vector2(x, Mathf.Min(y + 5, Size.Y - 12)), UiStyle.NightInk, 2);
        }

        foreach (var (centre, lower) in new[] { (new Vector2(x, 1), true), (new Vector2(x, Size.Y - 1), false) })
        {
            DrawColoredPolygon(HalfDisc(centre, 8, lower), UiStyle.Surface);
            DrawArc(centre, 8, lower ? 0 : Mathf.Pi, lower ? Mathf.Pi : Mathf.Tau, 16, UiStyle.NightInk, 2, true);
        }
    }
}

/// <summary>A dashed line down the middle of a narrow strip: the tear between the two halves of the top ticket.</summary>
public partial class TearLine : Control
{
    public Color Colour { get; set; } = UiStyle.PinkTint;

    public TearLine()
    {
        CustomMinimumSize = new Vector2(2, 0);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        for (var y = 4f; y < Size.Y - 4; y += 8)
        {
            DrawLine(new Vector2(1, y), new Vector2(1, Mathf.Min(y + 4, Size.Y - 4)), Colour, 2);
        }
    }
}
