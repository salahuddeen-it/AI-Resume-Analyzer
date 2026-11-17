using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ResumeAnalyzer.Web.Data;
using ResumeAnalyzer.Web.Services;
using ResumeAnalyzer.Web.Services.AI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.Configure<AiSettings>(builder.Configuration.GetSection("AiSettings"));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient(HttpAiServiceBase.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(120));

builder.Services.AddScoped<IResumeTextExtractor, ResumeTextExtractor>();
builder.Services.AddScoped<IResumeAnalysisService, ResumeAnalysisService>();

// Choose AI provider from appsettings.json (falls back to Demo mode when no API key is set)
builder.Services.AddScoped<IResumeAiService>(sp =>
{
    var ai = sp.GetRequiredService<IOptions<AiSettings>>().Value;
    if (!ai.IsConfigured) return new DemoResumeAiService();
    return ai.IsOpenAi
        ? (IResumeAiService)ActivatorUtilities.CreateInstance<OpenAiResumeAiService>(sp)
        : ActivatorUtilities.CreateInstance<GeminiResumeAiService>(sp);
});

var app = builder.Build();

// Create database automatically on first run
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
