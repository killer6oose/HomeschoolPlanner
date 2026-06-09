namespace HomeschoolPlanner.Models;

public class Resource
{
    public int    Id          { get; set; }
    public string Name        { get; set; } = "";
    public string Type        { get; set; } = "URL";  // URL | File
    public string Path        { get; set; } = "";
    public int?   SubjectId   { get; set; }            // null = not linked to a class
    public string Description { get; set; } = "";

    // Not stored; populated by join query for display
    public string SubjectName { get; set; } = "";

    public override string ToString() => Name;
}
