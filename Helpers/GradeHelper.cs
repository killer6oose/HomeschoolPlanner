namespace HomeschoolPlanner.Helpers;

public static class GradeHelper
{
    public static readonly (string Key, string Display)[] Grades =
    {
        ("PreK", "Pre-K"),
        ("K",    "Kindergarten"),
        ("1",    "1st Grade"),
        ("2",    "2nd Grade"),
        ("3",    "3rd Grade"),
        ("4",    "4th Grade"),
        ("5",    "5th Grade"),
        ("6",    "6th Grade"),
        ("7",    "7th Grade"),
        ("8",    "8th Grade"),
        ("9",    "9th Grade"),
        ("10",   "10th Grade"),
        ("11",   "11th Grade"),
        ("12",   "12th Grade"),
    };

    public static string KeyToDisplay(string key)
        => Grades.FirstOrDefault(g => g.Key == key).Display ?? key;

    public static string DisplayToKey(string display)
        => Grades.FirstOrDefault(g => g.Display == display).Key ?? display;
}
