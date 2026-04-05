using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;
using Teeforce.Api.Infrastructure.Auth;

namespace Teeforce.Api.Infrastructure.Observability;

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
