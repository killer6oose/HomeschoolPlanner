using HomeschoolPlanner.Helpers;

namespace HomeschoolPlanner.Models;

// Represents a distinct student + grade pairing used in the Reports dialog.
// A student who has been promoted from Grade 1 to Grade 2 will produce two
// of these - one per grade - so reports can be run per grade level separately.
public class StudentGradeGroup
{
    public int    StudentId    { get; set; }
    public string StudentName  { get; set; } = "";
    public string StudentColor { get; set; } = "#4A7CB5";
    // The grade key ("1", "2", "K", etc.) already resolved with fallback applied
    public string GradeKey     { get; set; } = "";

    public string GradeDisplay => GradeHelper.KeyToDisplay(GradeKey);
    public string Label        => $"{StudentName} - {GradeDisplay}";

    // Stable key used to identify this pair in checked-sets
    public string Key => $"{StudentId}:{GradeKey}";
}
