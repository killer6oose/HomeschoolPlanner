namespace HomeschoolPlanner.Models;

public class Subject
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#4A7CB5";
    // Grade the student was in when this subject was created (e.g. "1", "2", "K").
    // Empty string on pre-migration rows - treated as the student's current grade.
    public string GradeKey { get; set; } = "";
    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    // --- Scheduling ---
    // 'EveryDay'      - Mon-Fri every week
    // 'DaysOfWeek'    - specific days (ScheduleDays)
    // 'SpecificDates' - explicit dates (ScheduleDates)
    // 'Monthly'       - monthly pattern (ScheduleMonthly)
    // 'None'          - no automatic schedule
    public string ScheduleType { get; set; } = "None";

    // ISO day numbers Mon=1..Sun=7 e.g. "1,3,5"
    public string ScheduleDays { get; set; } = "";

    // Comma-separated YYYY-MM-DD for specific dates
    public string ScheduleDates { get; set; } = "";

    // Monthly: "First Mon", "Last Fri", "15" (day of month)
    public string ScheduleMonthly { get; set; } = "";

    // Optional end conditions for DaysOfWeek / EveryDay / Monthly
    // 'None' | 'ByDate' | 'ByCount'
    public string ScheduleEndType { get; set; } = "None";
    public string ScheduleEndDate { get; set; } = "";   // YYYY-MM-DD
    public int ScheduleEndCount { get; set; } = 0;      // number of occurrences

    // Comma-separated YYYY-MM-DD dates to skip (single-occurrence deletions)
    public string ExcludedDates { get; set; } = "";

    // Returns true if this subject should appear on the given date
    public bool IsScheduledOn(DateTime date)
    {
        var dateStr = date.ToString("yyyy-MM-dd");

        // Skip excluded dates
        if (!string.IsNullOrEmpty(ExcludedDates))
        {
            var excluded = ExcludedDates.Split(',').Select(d => d.Trim());
            if (excluded.Contains(dateStr)) return false;
        }

        // Check end-by-date
        if (ScheduleEndType == "ByDate" && !string.IsNullOrEmpty(ScheduleEndDate))
        {
            if (DateTime.TryParse(ScheduleEndDate, out var endDate) && date.Date > endDate.Date)
                return false;
        }

        bool baseMatch = ScheduleType switch
        {
            "EveryDay" =>
                date.DayOfWeek >= DayOfWeek.Monday && date.DayOfWeek <= DayOfWeek.Friday,

            "DaysOfWeek" => !string.IsNullOrWhiteSpace(ScheduleDays) && MatchesDayOfWeek(date),

            "SpecificDates" => !string.IsNullOrWhiteSpace(ScheduleDates) &&
                ScheduleDates.Split(',').Select(d => d.Trim()).Contains(dateStr),

            "Monthly" => MatchesMonthly(date),

            _ => false
        };

        if (!baseMatch) return false;

        // Check end-by-count (requires counting previous occurrences - expensive,
        // so we store it as "don't show after N occurrences from start")
        // For ByCount we rely on the start being the first scheduled date visible.
        // This is handled when saving: we pre-expand to SpecificDates.
        return true;
    }

    private bool MatchesDayOfWeek(DateTime date)
    {
        int isoDay = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
        return ScheduleDays.Split(',').Select(d => d.Trim()).Contains(isoDay.ToString());
    }

    private bool MatchesMonthly(DateTime date)
    {
        if (string.IsNullOrEmpty(ScheduleMonthly)) return false;

        // Numeric: day of month e.g. "15"
        if (int.TryParse(ScheduleMonthly, out int dayOfMonth))
            return date.Day == dayOfMonth;

        // "First Mon", "Last Fri", "First Weekday", "Last Weekday"
        var parts = ScheduleMonthly.Split(' ');
        if (parts.Length != 2) return false;

        var which = parts[0]; // "First" or "Last"
        var dayName = parts[1]; // "Mon","Tue","Wed","Thu","Fri","Sat","Sun","Weekday","Weekend"

        DayOfWeek? targetDow = dayName switch
        {
            "Mon" => DayOfWeek.Monday,
            "Tue" => DayOfWeek.Tuesday,
            "Wed" => DayOfWeek.Wednesday,
            "Thu" => DayOfWeek.Thursday,
            "Fri" => DayOfWeek.Friday,
            "Sat" => DayOfWeek.Saturday,
            "Sun" => DayOfWeek.Sunday,
            _ => null
        };

        if (targetDow == null) return false;

        if (which == "First")
        {
            // Find the first occurrence of targetDow in this month
            var first = new DateTime(date.Year, date.Month, 1);
            int diff = ((int)targetDow.Value - (int)first.DayOfWeek + 7) % 7;
            return date.Day == 1 + diff;
        }
        else if (which == "Last")
        {
            // Find the last occurrence of targetDow in this month
            var lastDay = new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
            int diff = ((int)lastDay.DayOfWeek - (int)targetDow.Value + 7) % 7;
            return date.Day == lastDay.Day - diff;
        }

        return false;
    }
}
