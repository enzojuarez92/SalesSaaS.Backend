using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SalesSaaS.Application.Exceptions;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
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

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
