using MediatR;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Models.Responses;
using TaskFlow.Application.Features.Identity.User.Commands.LoginUser;
using TaskFlow.Application.Features.Identity.User.Commands.LogoutUser;
using TaskFlow.Application.Features.Identity.User.Commands.RefreshUserToken;
using TaskFlow.Application.Features.Identity.User.Commands.RegisterUser;
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
