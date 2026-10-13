using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.Interfaces;
using Ayurveda.Domain.Entities;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Admin.Controllers;

[Area("Admin")]
[Authorize(AuthenticationSchemes = "AdminCookieScheme", Policy = "RequireCustomerSupport")]
public class ModerationController : Controller
{
    private readonly AyurvedaDbContext _context;
    private readonly IAuditService _auditService;

    public ModerationController(AyurvedaDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<IActionResult> Reviews()
    {
        var productReviews = await _context.ProductReviews
            .Include(r => r.Product)
            .OrderByDescending(r => r.CreatedAt)
            .Take(20)
            .ToListAsync();

        var doctorReviews = await _context.DoctorReviews
            .Include(r => r.Doctor)
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .Take(20)
            .ToListAsync();

        ViewBag.DoctorReviews = doctorReviews;
        return View(productReviews);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProductReview(int id)
    {
        var r = await _context.ProductReviews.FindAsync(id);
        if (r != null)
        {
            _context.ProductReviews.Remove(r);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("ProductReviewModerated", "ProductReview", id.ToString(), $"Removed abusive or flagged product review #{id}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            TempData["SuccessMessage"] = "Review removed from catalogue.";
        }
        return RedirectToAction(nameof(Reviews));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDoctorReview(int id)
    {
        var r = await _context.DoctorReviews.FindAsync(id);
        if (r != null)
        {
            _context.DoctorReviews.Remove(r);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("DoctorReviewModerated", "DoctorReview", id.ToString(), $"Removed doctor review #{id}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            TempData["SuccessMessage"] = "Review removed from doctor profile.";
        }
        return RedirectToAction(nameof(Reviews));
    }

    public async Task<IActionResult> Questions()
    {
        var questions = await _context.ProductQuestions
            .Include(q => q.Product)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();

        return View(questions);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnswerQuestion(int questionId, string answerText)
    {
        var q = await _context.ProductQuestions.FindAsync(questionId);
        if (q != null)
        {
            q.AnswerText = answerText.Trim();
            q.AnsweredBy = User.Identity?.Name ?? "Senior Medical Vaidya";
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("QuestionAnswered", "ProductQuestion", questionId.ToString(), $"Posted official answer to product question #{questionId}", User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString());
            TempData["SuccessMessage"] = "Customer question answered and published.";
        }
        return RedirectToAction(nameof(Questions));
    }
}
