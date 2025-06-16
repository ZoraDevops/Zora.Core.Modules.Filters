using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using SystemTestJson =  System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
namespace Zora.Core.Modules.Filters

{
    public class ErrorHandler(IHostEnvironment env, ILogger<ErrorHandler> logger) : IExceptionHandler
    {
        private const string UnhandledExceptionMsg = "An unhandled exception has occurred while executing the request.";

        private static readonly SystemTestJson.JsonSerializerOptions SerializerOptions = new(SystemTestJson.JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter(SystemTestJson.JsonNamingPolicy.CamelCase) }
        };

        public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception,
            CancellationToken cancellationToken)
        {
            logger.LogError($"{env.EnvironmentName} : {env.ApplicationName} : {context?.User?.Identity?.Name} : {exception.Message} ");

            ProblemDetails problemDetails = CreateErrorResponse(context, exception);
            var json = ToJson(problemDetails);

            const string contentType = "application/problem+json";
            context.Response.ContentType = contentType;
            context.Response.StatusCode = (int)problemDetails?.Status;
            await context.Response.WriteAsync(json, cancellationToken);

            return true;
        }

        private ProblemDetails CreateErrorResponse(HttpContext context, Exception exception)
        {
            int errorCode = exception.GetHashCode();
            int statusCode = context.Response.StatusCode;

            switch (exception)
            {
                case DbUpdateConcurrencyException:
                    statusCode = StatusCodes.Status409Conflict;
                    break;
                case KeyNotFoundException:
                    statusCode = StatusCodes.Status404NotFound;
                    break;
                case BadHttpRequestException:
                case InvalidOperationException:              
                case JsonReaderException:
                case JsonException:
                    statusCode = StatusCodes.Status400BadRequest;
                    break;
                case NotImplementedException:
                    statusCode = StatusCodes.Status501NotImplemented;
                    break;
            }

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = string.IsNullOrEmpty(ReasonPhrases.GetReasonPhrase(statusCode)) ? UnhandledExceptionMsg : ReasonPhrases.GetReasonPhrase(statusCode),
                Extensions =
                    {
                        [nameof(errorCode)] = errorCode,
                        ["details"] = exception.Message
                    }
            };

            if (!env.IsDevelopment())
            {
                return problemDetails;
            }
            problemDetails.Extensions["traceId"] = context.TraceIdentifier;           
            problemDetails.Extensions["data"] = exception.Data;
            problemDetails.Extensions["innerException"] = exception.InnerException;

            return problemDetails;
        }

        public string ToJson(ProblemDetails problemDetails)
        {
            try
            {
                return SystemTestJson.JsonSerializer.Serialize(problemDetails, SerializerOptions);
            }
            catch (Exception ex)
            {
                const string msg = "An exception has occurred while serializing error to JSON";
                logger.LogError(ex, msg);
            }
            return string.Empty;
        }
    }
}