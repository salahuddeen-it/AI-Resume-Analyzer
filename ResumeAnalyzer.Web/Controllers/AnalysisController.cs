using Microsoft.AspNetCore.Mvc;
using ResumeAnalyzer.Web.Services;

namespace ResumeAnalyzer.Web.Controllers;

public class AnalysisController : Controller
{
    private readonly IResumeAnalysisService _service;

    public AnalysisController(IResumeAnalysisService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var model = await _service.GetDetailsAsync(id);
        return model is null ? NotFound() : View(model);
    }

    [HttpGet]
    public async Task<IActionResult> History(string? search, int page = 1)
    {
        return View(await _service.GetHistoryAsync(search, page));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (await _service.DeleteAsync(id))
            TempData["Success"] = "Analysis deleted successfully.";
        else
            TempData["Error"] = "Analysis not found.";

        return RedirectToAction(nameof(History));
    }
}
