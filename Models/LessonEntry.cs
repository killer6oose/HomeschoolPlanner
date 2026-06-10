namespace HomeschoolPlanner.Models;

public class LessonEntry
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public int StudentId { get; set; }
    // Stored as YYYY-MM-DD
    public string LessonDate { get; set; } = "";
    public string Notes { get; set; } = "";
    // True when the whole block is marked complete (overrides per-item state)
    public bool IsComplete { get; set; } = false;

    // Loaded by DatabaseService.GetEntry / GetEntriesForRange
    public List<LessonItem> Items { get; set; } = new();

    // Convenience: parsed date for display logic
    public DateTime Date => DateTime.TryParse(LessonDate, out var d) ? d : DateTime.MinValue;
}
