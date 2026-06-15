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
                    HttpStatusCode.BadRequest,
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
                    HttpStatusCode.Conflict,
                    ex.Code,
                    ex.Message,
                    FailureReason.Conflict);
            }
            catch (NotFoundException ex)
            {
                await WriteErrorResponseAsync(
                    context,
                    HttpStatusCode.NotFound,
                    ex.Code,
                    ex.Message,
                    FailureReason.NotFound);
            }
            catch (UnauthorizedException ex)
            {
                await WriteErrorResponseAsync(
                    context,
                    HttpStatusCode.Unauthorized,
                    ex.Code,
                    ex.Message,
                    FailureReason.Unauthorized);
            }
            catch (ForbiddenException ex)
            {
                await WriteErrorResponseAsync(
                    context,
                    HttpStatusCode.Forbidden,
                    ex.Code,
                    ex.Message,
                    FailureReason.Forbidden);
            }
            catch (BusinessException ex)
            {
                await WriteErrorResponseAsync(
                    context,
                    HttpStatusCode.BadRequest,
                    ex.Code,
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
                    HttpStatusCode.InternalServerError,
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