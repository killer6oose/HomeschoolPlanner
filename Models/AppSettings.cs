namespace HomeschoolPlanner.Models;

public class AppSettings
{
    public string Theme { get; set; } = "Light";

    // Custom theme colors (used when Theme == "Custom")
    public string CustomPrimaryColor    { get; set; } = "#4A7CB5";
    public string CustomSecondaryColor  { get; set; } = "#F5F6FA";
    public string CustomFontColor       { get; set; } = "#1C2333";

    // "Small" | "Medium" | "Large"
    public string FontSize { get; set; } = "Medium";

    public string FontFamily { get; set; } = "Segoe UI";

    // YYYY-MM-DD
    public string SchoolYearStart { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");

    // Comma-separated ISO day numbers: Mon=1, Tue=2, Wed=3, Thu=4, Fri=5, Sat=6, Sun=7
    public string SchoolDays { get; set; } = "1,2,3,4,5";

    // Parsed school day numbers in order
    public int[] SchoolDayNumbers =>
        SchoolDays.Split(',')
                  .Select(d => d.Trim())
                  .Where(d => int.TryParse(d, out _))
                  .Select(int.Parse)
                  .OrderBy(d => d)
                  .ToArray();

    // Font size mapped to a WPF double
    public double FontSizeValue => FontSize switch
    {
        "Small" => 11.0,
        "Large" => 16.0,
        _       => 13.0   // Medium
    };
}
