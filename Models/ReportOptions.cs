namespace HomeschoolPlanner.Models;

public class ReportOptions
{
    public List<Student> Students { get; set; } = new();
    public List<Subject> Subjects { get; set; } = new();
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime EndDate   { get; set; } = DateTime.Today;

    // "Summary" = per-subject completion totals
    // "Log"     = day-by-day chronological list
    public string ReportType   { get; set; } = "Summary";

    // "PDF" | "CSV"
    public string OutputFormat { get; set; } = "PDF";
}
