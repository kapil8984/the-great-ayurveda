using Microsoft.Extensions.Logging;
using Ayurveda.Application.Interfaces;
using Ayurveda.Domain.Entities;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly AyurvedaDbContext _context;
    private readonly ILogger<AuditService> _logger;

    public AuditService(AyurvedaDbContext context, ILogger<AuditService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogAsync(string action, string entityName, string? entityId, string? details, string? userEmail = null, string? ipAddress = null)
    {
        try
        {
            var entry = new AuditLog
            {
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                Details = details,
                UserEmail = userEmail ?? "system@thegreatayurveda.com",
                IpAddress = ipAddress ?? "127.0.0.1",
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(entry);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist audit log entry: {Action} on {EntityName}:{EntityId}", action, entityName, entityId);
        }
    }
}
