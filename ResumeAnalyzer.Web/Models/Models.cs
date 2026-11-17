namespace ResumeAnalyzer.Web.Models;

/// <summary>Database entity – one row per analysis (history).</summary>
public class ResumeAnalysis
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ExtractedText { get; set; } = string.Empty;
    public string? JobDescription { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public int OverallScore { get; set; }
    public int? MatchScore { get; set; }
    /// <summary>Full AI result stored as JSON.</summary>
    public string ResultJson { get; set; } = string.Empty;
    public string AiProvider { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Structured result returned by the AI.</summary>
public class AnalysisResult
{
    public string CandidateName { get; set; } = string.Empty;
    public string TargetRole { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public double OverallScore { get; set; }
    public ScoreBreakdown ScoreBreakdown { get; set; } = new();
    public List<string> Skills { get; set; } = new();
    public List<ExperienceItem> Experience { get; set; } = new();
    public double TotalExperienceYears { get; set; }
    public List<EducationItem> Education { get; set; } = new();
    public List<string> MissingSkills { get; set; } = new();
    public ResumeQuality ResumeQuality { get; set; } = new();
    public List<string> Improvements { get; set; } = new();
    public JobMatchResult? JobMatch { get; set; }

    /// <summary>Cleans and clamps values coming from the AI.</summary>
    public void Normalize(bool hasJobDescription)
    {
        OverallScore = Clamp(OverallScore);
        ScoreBreakdown ??= new();
        ScoreBreakdown.Skills = Clamp(ScoreBreakdown.Skills);
        ScoreBreakdown.Experience = Clamp(ScoreBreakdown.Experience);
        ScoreBreakdown.Education = Clamp(ScoreBreakdown.Education);
        ScoreBreakdown.Formatting = Clamp(ScoreBreakdown.Formatting);
        ScoreBreakdown.Impact = Clamp(ScoreBreakdown.Impact);

        Skills = Clean(Skills);
        MissingSkills = Clean(MissingSkills);
        Improvements = Clean(Improvements);
        Experience ??= new();
        Education ??= new();
        ResumeQuality ??= new();
        ResumeQuality.Strengths = Clean(ResumeQuality.Strengths);
        ResumeQuality.Weaknesses = Clean(ResumeQuality.Weaknesses);
        if (TotalExperienceYears < 0) TotalExperienceYears = 0;
        if (string.IsNullOrWhiteSpace(CandidateName)) CandidateName = "Unknown Candidate";

        if (!hasJobDescription) JobMatch = null;
        else if (JobMatch is not null)
        {
            JobMatch.MatchScore = Clamp(JobMatch.MatchScore);
            JobMatch.MatchingSkills = Clean(JobMatch.MatchingSkills);
            JobMatch.MissingSkills = Clean(JobMatch.MissingSkills);
        }
    }

    private static double Clamp(double v) => Math.Round(Math.Clamp(v, 0, 100));

    private static List<string> Clean(List<string>? list) =>
        (list ?? new()).Where(s => !string.IsNullOrWhiteSpace(s))
                       .Select(s => s.Trim())
                       .Distinct(StringComparer.OrdinalIgnoreCase)
                       .ToList();
}

public class ScoreBreakdown
{
    public double Skills { get; set; }
    public double Experience { get; set; }
    public double Education { get; set; }
    public double Formatting { get; set; }
    public double Impact { get; set; }
}

public class ExperienceItem
{
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public List<string> Highlights { get; set; } = new();
}

public class EducationItem
{
    public string Degree { get; set; } = string.Empty;
    public string Institution { get; set; } = string.Empty;
    public string Year { get; set; } = string.Empty;
}

public class ResumeQuality
{
    public string Rating { get; set; } = string.Empty;
    public List<string> Strengths { get; set; } = new();
    public List<string> Weaknesses { get; set; } = new();
}

public class JobMatchResult
{
    public double MatchScore { get; set; }
    public List<string> MatchingSkills { get; set; } = new();
    public List<string> MissingSkills { get; set; } = new();
    public string Verdict { get; set; } = string.Empty;
}

/// <summary>Errors that are safe to show to the user.</summary>
public class ResumeAnalysisException : Exception
{
    public ResumeAnalysisException(string message) : base(message) { }
}

public class AiSettings
{
    public string Provider { get; set; } = "Gemini";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string OpenAiBaseUrl { get; set; } = "https://api.openai.com/v1";
    public int MaxFileSizeMB { get; set; } = 5;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
    public bool IsOpenAi => Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase);

    public string EffectiveModel => !string.IsNullOrWhiteSpace(Model) ? Model : IsOpenAi ? "gpt-4o-mini" : "gemini-2.5-flash";

    public string DisplayName => IsConfigured ? $"{(IsOpenAi ? "OpenAI" : "Gemini")} · {EffectiveModel}" : "Demo mode";
}
