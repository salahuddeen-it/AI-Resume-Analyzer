namespace ResumeAnalyzer.Web.Helpers;

public static class ScoreHelper
{
    public static int Round(double score) => (int)Math.Round(Math.Clamp(score, 0, 100));

    public static string Color(double score) => score switch
    {
        >= 80 => "#16a34a",
        >= 65 => "#4f46e5",
        >= 45 => "#f59e0b",
        _ => "#dc2626"
    };

    public static string Label(double score) => score switch
    {
        >= 80 => "Excellent",
        >= 65 => "Good",
        >= 45 => "Average",
        _ => "Needs Work"
    };

    public static string BadgeClass(double score) => score switch
    {
        >= 80 => "score-badge excellent",
        >= 65 => "score-badge good",
        >= 45 => "score-badge average",
        _ => "score-badge poor"
    };

    public static string FileSize(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / 1024d / 1024d:0.0} MB" : $"{Math.Max(1, bytes / 1024)} KB";
}
