using System.Security.Claims;
using Darbak.Data;
using Darbak.Models;
using Darbak.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Darbak.Controllers
{
    public class TestimonialsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TestimonialsController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        // PUBLIC TESTIMONIALS
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var testimonials =
                await _context.Testimonials
                    .AsNoTracking()
                    .Include(t => t.User)
                    .Where(t =>
                        t.Status ==
                        ApprovalStatus.Approved)
                    .OrderByDescending(t =>
                        t.CreatedAt)
                    .ToListAsync();

            return View(testimonials);
        }

        // CREATE GET
        [Authorize]
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // CREATE POST
        // CREATE POST
        // CREATE POST
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Content")]
    Testimonial testimonial)
        {
            var userId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Challenge();
            }

            // These properties are assigned by the server,
            // not by the user.
            ModelState.Remove(
                nameof(Testimonial.UserId));

            ModelState.Remove(
                nameof(Testimonial.User));

            ModelState.Remove(
                nameof(Testimonial.Status));

            ModelState.Remove(
                nameof(Testimonial.CreatedAt));

            if (!string.IsNullOrWhiteSpace(
                testimonial.Content))
            {
                testimonial.Content =
                    testimonial.Content.Trim();
            }

            if (!ModelState.IsValid)
            {
                return View(testimonial);
            }

            testimonial.UserId =
                userId;

            testimonial.Status =
                ApprovalStatus.Pending;

            testimonial.CreatedAt =
                DateTime.UtcNow;

            _context.Testimonials.Add(
                testimonial);

            await _context.SaveChangesAsync();

            TempData["TestimonialSuccess"] =
                "Your testimonial was submitted and is waiting for approval.";

            return RedirectToAction(
                nameof(Index));
        }

        // ADMIN INDEX
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> AdminIndex()
        {
            var testimonials =
                await _context.Testimonials
                    .AsNoTracking()
                    .Include(t => t.User)
                    .OrderByDescending(t =>
                        t.CreatedAt)
                    .ToListAsync();

            return View(testimonials);
        }

        // APPROVE
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(
            int id)
        {
            var testimonial =
                await _context.Testimonials
                    .FindAsync(id);

            if (testimonial == null)
            {
                return NotFound();
            }

            testimonial.Status =
                ApprovalStatus.Approved;

            await _context.SaveChangesAsync();

            TempData["TestimonialSuccess"] =
                "Testimonial approved successfully.";

            return RedirectToAction(
                nameof(AdminIndex));
        }

        // REJECT
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(
            int id)
        {
            var testimonial =
                await _context.Testimonials
                    .FindAsync(id);

            if (testimonial == null)
            {
                return NotFound();
            }

            testimonial.Status =
                ApprovalStatus.Rejected;

            await _context.SaveChangesAsync();

            TempData["TestimonialSuccess"] =
                "Testimonial rejected successfully.";

            return RedirectToAction(
                nameof(AdminIndex));
        }
    }
}