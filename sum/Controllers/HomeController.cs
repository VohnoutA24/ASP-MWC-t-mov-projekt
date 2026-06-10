using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sum.Data;
using sum.Models;
using System.Diagnostics;

namespace sum.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;

        public HomeController(AppDbContext db)
        {
            _db = db;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Dashboard()
        {
            if (User.Identity?.IsAuthenticated != true)
                return RedirectToAction("Login", "Account");

            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
                return RedirectToAction("Login", "Account");

            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                return RedirectToAction("Login", "Account");

            var unreadMessagesCount = await _db.Messages
                .CountAsync(m => m.RecipientId == userId && !m.IsRead && !m.RecipientDeleted);

            if (user.Role == "Teacher")
            {
                var homeworks = await _db.Homeworks
                    .Where(h => h.TeacherId == userId)
                    .OrderByDescending(h => h.Deadline)
                    .ToListAsync();

                ViewBag.UnreadMessages = unreadMessagesCount;
                ViewBag.TotalHomeworks = homeworks.Count;
                ViewBag.ActiveHomeworks = homeworks.Count(h => h.Deadline > DateTime.UtcNow);

                return View("TeacherDashboard", homeworks);
            }
            else
            {
                // Deterministic per-user randomization for grades
                var rng = new Random(userId * 31337);

                // Per-subject grades (1–5 scale, Czech school system)
                var subjects = new[]
                {
                    "Matematika", "Český jazyk", "Anglický jazyk", "Fyzika",
                    "Chemie", "Dějepis", "Zeměpis", "Přírodopis",
                    "Informatika", "Občanská výchova"
                };

                var subjectGrades = subjects.Select(s =>
                {
                    // Each subject gets 3–5 individual grades
                    var count = rng.Next(3, 6);
                    var grades = Enumerable.Range(0, count)
                        .Select(_ => rng.Next(1, 6))
                        .ToList();
                    var avg = Math.Round(grades.Average(), 1);
                    return new { Subject = s, Grades = grades, Average = avg };
                }).ToList();

                var gradeAvg = Math.Round(subjectGrades.Average(sg => sg.Average), 2);

                var completedHomeworkIds = await _db.HomeworkCompletions
                    .Where(hc => hc.StudentId == userId)
                    .Select(hc => hc.HomeworkId)
                    .ToListAsync();

                var pendingHomeworksCount = await _db.Homeworks
                    .CountAsync(h => h.Deadline > DateTime.UtcNow && !_db.HomeworkCompletions.Any(hc => hc.StudentId == userId && hc.HomeworkId == h.Id));

                var homeworks = await _db.Homeworks
                    .Include(h => h.Teacher)
                    .OrderBy(h => h.Deadline)
                    .ToListAsync();

                ViewBag.GradeAverage = gradeAvg;
                ViewBag.SubjectGrades = subjectGrades.Select(sg => new
                {
                    sg.Subject,
                    sg.Grades,
                    sg.Average
                }).ToList<dynamic>();
                ViewBag.PendingHomework = pendingHomeworksCount;
                ViewBag.UnreadMessages = unreadMessagesCount;
                ViewBag.CompletedHomeworkIds = completedHomeworkIds;

                return View(homeworks);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteHomework(int id)
        {
            if (User.Identity?.IsAuthenticated != true)
                return RedirectToAction("Login", "Account");

            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
                return RedirectToAction("Login", "Account");

            var user = await _db.Users.FindAsync(userId);
            if (user == null || user.Role != "Student")
            {
                return Forbid();
            }

            var homework = await _db.Homeworks.FindAsync(id);
            if (homework == null) return NotFound();

            var alreadyCompleted = await _db.HomeworkCompletions
                .AnyAsync(hc => hc.StudentId == userId && hc.HomeworkId == id);

            if (!alreadyCompleted)
            {
                var completion = new HomeworkCompletion
                {
                    StudentId = userId,
                    HomeworkId = id,
                    CompletedAt = DateTime.UtcNow
                };
                _db.HomeworkCompletions.Add(completion);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Domácí úkol byl splněn!";
            }

            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignHomework(string title, string subject, string? description, DateTime deadline)
        {
            if (User.Identity?.IsAuthenticated != true)
                return RedirectToAction("Login", "Account");

            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
                return RedirectToAction("Login", "Account");

            var user = await _db.Users.FindAsync(userId);
            if (user == null || user.Role != "Teacher")
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(subject))
            {
                TempData["ErrorMessage"] = "Název úkolu a předmět jsou povinné.";
                return RedirectToAction("Dashboard");
            }

            var homework = new Homework
            {
                Title = title.Trim(),
                Subject = subject.Trim(),
                Description = description?.Trim(),
                Deadline = deadline.ToUniversalTime(),
                TeacherId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _db.Homeworks.Add(homework);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Domácí úkol byl úspěšně zadán!";
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteHomework(int id)
        {
            if (User.Identity?.IsAuthenticated != true)
                return RedirectToAction("Login", "Account");

            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
                return RedirectToAction("Login", "Account");

            var homework = await _db.Homeworks.FindAsync(id);
            if (homework == null) return NotFound();

            if (homework.TeacherId != userId)
            {
                return Forbid();
            }

            _db.Homeworks.Remove(homework);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Domácí úkol byl úspěšně smazán.";
            return RedirectToAction("Dashboard");
        }

        public IActionResult Timetable(int grade = 8)
        {
            var model = sum.Services.TimetableGenerator.GenerateForGrade(grade);
            return View(model);
        }

        public IActionResult Cafeteria()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
