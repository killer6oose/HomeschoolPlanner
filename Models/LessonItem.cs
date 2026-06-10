namespace HomeschoolPlanner.Models;

// An individual lesson within a subject block on a given day.
// One LessonEntry can have many LessonItems.
public class LessonItem
{
    public int Id { get; set; }
    public int LessonEntryId { get; set; }
    public string Title { get; set; } = "";
    public string SubTitle { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsComplete { get; set; }
}
