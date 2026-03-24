using Serilog.Core;
using Serilog.Events;

namespace Shadowbrook.Api.Infrastructure.Observability;

public class TenantIdEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var claim = httpContextAccessor.HttpContext?.User.FindFirst("tenant_id");
        if (claim is not null && Guid.TryParse(claim.Value, out var tenantId))
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("TenantId", tenantId));
        }
    }
}
