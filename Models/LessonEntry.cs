namespace HomeschoolPlanner.Models;

public class LessonEntry
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public int StudentId { get; set; }
    // Stored as YYYY-MM-DD
    public string LessonDate { get; set; } = "";
    public string Title { get; set; } = "";
    public string Notes { get; set; } = "";
    public bool IsComplete { get; set; } = false;

    // Convenience: parsed date for display logic
    public DateTime Date => DateTime.TryParse(LessonDate, out var d) ? d : DateTime.MinValue;
}
