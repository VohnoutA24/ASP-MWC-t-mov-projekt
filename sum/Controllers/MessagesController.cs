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
                if (m.Body == null || !m.Body.StartsWith("__E2E__:"))
                {
                    m.Subject = EncryptionHelper.Decrypt(m.Subject);
                    m.Body = EncryptionHelper.Decrypt(m.Body);
                }
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
                .Include(m => m.Sender)
                .Where(m => m.SenderId == userId.Value && !m.SenderDeleted)
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();

            foreach (var m in messages)
            {
                if (m.Body == null || !m.Body.StartsWith("__E2E__:"))
                {
                    m.Subject = EncryptionHelper.Decrypt(m.Subject);
                    m.Body = EncryptionHelper.Decrypt(m.Body);
                }
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
                    bool isOriginalE2e = original.Body != null && original.Body.StartsWith("__E2E__:");
                    var decryptedSubject = isOriginalE2e ? "[Zašifrovaná zpráva]" : EncryptionHelper.Decrypt(original.Subject);
                    var decryptedBody = isOriginalE2e ? original.Body : EncryptionHelper.Decrypt(original.Body);

                    model.RecipientId = original.SenderId == userId.Value
                        ? original.RecipientId
                        : original.SenderId;
                    model.Subject = decryptedSubject.StartsWith("Re: ")
                        ? decryptedSubject
                        : $"Re: {decryptedSubject}";
                    model.Body = $"\n\n--- Původní zpráva ---\nOd: {original.Sender?.Email}\nDne: {original.SentAt.ToCzechTime():dd.MM.yyyy HH:mm}\n\n{decryptedBody}";
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

            bool isE2e = model.Body != null && model.Body.StartsWith("__E2E__:");
            var message = new Message
            {
                SenderId = userId.Value,
                RecipientId = model.RecipientId,
                Subject = isE2e ? model.Subject : EncryptionHelper.Encrypt(model.Subject),
                Body = isE2e ? model.Body : EncryptionHelper.Encrypt(model.Body),
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

            if (message.Body == null || !message.Body.StartsWith("__E2E__:"))
            {
                message.Subject = EncryptionHelper.Decrypt(message.Subject);
                message.Body = EncryptionHelper.Decrypt(message.Body);
            }

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

        // GET: /Messages/GetNotifications (AJAX polling endpoint)
        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var mailCount = await _db.Messages
                .CountAsync(m => m.RecipientId == userId.Value && !m.IsRead && !m.RecipientDeleted);

            var chatCounts = await _db.ChatMessages
                .Where(m => m.RecipientId == userId.Value && !m.IsRead)
                .GroupBy(m => m.SenderId)
                .Select(g => new { SenderId = g.Key, Count = g.Count() })
                .ToListAsync();

            var totalChatCount = chatCounts.Sum(c => c.Count);
            var chatMap = chatCounts.ToDictionary(c => c.SenderId, c => c.Count);

            return Json(new {
                mailCount = mailCount,
                chatTotalCount = totalChatCount,
                chatContacts = chatMap
            });
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
                if (m.Body == null || !m.Body.StartsWith("__E2E__:"))
                {
                    m.Subject = EncryptionHelper.Decrypt(m.Subject);
                    m.Body = EncryptionHelper.Decrypt(m.Body);
                }
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

        // POST: /Messages/BulkDelete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkDelete(List<string> ids)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");
            if (ids == null || !ids.Any()) return RedirectToAction("Index");

            foreach (var id in ids)
            {
                Guid? secureId = Guid.TryParse(id, out var g) ? g : null;
                int? numericId = int.TryParse(id, out var n) ? n : null;

                var message = await _db.Messages
                    .FirstOrDefaultAsync(m => 
                        ((secureId != null && m.SecureId == secureId) || (numericId != null && m.Id == numericId)) &&
                        (m.RecipientId == userId.Value || m.SenderId == userId.Value));

                if (message != null)
                {
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
                }
            }

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Vybrané zprávy byly přesunuty do koše.";

            var referer = Request.Headers["Referer"].ToString();
            if (referer.Contains("/Sent", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Sent");
            return RedirectToAction("Index");
        }

        // POST: /Messages/BulkRestore
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkRestore(List<string> ids)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");
            if (ids == null || !ids.Any()) return RedirectToAction("Trash");

            foreach (var id in ids)
            {
                Guid? secureId = Guid.TryParse(id, out var g) ? g : null;
                int? numericId = int.TryParse(id, out var n) ? n : null;

                var message = await _db.Messages
                    .FirstOrDefaultAsync(m => 
                        ((secureId != null && m.SecureId == secureId) || (numericId != null && m.Id == numericId)) &&
                        (m.RecipientId == userId.Value || m.SenderId == userId.Value));

                if (message != null)
                {
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
                }
            }

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Vybrané zprávy byly obnoveny z koše.";
            return RedirectToAction("Trash");
        }

        // POST: /Messages/BulkDeletePermanently
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkDeletePermanently(List<string> ids)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");
            if (ids == null || !ids.Any()) return RedirectToAction("Trash");

            foreach (var id in ids)
            {
                Guid? secureId = Guid.TryParse(id, out var g) ? g : null;
                int? numericId = int.TryParse(id, out var n) ? n : null;

                var message = await _db.Messages
                    .FirstOrDefaultAsync(m => 
                        ((secureId != null && m.SecureId == secureId) || (numericId != null && m.Id == numericId)) &&
                        (m.RecipientId == userId.Value || m.SenderId == userId.Value));

                if (message != null)
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
            }

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Vybrané zprávy byly trvale smazány.";
            return RedirectToAction("Trash");
        }

        // GET: /Messages/Chat
        [HttpGet]
        public async Task<IActionResult> Chat()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            var users = await _db.Users
                .Where(u => u.Id != userId.Value)
                .OrderBy(u => u.FullName ?? u.Email)
                .ToListAsync();

            ViewBag.Users = users;

            // For unread messages count in sidebar
            var inboxUnreadCount = await _db.Messages
                .CountAsync(m => m.RecipientId == userId.Value && !m.IsRead && !m.RecipientDeleted);
            ViewBag.UnreadCount = inboxUnreadCount;

            // Calculate unread chat messages per contact
            var unreadChatCounts = await _db.ChatMessages
                .Where(m => m.RecipientId == userId.Value && !m.IsRead)
                .GroupBy(m => m.SenderId)
                .Select(g => new { SenderId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.SenderId, g => g.Count);
            ViewBag.UnreadChatCounts = unreadChatCounts;

            // For each contact, find the timestamp of the last message exchanged (either sent or received)
            var lastMessages = await _db.ChatMessages
                .Where(m => m.SenderId == userId.Value || m.RecipientId == userId.Value)
                .ToListAsync();

            var lastMessageTimeMap = lastMessages
                .GroupBy(m => m.SenderId == userId.Value ? m.RecipientId : m.SenderId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Max(m => m.SentAt)
                );
            ViewBag.LastMessageTimes = lastMessageTimeMap;

            ViewBag.ActiveTab = "chat";
            return View();
        }

        // GET: /Messages/GetChatMessages
        [HttpGet]
        public async Task<IActionResult> GetChatMessages(int contactId)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var messages = await _db.ChatMessages
                .Where(m => (m.SenderId == userId.Value && m.RecipientId == contactId) ||
                            (m.SenderId == contactId && m.RecipientId == userId.Value))
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            // Mark incoming messages from contactId to current user as read
            var unreadIncoming = messages.Where(m => m.SenderId == contactId && m.RecipientId == userId.Value && !m.IsRead).ToList();
            if (unreadIncoming.Any())
            {
                foreach (var msg in unreadIncoming)
                {
                    msg.IsRead = true;
                }
                await _db.SaveChangesAsync();
            }

            var result = messages.Select(m => {
                string payload = m.EncryptedPayload;
                bool isE2e = payload.StartsWith("__E2E__:");
                string bodyDecrypted = payload;
                if (!isE2e)
                {
                    bodyDecrypted = EncryptionHelper.Decrypt(payload);
                }

                return new {
                    id = m.Id,
                    senderId = m.SenderId,
                    recipientId = m.RecipientId,
                    body = bodyDecrypted,
                    isE2e = isE2e,
                    sentAt = m.SentAt.ToCzechTime().ToString("dd.MM.yyyy HH:mm:ss")
                };
            });

            return Json(result);
        }

        // POST: /Messages/SendChatMessage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendChatMessage(int contactId, string body)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(body))
            {
                return BadRequest("Zpráva nesmí být prázdná.");
            }

            if (body.Length > 2000 && !body.StartsWith("__E2E__:"))
            {
                return BadRequest("Zpráva překročila limit 2000 znaků.");
            }

            var recipient = await _db.Users.FindAsync(contactId);
            if (recipient == null)
            {
                return NotFound("Příjemce nebyl nalezen.");
            }

            string encryptedPayload;
            if (body.StartsWith("__E2E__:"))
            {
                encryptedPayload = body;
            }
            else
            {
                // Fallback to server-side encryption
                encryptedPayload = EncryptionHelper.Encrypt(body);
            }

            var chatMsg = new ChatMessage
            {
                SenderId = userId.Value,
                RecipientId = contactId,
                EncryptedPayload = encryptedPayload,
                SentAt = DateTime.UtcNow
            };

            _db.ChatMessages.Add(chatMsg);
            await _db.SaveChangesAsync();

            return Json(new { 
                success = true, 
                id = chatMsg.Id,
                sentAt = chatMsg.SentAt.ToCzechTime().ToString("dd.MM.yyyy HH:mm:ss")
            });
        }
    }
}
