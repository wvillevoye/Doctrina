using System.Text.RegularExpressions;

namespace Doctrina.Modules.Content.Application;

/// <summary>What a toolbar button does (design D4 of add-markdown-toolbar).</summary>
internal enum MarkdownActionKind
{
    Heading1, Heading2, Heading3, NormalText, Bold, Italic, Strikethrough, InlineCode, BulletedList, NumberedList, TaskList,
    Quote, CodeBlock, Link, Image, Table, HorizontalRule, Color, Align,
}

/// <summary>
/// A toolbar action; <see cref="Argument"/> is the colour or alignment name (<see cref="MarkdownActionKind.Color"/>,
/// <see cref="MarkdownActionKind.Align"/>) or the language of a <see cref="MarkdownActionKind.CodeBlock"/>.
/// </summary>
internal sealed record MarkdownAction(MarkdownActionKind Kind, string? Argument = null);

/// <summary>The text of the editor and the selection (<see cref="Start"/> up to <see cref="End"/>, both 0-based).</summary>
internal sealed record EditorState(string Text, int Start, int End);

/// <summary>
/// Inserts Markdown syntax for a toolbar button (design D1 and D4 of add-markdown-toolbar). Pure: the browser only
/// reports and applies the text and the selection, so all the rules can be tested here.
/// </summary>
internal static partial class MarkdownFormatter
{
    private const string PlaceholderText = "text";
    private const string LinkAddress = "https://";
    private const string TaskPrefix = "- [ ] ";

    /// <summary>The languages of the code block choice; the empty name is plain text.</summary>
    public static readonly IReadOnlyList<string> CodeLanguages = ["", "csharp", "sql", "json", "html", "bash"];

    [GeneratedRegex(@"^(- \[[ xX]\] |- |\d+\. )")]
    private static partial Regex ListPrefix();

    public static EditorState Apply(MarkdownAction action, EditorState state)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(state);
        var text = state.Text ?? string.Empty;
        var start = Math.Clamp(Math.Min(state.Start, state.End), 0, text.Length);
        var end = Math.Clamp(Math.Max(state.Start, state.End), 0, text.Length);
        var current = new EditorState(text, start, end);

