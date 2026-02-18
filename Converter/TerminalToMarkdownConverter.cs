using System.Text;

namespace MdConverter.Converter;

/// <summary>
/// Converts terminal/console output text to Markdown format.
/// Handles box-drawing tables, headers, separators, and bullet lists.
/// </summary>
public class TerminalToMarkdownConverter
{
    // Box-drawing characters used for table borders
    private static readonly char[] TableBorderChars = ['┌', '┐', '└', '┘', '├', '┤', '┬', '┴', '┼', '─', '│'];
    private static readonly char HorizontalLine = '─';
    private static readonly char VerticalBar = '│';

    public string Convert(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var lines = input
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Split('\n')
            .Select(l => l.TrimEnd())
            .ToArray();

        var result = new StringBuilder();
        int i = 0;
        bool firstHeader = true;

        while (i < lines.Length)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            // --- Table block ---
            if (IsTableTopBorder(trimmed))
            {
                int tableEnd = FindTableEnd(lines, i);
                result.Append(ConvertTable(lines, i, tableEnd));
                result.AppendLine();
                i = tableEnd + 1;
                continue;
            }

            // --- Empty line ---
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                result.AppendLine();
                i++;
                continue;
            }

            // --- Pure horizontal separator (─────) ---
            if (IsPureHorizontalRule(trimmed))
            {
                result.AppendLine("---");
                i++;
                continue;
            }

            // --- Bullet point ---
            if (IsBulletPoint(trimmed))
            {
                result.AppendLine(FormatBullet(trimmed));
                i++;
                continue;
            }

            // --- Header detection: standalone short line ---
            if (IsHeader(lines, i))
            {
                string prefix = firstHeader ? "#" : "##";
                firstHeader = false;
                result.AppendLine($"{prefix} {trimmed}");
                i++;
                continue;
            }

            // --- Sub-header: short line ending with colon, possibly starting with emoji ---
            if (IsSubHeader(trimmed))
            {
                result.AppendLine($"**{trimmed}**");
                i++;
                continue;
            }

            // --- Plain text ---
            result.AppendLine(trimmed);
            i++;
        }

        return result.ToString().TrimEnd();
    }

    // -------------------------------------------------------------------------
    // Table detection and conversion
    // -------------------------------------------------------------------------

    private static bool IsTableTopBorder(string trimmed) =>
        trimmed.StartsWith('┌') && trimmed.Contains('┐');

    private static bool IsTableBottomBorder(string trimmed) =>
        trimmed.StartsWith('└') && trimmed.Contains('┘');

    private static bool IsTableSeparatorRow(string trimmed) =>
        (trimmed.StartsWith('├') || trimmed.StartsWith('┌') || trimmed.StartsWith('└'))
        && trimmed.Any(c => c == '─');

    private static bool IsTableDataRow(string trimmed) =>
        trimmed.StartsWith('│') && trimmed.EndsWith('│');

    private static int FindTableEnd(string[] lines, int start)
    {
        for (int i = start + 1; i < lines.Length; i++)
        {
            if (IsTableBottomBorder(lines[i].TrimStart()))
                return i;
        }
        return start; // malformed table — treat as single line
    }

    private static string ConvertTable(string[] lines, int start, int end)
    {
        var result = new StringBuilder();
        bool headerDone = false;

        for (int i = start; i <= end; i++)
        {
            var trimmed = lines[i].TrimStart();

            if (IsTableDataRow(trimmed))
            {
                var cells = ParseDataRow(trimmed);
                result.AppendLine(BuildMarkdownRow(cells));

                if (!headerDone)
                {
                    // Insert separator row after the first data row (header row)
                    result.AppendLine(BuildSeparatorRow(cells.Count));
                    headerDone = true;
                }
            }
            // Skip border and separator lines (┌, ├, └ rows)
        }

        return result.ToString();
    }

    private static List<string> ParseDataRow(string line)
    {
        // Split by │ and trim whitespace from each cell
        var parts = line.Split(VerticalBar);
        var cells = new List<string>();

        // parts[0] is empty (before first │), parts[^1] is empty (after last │)
        for (int i = 1; i < parts.Length - 1; i++)
            cells.Add(parts[i].Trim());

        return cells;
    }

    private static string BuildMarkdownRow(List<string> cells) =>
        "| " + string.Join(" | ", cells) + " |";

    private static string BuildSeparatorRow(int columnCount) =>
        "| " + string.Join(" | ", Enumerable.Repeat("---", columnCount)) + " |";

    // -------------------------------------------------------------------------
    // Line classification helpers
    // -------------------------------------------------------------------------

    private static bool IsPureHorizontalRule(string trimmed) =>
        trimmed.Length >= 3 && trimmed.All(c => c == HorizontalLine || c == '-' || c == '=');

    private static bool IsBulletPoint(string trimmed) =>
        trimmed.StartsWith("- ") || trimmed.StartsWith("* ") ||
        trimmed.StartsWith("• ");

    private static string FormatBullet(string trimmed)
    {
        // Normalise bullet chars to markdown dash
        if (trimmed.StartsWith("• "))
            return "- " + trimmed[2..];
        return trimmed;
    }

    private static bool IsHeader(string[] lines, int index)
    {
        var trimmed = lines[index].TrimStart();

        // Length guard: not too short, not too long for a title
        if (trimmed.Length < 3 || trimmed.Length > 80)
            return false;

        // Must not look like a list item or separator
        if (IsBulletPoint(trimmed))
            return false;
        if (IsPureHorizontalRule(trimmed))
            return false;
        if (IsTableTopBorder(trimmed) || IsTableDataRow(trimmed) || IsTableSeparatorRow(trimmed))
            return false;

        // Must not end with colon (those are sub-headers)
        if (trimmed.EndsWith(':'))
            return false;

        // Must be surrounded by blank lines (or at document boundary)
        bool prevBlank = index == 0 || string.IsNullOrWhiteSpace(lines[index - 1]);
        bool nextBlank = index == lines.Length - 1 || string.IsNullOrWhiteSpace(lines[index + 1]);

        return prevBlank && nextBlank;
    }

    private static bool IsSubHeader(string trimmed)
    {
        // Short line ending with colon, e.g. "✅ Wins:" or "❌ Issues:"
        if (!trimmed.EndsWith(':'))
            return false;
        if (trimmed.Length > 60)
            return false;

        // Should not be a bullet
        if (IsBulletPoint(trimmed))
            return false;

        return true;
    }
}
