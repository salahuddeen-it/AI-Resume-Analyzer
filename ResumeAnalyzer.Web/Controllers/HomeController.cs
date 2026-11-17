using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ResumeAnalyzer.Web.Services;

namespace ResumeAnalyzer.Web.Controllers;

public class HomeController : Controller
{
    private readonly IResumeAnalysisService _service;
    private readonly ILogger<HomeController> _logger;

    public HomeController(IResumeAnalysisService service, ILogger<HomeController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(new UploadViewModel { RecentAnalyses = await _service.GetRecentAsync(4) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Analyze(UploadViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var id = await _service.AnalyzeAsync(model.ResumeFile!, model.JobDescription, cancellationToken);
                TempData["Success"] = "Your resume has been analysed successfully.";
                return RedirectToAction("Details", "Analysis", new { id });
            }
            catch (ResumeAnalysisException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while analysing resume");
                ModelState.AddModelError(string.Empty, "Something went wrong while analysing your resume. Please try again.");
            }
        }

        model.RecentAnalyses = await _service.GetRecentAsync(4);
        return View("Index", model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        ViewData["RequestId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View();
    }
}
