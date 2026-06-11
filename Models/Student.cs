namespace HomeschoolPlanner.Models;

public class Student
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Grade { get; set; } = "";
    // Hex color used as the accent for this student's display
    public string Color { get; set; } = "#4A7CB5";
    // School year this student is currently enrolled in, e.g. "2025-2026"
    public string SchoolYear { get; set; } = "";
}