        return action.Kind switch
        {
            MarkdownActionKind.Heading1 => Heading(current, 1),
            MarkdownActionKind.Heading2 => Heading(current, 2),
            MarkdownActionKind.Heading3 => Heading(current, 3),
            MarkdownActionKind.NormalText => EditLines(current, lines => [.. lines.Select(StripHeading)]),
            MarkdownActionKind.Bold => Wrap(current, "**", "**"),
            MarkdownActionKind.Italic => Wrap(current, "*", "*"),
            MarkdownActionKind.Strikethrough => Wrap(current, "~~", "~~"),
            MarkdownActionKind.InlineCode => Wrap(current, "`", "`"),
            MarkdownActionKind.BulletedList => Prefixed(current, "- ", _ => "- ", l => l.StartsWith("- ", StringComparison.Ordinal) && !l.StartsWith(TaskPrefix, StringComparison.Ordinal)),
            MarkdownActionKind.NumberedList => NumberedList(current),
            MarkdownActionKind.TaskList => Prefixed(current, TaskPrefix, _ => TaskPrefix, l => l.StartsWith(TaskPrefix, StringComparison.Ordinal)),
            MarkdownActionKind.Quote => Prefixed(current, "> ", _ => "> ", l => l.StartsWith("> ", StringComparison.Ordinal), keepList: true),
            MarkdownActionKind.CodeBlock => CodeBlock(current, action.Argument),
            MarkdownActionKind.Link => Link(current, "["),
            MarkdownActionKind.Image => Link(current, "!["),
            MarkdownActionKind.Table => Block(current, current.End, current.End, TableTemplate, 2, 8),
            MarkdownActionKind.HorizontalRule => Block(current, current.End, current.End, "---", 3, 0),
            MarkdownActionKind.Color => Color(current, action.Argument, Markdown.ColorNames),
            MarkdownActionKind.Align => Color(current, action.Argument, Markdown.AlignNames),
            _ => current,
        };
    }

    // Around the selection; without a selection a placeholder that ends up selected.
    private static EditorState Wrap(EditorState s, string before, string after)
    {
        var selected = s.Start == s.End ? PlaceholderText : s.Text[s.Start..s.End];
        var text = s.Text[..s.Start] + before + selected + after + s.Text[s.End..];
        return new EditorState(text, s.Start + before.Length, s.Start + before.Length + selected.Length);
    }

    private static EditorState Color(EditorState s, string? name, IReadOnlyList<string> allowed) =>
        name is not null && allowed.Contains(name, StringComparer.Ordinal)
            ? Wrap(s, $"{{{name}}}", $"{{/{name}}}")
            : s;

    // [selection](https://) with the address selected, so it can be typed over.
    private static EditorState Link(EditorState s, string open)
    {
        var wrapped = Wrap(s, open, $"]({LinkAddress})");
        var address = wrapped.End + 2; // after "]("
        return new EditorState(wrapped.Text, address, address + LinkAddress.Length);
    }

    // The lines the selection touches; a heading prefix is replaced, and the same level again removes it.
    private static EditorState Heading(EditorState s, int level)
    {
        var prefix = new string('#', level) + " ";
        return EditLines(s, lines =>
        {
            var content = lines.Where(l => l.Length > 0).ToList();
            var allSame = content.Count > 0 && content.All(l => l.StartsWith(prefix, StringComparison.Ordinal));
            return [.. lines.Select(l => l.Length == 0 && lines.Count > 1 ? l : allSame ? StripHeading(l) : prefix + StripHeading(l))];
        });
    }

    // A prefix in front of every non-empty line; when all already have it, it is removed. Another list kind is replaced
    // (unless keepList, for a quote around a list).
    private static EditorState Prefixed(EditorState s, string prefix, Func<int, string> make, Func<string, bool> has, bool keepList = false) =>
        EditLines(s, lines =>
        {
            var content = lines.Where(l => l.Length > 0).ToList();
            var all = content.Count > 0 && content.All(has);
            return [.. lines.Select(l => l.Length == 0 && lines.Count > 1 ? l : all ? l[prefix.Length..] : make(0) + (keepList ? l : StripList(l)))];
        });

    private static EditorState NumberedList(EditorState s) =>
        EditLines(s, lines =>
        {
            var content = lines.Where(l => l.Length > 0).ToList();
            var all = content.Count > 0 && content.All(l => NumberedMatch(l).Success);
            var n = 0;
            return [.. lines.Select(l => l.Length == 0 && lines.Count > 1 ? l : all ? l[NumberedMatch(l).Length..] : $"{++n}. " + StripList(l))];
        });

    private static Match NumberedMatch(string line) => NumberedPrefix().Match(line);

    [GeneratedRegex(@"^\d+\. ")]
    private static partial Regex NumberedPrefix();

    private static string StripList(string line) => ListPrefix().Replace(line, string.Empty, 1);


    private const string TableTemplate = "| Column 1 | Column 2 |\n| --- | --- |\n|  |  |";

    // A fenced block around the selection (or a placeholder); an unknown language falls back to plain text.
    private static EditorState CodeBlock(EditorState s, string? language)
    {
        var lang = language is not null && CodeLanguages.Contains(language, StringComparer.Ordinal) ? language : string.Empty;
        var code = s.Start == s.End ? "code" : s.Text[s.Start..s.End];
        var fence = "```" + lang + "\n";
        return Block(s, s.Start, s.End, fence + code + "\n```", fence.Length, code.Length);
    }

    // Replaces [from, to) by a block on its own lines, separated from the text around it by a blank line.
    private static EditorState Block(EditorState s, int from, int to, string content, int selectOffset, int selectLength)
    {
        var before = s.Text[..from];
        var after = s.Text[to..];
        var head = before.Length == 0 || before.EndsWith("\n\n", StringComparison.Ordinal) ? string.Empty
            : before.EndsWith('\n') ? "\n" : "\n\n";
        var tail = after.Length == 0 ? "\n" : after.StartsWith("\n\n", StringComparison.Ordinal) ? string.Empty
            : after.StartsWith('\n') ? "\n" : "\n\n";
        var begin = before.Length + head.Length;
        return new EditorState(before + head + content + tail + after, begin + selectOffset, begin + selectOffset + selectLength);
    }


    private static string StripHeading(string line)
    {
        var i = 0;
        while (i < line.Length && i < 6 && line[i] == '#')
        {
            i++;
        }

        return i > 0 && i < line.Length && line[i] == ' ' ? line[(i + 1)..] : line;
    }

    private static EditorState EditLines(EditorState s, Func<List<string>, List<string>> change)
    {
        var from = s.Start == 0 ? 0 : s.Text.LastIndexOf('\n', s.Start - 1) + 1;
        var lineEnd = s.Text.IndexOf('\n', s.End);
        var to = lineEnd < 0 ? s.Text.Length : lineEnd;
        var edited = string.Join('\n', change([.. s.Text[from..to].Split('\n')]));
        var text = s.Text[..from] + edited + s.Text[to..];
        // Multi-line: the whole block stays selected; a single line: the cursor goes to the end of it.
        return s.Start == s.End || !edited.Contains('\n', StringComparison.Ordinal)
            ? new EditorState(text, from + edited.Length, from + edited.Length)
            : new EditorState(text, from, from + edited.Length);
    }
}
