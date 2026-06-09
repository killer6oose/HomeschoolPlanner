namespace HomeschoolPlanner.Models;

// A class (subject) in the grade-level class library.
// Also serves as the template record: ScheduleType/ScheduleDays define the
// default schedule that gets applied when a student of that grade is created.
public class GradeClass
{
    public int Id { get; set; }

    // "PreK" | "K" | "1" .. "12"
    public string GradeKey { get; set; } = "";

    public string Name { get; set; } = "";
    public string Color { get; set; } = "#4A7CB5";

    // Default schedule for template use
    public string ScheduleType { get; set; } = "EveryDay";   // EveryDay | DaysOfWeek | None
    public string ScheduleDays { get; set; } = "";            // "1,2,3,4,5" for Mon-Fri
}
