using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;
using System.Security.Claims;

namespace SalesSaaS.Application.Exceptions;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IServiceScopeFactory scopeFactory) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail, extensions) = exception switch
        {
            ValidationException validationException => (
                StatusCodes.Status400BadRequest,
                "La solicitud contiene datos inválidos.",
                "Corregí los campos indicados e intentá nuevamente.",
                new Dictionary<string, object?>
                {
                    ["errors"] = validationException.Errors
                        .GroupBy(error => error.PropertyName)
                        .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray())
                }),
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "Conflicto de concurrencia.",
                "El stock fue modificado por otra operación. Actualizá los datos e intentá nuevamente.",
                new Dictionary<string, object?>()),
            DbUpdateException => (
                StatusCodes.Status409Conflict,
                "Conflicto al guardar los datos.",
                "Los datos ya existen o fueron modificados por otra operación.",
                new Dictionary<string, object?>()),
            ForbiddenAccessException forbiddenAccessException => (
                StatusCodes.Status403Forbidden,
                "No tenés permiso para realizar esta operación.",
                forbiddenAccessException.Message,
                new Dictionary<string, object?>()),
            UnauthorizedAccessException unauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "Autenticación requerida.",
                unauthorizedAccessException.Message,
                new Dictionary<string, object?>()),
            SubscriptionAccessException subscriptionAccessException => (
                StatusCodes.Status402PaymentRequired,
                "Se requiere una suscripción activa.",
                subscriptionAccessException.Message,
                new Dictionary<string, object?>()),
            InvalidOperationException invalidOperationException => (
                StatusCodes.Status400BadRequest,
                "No se pudo completar la operación.",
                invalidOperationException.Message,
                new Dictionary<string, object?>()),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Ocurrió un error interno.",
                "Ocurrió un error inesperado al procesar la solicitud.",
                new Dictionary<string, object?>())
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Path}", httpContext.Request.Path);
            await SavePlatformErrorAsync(httpContext, exception, statusCode, cancellationToken);
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        foreach (var (key, value) in extensions)
        {
            problem.Extensions[key] = value;
        }
        problem.Extensions["message"] = detail;

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private async Task SavePlatformErrorAsync(HttpContext httpContext, Exception exception, int statusCode, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Guid? userId = Guid.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId) ? parsedUserId : null;
            Guid? tenantId = Guid.TryParse(httpContext.User.FindFirstValue("tenant_id"), out var parsedTenantId) ? parsedTenantId : null;
            context.PlatformErrorLogs.Add(new PlatformErrorLog
            {
                Id = Guid.NewGuid(),
                StatusCode = statusCode,
                Method = httpContext.Request.Method,
                Path = httpContext.Request.Path.Value ?? "/",
                UserId = userId,
                TenantId = tenantId,
                UserEmail = httpContext.User.FindFirstValue(ClaimTypes.Email),
                ErrorType = exception.GetType().Name,
                Message = exception.Message.Length > 2000 ? exception.Message[..2000] : exception.Message,
                TraceId = httpContext.TraceIdentifier
            });
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception loggingException)
        {
            logger.LogWarning(loggingException, "Could not persist platform error log");
        }
    }
}
