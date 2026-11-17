using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;
using WordParagraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;

namespace ResumeAnalyzer.Web.Services;

public interface IResumeTextExtractor
{
    Task<string> ExtractAsync(IFormFile file, CancellationToken cancellationToken = default);
}

/// <summary>Extracts plain text from PDF (PdfPig) and DOCX (OpenXML) files.</summary>
public class ResumeTextExtractor : IResumeTextExtractor
{
    private readonly ILogger<ResumeTextExtractor> _logger;

    public ResumeTextExtractor(ILogger<ResumeTextExtractor> logger) => _logger = logger;

    public async Task<string> ExtractAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, cancellationToken);
        var bytes = ms.ToArray();
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        // Verify real file type (magic bytes), not just the extension
        var isPdf = bytes.Length > 4 && bytes[0] == '%' && bytes[1] == 'P' && bytes[2] == 'D' && bytes[3] == 'F';
        var isZip = bytes.Length > 2 && bytes[0] == 'P' && bytes[1] == 'K';

        try
        {
            var text = ext switch
            {
                ".pdf" when isPdf => ExtractPdf(bytes),
                ".docx" when isZip => ExtractDocx(bytes),
                ".pdf" or ".docx" => throw new ResumeAnalysisException("The file content does not match its extension. Please upload a valid PDF or DOCX."),
                _ => throw new ResumeAnalysisException("Only PDF and DOCX files are supported.")
            };
            return Clean(text);
        }
        catch (ResumeAnalysisException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract text from {File}", file.FileName);
            throw new ResumeAnalysisException("This file could not be read. It may be corrupted or password-protected.");
        }
    }

    private static string ExtractPdf(byte[] bytes)
    {
        var sb = new StringBuilder();
        using var document = PdfDocument.Open(bytes);

        foreach (var page in document.GetPages())
        {
            // Rebuild lines from word positions (top-to-bottom, left-to-right)
            var words = page.GetWords()
                .OrderByDescending(w => Math.Round(w.BoundingBox.Bottom))
                .ThenBy(w => w.BoundingBox.Left);

            double? lastY = null;
            foreach (var word in words)
            {
                var y = Math.Round(word.BoundingBox.Bottom);
                if (lastY.HasValue) sb.Append(Math.Abs(y - lastY.Value) > 2 ? '\n' : ' ');
                sb.Append(word.Text);
                lastY = y;
            }
            sb.Append("\n\n");
        }
        return sb.ToString();
    }

    private static string ExtractDocx(byte[] bytes)
    {
        var sb = new StringBuilder();
        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);

        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null) return string.Empty;

        foreach (var paragraph in body.Descendants<WordParagraph>())
            sb.AppendLine(paragraph.InnerText);

        return sb.ToString();
    }

    private static string Clean(string text)
    {
        text = text.Replace("\r", "");
        text = Regex.Replace(text, @"[ \t]{2,}", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }
}
