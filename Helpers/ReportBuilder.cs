using System.IO;
using System.Text;
using HomeschoolPlanner.Data;
using HomeschoolPlanner.Models;
using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HomeschoolPlanner.Helpers;

public static class ReportBuilder
{
    static ReportBuilder()
    {
        Settings.License = LicenseType.Community;
    }

    // -------------------------------------------------------------------------
    // Data structures
    // -------------------------------------------------------------------------

    public record SubjectRow(
        string StudentName,
        string StudentColor,
        string SubjectName,
        string SubjectColor,
        int    Scheduled,
        int    Completed)
    {
        public double RateValue => Scheduled == 0 ? 0 : (double)Completed / Scheduled;
        public string RatePct   => Scheduled == 0 ? "N/A"
            : $"{(int)Math.Round(RateValue * 100)}%";
    }

    public record DayRow(
        DateTime Date,
        string   StudentName,
        string   StudentColor,
        string   SubjectName,
        string   SubjectColor,
        bool     HasEntry,
        bool     IsComplete,
        string   Notes);

    // -------------------------------------------------------------------------
    // Public entry point
    // -------------------------------------------------------------------------

    public static void Generate(ReportOptions opts, DatabaseService db, string outputPath)
    {
        if (opts.OutputFormat == "CSV")
            File.WriteAllText(outputPath, BuildCsv(opts, db), Encoding.UTF8);
        else
            BuildPdf(opts, db, outputPath);
    }

    // -------------------------------------------------------------------------
    // CSV  (unchanged - colors don't apply)
    // -------------------------------------------------------------------------

    private static string BuildCsv(ReportOptions opts, DatabaseService db)
    {
        var sb = new StringBuilder();

        if (opts.ReportType == "Summary")
        {
            sb.AppendLine("Student,Subject,Scheduled Sessions,Completed Sessions,Completion Rate");
            foreach (var row in ComputeSummary(opts, db))
                sb.AppendLine($"\"{row.StudentName}\",\"{row.SubjectName}\",{row.Scheduled},{row.Completed},{row.RatePct}");
        }
        else
        {
            sb.AppendLine("Date,Student,Subject,Completed,Notes");
            foreach (var row in ComputeLog(opts, db))
            {
                var completed = row.HasEntry ? (row.IsComplete ? "Yes" : "No") : "No entry";
                var notes     = row.Notes.Replace("\"", "\"\"");
                sb.AppendLine($"{row.Date:yyyy-MM-dd},\"{row.StudentName}\",\"{row.SubjectName}\",{completed},\"{notes}\"");
            }
        }

        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // PDF
    // -------------------------------------------------------------------------

    // Navy banner color used across all report styles
    private const string BannerColor = "#1E3A5F";
    private const string BannerText  = "#FFFFFF";

    private static void BuildPdf(ReportOptions opts, DatabaseService db, string outputPath)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontFamily("Segoe UI").FontSize(9));

                // ----- Header -----
                page.Header()
                    .Column(hdr =>
                    {
                        // Colored banner
                        hdr.Item()
                           .Background(BannerColor)
                           .Padding(12)
                           .Row(row =>
                           {
                               row.RelativeItem().Column(col =>
                               {
                                   col.Item()
                                      .Text("HOMESCHOOL PROGRESS REPORT")
                                      .FontSize(15).Bold().FontColor(BannerText);
                                   col.Item()
                                      .Text($"{opts.StartDate:MMMM d, yyyy}  –  {opts.EndDate:MMMM d, yyyy}")
                                      .FontSize(9).FontColor("#A8C4E0");
                               });
                               row.ConstantItem(130).AlignRight().AlignMiddle()
                                  .Text($"Generated {DateTime.Today:MMM d, yyyy}")
                                  .FontSize(8).FontColor("#A8C4E0");
                           });

                        // Student chips under the banner (when multiple students)
                        if (opts.Students.Count > 1)
                        {
                            hdr.Item()
                               .Background("#F0F4FA")
                               .BorderBottom(1).BorderColor("#D0D8E8")
                               .PaddingHorizontal(12).PaddingVertical(6)
                               .Row(row =>
                               {
                                   row.AutoItem().AlignMiddle()
                                      .Text("Students: ").FontSize(8).FontColor(Colors.Grey.Darken2);
                                   foreach (var s in opts.Students)
                                   {
                                       var chipColor = s.Color;
                                       row.AutoItem()
                                          .Background(Lighten(chipColor, 0.75f))
                                          .Border(1).BorderColor(chipColor)
                                          .PaddingHorizontal(6).PaddingVertical(2)
                                          .PaddingLeft(3)
                                          .Text(s.Name)
                                          .FontSize(8).FontColor(Darken(chipColor, 0.3f));
                                   }
                               });
                        }

                        hdr.Item().Height(8);
                    });

