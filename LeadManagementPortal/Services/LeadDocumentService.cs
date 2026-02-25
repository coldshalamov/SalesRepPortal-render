using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace LeadManagementPortal.Services
{
    public class LeadDocumentService : ILeadDocumentService
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _storage;

        public LeadDocumentService(ApplicationDbContext db, IFileStorageService storage)
        {
            _db = db;
            _storage = storage;
        }

        public async Task<LeadDocument> AddAsync(string leadId, string fileName, string contentType, long sizeBytes, Stream content, string? uploadedByUserId, CancellationToken ct = default)
        {
            var keyFileName = System.IO.Path.GetFileName(fileName);
            var ext = System.IO.Path.GetExtension(keyFileName);
            var safeName = System.IO.Path.GetFileNameWithoutExtension(keyFileName);
            var unique = $"{safeName}_{Guid.NewGuid():N}{ext}";
            var key = $"leads/{leadId}/{unique}".Replace("\\", "/");

            await _storage.UploadAsync(content, contentType, key, ct);

            var doc = new LeadDocument
            {
                LeadId = leadId,
                FileName = keyFileName,
                ContentType = contentType,
                SizeBytes = sizeBytes,
                StorageKey = key,
                UploadedAtUtc = DateTime.UtcNow,
                UploadedByUserId = uploadedByUserId
            };

            _db.LeadDocuments.Add(doc);
            await _db.SaveChangesAsync(ct);
            return doc;
        }

        public async Task<IReadOnlyList<LeadDocument>> ListAsync(string leadId, CancellationToken ct = default)
        {
            return await _db.LeadDocuments
                .Where(d => d.LeadId == leadId)
                .OrderByDescending(d => d.UploadedAtUtc)
                .ToListAsync(ct);
        }

        public async Task<LeadDocument?> GetAsync(int id, CancellationToken ct = default)
        {
            return await _db.LeadDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);
        }

        public async Task<string?> GetDownloadUrlAsync(int id, TimeSpan? expires = null, CancellationToken ct = default)
        {
            var doc = await GetAsync(id, ct);
            if (doc == null) return null;
            var url = await _storage.GetPreSignedDownloadUrlAsync(doc.StorageKey, expires ?? TimeSpan.FromMinutes(10), ct);
            return url;
        }

        public async Task<int> DeleteForLeadAsync(string leadId, CancellationToken ct = default)
        {
            var docs = await _db.LeadDocuments
                .Where(d => d.LeadId == leadId)
                .ToListAsync(ct);

            if (docs.Count == 0)
            {
                return 0;
            }

            foreach (var doc in docs)
            {
                await _storage.DeleteAsync(doc.StorageKey, ct);
            }

            _db.LeadDocuments.RemoveRange(docs);
            await _db.SaveChangesAsync(ct);
            return docs.Count;
        }
    }
}
