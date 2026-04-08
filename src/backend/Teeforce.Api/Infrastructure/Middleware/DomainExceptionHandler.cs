using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Teeforce.Domain.AppUserAggregate.Exceptions;
using Teeforce.Domain.BookingAggregate.Exceptions;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate.Exceptions;
using Teeforce.Domain.CourseWaitlistAggregate.Exceptions;
using Teeforce.Domain.GolferWaitlistEntryAggregate.Exceptions;
using Teeforce.Domain.TeeSheetAggregate.Exceptions;
using Teeforce.Domain.TeeTimeAggregate.Exceptions;
using Teeforce.Domain.TeeTimeOpeningAggregate.Exceptions;
using Teeforce.Domain.WaitlistOfferAggregate.Exceptions;

namespace Teeforce.Api.Infrastructure.Middleware;

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
                    OfferAlreadyNotifiedException => StatusCodes.Status409Conflict,
                    BookingNotPendingException => StatusCodes.Status409Conflict,
                    BookingNotCancellableException => StatusCodes.Status409Conflict,
                    CannotExtendRemovedEntryException => StatusCodes.Status409Conflict,
                    TeeSheetNotPublishedException => StatusCodes.Status409Conflict,
                    TeeSheetAlreadyExistsException => StatusCodes.Status409Conflict,
                    TeeTimeBlockedException => StatusCodes.Status409Conflict,
                    TeeTimeFilledException => StatusCodes.Status409Conflict,
                    TeeTimeHasClaimsException => StatusCodes.Status409Conflict,
                    BookingAuthorizationMismatchException => StatusCodes.Status409Conflict,
                    CourseScheduleNotConfiguredException => StatusCodes.Status409Conflict,
                    EntityNotFoundException => StatusCodes.Status404NotFound,
                    InvalidSlotsAvailableException => StatusCodes.Status422UnprocessableEntity,
                    Teeforce.Domain.TeeTimeOpeningAggregate.Exceptions.InvalidGroupSizeException => StatusCodes.Status422UnprocessableEntity,
                    InsufficientCapacityException => StatusCodes.Status422UnprocessableEntity,
                    Teeforce.Domain.TeeTimeAggregate.Exceptions.InvalidGroupSizeException => StatusCodes.Status422UnprocessableEntity,
                    InvalidTeeTimeCapacityException => StatusCodes.Status422UnprocessableEntity,
                    InvalidScheduleSettingsException => StatusCodes.Status422UnprocessableEntity,
                    IdentityAlreadyLinkedException => StatusCodes.Status409Conflict,
                    DuplicateEmailException => StatusCodes.Status409Conflict,
                    EmptyOrganizationIdException => StatusCodes.Status422UnprocessableEntity,
                    _ => StatusCodes.Status400BadRequest
                };
                await context.Response.WriteAsJsonAsync(new { error = domainEx.Message });
            }
            else if (IsUniqueConstraintViolation(ex))
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                await context.Response.WriteAsJsonAsync(new { error = "A duplicate record already exists." });
            }
            else if (ex is not null)
            {
                var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("DomainExceptionHandler");
                logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
            }
        }));
    }

    private static bool IsUniqueConstraintViolation(Exception? ex) =>
        ex is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } };
}
