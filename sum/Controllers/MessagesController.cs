using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sum.Data;
using sum.Models;
using sum.Services;
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
                .Where(m => m.RecipientId == userId.Value && !m.RecipientDeleted)
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();

            foreach (var m in messages)
            {
                m.Subject = EncryptionHelper.Decrypt(m.Subject);
                m.Body = EncryptionHelper.Decrypt(m.Body);
            }

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
                .Where(m => m.SenderId == userId.Value && !m.SenderDeleted)
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();

            foreach (var m in messages)
            {
                m.Subject = EncryptionHelper.Decrypt(m.Subject);
                m.Body = EncryptionHelper.Decrypt(m.Body);
            }

            // For unread messages count in sidebar
            var inboxUnreadCount = await _db.Messages
                .CountAsync(m => m.RecipientId == userId.Value && !m.IsRead && !m.RecipientDeleted);
            ViewBag.UnreadCount = inboxUnreadCount;

            ViewBag.ActiveTab = "sent";
            return View(messages);
        }

        // GET: /Messages/Compose
        [HttpGet]
        public async Task<IActionResult> Compose(string? replyTo = null)
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
            if (!string.IsNullOrEmpty(replyTo))
            {
                Guid? replySecureId = Guid.TryParse(replyTo, out var g) ? g : null;
                int? replyNumericId = int.TryParse(replyTo, out var n) ? n : null;

                var original = await _db.Messages
                    .Include(m => m.Sender)
                    .FirstOrDefaultAsync(m => 
                        ((replySecureId != null && m.SecureId == replySecureId) || (replyNumericId != null && m.Id == replyNumericId)) &&
                        (m.RecipientId == userId.Value || m.SenderId == userId.Value));

                if (original != null)
                {
                    var decryptedSubject = EncryptionHelper.Decrypt(original.Subject);
                    var decryptedBody = EncryptionHelper.Decrypt(original.Body);

                    model.RecipientId = original.SenderId == userId.Value
                        ? original.RecipientId
                        : original.SenderId;
                    model.Subject = decryptedSubject.StartsWith("Re: ")
                        ? decryptedSubject
                        : $"Re: {decryptedSubject}";
                    model.Body = $"\n\n--- Původní zpráva ---\nOd: {original.Sender?.Email}\nDne: {original.SentAt:dd.MM.yyyy HH:mm}\n\n{decryptedBody}";
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
                Subject = EncryptionHelper.Encrypt(model.Subject),
                Body = EncryptionHelper.Encrypt(model.Body),
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

                // Read attachment bytes to persist directly in database (protects against ephemeral disk clears)
                using (var memoryStream = new MemoryStream())
                {
                    await model.Attachment.CopyToAsync(memoryStream);
                    message.AttachmentData = memoryStream.ToArray();
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
        public async Task<IActionResult> Read(string id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            Guid? secureId = Guid.TryParse(id, out var g) ? g : null;
            int? numericId = int.TryParse(id, out var n) ? n : null;

            var message = await _db.Messages
                .Include(m => m.Sender)
                .Include(m => m.Recipient)
                .FirstOrDefaultAsync(m => 
                    ((secureId != null && m.SecureId == secureId) || (numericId != null && m.Id == numericId)) &&
                    (m.RecipientId == userId.Value || m.SenderId == userId.Value));

            if (message == null) return NotFound();

            message.Subject = EncryptionHelper.Decrypt(message.Subject);
            message.Body = EncryptionHelper.Decrypt(message.Body);

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
        public async Task<IActionResult> Download(string id, bool inline = false)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            Guid? secureId = Guid.TryParse(id, out var g) ? g : null;
            int? numericId = int.TryParse(id, out var n) ? n : null;

            var message = await _db.Messages
                .FirstOrDefaultAsync(m => 
                    ((secureId != null && m.SecureId == secureId) || (numericId != null && m.Id == numericId)) &&
                    (m.RecipientId == userId.Value || m.SenderId == userId.Value));

            if (message == null)
                return NotFound();

            byte[] fileBytes;
            var contentType = message.AttachmentContentType ?? "application/octet-stream";
            var fileName = message.AttachmentFileName ?? "attachment";

            // Prefer database stored attachment bytes (safe from ephemeral disk cleanups)
            if (message.AttachmentData != null)
            {
                fileBytes = message.AttachmentData;
            }
            else if (message.AttachmentStoredName != null)
            {
                // Fallback to disk storage for older messages or local cache
                var filePath = Path.Combine(_env.ContentRootPath, "Uploads", "Messages", message.AttachmentStoredName);
                if (!System.IO.File.Exists(filePath))
                    return NotFound();

                fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            }
            else
            {
                return NotFound();
            }

            if (inline)
            {
                return File(fileBytes, contentType);
            }

            return File(fileBytes, contentType, fileName);
        }

        // GET: /Messages/PublicAttachment/c7b2a654-da5c-4f9e-bc43-98282110c71a/image.png
        [HttpGet]
        [Route("Messages/PublicAttachment/{secureId}/{fileName?}")]
        public async Task<IActionResult> PublicAttachment(Guid secureId, string? fileName)
        {
            var message = await _db.Messages
                .FirstOrDefaultAsync(m => m.SecureId == secureId);

            if (message == null)
                return NotFound();

            byte[] fileBytes;
            var contentType = message.AttachmentContentType ?? "application/octet-stream";

            if (message.AttachmentData != null)
            {
                fileBytes = message.AttachmentData;
            }
            else if (message.AttachmentStoredName != null)
            {
                var filePath = Path.Combine(_env.ContentRootPath, "Uploads", "Messages", message.AttachmentStoredName);
                if (!System.IO.File.Exists(filePath))
                    return NotFound();

                fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            }
            else
            {
                return NotFound();
            }

            // Always serve inline for embedding/hotlinking
            return File(fileBytes, contentType);
        }

        // GET: /Messages/UnreadCount (AJAX endpoint for navbar badge)
        [HttpGet]
        public async Task<IActionResult> UnreadCount()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Json(new { count = 0 });

            var count = await _db.Messages
                .CountAsync(m => m.RecipientId == userId.Value && !m.IsRead && !m.RecipientDeleted);

            return Json(new { count });
        }

        // GET: /Messages/Trash
        [HttpGet]
        public async Task<IActionResult> Trash()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            // Clean up expired trash messages
            var cutoff = DateTime.UtcNow.AddDays(-15);
            var expiredMessages = await _db.Messages
                .Where(m => 
                    (m.SenderDeleted && m.SenderDeletedAt <= cutoff) &&
                    (m.RecipientDeleted && m.RecipientDeletedAt <= cutoff))
                .ToListAsync();
            if (expiredMessages.Any())
            {
                _db.Messages.RemoveRange(expiredMessages);
                await _db.SaveChangesAsync();
            }

            var fifteenDaysAgo = DateTime.UtcNow.AddDays(-15);
            var messages = await _db.Messages
                .Include(m => m.Sender)
                .Include(m => m.Recipient)
                .Where(m => 
                    (m.RecipientId == userId.Value && m.RecipientDeleted && m.RecipientDeletedAt > fifteenDaysAgo) ||
                    (m.SenderId == userId.Value && m.SenderDeleted && m.SenderDeletedAt > fifteenDaysAgo))
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();

            foreach (var m in messages)
            {
                m.Subject = EncryptionHelper.Decrypt(m.Subject);
                m.Body = EncryptionHelper.Decrypt(m.Body);
            }

            var inboxUnreadCount = await _db.Messages
                .CountAsync(m => m.RecipientId == userId.Value && !m.IsRead && !m.RecipientDeleted);
            ViewBag.UnreadCount = inboxUnreadCount;

            ViewBag.ActiveTab = "trash";
            return View("Index", messages);
        }

        // POST: /Messages/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            Guid? secureId = Guid.TryParse(id, out var g) ? g : null;
            int? numericId = int.TryParse(id, out var n) ? n : null;

            var message = await _db.Messages
                .FirstOrDefaultAsync(m => 
                    ((secureId != null && m.SecureId == secureId) || (numericId != null && m.Id == numericId)) &&
                    (m.RecipientId == userId.Value || m.SenderId == userId.Value));

            if (message == null) return NotFound();

            if (message.RecipientId == userId.Value)
            {
                message.RecipientDeleted = true;
                message.RecipientDeletedAt = DateTime.UtcNow;
            }
            if (message.SenderId == userId.Value)
            {
                message.SenderDeleted = true;
                message.SenderDeletedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Zpráva byla přesunuta do koše.";

            if (message.RecipientId == userId.Value && !message.SenderDeleted)
                return RedirectToAction("Index");
            else
                return RedirectToAction("Sent");
        }

        // POST: /Messages/Restore/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(string id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            Guid? secureId = Guid.TryParse(id, out var g) ? g : null;
            int? numericId = int.TryParse(id, out var n) ? n : null;

            var message = await _db.Messages
                .FirstOrDefaultAsync(m => 
                    ((secureId != null && m.SecureId == secureId) || (numericId != null && m.Id == numericId)) &&
                    (m.RecipientId == userId.Value || m.SenderId == userId.Value));

            if (message == null) return NotFound();

            if (message.RecipientId == userId.Value)
            {
                message.RecipientDeleted = false;
                message.RecipientDeletedAt = null;
            }
            if (message.SenderId == userId.Value)
            {
                message.SenderDeleted = false;
                message.SenderDeletedAt = null;
            }

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Zpráva byla obnovena z koše.";

            return RedirectToAction("Trash");
        }

        // POST: /Messages/DeletePermanently/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePermanently(string id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            Guid? secureId = Guid.TryParse(id, out var g) ? g : null;
            int? numericId = int.TryParse(id, out var n) ? n : null;

            var message = await _db.Messages
                .FirstOrDefaultAsync(m => 
                    ((secureId != null && m.SecureId == secureId) || (numericId != null && m.Id == numericId)) &&
                    (m.RecipientId == userId.Value || m.SenderId == userId.Value));

            if (message == null) return NotFound();

            bool deleteFromDb = false;

            if (message.RecipientId == userId.Value)
            {
                message.RecipientDeleted = true;
                message.RecipientDeletedAt = DateTime.UtcNow.AddDays(-30);
                if (message.SenderDeleted)
                {
                    deleteFromDb = true;
                }
            }
            if (message.SenderId == userId.Value)
            {
                message.SenderDeleted = true;
                message.SenderDeletedAt = DateTime.UtcNow.AddDays(-30);
                if (message.RecipientDeleted)
                {
                    deleteFromDb = true;
                }
            }

            if (deleteFromDb)
            {
                _db.Messages.Remove(message);
            }

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Zpráva byla trvale smazána.";

            return RedirectToAction("Trash");
        }

        // POST: /Messages/EmptyTrash
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EmptyTrash()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            var fifteenDaysAgo = DateTime.UtcNow.AddDays(-15);
            var messages = await _db.Messages
                .Where(m => 
                    (m.RecipientId == userId.Value && m.RecipientDeleted && m.RecipientDeletedAt > fifteenDaysAgo) ||
                    (m.SenderId == userId.Value && m.SenderDeleted && m.SenderDeletedAt > fifteenDaysAgo))
                .ToListAsync();

            foreach (var message in messages)
            {
                bool deleteFromDb = false;

                if (message.RecipientId == userId.Value)
                {
                    message.RecipientDeleted = true;
                    message.RecipientDeletedAt = DateTime.UtcNow.AddDays(-30);
                    if (message.SenderDeleted)
                    {
                        deleteFromDb = true;
                    }
                }
                if (message.SenderId == userId.Value)
                {
                    message.SenderDeleted = true;
                    message.SenderDeletedAt = DateTime.UtcNow.AddDays(-30);
                    if (message.RecipientDeleted)
                    {
                        deleteFromDb = true;
                    }
                }

                if (deleteFromDb)
                {
                    _db.Messages.Remove(message);
                }
            }

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Koš byl úspěšně vysypán.";

            return RedirectToAction("Trash");
        }
    }
}