                // ----- Content -----
                page.Content().Column(col =>
                {
                    if (opts.ReportType == "Summary")
                        BuildSummaryContent(col, opts, db);
                    else
                        BuildLogContent(col, opts, db);
                });

                // ----- Footer -----
                page.Footer()
                    .BorderTop(1).BorderColor(Colors.Grey.Lighten2)
                    .PaddingTop(6)
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                        x.Span(" of ").FontSize(8).FontColor(Colors.Grey.Medium);
                        x.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                    });
            });
        }).GeneratePdf(outputPath);
    }

    // -------------------------------------------------------------------------
    // Summary layout
    // -------------------------------------------------------------------------

    private static void BuildSummaryContent(ColumnDescriptor col, ReportOptions opts, DatabaseService db)
    {
        var rows = ComputeSummary(opts, db);

        foreach (var studentGroup in rows.GroupBy(r => r.StudentName))
        {
            var studentColor = studentGroup.First().StudentColor;
            var headerBg     = Lighten(studentColor, 0.82f);
            var headerBorder = studentColor;

            // Student section header
            col.Item()
               .Background(headerBg)
               .BorderLeft(4).BorderColor(headerBorder)
               .PaddingHorizontal(10).PaddingVertical(7)
               .Text(studentGroup.Key)
               .FontSize(11).Bold().FontColor(Darken(studentColor, 0.25f));

            col.Item().PaddingBottom(16).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(10); // color dot
                    c.RelativeColumn(4);  // subject name
                    c.RelativeColumn(1.4f);
                    c.RelativeColumn(1.4f);
                    c.RelativeColumn(1.2f); // rate (colored)
                });

                // Table header
                IContainer TH(IContainer c) =>
                    c.Background("#E8EDF5")
                     .Padding(5)
                     .BorderBottom(1).BorderColor("#C5CDE0");

                table.Header(h =>
                {
                    h.Cell().Element(TH); // dot column - no label
                    h.Cell().Element(TH).Text("Subject").Bold().FontColor("#2C3E6B");
                    h.Cell().Element(TH).AlignCenter().Text("Scheduled").Bold().FontColor("#2C3E6B");
                    h.Cell().Element(TH).AlignCenter().Text("Completed").Bold().FontColor("#2C3E6B");
                    h.Cell().Element(TH).AlignCenter().Text("Rate").Bold().FontColor("#2C3E6B");
                });

                bool alt = false;
                int totSched = 0, totComp = 0;

                foreach (var row in studentGroup)
                {
                    var bg = alt ? "#F7F8FC" : "#FFFFFF";
                    alt = !alt;
                    totSched += row.Scheduled;
                    totComp  += row.Completed;

                    IContainer TD(IContainer c) =>
                        c.Background(bg).PaddingVertical(5).PaddingHorizontal(5);

                    var rateColor = RateColor(row.RateValue);

                    // Colored dot using a filled-square glyph in the subject's color
                    table.Cell().Element(TD).AlignCenter().AlignMiddle()
                         .Text(t => t.Span("■").FontSize(10).FontColor(row.SubjectColor));

                    table.Cell().Element(TD).Text(row.SubjectName).FontColor(Colors.Grey.Darken3);
                    table.Cell().Element(TD).AlignCenter().Text(row.Scheduled.ToString()).FontColor(Colors.Grey.Darken2);
                    table.Cell().Element(TD).AlignCenter().Text(row.Completed.ToString()).FontColor(Colors.Grey.Darken2);
                    table.Cell().Element(TD).AlignCenter().Text(row.RatePct).Bold().FontColor(rateColor);
                }

                // Totals row
                double totRate    = totSched == 0 ? 0 : (double)totComp / totSched;
                string totRateStr = totSched == 0 ? "N/A" : $"{(int)Math.Round(totRate * 100)}%";
                var    totColor   = RateColor(totRate);

                IContainer TOT(IContainer c) =>
                    c.Background("#DDE4F0")
                     .PaddingVertical(5).PaddingHorizontal(5)
                     .BorderTop(1).BorderColor("#C5CDE0");

                table.Cell().Element(TOT);
                table.Cell().Element(TOT).Text("TOTAL").Bold().FontColor("#2C3E6B");
                table.Cell().Element(TOT).AlignCenter().Text(totSched.ToString()).Bold().FontColor("#2C3E6B");
                table.Cell().Element(TOT).AlignCenter().Text(totComp.ToString()).Bold().FontColor("#2C3E6B");
                table.Cell().Element(TOT).AlignCenter().Text(totRateStr).Bold().FontColor(totColor);
            });
        }
    }

    // -------------------------------------------------------------------------
    // Daily log layout
    // -------------------------------------------------------------------------

    private static void BuildLogContent(ColumnDescriptor col, ReportOptions opts, DatabaseService db)
    {
        var rows          = ComputeLog(opts, db);
        bool multiStudent = opts.Students.Count > 1;

        foreach (var dayGroup in rows.GroupBy(r => r.Date))
        {
            // Day header
            col.Item()
               .Background("#EEF2FF")
               .BorderLeft(3).BorderColor("#4A6FA5")
               .PaddingHorizontal(8).PaddingVertical(5)
               .Text(dayGroup.Key.ToString("dddd, MMMM d, yyyy"))
               .FontSize(10).Bold().FontColor("#1E3A5F");

            col.Item().PaddingBottom(10).PaddingLeft(12).Column(inner =>
            {
                // If multi-student, sub-group by student within the day
                if (multiStudent)
                {
                    foreach (var studentGroup in dayGroup.GroupBy(r => r.StudentName))
                    {
                        var sc = studentGroup.First().StudentColor;
                        inner.Item()
                             .PaddingTop(4)
                             .Text(studentGroup.Key)
                             .FontSize(8).Bold().FontColor(sc);

                        foreach (var row in studentGroup)
                            RenderLogRow(inner, row);
                    }
                }
                else
                {
                    foreach (var row in dayGroup)
                        RenderLogRow(inner, row);
                }
            });
        }
    }

    private static void RenderLogRow(ColumnDescriptor inner, DayRow row)
    {
        inner.Item().PaddingLeft(4).Row(r =>
        {
            // Colored subject dot
            r.ConstantItem(14).AlignMiddle()
             .Text(t => t.Span("■").FontSize(10).FontColor(row.SubjectColor));

            // Status icon
            var icon      = row.HasEntry ? (row.IsComplete ? "✓" : "○") : "–";
            var iconColor = row.IsComplete ? "#27AE60"
                          : row.HasEntry   ? "#E67E22"
                          :                  Colors.Grey.Medium;

            r.ConstantItem(14).AlignMiddle()
             .Text(t => t.Span(icon).FontColor(iconColor).FontSize(9));

            r.RelativeItem().Column(c =>
            {
                c.Item().Text(row.SubjectName)
                 .SemiBold().FontSize(9).FontColor(Colors.Grey.Darken3);
                if (!string.IsNullOrWhiteSpace(row.Notes))
                    c.Item().Text(row.Notes)
                     .FontSize(8).FontColor(Colors.Grey.Darken1).Italic();
            });
        });
    }

    // -------------------------------------------------------------------------
    // Data computation
    // -------------------------------------------------------------------------

    private static List<SubjectRow> ComputeSummary(ReportOptions opts, DatabaseService db)
    {
        var result     = new List<SubjectRow>();
        var schoolDays = AppState.Settings.SchoolDayNumbers.ToHashSet();
        var startStr   = opts.StartDate.ToString("yyyy-MM-dd");
        var endStr     = opts.EndDate.ToString("yyyy-MM-dd");

        var studentColorMap = opts.Students.ToDictionary(s => s.Id, s => s.Color);

        foreach (var student in opts.Students)
        {
            var entriesBySubject = db.GetEntriesForRange(student.Id, startStr, endStr)
                                     .ToLookup(e => e.SubjectId);

            foreach (var subject in opts.Subjects.Where(s => s.StudentId == student.Id))
            {
                int scheduled = 0;
                for (var d = opts.StartDate; d <= opts.EndDate; d = d.AddDays(1))
                {
                    int iso = d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;
                    if (schoolDays.Contains(iso) && subject.IsScheduledOn(d))
                        scheduled++;
                }

                int completed = entriesBySubject[subject.Id].Count(e => e.IsComplete);

                result.Add(new SubjectRow(
                    student.Name,
                    student.Color,
                    subject.Name,
                    subject.Color,
                    scheduled,
                    completed));
            }
        }

        return result;
    }

    private static List<DayRow> ComputeLog(ReportOptions opts, DatabaseService db)
    {
        var result     = new List<DayRow>();
        var schoolDays = AppState.Settings.SchoolDayNumbers.ToHashSet();
        var startStr   = opts.StartDate.ToString("yyyy-MM-dd");
        var endStr     = opts.EndDate.ToString("yyyy-MM-dd");

        foreach (var student in opts.Students)
        {
            var entryMap = db.GetEntriesForRange(student.Id, startStr, endStr)
                             .ToDictionary(e => (e.SubjectId, e.LessonDate));

            var studentSubjects = opts.Subjects.Where(s => s.StudentId == student.Id).ToList();

            for (var d = opts.StartDate; d <= opts.EndDate; d = d.AddDays(1))
            {
                int iso = d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;
                if (!schoolDays.Contains(iso)) continue;

                var dateStr = d.ToString("yyyy-MM-dd");
                foreach (var subject in studentSubjects.Where(s => s.IsScheduledOn(d)))
                {
                    entryMap.TryGetValue((subject.Id, dateStr), out var dayEntry);
                    result.Add(new DayRow(
                        Date:        d,
                        StudentName: student.Name,
                        StudentColor: student.Color,
                        SubjectName: subject.Name,
                        SubjectColor: subject.Color,
                        HasEntry:    dayEntry != null,
                        IsComplete:  dayEntry?.IsComplete ?? false,
                        Notes:       dayEntry?.Notes ?? ""));
                }
            }
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // Color utilities
    // -------------------------------------------------------------------------

    // Blend a hex color toward white by `factor` (0 = original, 1 = white)
    private static string Lighten(string hex, float factor)
    {
        var (r, g, b) = ParseHex(hex);
        var lr = (byte)(r + (255 - r) * factor);
        var lg = (byte)(g + (255 - g) * factor);
        var lb = (byte)(b + (255 - b) * factor);
        return $"#{lr:X2}{lg:X2}{lb:X2}";
    }

    // Blend a hex color toward black by `factor` (0 = original, 1 = black)
    private static string Darken(string hex, float factor)
    {
        var (r, g, b) = ParseHex(hex);
        var dr = (byte)(r * (1 - factor));
        var dg = (byte)(g * (1 - factor));
        var db = (byte)(b * (1 - factor));
        return $"#{dr:X2}{dg:X2}{db:X2}";
    }

    private static (byte r, byte g, byte b) ParseHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6) return (0x4A, 0x7C, 0xB5); // fallback blue
        return (
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16)
        );
    }

    // Color-code completion rate: green >= 80%, amber >= 50%, red < 50%
    private static string RateColor(double rate) =>
        rate >= 0.80 ? "#27AE60" :
        rate >= 0.50 ? "#D68910" :
                       "#C0392B";
}
