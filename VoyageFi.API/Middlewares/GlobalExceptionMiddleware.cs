using System.Net;
using System.Text.Json;

namespace VoyageFi.API.Middlewares;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sunucu tarafında beklenmeyen bir hata oluştu.");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response = new 
        {
            StatusCode = context.Response.StatusCode,
            Message = "İşleminiz sırasında bir hata oluştu.",
            Detailed = exception.Message // Not: Prod ortamında gizlenebilir
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
