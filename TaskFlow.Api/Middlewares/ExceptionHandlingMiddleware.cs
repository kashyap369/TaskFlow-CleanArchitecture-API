using FluentValidation;
using System.Net;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Models.Responses;
using TaskFlow.Application.Exceptions;

namespace TaskFlow.Api.Middlewares
{
    public sealed class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next,ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// The single mapping from a thrown exception to the status this middleware will write.
        ///
        /// It is public because this middleware is the outermost one, so anything inside it — the
        /// meeting observability middleware, for instance — sees an exception propagating past with
        /// the response status still untouched. Classifying that as a success would report every
        /// refused meeting request as a healthy one, which is the opposite of what the P7.4 alert
        /// rules need. Both callers reading one method is what keeps them from drifting apart.
        ///
        /// Order matters: Conflict/NotFound/Unauthorized/Forbidden all derive from
        /// <see cref="BusinessException"/>, so the derived types are matched first.
        /// </summary>
        public static HttpStatusCode StatusCodeFor(Exception exception) => exception switch
        {
            ValidationException => HttpStatusCode.BadRequest,
            ConflictException => HttpStatusCode.Conflict,
            NotFoundException => HttpStatusCode.NotFound,
            UnauthorizedException => HttpStatusCode.Unauthorized,
            ForbiddenException => HttpStatusCode.Forbidden,
            BusinessException => HttpStatusCode.BadRequest,
            ArgumentException or InvalidOperationException => HttpStatusCode.BadRequest,
            _ => HttpStatusCode.InternalServerError
        };

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (ValidationException ex)
            {
                await WriteErrorResponseAsync(
                    context,
                    StatusCodeFor(ex),
                    "VALIDATION_ERROR",
                    "Validation failed.",
                    FailureReason.ValidationFailure,
                    ex.Errors.Select(x => new
                    {
                        x.PropertyName,
                        x.ErrorMessage
                    }));
            }
            catch (ConflictException ex)
            {
                await WriteErrorResponseAsync(
                    context,
                    StatusCodeFor(ex),
                    ex.Code,
                    ex.Message,
                    FailureReason.Conflict);
            }
            catch (NotFoundException ex)
            {
                await WriteErrorResponseAsync(
                    context,
                    StatusCodeFor(ex),
                    ex.Code,
                    ex.Message,
                    FailureReason.NotFound);
            }
            catch (UnauthorizedException ex)
            {
                await WriteErrorResponseAsync(
                    context,
                    StatusCodeFor(ex),
                    ex.Code,
                    ex.Message,
                    FailureReason.Unauthorized);
            }
            catch (ForbiddenException ex)
            {
                await WriteErrorResponseAsync(
                    context,
                    StatusCodeFor(ex),
                    ex.Code,
                    ex.Message,
                    FailureReason.Forbidden);
            }
            catch (BusinessException ex)
            {
                await WriteErrorResponseAsync(
                    context,
                    StatusCodeFor(ex),
                    ex.Code,
                    ex.Message,
                    FailureReason.BusinessRuleViolation);
            }
            // Domain invariants guard themselves with ArgumentException /
            // InvalidOperationException (e.g. "Personal tasks cannot be
            // assigned.", "End time cannot be in the future."). Those are
            // caller mistakes, not server faults — map them to 400 rather
            // than letting them fall through as a 500.
            catch (Exception ex) when (
                ex is ArgumentException or InvalidOperationException)
            {
                _logger.LogWarning(
                    ex,
                    "Domain rule violated.");

                await WriteErrorResponseAsync(
                    context,
                    StatusCodeFor(ex),
                    "DOMAIN_RULE_VIOLATION",
                    ex.Message,
                    FailureReason.BusinessRuleViolation);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unhandled exception occurred.");

                await WriteErrorResponseAsync(
                    context,
                    StatusCodeFor(ex),
                    "INTERNAL_SERVER_ERROR",
                    "An unexpected error occurred.",
                    FailureReason.InternalServerError);
            }
        }

        private static async Task WriteErrorResponseAsync(
            HttpContext context,
            HttpStatusCode statusCode,
            string code,
            string message,
            FailureReason failureReason,
            object? errors = null)
        {
            context.Response.ContentType = "application/json";

            context.Response.StatusCode = (int)statusCode;

            var response = new ApiErrorResponse
            {
                Code = code,
                Message = message,
                FailureReason = failureReason.ToString(),
                Errors = errors,
                TraceId = context.TraceIdentifier
            };

            await context.Response.WriteAsJsonAsync(response);
        }
    }
}