using Microsoft.AspNetCore.Mvc;
using sum.Models;
using System.Diagnostics;

namespace sum.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Dashboard()
        {
            if (User.Identity?.IsAuthenticated != true)
                return RedirectToAction("Login", "Account");

            // Deterministic per-user randomization using UserId as seed
            var userIdClaim = User.FindFirst("UserId")?.Value;
            int userId = int.TryParse(userIdClaim, out var id) ? id : 1;
            var rng = new Random(userId * 31337);

            // Grade average: weighted towards 2.4, range 1.0-4.5
            // Use normal-ish distribution via multiple uniform rolls
            double sum = 0;
            for (int i = 0; i < 6; i++)
                sum += rng.NextDouble();
            double normalized = sum / 6.0; // ~0.5 center, roughly normal
            double gradeAvg = 1.0 + normalized * 3.5; // range 1.0-4.5
            // Shift center towards 2.4: map 0.5 -> 2.4
            gradeAvg = 1.0 + (normalized * 0.8 + 0.2) * 3.5;
            gradeAvg = Math.Round(Math.Clamp(gradeAvg, 1.0, 4.5), 2);

            // Homework: weighted towards 1-2
            // Weights: 0=5%, 1=35%, 2=35%, 3=15%, 4=10%
            int roll = rng.Next(100);
            int homework;
            if (roll < 5) homework = 0;
            else if (roll < 40) homework = 1;
            else if (roll < 75) homework = 2;
            else if (roll < 90) homework = 3;
            else homework = 4;

            ViewBag.GradeAverage = gradeAvg;
            ViewBag.PendingHomework = homework;

            return View();
        }

        public IActionResult Timetable()
        {
            if (User.Identity?.IsAuthenticated != true)
                return RedirectToAction("Login", "Account");

            return View();
        }

        public IActionResult Cafeteria()
        {
            if (User.Identity?.IsAuthenticated != true)
                return RedirectToAction("Login", "Account");

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
