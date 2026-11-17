using System.Text.RegularExpressions;

namespace ResumeAnalyzer.Web.Services.AI;

/// <summary>
/// Offline, rule-based analyzer used automatically when no AI API key is configured,
/// so the application can always be run and demonstrated. Add an API key for real AI analysis.
/// </summary>
public class DemoResumeAiService : IResumeAiService
{
    public string ProviderName => "Demo mode";

    private static readonly string[] KnownSkills =
    {
        "C#", ".NET", "ASP.NET", "ASP.NET Core", "Entity Framework", "LINQ", "SQL", "SQL Server", "MySQL", "PostgreSQL",
        "MongoDB", "Java", "Python", "JavaScript", "TypeScript", "React", "Angular", "Vue", "Node.js", "HTML", "CSS",
        "Bootstrap", "Tailwind", "jQuery", "PHP", "Laravel", "WordPress", "Kotlin", "Swift", "Flutter", "Dart", "Android",
        "Git", "GitHub", "Docker", "Kubernetes", "Azure", "AWS", "Linux", "REST API", "Web API", "Microservices", "GraphQL",
        "Unit Testing", "xUnit", "Selenium", "CI/CD", "Agile", "Scrum", "Jira", "Power BI", "Excel", "Tableau",
        "Machine Learning", "Deep Learning", "TensorFlow", "PyTorch", "Pandas", "Data Analysis", "Figma", "Photoshop",
        "MVC", "OOP", "Design Patterns", "Communication", "Teamwork", "Leadership", "Problem Solving",
        "Time Management", "Project Management", "Teaching", "Research"
    };

    private static readonly string[] InDemandSkills =
        { "Git", "SQL", "REST API", "Unit Testing", "Docker", "Azure", "CI/CD", "Agile", "Communication", "Problem Solving" };

    private static readonly string[] DegreeKeywords =
        { "phd", "doctor", "master", "msc", "m.sc", "mba", "bachelor", "bsc", "b.sc", "bs ", "bba", "be ", "b.e", "diploma",
          "intermediate", "a level", "a-level", "o level", "o-level", "matric", "hsc", "ssc" };

