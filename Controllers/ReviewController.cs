using KerzelPay.Constants;
using KerzelPay.Data;
using KerzelPay.Models;
using KerzelPay.Repositories;
using KerzelPay.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KerzelPay.Controllers
{
    public class ReviewController : Controller
    {
        private readonly IRepository<Review> _reviewRepo;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;

        public ReviewController(
            IRepository<Review> reviewRepo,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db)
        {
            _reviewRepo = reviewRepo;
            _userManager = userManager;
            _db = db;
        }

        // GET: /Review — public testimonials wall
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var reviews = await _db.Reviews
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .Take(50)
                .ToListAsync();

            // Average rating for the header
            ViewBag.AverageRating = reviews.Any()
                ? Math.Round(reviews.Average(r => r.Rating), 1)
                : 0;
            ViewBag.TotalReviews = reviews.Count;

            return View(reviews);
        }

        // GET: /Review/Create
        [Authorize(Roles = Roles.User)]
        public IActionResult Create()
        {
            return View(new ReviewViewModel());
        }

        // POST: /Review/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.User)]
        public async Task<IActionResult> Create(ReviewViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var userId = _userManager.GetUserId(User)!;

            var review = new Review
            {
                Rating = vm.Rating,
                Comment = string.IsNullOrWhiteSpace(vm.Comment) ? null : vm.Comment.Trim(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            await _reviewRepo.AddAsync(review);

            TempData["Success"] = "Thank you for your feedback! ⭐";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Review/Mine — current user's reviews
        [Authorize(Roles = Roles.User)]
        public async Task<IActionResult> Mine()
        {
            var userId = _userManager.GetUserId(User);

            var reviews = await _db.Reviews
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(reviews);
        }

        // POST: /Review/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.User)]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);

            var review = await _db.Reviews
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

            if (review == null) return NotFound();

            await _reviewRepo.DeleteAsync(id);
            TempData["Success"] = "Review deleted.";
            return RedirectToAction(nameof(Mine));
        }
    }
}