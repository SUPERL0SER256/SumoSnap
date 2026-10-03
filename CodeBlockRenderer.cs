using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using FontFamily = System.Windows.Media.FontFamily;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using Clipboard = System.Windows.Clipboard;
using Cursors = System.Windows.Input.Cursors;
using RichTextBox = System.Windows.Controls.RichTextBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace SumoSnap;

/// <summary>
/// Splits AI responses on ``` fences and renders code segments as IDE-style blocks
/// with a language label, a Copy button and muted syntax highlighting.
/// </summary>
public static class CodeBlockRenderer
{
    public abstract record Segment;
    public record TextSegment(string Text) : Segment;
    public record CodeSegment(string Language, string Code) : Segment;

    // Opening fence with optional language, body, then closing fence (or end of text if the model forgot to close it)
    private static readonly Regex FenceRegex = new(@"```[ \t]*([^\r\n`]*)\r?\n([\s\S]*?)(?:\r?\n)?(?:```|\z)", RegexOptions.Compiled);

    public static List<Segment> Parse(string text)
    {
        var segments = new List<Segment>();
        int last = 0;
        foreach (Match m in FenceRegex.Matches(text))
        {
            if (m.Index > last)
            {
                string before = text.Substring(last, m.Index - last).Trim('\r', '\n');
                if (before.Trim().Length > 0) segments.Add(new TextSegment(before));
            }
            segments.Add(new CodeSegment(m.Groups[1].Value.Trim(), m.Groups[2].Value.TrimEnd()));
            last = m.Index + m.Length;
        }
        if (last < text.Length)
        {
            string rest = text.Substring(last).Trim('\r', '\n');
            if (rest.Trim().Length > 0) segments.Add(new TextSegment(rest));
        }
        return segments;
    }

    // ---- Palette (muted, to sit inside the grayscale UI) ----
    private static SolidColorBrush B(byte r, byte g, byte b) { var br = new SolidColorBrush(Color.FromRgb(r, g, b)); br.Freeze(); return br; }
    private static readonly SolidColorBrush PlainBrush   = B(0xD4, 0xD4, 0xD8);
    private static readonly SolidColorBrush KeywordBrush = B(0x9C, 0xB4, 0xE8);
    private static readonly SolidColorBrush StringBrush  = B(0xC9, 0xB8, 0x8E);
    private static readonly SolidColorBrush CommentBrush = B(0x5C, 0x63, 0x70);
    private static readonly SolidColorBrush NumberBrush  = B(0xD1, 0x9A, 0x86);
    private static readonly SolidColorBrush FuncBrush    = B(0xE8, 0xE8, 0xEC);
    private static readonly SolidColorBrush MutedBrush   = B(0x80, 0x80, 0x88);

    private static readonly FontFamily MonoFont = new("Cascadia Mono, Consolas, Courier New");

    private const string Keywords =
        "abstract|async|await|bool|break|case|catch|char|class|const|continue|def|default|del|do|double|elif|else|enum|" +
        "except|export|extends|false|final|finally|float|fn|for|foreach|from|func|function|get|if|impl|implements|import|in|" +
        "int|interface|is|let|long|match|mut|namespace|new|nil|None|not|null|or|and|override|package|pass|private|protected|" +
        "public|raise|readonly|return|self|set|static|string|struct|super|switch|this|throw|throws|True|true|False|try|type|" +
        "typeof|using|var|virtual|void|while|with|yield|lambda|select|where|echo|then|fi|done|elseif|end|local";

    private static readonly HashSet<string> HashCommentLangs = new(StringComparer.OrdinalIgnoreCase)
    { "python", "py", "bash", "sh", "shell", "zsh", "ruby", "rb", "yaml", "yml", "toml", "powershell", "ps1", "pwsh", "r", "perl", "dockerfile", "makefile" };