    public Task<AnalysisResult> AnalyzeAsync(string resumeText, string? jobDescription, CancellationToken cancellationToken = default)
    {
        var lines = resumeText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var lower = resumeText.ToLowerInvariant();

        var skills = FindSkills(resumeText);
        var education = lines
            .Where(l => l.Length < 160 && DegreeKeywords.Any(k => (" " + l.ToLowerInvariant() + " ").Contains(" " + k)))
            .Take(4)
            .Select(l => new EducationItem { Degree = l, Year = Regex.Match(l, @"(19|20)\d{2}").Value })
            .ToList();
        var years = EstimateYears(resumeText);

        string[] sections = { "experience", "education", "skills", "summary|objective|profile", "projects", "certification" };
        var sectionsFound = sections.Count(s => Regex.IsMatch(lower, $@"\b({s})"));
        var metrics = Regex.Matches(resumeText, @"\d+\s?%|\$\s?\d+|\b\d{2,}\+?\s(users|clients|students|projects|customers)").Count;
        var hasEmail = Regex.IsMatch(resumeText, @"[\w.+-]+@[\w-]+\.[\w.]+");
        var hasPhone = Regex.IsMatch(resumeText, @"\+?\d[\d\s-]{8,}\d");
        var hasLinkedIn = lower.Contains("linkedin");

        var breakdown = new ScoreBreakdown
        {
            Skills = Math.Min(100, 35 + skills.Count * 5),
            Experience = Math.Min(100, 30 + years * 12),
            Education = education.Count > 0 ? 85 : 45,
            Formatting = Math.Min(100, 30 + sectionsFound * 10 + (hasEmail ? 5 : 0) + (hasPhone ? 5 : 0)),
            Impact = Math.Min(100, 40 + metrics * 10)
        };
        var overall = breakdown.Skills * .3 + breakdown.Experience * .25 + breakdown.Education * .15 +
                      breakdown.Formatting * .15 + breakdown.Impact * .15;

        var strengths = new List<string>();
        var weaknesses = new List<string>();
        var improvements = new List<string>();

        if (skills.Count >= 8) strengths.Add($"Good range of skills listed ({skills.Count} detected)."); else { weaknesses.Add("Limited number of skills listed."); improvements.Add("Add a dedicated Skills section listing tools, languages and frameworks you know."); }
        if (education.Count > 0) strengths.Add("Education details are present."); else { weaknesses.Add("Education section not clearly found."); improvements.Add("Add a clear Education section with degree, institution and year."); }
        if (sectionsFound >= 4) strengths.Add("Resume has a clear section structure."); else improvements.Add("Organise the resume with clear headings: Summary, Experience, Education, Skills, Projects.");
        if (metrics >= 2) strengths.Add("Achievements include measurable results."); else { weaknesses.Add("Few measurable achievements (numbers, %)."); improvements.Add("Quantify achievements, e.g. \"Improved page load time by 40%\"."); }
        if (!hasLinkedIn) improvements.Add("Add your LinkedIn and GitHub/portfolio links.");
        if (!Regex.IsMatch(lower, @"\b(summary|objective|profile)")) improvements.Add("Add a 2-3 line professional summary at the top.");
        if (!lower.Contains("project")) improvements.Add("Add a Projects section with 2-3 relevant projects and the technologies used.");
        improvements.Add("Start each bullet with a strong action verb (Developed, Led, Designed, Improved).");

        var result = new AnalysisResult
        {
            CandidateName = GuessName(lines),
            TargetRole = skills.Any(s => s is "C#" or ".NET" or "ASP.NET Core" or "Java" or "Python" or "JavaScript") ? "Software Developer" : "Professional",
            Summary = "This analysis was generated in offline DEMO MODE using keyword rules (no AI API key configured). " +
                      $"The resume shows {skills.Count} recognised skills and about {years} year(s) of experience. " +
                      "Add an AI API key in appsettings.json for a detailed, AI-powered review.",
            OverallScore = overall,
            ScoreBreakdown = breakdown,
            Skills = skills,
            TotalExperienceYears = years,
            Education = education,
            MissingSkills = InDemandSkills.Where(s => !skills.Contains(s, StringComparer.OrdinalIgnoreCase)).Take(6).ToList(),
            ResumeQuality = new ResumeQuality
            {
                Rating = overall >= 80 ? "Excellent" : overall >= 65 ? "Good" : overall >= 45 ? "Average" : "Poor",
                Strengths = strengths,
                Weaknesses = weaknesses
            },
            Improvements = improvements
        };

        if (jobDescription is not null)
        {
            var jdSkills = FindSkills(jobDescription);
            var matching = jdSkills.Where(s => skills.Contains(s, StringComparer.OrdinalIgnoreCase)).ToList();
            var missing = jdSkills.Except(matching, StringComparer.OrdinalIgnoreCase).ToList();
            var score = jdSkills.Count == 0 ? 50 : Math.Round(matching.Count * 100.0 / jdSkills.Count);
            result.JobMatch = new JobMatchResult
            {
                MatchScore = score,
                MatchingSkills = matching,
                MissingSkills = missing,
                Verdict = score >= 70 ? "Strong match for the required skills." :
                          score >= 40 ? "Partial match – some required skills are missing." :
                                        "Weak match – most required skills are not shown on the resume."
            };
            if (missing.Count > 0) result.MissingSkills = missing.Concat(result.MissingSkills).Distinct().Take(10).ToList();
        }

        return Task.FromResult(result);
    }

    private static List<string> FindSkills(string text) =>
        KnownSkills.Where(s => Regex.IsMatch(text, $@"(?<![A-Za-z0-9]){Regex.Escape(s)}(?![A-Za-z0-9])", RegexOptions.IgnoreCase))
                   .ToList();

    private static double EstimateYears(string text)
    {
        var explicitYears = Regex.Matches(text, @"(\d{1,2})\+?\s*(years|yrs)", RegexOptions.IgnoreCase)
            .Select(m => int.Parse(m.Groups[1].Value)).Where(v => v <= 40).DefaultIfEmpty(0).Max();

        var total = 0;
        foreach (Match m in Regex.Matches(text, @"((?:19|20)\d{2})\s*(?:-|–|to)\s*((?:19|20)\d{2}|present|current|now)", RegexOptions.IgnoreCase))
        {
            var from = int.Parse(m.Groups[1].Value);
            var to = int.TryParse(m.Groups[2].Value, out var t) ? t : DateTime.UtcNow.Year;
            if (to >= from) total += to - from;
        }
        return Math.Max(explicitYears, Math.Min(total, 40));
    }

    private static string GuessName(string[] lines) =>
        lines.Take(5).FirstOrDefault(l => Regex.IsMatch(l, @"^[A-Za-z][A-Za-z.\s]{2,40}$") && l.Split(' ').Length is >= 2 and <= 5)
        ?? "Unknown Candidate";
}
