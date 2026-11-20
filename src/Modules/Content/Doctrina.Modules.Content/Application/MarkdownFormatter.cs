namespace Doctrina.Modules.Content.Application;

/// <summary>What a toolbar button does (design D4 of add-markdown-toolbar).</summary>
internal enum MarkdownActionKind { Heading1, Heading2, Heading3, Bold, Italic, BulletedList, Link, Color, Align }

/// <summary>A toolbar action; <see cref="ColorName"/> is only used by <see cref="MarkdownActionKind.Color"/>.</summary>
internal sealed record MarkdownAction(MarkdownActionKind Kind, string? ColorName = null);

/// <summary>The text of the editor and the selection (<see cref="Start"/> up to <see cref="End"/>, both 0-based).</summary>
internal sealed record EditorState(string Text, int Start, int End);

/// <summary>
/// Inserts Markdown syntax for a toolbar button (design D1 and D4 of add-markdown-toolbar). Pure: the browser only
/// reports and applies the text and the selection, so all the rules can be tested here.
/// </summary>
internal static class MarkdownFormatter
{
    private const string PlaceholderText = "text";
    private const string LinkAddress = "https://";

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
            MarkdownActionKind.Bold => Wrap(current, "**", "**"),
            MarkdownActionKind.Italic => Wrap(current, "*", "*"),
            MarkdownActionKind.BulletedList => BulletedList(current),
            MarkdownActionKind.Link => Link(current),
            MarkdownActionKind.Color => Color(current, action.ColorName, Markdown.ColorNames),
            MarkdownActionKind.Align => Color(current, action.ColorName, Markdown.AlignNames),
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
    private static EditorState Link(EditorState s)
    {
        var wrapped = Wrap(s, "[", $"]({LinkAddress})");
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

    // "- " in front of every non-empty line; when all already have it, it is removed.
    private static EditorState BulletedList(EditorState s) =>
        EditLines(s, lines =>
        {
            var content = lines.Where(l => l.Length > 0).ToList();
            var allListed = content.Count > 0 && content.All(l => l.StartsWith("- ", StringComparison.Ordinal));
            return [.. lines.Select(l => l.Length == 0 && lines.Count > 1 ? l : allListed ? l[2..] : "- " + l)];
        });

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
