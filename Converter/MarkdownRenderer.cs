using Markdig;

namespace MdConverter.Converter;

/// <summary>
/// Converts a Markdown string to a self-contained HTML page for WebView2 preview.
/// </summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseEmojiAndSmiley()
        .Build();

    public static string ToHtml(string markdown, bool isDark = false)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return WrapInPage("<p style='color:#888;font-style:italic'>No content yet.</p>", isDark);

        var body = Markdig.Markdown.ToHtml(markdown, Pipeline);
        return WrapInPage(body, isDark);
    }

    private static string WrapInPage(string body, bool isDark)
    {
        string bg        = isDark ? "#1e1e2e" : "#ffffff";
        string fg        = isDark ? "#cdd6f4" : "#1e1e1e";
        string headingFg = isDark ? "#cdd6f4" : "#111111";
        string h1Border  = isDark ? "#45475a" : "#e0e0e0";
        string h2Border  = isDark ? "#45475a" : "#e8e8e8";
        string codeBg    = isDark ? "#313244" : "#f3f3f3";
        string preBg     = isDark ? "#11111b" : "#1e1e1e";
        string preFg     = isDark ? "#cdd6f4" : "#d4d4d4";
        string bqBg      = isDark ? "#1e2030" : "#f0f7ff";
        string bqBorder  = isDark ? "#89b4fa" : "#0078d4";
        string hrColor   = isDark ? "#45475a" : "#e0e0e0";
        string thBg      = isDark ? "#313244" : "#f0f4f8";
        string tdBorder  = isDark ? "#45475a" : "#d0d7de";
        string tdEvenBg  = isDark ? "#252535" : "#f9fbfc";
        string trHoverBg = isDark ? "#2a2a3a" : "#eef4fb";
        string linkFg    = isDark ? "#89b4fa" : "#0078d4";

        return $$"""
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
                color: {{fg}};
                background: {{bg}};
                max-width: 860px;
                margin: 0 auto;
                padding: 24px 28px 48px;
              }

              h1 { font-size: 1.9em; border-bottom: 2px solid {{h1Border}}; padding-bottom: .3em; margin-top: 1em; }
              h2 { font-size: 1.45em; border-bottom: 1px solid {{h2Border}}; padding-bottom: .25em; margin-top: 1.4em; }
              h3 { font-size: 1.15em; margin-top: 1.2em; }
              h1, h2, h3 { font-weight: 700; color: {{headingFg}}; }

              p { margin: .6em 0; }

              a { color: {{linkFg}}; text-decoration: none; }
              a:hover { text-decoration: underline; }

              code {
                font-family: "Cascadia Code", Consolas, "Courier New", monospace;
                font-size: .88em;
                background: {{codeBg}};
                padding: 2px 5px;
                border-radius: 3px;
              }

              pre {
                background: {{preBg}};
                color: {{preFg}};
                padding: 14px 16px;
                border-radius: 6px;
                overflow-x: auto;
                font-size: .88em;
              }
              pre code { background: none; padding: 0; color: inherit; }

              blockquote {
                border-left: 4px solid {{bqBorder}};
                margin: 1em 0;
                padding: .4em 1em;
                color: {{fg}};
                background: {{bqBg}};
                border-radius: 0 4px 4px 0;
              }

              hr {
                border: none;
                border-top: 2px solid {{hrColor}};
                margin: 1.5em 0;
              }

              ul, ol { padding-left: 1.6em; margin: .5em 0; }
              li { margin: .2em 0; }

              table {
                border-collapse: collapse;
                width: 100%;
                margin: 1em 0;
                font-size: .93em;
              }
              th {
                background: {{thBg}};
                font-weight: 600;
                text-align: left;
                padding: 8px 12px;
                border: 1px solid {{tdBorder}};
                color: {{fg}};
              }
              td {
                padding: 7px 12px;
                border: 1px solid {{tdBorder}};
                vertical-align: top;
                color: {{fg}};
              }
              tr:nth-child(even) td { background: {{tdEvenBg}}; }
              tr:hover td { background: {{trHoverBg}}; }

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
}