    private static Regex BuildTokenRegex(bool hashComments)
    {
        string comment = hashComments ? @"//.*?$|/\*[\s\S]*?\*/|#.*?$" : @"//.*?$|/\*[\s\S]*?\*/|<!--[\s\S]*?-->";
        return new Regex(
            $@"(?<comment>{comment})" +
            @"|(?<string>""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\\r\n])*'|`(?:\\.|[^`\\])*`)" +
            @"|(?<number>\b\d+(?:\.\d+)?\b)" +
            $@"|(?<keyword>\b(?:{Keywords})\b)" +
            @"|(?<func>\b[A-Za-z_]\w*(?=\s*\())",
            RegexOptions.Multiline);
    }

    private static readonly Regex TokenRegexHash = BuildTokenRegex(true);
    private static readonly Regex TokenRegexSlash = BuildTokenRegex(false);

    public static FrameworkElement BuildCodeBlock(string language, string code, ScrollViewer wheelTarget)
    {
        var container = new Border
        {
            Background = B(0x0C, 0x0C, 0x0E),
            BorderBrush = B(0x2A, 0x2A, 0x2E),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 6, 0, 6)
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Header: language label + Copy
        var header = new Border
        {
            BorderBrush = B(0x22, 0x22, 0x26),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 6, 10, 6)
        };
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var langLabel = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(language) ? "code" : language.ToLowerInvariant(),
            FontFamily = MonoFont,
            FontSize = 11,
            Foreground = MutedBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        headerGrid.Children.Add(langLabel);

        var copy = new TextBlock
        {
            Text = "Copy",
            FontFamily = MonoFont,
            FontSize = 11,
            Foreground = MutedBrush,
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent
        };
        var resetTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        resetTimer.Tick += (s, e) => { resetTimer.Stop(); copy.Text = "Copy"; };
        copy.MouseEnter += (s, e) => copy.Foreground = Brushes.White;
        copy.MouseLeave += (s, e) => copy.Foreground = MutedBrush;
        copy.MouseLeftButtonUp += (s, e) =>
        {
            try { Clipboard.SetText(code); copy.Text = "Copied"; }
            catch { copy.Text = "Failed"; }
            resetTimer.Stop();
            resetTimer.Start();
        };
        Grid.SetColumn(copy, 1);
        headerGrid.Children.Add(copy);
        header.Child = headerGrid;
        root.Children.Add(header);

        // Body: highlighted, selectable code
        var paragraph = new Paragraph { Margin = new Thickness(0), LineHeight = 19 };
        Highlight(paragraph, code, HashCommentLangs.Contains(language));

        var body = new RichTextBox
        {
            IsReadOnly = true,
            IsDocumentEnabled = false,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = PlainBrush,
            FontFamily = MonoFont,
            FontSize = 12.5,
            Padding = new Thickness(8, 8, 8, 10),
            CaretBrush = Brushes.Transparent,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Document = new FlowDocument(paragraph) { PagePadding = new Thickness(0) }
        };
        // Let the chat keep scrolling when the cursor is over a code block
        body.PreviewMouseWheel += (s, e) =>
        {
            wheelTarget.ScrollToVerticalOffset(wheelTarget.VerticalOffset - e.Delta / 3.0);
            e.Handled = true;
        };
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        container.Child = root;
        return container;
    }

    private static void Highlight(Paragraph p, string code, bool hashComments)
    {
        var regex = hashComments ? TokenRegexHash : TokenRegexSlash;
        int last = 0;
        foreach (Match m in regex.Matches(code))
        {
            if (m.Index > last) p.Inlines.Add(new Run(code.Substring(last, m.Index - last)) { Foreground = PlainBrush });

            var run = new Run(m.Value);
            if (m.Groups["comment"].Success) { run.Foreground = CommentBrush; run.FontStyle = FontStyles.Italic; }
            else if (m.Groups["string"].Success) run.Foreground = StringBrush;
            else if (m.Groups["number"].Success) run.Foreground = NumberBrush;
            else if (m.Groups["keyword"].Success) run.Foreground = KeywordBrush;
            else if (m.Groups["func"].Success) run.Foreground = FuncBrush;
            p.Inlines.Add(run);

            last = m.Index + m.Length;
        }
        if (last < code.Length) p.Inlines.Add(new Run(code.Substring(last)) { Foreground = PlainBrush });
    }
}
