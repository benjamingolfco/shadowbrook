using FluentValidation;

namespace Shadowbrook.Api.Endpoints.Filters;

public static class ValidationFilter
{
    public static RouteGroupBuilder AddValidationFilter(this RouteGroupBuilder group)
    {
        group.AddEndpointFilterFactory((filterFactoryContext, next) =>
        {
            var parameters = filterFactoryContext.MethodInfo.GetParameters();

            for (var i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                var validatorType = typeof(IValidator<>).MakeGenericType(paramType);

                var hasValidator = filterFactoryContext.ApplicationServices
                    .GetService(validatorType) is not null;

                if (hasValidator)
                {
                    var argIndex = i;
                    return async invocationContext =>
                    {
                        var argument = invocationContext.Arguments[argIndex];
                        if (argument is null)
                        {
                            return await next(invocationContext);
                        }

                        var validator = invocationContext.HttpContext
                            .RequestServices.GetRequiredService(validatorType);
                        var validateMethod = validatorType.GetMethod("ValidateAsync",
                            [paramType, typeof(CancellationToken)]);
                        var validationResult = await (Task<FluentValidation.Results.ValidationResult>)validateMethod!
                            .Invoke(validator, [argument, CancellationToken.None])!;

                        if (!validationResult.IsValid)
                        {
                            return Results.BadRequest(new { error = validationResult.Errors[0].ErrorMessage });
                        }

                        return await next(invocationContext);
                    };
                }
            }

            return invocationContext => next(invocationContext);
        });

        return group;
    }
}
