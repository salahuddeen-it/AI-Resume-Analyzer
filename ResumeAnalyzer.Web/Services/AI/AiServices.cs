using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ResumeAnalyzer.Web.Services.AI;

public interface IResumeAiService
{
    string ProviderName { get; }
    Task<AnalysisResult> AnalyzeAsync(string resumeText, string? jobDescription, CancellationToken cancellationToken = default);
}

/// <summary>Builds the prompt and parses the JSON returned by any AI provider.</summary>
public static class ResumePrompt
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    public static string Build(string resumeText, string? jobDescription)
    {
        var hasJd = jobDescription is not null;
        var task = hasJd ? " and compare it with the JOB DESCRIPTION" : "";
        var jobMatchSchema = hasJd
            ? "{ \"matchScore\": 0-100, \"matchingSkills\": [\"string\"], \"missingSkills\": [\"string\"], \"verdict\": \"2-3 sentence fit assessment\" }"
            : "null";
        var missingFor = hasJd ? " / job description" : "";
        var jdBlock = hasJd ? $"\nJOB DESCRIPTION:\n-----\n{jobDescription}\n-----" : "";

        return $$"""
            You are an expert technical recruiter and ATS (Applicant Tracking System) specialist.
            Analyze the RESUME below{{task}}.

            Respond with ONLY one valid JSON object (no markdown, no extra text) using exactly this structure:
            {
              "candidateName": "string",
              "targetRole": "most suitable job title for this candidate",
              "summary": "3-4 sentence professional assessment of the resume",
              "overallScore": 0-100,
              "scoreBreakdown": { "skills": 0-100, "experience": 0-100, "education": 0-100, "formatting": 0-100, "impact": 0-100 },
              "skills": ["skill found in resume"],
              "experience": [ { "title": "string", "company": "string", "duration": "string", "highlights": ["string"] } ],
              "totalExperienceYears": 0,
              "education": [ { "degree": "string", "institution": "string", "year": "string" } ],
              "missingSkills": ["important skill for the target role that is missing"],
              "resumeQuality": { "rating": "Excellent | Good | Average | Poor", "strengths": ["string"], "weaknesses": ["string"] },
              "improvements": ["specific, actionable suggestion"],
              "jobMatch": {{jobMatchSchema}}
            }

            Rules:
            - All scores are integers from 0 to 100. Be realistic and strict: an average resume scores 55-70.
            - "skills": technical and soft skills explicitly present in the resume (max 25).
            - "missingSkills": up to 10 in-demand skills for the target role{{missingFor}} that are absent.
            - "improvements": 5-8 concrete suggestions (e.g. add metrics, rewrite summary, add projects).
            - "highlights": max 3 short bullets per job.
            - Never invent experience, education or skills that are not in the resume.
            - Use "" or [] when information is not available.

            RESUME:
            -----
            {{resumeText}}
            -----
            {{jdBlock}}
            """;
    }

    public static AnalysisResult Parse(string? aiText)
    {
        if (string.IsNullOrWhiteSpace(aiText))
            throw new ResumeAnalysisException("The AI returned an empty response. Please try again.");

        // Remove ```json fences or any text around the JSON object
        var start = aiText.IndexOf('{');
        var end = aiText.LastIndexOf('}');
        if (start < 0 || end <= start)
            throw new ResumeAnalysisException("The AI response could not be understood. Please try again.");

        try
        {
            return JsonSerializer.Deserialize<AnalysisResult>(aiText[start..(end + 1)], JsonOptions)
                   ?? throw new ResumeAnalysisException("The AI response was empty.");
        }
        catch (JsonException)
        {
            throw new ResumeAnalysisException("The AI response was not valid JSON. Please try again.");
        }
    }
}

public abstract class HttpAiServiceBase : IResumeAiService
{
    public const string HttpClientName = "ai";

    protected readonly IHttpClientFactory HttpFactory;
    protected readonly AiSettings Settings;
    protected readonly ILogger Logger;

    protected HttpAiServiceBase(IHttpClientFactory httpFactory, IOptions<AiSettings> settings, ILogger logger)
    {
        HttpFactory = httpFactory;
        Settings = settings.Value;
        Logger = logger;
    }

    public string ProviderName => Settings.DisplayName;

    public async Task<AnalysisResult> AnalyzeAsync(string resumeText, string? jobDescription, CancellationToken cancellationToken = default)
    {
        var prompt = ResumePrompt.Build(resumeText, jobDescription);
        using var request = CreateRequest(prompt);
        var client = HttpFactory.CreateClient(HttpClientName);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            throw new ResumeAnalysisException("The AI service took too long to respond. Please try again.");
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "AI request failed");
            throw new ResumeAnalysisException("Could not reach the AI service. Please check your internet connection.");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Logger.LogError("AI error {Status}: {Body}", (int)response.StatusCode, body);
                throw new ResumeAnalysisException((int)response.StatusCode switch
                {
                    400 or 401 or 403 => "AI request was rejected. Please check the API key and model in appsettings.json.",
                    404 => "AI model not found. Please check the model name in appsettings.json.",
                    429 => "AI rate limit reached. Please wait a minute and try again.",
                    _ => $"AI service error ({(int)response.StatusCode}). Please try again later."
                });
            }

            using var doc = JsonDocument.Parse(body);
            return ResumePrompt.Parse(ReadText(doc.RootElement));
        }
    }

    protected abstract HttpRequestMessage CreateRequest(string prompt);
    protected abstract string? ReadText(JsonElement root);
}

/// <summary>Google Gemini (free API key from https://aistudio.google.com).</summary>
public class GeminiResumeAiService : HttpAiServiceBase
{
    public GeminiResumeAiService(IHttpClientFactory f, IOptions<AiSettings> s, ILogger<GeminiResumeAiService> l) : base(f, s, l) { }

    protected override HttpRequestMessage CreateRequest(string prompt)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Settings.EffectiveModel}:generateContent";
        var payload = new
        {
            contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
            generationConfig = new { temperature = 0.2, responseMimeType = "application/json" }
        };
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
        request.Headers.Add("x-goog-api-key", Settings.ApiKey);
        return request;
    }

    protected override string? ReadText(JsonElement root) =>
        root.TryGetProperty("candidates", out var c) && c.GetArrayLength() > 0
            ? c[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()
            : null;
}

/// <summary>OpenAI or any OpenAI-compatible API (Groq, OpenRouter, etc. via OpenAiBaseUrl).</summary>
public class OpenAiResumeAiService : HttpAiServiceBase
{
    public OpenAiResumeAiService(IHttpClientFactory f, IOptions<AiSettings> s, ILogger<OpenAiResumeAiService> l) : base(f, s, l) { }

    protected override HttpRequestMessage CreateRequest(string prompt)
    {
        var url = $"{Settings.OpenAiBaseUrl.TrimEnd('/')}/chat/completions";
        var payload = new
        {
            model = Settings.EffectiveModel,
            temperature = 0.2,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = "You are an expert recruiter. Always reply with a single valid JSON object." },
                new { role = "user", content = prompt }
            }
        };
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Settings.ApiKey);
        return request;
    }

    protected override string? ReadText(JsonElement root) =>
        root.TryGetProperty("choices", out var c) && c.GetArrayLength() > 0
            ? c[0].GetProperty("message").GetProperty("content").GetString()
            : null;
}
