using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sum.Data;
using sum.Models;
using System.Security.Claims;

namespace sum.Controllers
{
    public class MessagesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private const long MaxAttachmentSize = 25 * 1024 * 1024; // 25 MB

        public MessagesController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirst("UserId")?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }

        // GET: /Messages  (Inbox)
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            var messages = await _db.Messages
                .Include(m => m.Sender)
                .Where(m => m.RecipientId == userId.Value)
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();

            ViewBag.ActiveTab = "inbox";
            ViewBag.UnreadCount = messages.Count(m => !m.IsRead);
            return View(messages);
        }

        // GET: /Messages/Sent
        [HttpGet]
        public async Task<IActionResult> Sent()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            var messages = await _db.Messages
                .Include(m => m.Recipient)
                .Where(m => m.SenderId == userId.Value)
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();

            ViewBag.ActiveTab = "sent";
            return View(messages);
        }

        // GET: /Messages/Compose
        [HttpGet]
        public async Task<IActionResult> Compose(int? replyTo = null)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            var users = await _db.Users
                .Where(u => u.Id != userId.Value)
                .OrderBy(u => u.FullName ?? u.Email)
                .ToListAsync();

            ViewBag.Users = users;

            var model = new ComposeMessageViewModel();

            // Pre-fill if replying
            if (replyTo.HasValue)
            {
                var original = await _db.Messages
                    .Include(m => m.Sender)
                    .FirstOrDefaultAsync(m => m.Id == replyTo.Value &&
                        (m.RecipientId == userId.Value || m.SenderId == userId.Value));

                if (original != null)
                {
                    model.RecipientId = original.SenderId == userId.Value
                        ? original.RecipientId
                        : original.SenderId;
                    model.Subject = original.Subject.StartsWith("Re: ")
                        ? original.Subject
                        : $"Re: {original.Subject}";
                    model.Body = $"\n\n--- Původní zpráva ---\nOd: {original.Sender?.Email}\nDne: {original.SentAt:dd.MM.yyyy HH:mm}\n\n{original.Body}";
                }
            }

            return View(model);
        }

        // POST: /Messages/Compose
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Compose(ComposeMessageViewModel model)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            // Validate attachment size
            if (model.Attachment != null && model.Attachment.Length > MaxAttachmentSize)
            {
                ModelState.AddModelError("Attachment", "Příloha nesmí přesáhnout 25 MB.");
            }

            if (!ModelState.IsValid)
            {
                var users = await _db.Users
                    .Where(u => u.Id != userId.Value)
                    .OrderBy(u => u.FullName ?? u.Email)
                    .ToListAsync();
                ViewBag.Users = users;
                return View(model);
            }

            var message = new Message
            {
                SenderId = userId.Value,
                RecipientId = model.RecipientId,
                Subject = model.Subject,
                Body = model.Body,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            // Handle attachment
            if (model.Attachment != null && model.Attachment.Length > 0)
            {
                var uploadsDir = Path.Combine(_env.ContentRootPath, "Uploads", "Messages");
                Directory.CreateDirectory(uploadsDir);

                var storedName = $"{Guid.NewGuid()}{Path.GetExtension(model.Attachment.FileName)}";
                var filePath = Path.Combine(uploadsDir, storedName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.Attachment.CopyToAsync(stream);
                }

                message.AttachmentFileName = model.Attachment.FileName;
                message.AttachmentStoredName = storedName;
                message.AttachmentContentType = model.Attachment.ContentType;
                message.AttachmentSize = model.Attachment.Length;
            }

            _db.Messages.Add(message);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Zpráva byla úspěšně odeslána!";
            return RedirectToAction("Sent");
        }

        // GET: /Messages/Read/5
        [HttpGet]
        public async Task<IActionResult> Read(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            var message = await _db.Messages
                .Include(m => m.Sender)
                .Include(m => m.Recipient)
                .FirstOrDefaultAsync(m => m.Id == id &&
                    (m.RecipientId == userId.Value || m.SenderId == userId.Value));

            if (message == null) return NotFound();

            // Mark as read if recipient is viewing
            if (message.RecipientId == userId.Value && !message.IsRead)
            {
                message.IsRead = true;
                await _db.SaveChangesAsync();
            }

            return View(message);
        }

        // GET: /Messages/Download/5
        [HttpGet]
        public async Task<IActionResult> Download(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            var message = await _db.Messages
                .FirstOrDefaultAsync(m => m.Id == id &&
                    (m.RecipientId == userId.Value || m.SenderId == userId.Value));

            if (message == null || message.AttachmentStoredName == null)
                return NotFound();

            var filePath = Path.Combine(_env.ContentRootPath, "Uploads", "Messages", message.AttachmentStoredName);
            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var contentType = message.AttachmentContentType ?? "application/octet-stream";
            var fileName = message.AttachmentFileName ?? "attachment";

            return PhysicalFile(filePath, contentType, fileName);
        }

        // GET: /Messages/UnreadCount (AJAX endpoint for navbar badge)
        [HttpGet]
        public async Task<IActionResult> UnreadCount()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Json(new { count = 0 });

            var count = await _db.Messages
                .CountAsync(m => m.RecipientId == userId.Value && !m.IsRead);

            return Json(new { count });
        }
    }
}
