using Godot;

namespace BlueprintFestival.UI;

/// <summary>
/// The shared look of every screen: the light panel layout of Blueprint Hospital in the festival's
/// colours. Red, amber and green are kept for safety status and never used as decoration.
/// </summary>
public static class UiStyle
{
    public static readonly Color StagePink = new("#E94F7B");
    public static readonly Color PinkHover = new("#F0668D");
    public static readonly Color PinkPressed = new("#C73A64");
    public static readonly Color PinkTint = new("#FCE8EE");
    public static readonly Color Sunflower = new("#F6B93B");
    public static readonly Color NightInk = new("#1D2B36");
    public static readonly Color Meadow = new("#5E9C4E");
    public static readonly Color Mud = new("#7A6646");
    public static readonly Color FirstAidGreen = new("#1E9E52");

    public static readonly Color Surface = Colors.White;
    public static readonly Color Ground = new("#F7F4EE");
    public static readonly Color Border = new("#E0DACE");
    public static readonly Color TextDark = NightInk;
    public static readonly Color TextMuted = new("#5F6B74");

    /// <summary>Theme type variation for white buttons with a grey border.</summary>
    public const string Secondary = "SecondaryButton";

    private static Theme? _theme;

    public static Theme Theme => _theme ??= BuildTheme();

    public static StyleBoxFlat Box(Color background, int radius = 12, Color? border = null, int margin = 0)
    {
        var box = new StyleBoxFlat
        {
            BgColor = background,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ContentMarginLeft = margin, ContentMarginRight = margin, ContentMarginTop = margin, ContentMarginBottom = margin,
        };
        if (border is { } colour)
        {
            box.BorderColor = colour;
            box.BorderWidthBottom = box.BorderWidthTop = box.BorderWidthLeft = box.BorderWidthRight = 1;
        }

        return box;
    }

    /// <summary>A white card with a soft border and shadow, used for every panel.</summary>
    public static StyleBoxFlat Card(int margin = 16, int radius = 16)
    {
        var box = Box(Surface, radius, Border, margin);
        box.ShadowColor = new Color(0.11f, 0.17f, 0.21f, 0.14f);
        box.ShadowSize = 10;
        box.ShadowOffset = new Vector2(0, 4);
        return box;
    }

    private static StyleBoxFlat ButtonBox(Color background, Color? border = null)
    {
        var box = Box(background, 10, border);
        box.ContentMarginLeft = box.ContentMarginRight = 18;
        box.ContentMarginTop = box.ContentMarginBottom = 9;
        return box;
    }

    private static Theme BuildTheme()
    {
        var theme = new Theme { DefaultFontSize = 16 };
        theme.SetStylebox("panel", "PanelContainer", Card(24));

        theme.SetStylebox("normal", "Button", ButtonBox(StagePink));
        theme.SetStylebox("hover", "Button", ButtonBox(PinkHover));
        theme.SetStylebox("pressed", "Button", ButtonBox(PinkPressed));
        theme.SetStylebox("hover_pressed", "Button", ButtonBox(PinkPressed));
        theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
        theme.SetStylebox("disabled", "Button", ButtonBox(new Color("#CFCBC2")));
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color", "font_disabled_color" })
        {
            theme.SetColor(state, "Button", Colors.White);
        }

        theme.SetTypeVariation(Secondary, "Button");
        theme.SetStylebox("normal", Secondary, ButtonBox(Surface, Border));
        theme.SetStylebox("hover", Secondary, ButtonBox(PinkTint, StagePink));
        theme.SetStylebox("pressed", Secondary, ButtonBox(StagePink, StagePink));
        theme.SetStylebox("hover_pressed", Secondary, ButtonBox(PinkPressed, PinkPressed));
        theme.SetStylebox("focus", Secondary, new StyleBoxEmpty());
        foreach (var state in new[] { "font_color", "font_hover_color", "font_focus_color" })
        {
            theme.SetColor(state, Secondary, TextDark);
        }

        theme.SetColor("font_pressed_color", Secondary, Colors.White);
        theme.SetColor("font_hover_pressed_color", Secondary, Colors.White);

        theme.SetColor("font_color", "Label", TextDark);

        var field = Box(Surface, 10, Border);
        field.ContentMarginLeft = field.ContentMarginRight = 14;
        field.ContentMarginTop = field.ContentMarginBottom = 10;
        var fieldFocus = (StyleBoxFlat)field.Duplicate();
        fieldFocus.BorderColor = StagePink;
        fieldFocus.BorderWidthBottom = fieldFocus.BorderWidthTop = fieldFocus.BorderWidthLeft = fieldFocus.BorderWidthRight = 2;
        theme.SetStylebox("normal", "LineEdit", field);
        theme.SetStylebox("focus", "LineEdit", fieldFocus);
        theme.SetColor("font_color", "LineEdit", TextDark);
        theme.SetColor("font_placeholder_color", "LineEdit", new Color("#9AA3AA"));
        theme.SetColor("caret_color", "LineEdit", StagePink);
        return theme;
    }

    public static Label Heading(string text, int size = 28)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }

    public static Label Body(string text, bool muted = false, int size = 16)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        if (muted)
        {
            label.AddThemeColorOverride("font_color", TextMuted);
        }

        return label;
    }

    public static Button Button(string text, System.Action pressed)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        button.Pressed += pressed;
        return button;
    }

    public static Button SecondaryButton(string text, System.Action pressed)
    {
        var button = Button(text, pressed);
        button.ThemeTypeVariation = Secondary;
        return button;
    }
}
