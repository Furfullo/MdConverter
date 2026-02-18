using Markdig;

namespace MdConverter.Converter;

/// <summary>
/// Converts a Markdown string to a self-contained HTML page for WebView2 preview.
/// </summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()   // tables, task lists, footnotes, etc.
        .UseEmojiAndSmiley()
        .Build();

    public static string ToHtml(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return WrapInPage("<p style='color:#888;font-style:italic'>No content yet.</p>");

        var body = Markdig.Markdown.ToHtml(markdown, Pipeline);
        return WrapInPage(body);
    }

    private static string WrapInPage(string body) => $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="UTF-8"/>
        <meta name="viewport" content="width=device-width, initial-scale=1"/>
        <style>
          *, *::before, *::after { box-sizing: border-box; }

          body {
            font-family: -apple-system, "Segoe UI", Roboto, Arial, sans-serif;
            font-size: 15px;
            line-height: 1.65;
            color: #1e1e1e;
            background: #ffffff;
            max-width: 860px;
            margin: 0 auto;
            padding: 24px 28px 48px;
          }

          h1 { font-size: 1.9em; border-bottom: 2px solid #e0e0e0; padding-bottom: .3em; margin-top: 1em; }
          h2 { font-size: 1.45em; border-bottom: 1px solid #e8e8e8; padding-bottom: .25em; margin-top: 1.4em; }
          h3 { font-size: 1.15em; margin-top: 1.2em; }
          h1, h2, h3 { font-weight: 700; color: #111; }

          p { margin: .6em 0; }

          a { color: #0078d4; text-decoration: none; }
          a:hover { text-decoration: underline; }

          code {
            font-family: "Cascadia Code", Consolas, "Courier New", monospace;
            font-size: .88em;
            background: #f3f3f3;
            padding: 2px 5px;
            border-radius: 3px;
          }

          pre {
            background: #1e1e1e;
            color: #d4d4d4;
            padding: 14px 16px;
            border-radius: 6px;
            overflow-x: auto;
            font-size: .88em;
          }
          pre code { background: none; padding: 0; color: inherit; }

          blockquote {
            border-left: 4px solid #0078d4;
            margin: 1em 0;
            padding: .4em 1em;
            color: #555;
            background: #f0f7ff;
            border-radius: 0 4px 4px 0;
          }

          hr {
            border: none;
            border-top: 2px solid #e0e0e0;
            margin: 1.5em 0;
          }

          ul, ol { padding-left: 1.6em; margin: .5em 0; }
          li { margin: .2em 0; }

          /* Tables */
          table {
            border-collapse: collapse;
            width: 100%;
            margin: 1em 0;
            font-size: .93em;
          }
          th {
            background: #f0f4f8;
            font-weight: 600;
            text-align: left;
            padding: 8px 12px;
            border: 1px solid #d0d7de;
          }
          td {
            padding: 7px 12px;
            border: 1px solid #d0d7de;
            vertical-align: top;
          }
          tr:nth-child(even) td { background: #f9fbfc; }
          tr:hover td { background: #eef4fb; }

          strong { font-weight: 700; }
          em     { font-style: italic; }
        </style>
        </head>
        <body>
        {{body}}
        </body>
        </html>
        """;
}
