using System.ComponentModel.DataAnnotations;

namespace ResumeAnalyzer.Web.ViewModels;

public class UploadViewModel : IValidatableObject
{
    public static readonly string[] AllowedExtensions = { ".pdf", ".docx" };
    public const int MaxFileSizeMB = 5;

    [Display(Name = "Resume (PDF or DOCX)")]
    [Required(ErrorMessage = "Please choose a resume file to upload.")]
    public IFormFile? ResumeFile { get; set; }

    [Display(Name = "Job Description (optional)")]
    [StringLength(8000, ErrorMessage = "Job description cannot exceed 8000 characters.")]
    public string? JobDescription { get; set; }

    public List<HistoryItem> RecentAnalyses { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ResumeFile is null) yield break;

        var ext = Path.GetExtension(ResumeFile.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            yield return new ValidationResult("Only PDF and DOCX files are allowed.", new[] { nameof(ResumeFile) });

        if (ResumeFile.Length == 0)
            yield return new ValidationResult("The selected file is empty.", new[] { nameof(ResumeFile) });
        else if (ResumeFile.Length > MaxFileSizeMB * 1024 * 1024)
            yield return new ValidationResult($"File size must not exceed {MaxFileSizeMB} MB.", new[] { nameof(ResumeFile) });

        if (!string.IsNullOrWhiteSpace(JobDescription) && JobDescription.Trim().Length < 30)
            yield return new ValidationResult("Job description is too short to compare (minimum 30 characters).", new[] { nameof(JobDescription) });
    }
}

public class HistoryItem
{
    public int Id { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public int OverallScore { get; set; }
    public int? MatchScore { get; set; }
    public string AiProvider { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class HistoryViewModel
{
    public List<HistoryItem> Items { get; set; } = new();
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public double AverageScore { get; set; }
}

public class AnalysisDetailsViewModel
{
    public ResumeAnalysis Analysis { get; set; } = null!;
    public AnalysisResult Result { get; set; } = null!;
}
