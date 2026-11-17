using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ResumeAnalyzer.Web.Data;
using ResumeAnalyzer.Web.Services.AI;

namespace ResumeAnalyzer.Web.Services;

public interface IResumeAnalysisService
{
    Task<int> AnalyzeAsync(IFormFile file, string? jobDescription, CancellationToken cancellationToken = default);
    Task<AnalysisDetailsViewModel?> GetDetailsAsync(int id);
    Task<HistoryViewModel> GetHistoryAsync(string? search, int page, int pageSize = 10);
    Task<List<HistoryItem>> GetRecentAsync(int count);
    Task<bool> DeleteAsync(int id);
}

/// <summary>Upload → extract text → AI analysis → save to database.</summary>
public class ResumeAnalysisService : IResumeAnalysisService
{
    private const int MaxCharsForAi = 15000;

    private readonly AppDbContext _db;
    private readonly IResumeTextExtractor _extractor;
    private readonly IResumeAiService _ai;
    private readonly ILogger<ResumeAnalysisService> _logger;

    public ResumeAnalysisService(AppDbContext db, IResumeTextExtractor extractor, IResumeAiService ai, ILogger<ResumeAnalysisService> logger)
    {
        _db = db;
        _extractor = extractor;
        _ai = ai;
        _logger = logger;
    }

    public async Task<int> AnalyzeAsync(IFormFile file, string? jobDescription, CancellationToken cancellationToken = default)
    {
        var text = await _extractor.ExtractAsync(file, cancellationToken);
        if (text.Length < 150)
            throw new ResumeAnalysisException("Not enough text could be read from this file. If it is a scanned (image) PDF, please upload a text-based PDF or DOCX.");

        var jd = string.IsNullOrWhiteSpace(jobDescription) ? null : jobDescription.Trim();
        var aiInput = text.Length > MaxCharsForAi ? text[..MaxCharsForAi] : text;

        var result = await _ai.AnalyzeAsync(aiInput, jd, cancellationToken);
        result.Normalize(jd is not null);

        var entity = new ResumeAnalysis
        {
            FileName = Path.GetFileName(file.FileName),
            FileType = Path.GetExtension(file.FileName).TrimStart('.').ToUpperInvariant(),
            FileSizeBytes = file.Length,
            ExtractedText = text,
            JobDescription = jd,
            CandidateName = result.CandidateName.Length > 150 ? result.CandidateName[..150] : result.CandidateName,
            OverallScore = (int)result.OverallScore,
            MatchScore = result.JobMatch is null ? null : (int)result.JobMatch.MatchScore,
            ResultJson = JsonSerializer.Serialize(result, ResumePrompt.JsonOptions),
            AiProvider = _ai.ProviderName,
            CreatedAt = DateTime.UtcNow
        };

        _db.ResumeAnalyses.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Resume {File} analysed – score {Score}", entity.FileName, entity.OverallScore);
        return entity.Id;
    }

    public async Task<AnalysisDetailsViewModel?> GetDetailsAsync(int id)
    {
        var entity = await _db.ResumeAnalyses.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (entity is null) return null;

        var result = JsonSerializer.Deserialize<AnalysisResult>(entity.ResultJson, ResumePrompt.JsonOptions) ?? new AnalysisResult();
        return new AnalysisDetailsViewModel { Analysis = entity, Result = result };
    }

    public async Task<HistoryViewModel> GetHistoryAsync(string? search, int page, int pageSize = 10)
    {
        var query = _db.ResumeAnalyses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a => a.CandidateName.Contains(term) || a.FileName.Contains(term));
        }

        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var items = await query.OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(ToHistoryItem())
            .ToListAsync();

        var average = total == 0 ? 0 : await query.AverageAsync(a => (double)a.OverallScore);

        return new HistoryViewModel
        {
            Items = items, Search = search, Page = page, TotalPages = totalPages, TotalCount = total,
            AverageScore = Math.Round(average)
        };
    }

    public Task<List<HistoryItem>> GetRecentAsync(int count) =>
        _db.ResumeAnalyses.AsNoTracking().OrderByDescending(a => a.CreatedAt).Take(count).Select(ToHistoryItem()).ToListAsync();

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _db.ResumeAnalyses.FindAsync(id);
        if (entity is null) return false;
        _db.ResumeAnalyses.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    // Light projection – never loads the large text/JSON columns for lists
    private static System.Linq.Expressions.Expression<Func<ResumeAnalysis, HistoryItem>> ToHistoryItem() => a => new HistoryItem
    {
        Id = a.Id,
        CandidateName = a.CandidateName,
        FileName = a.FileName,
        FileType = a.FileType,
        OverallScore = a.OverallScore,
        MatchScore = a.MatchScore,
        AiProvider = a.AiProvider,
        CreatedAt = a.CreatedAt
    };
}
