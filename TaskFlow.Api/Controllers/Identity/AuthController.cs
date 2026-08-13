using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskFlow.Api.Models.Responses;
using TaskFlow.Application.Features.Identity.User.Commands.LoginUser;
using TaskFlow.Application.Features.Identity.User.Commands.LoginWithCode;
using TaskFlow.Application.Features.Identity.User.Commands.LogoutUser;
using TaskFlow.Application.Features.Identity.User.Commands.RefreshUserToken;
using TaskFlow.Application.Features.Identity.User.Commands.RegisterUser;
using TaskFlow.Application.Features.Identity.User.Commands.RequestLoginCode;
using TaskFlow.Application.Features.Identity.User.Commands.RequestPasswordReset;
using TaskFlow.Application.Features.Identity.User.Commands.ResetPassword;
using TaskFlow.Application.Features.Identity.User.Commands.ResendVerificationEmail;
using TaskFlow.Application.Features.Identity.User.Commands.VerifyEmail;
using TaskFlow.Application.Features.Identity.User.DTOs.Commands.LoginUser;

namespace TaskFlow.Api.Controllers.Identity
{
    [ApiController]
    [Route("api/auth")]
    public sealed class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AuthController(
            IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(
            RegisterUserCommand command)
        {
            var userId = await _mediator.Send(command);

            return Ok(
                new ApiResponse<int>
                {
                    Message = "User registered successfully.",
                    Data = userId
                });
        }

        /// <summary>
        /// Completes registration. A new account is PendingVerification and
        /// cannot sign in until this is called with the token from the
        /// welcome email. Idempotent — clicking the link twice still succeeds.
        /// </summary>
        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail(
            VerifyEmailCommand command,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(
                command,
                cancellationToken);

            return Ok(
                new ApiResponse<object>
                {
                    Message = "Email verified. You can sign in now.",
                    Data = null
                });
        }

        /// <summary>
        /// Sends a fresh verification link. Always reports success, even for
        /// an unknown or already-verified address — otherwise it would be an
        /// account-enumeration oracle.
        /// </summary>
        [HttpPost("resend-verification")]
        public async Task<IActionResult> ResendVerification(
            ResendVerificationEmailCommand command,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(
                command,
                cancellationToken);

            return Ok(
                new ApiResponse<object>
                {
                    Message =
                        "If that address needs verifying, a new link is on its way.",
                    Data = null
                });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(
            LoginUserCommand command)
        {
            var result = await _mediator.Send(command);

            return Ok(
                new ApiResponse<LoginUserResponseDto>
                {
                    Message = "Login successful.",
                    Data = result
                });
        }

        /// <summary>
        /// Sends a single-use sign-in code when the address belongs to an
        /// active, verified account. The response never reveals whether it did.
        /// </summary>
        [HttpPost("login-code/request")]
        [EnableRateLimiting("auth-code")]
        public async Task<IActionResult> RequestLoginCode(
            RequestLoginCodeCommand command,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(command, cancellationToken);

            return Ok(new ApiResponse<object>
            {
                Message = "If the account is eligible, a sign-in code is on its way.",
                Data = null
            });
        }

        [HttpPost("login-code/verify")]
        [EnableRateLimiting("auth-code")]
        public async Task<IActionResult> LoginWithCode(
            LoginWithCodeCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);

            return Ok(new ApiResponse<LoginUserResponseDto>
            {
                Message = "Login successful.",
                Data = result
            });
        }

        /// <summary>
        /// Starts password recovery without disclosing account existence.
        /// </summary>
        [HttpPost("password/forgot")]
        [EnableRateLimiting("auth-code")]
        public async Task<IActionResult> ForgotPassword(
            RequestPasswordResetCommand command,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(command, cancellationToken);

            return Ok(new ApiResponse<object>
            {
                Message = "If an account exists for that address, a reset code is on its way.",
                Data = null
            });
        }

        [HttpPost("password/reset")]
        [EnableRateLimiting("auth-code")]
        public async Task<IActionResult> ResetPassword(
            ResetPasswordCommand command,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(command, cancellationToken);

            return Ok(new ApiResponse<object>
            {
                Message = "Password reset successfully. Sign in with your new password.",
                Data = null
            });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(
            RefreshUserTokenCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                command,
                cancellationToken);

            return Ok(
                new ApiResponse<LoginUserResponseDto>
                {
                    Message = "Token refreshed successfully.",
                    Data = result
                });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout(
            LogoutUserCommand command,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(
                command,
                cancellationToken);

            return Ok(
                new ApiResponse<object>
                {
                    Message = "Logged out successfully.",
                    Data = null
                });
        }
    }
}
