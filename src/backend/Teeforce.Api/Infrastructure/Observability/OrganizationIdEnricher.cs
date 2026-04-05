using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;
using Shadowbrook.Api.Infrastructure.Auth;

namespace Shadowbrook.Api.Infrastructure.Observability;

public class OrganizationIdEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var userContext = httpContextAccessor.HttpContext?.RequestServices?.GetService<IUserContext>();
        if (userContext?.OrganizationId is { } organizationId)
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("OrganizationId", organizationId));
        }
    }
}
