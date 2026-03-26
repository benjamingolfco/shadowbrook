using Microsoft.AspNetCore.Diagnostics;
using Shadowbrook.Domain.BookingAggregate.Exceptions;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseWaitlistAggregate.Exceptions;
using Shadowbrook.Domain.WaitlistOfferAggregate.Exceptions;

namespace Shadowbrook.Api.Infrastructure.Middleware;

public static class DomainExceptionHandler
{
    public static IApplicationBuilder UseDomainExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseExceptionHandler(error => error.Run(async context =>
        {
            var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
            if (ex is DomainException domainEx)
            {
                context.Response.StatusCode = domainEx switch
                {
                    GolferAlreadyOnWaitlistException => StatusCodes.Status409Conflict,
                    WaitlistAlreadyExistsException => StatusCodes.Status409Conflict,
                    WaitlistNotClosedException => StatusCodes.Status409Conflict,
                    OfferNotPendingException => StatusCodes.Status409Conflict,
                    BookingNotPendingException => StatusCodes.Status409Conflict,
                    EntityNotFoundException => StatusCodes.Status404NotFound,
                    _ => StatusCodes.Status400BadRequest
                };
                await context.Response.WriteAsJsonAsync(new { error = domainEx.Message });
            }
        }));
    }
}
