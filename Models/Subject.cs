namespace HomeschoolPlanner.Models;

public class Subject
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string Name { get; set; } = "";
    // Hex color for this subject's block
    public string Color { get; set; } = "#4A7CB5";
    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    // --- Scheduling ---
    // 'EveryDay'     - appears Mon-Fri every week
    // 'DaysOfWeek'   - appears on specific days of the week (stored in ScheduleDays)
    // 'SpecificDates'- appears only on explicit dates (stored in ScheduleDates)
    // 'None'         - no automatic schedule; must be added manually per day
    public string ScheduleType { get; set; } = "None";

    // Comma-separated ISO day numbers Mon=1..Fri=5, e.g. "1,3,5" for Mon/Wed/Fri
    public string ScheduleDays { get; set; } = "";

    // Comma-separated YYYY-MM-DD dates, e.g. "2026-06-09,2026-06-10"
    public string ScheduleDates { get; set; } = "";

    // Returns true if this subject should appear on the given date based on its schedule
    public bool IsScheduledOn(DateTime date)
    {
        switch (ScheduleType)
        {
            case "EveryDay":
                // Mon-Fri only
                return date.DayOfWeek >= DayOfWeek.Monday && date.DayOfWeek <= DayOfWeek.Friday;

            case "DaysOfWeek":
                if (string.IsNullOrWhiteSpace(ScheduleDays)) return false;
                // Convert DayOfWeek to Mon=1..Sun=7
                int isoDay = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
                var days = ScheduleDays.Split(',').Select(d => d.Trim());
                return days.Contains(isoDay.ToString());

            case "SpecificDates":
                if (string.IsNullOrWhiteSpace(ScheduleDates)) return false;
                var dateStr = date.ToString("yyyy-MM-dd");
                var dates = ScheduleDates.Split(',').Select(d => d.Trim());
                return dates.Contains(dateStr);

            default:
                return false;
        }
    }
}
