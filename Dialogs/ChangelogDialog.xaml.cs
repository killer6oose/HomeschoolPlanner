using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace HomeschoolPlanner.Dialogs;

public partial class ChangelogDialog : Window
{
    public ChangelogDialog(string version, string markdown)
    {
        InitializeComponent();
        VersionLabel.Text      = $"Version {version}";
        ChangelogBox.Document  = BuildDocument(markdown);
    }

    private void GotIt_Click(object sender, RoutedEventArgs e) => Close();

    // Parses a simple markdown subset into a FlowDocument.
    // Supported: **bold**, [text](url), - bullet lists, --- horizontal rule, blank lines.
    // A line starting with **Change Date:** also gets an underline applied.
    private FlowDocument BuildDocument(string markdown)
    {
        var doc = new FlowDocument
        {
            FontFamily  = (FontFamily)Application.Current.Resources["AppFontFamily"],
            FontSize    = (double)Application.Current.Resources["AppFontSize"],
            Foreground  = (Brush)Application.Current.Resources["TextPrimaryBrush"],
            PagePadding = new Thickness(0),
            LineHeight  = double.NaN
        };

        var lines       = markdown.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        var bulletLines = new List<string>();

        void FlushBullets()
        {
            if (bulletLines.Count == 0) return;

            var list = new List
            {
                MarkerStyle = TextMarkerStyle.Disc,
                Margin      = new Thickness(0, 2, 0, 10),
                Padding     = new Thickness(16, 0, 0, 0)
            };
            foreach (var b in bulletLines)
            {
                var item = new ListItem(new Paragraph(ParseInlines(b))
                {
                    Margin = new Thickness(0, 1, 0, 1)
                });
                list.ListItems.Add(item);
            }
            doc.Blocks.Add(list);
            bulletLines.Clear();
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();

            if (line.StartsWith("- "))
            {
                bulletLines.Add(line.Substring(2));
                continue;
            }

            FlushBullets();

            if (line == "---")
            {
                // Horizontal rule - thin bottom border on an otherwise empty paragraph
                doc.Blocks.Add(new Paragraph
                {
                    BorderBrush     = (Brush)Application.Current.Resources["BorderBrush"],
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Margin          = new Thickness(0, 4, 0, 10),
                    Padding         = new Thickness(0)
                });
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var para = new Paragraph { Margin = new Thickness(0, 0, 0, 6) };

            // "**Change Date:**..." lines get bold + underline on the whole paragraph
            if (line.StartsWith("**Change Date:**"))
            {
                var underline = new Underline(ParseInlines(line));
                para.Inlines.Add(underline);
                para.FontWeight = FontWeights.Bold;
            }
            else
            {
                para.Inlines.Add(ParseInlines(line));
            }

            doc.Blocks.Add(para);
        }

        FlushBullets();
        return doc;
    }

    // Walks a line of text and emits Bold, Hyperlink, or plain Run inlines.
    private Span ParseInlines(string text)
    {
        var result = new Span();
        int i      = 0;

        while (i < text.Length)
        {
            // **bold**
            if (i + 1 < text.Length && text[i] == '*' && text[i + 1] == '*')
            {
                int end = text.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (end >= 0)
                {
                    result.Inlines.Add(new Bold(new Run(text.Substring(i + 2, end - i - 2))));
                    i = end + 2;
                    continue;
                }
            }

            // [link text](url)
            if (text[i] == '[')
            {
                int textEnd = text.IndexOf(']', i + 1);
                if (textEnd >= 0 && textEnd + 1 < text.Length && text[textEnd + 1] == '(')
                {
                    int urlEnd = text.IndexOf(')', textEnd + 2);
                    if (urlEnd >= 0)
                    {
                        var linkText = text.Substring(i + 1, textEnd - i - 1);
                        var url      = text.Substring(textEnd + 2, urlEnd - textEnd - 2);
                        var link     = new Hyperlink(new Run(linkText))
                        {
                            Foreground = (Brush)Application.Current.Resources["AccentBrush"]
                        };
                        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                        {
                            link.NavigateUri = uri;
                            link.RequestNavigate += (_, e) =>
                                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
                                    { UseShellExecute = true });
                        }
                        result.Inlines.Add(link);
                        i = urlEnd + 1;
                        continue;
                    }
                }
            }

            // Plain text - accumulate until the next token start
            int start = i;
            while (i < text.Length)
            {
                if ((i + 1 < text.Length && text[i] == '*' && text[i + 1] == '*') || text[i] == '[')
                    break;
                i++;
            }
            if (i > start)
                result.Inlines.Add(new Run(text.Substring(start, i - start)));
            else
                result.Inlines.Add(new Run(text[i++].ToString()));
        }

        return result;
    }
}
