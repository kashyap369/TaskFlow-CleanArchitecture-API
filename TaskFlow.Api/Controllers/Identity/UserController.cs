using MediatR;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Features.Identity.User.Queries.GetCurrentUserProfile;
using TaskFlow.Application.Features.Identity.User.Queries.GetUserById;
using TaskFlow.Application.Features.Identity.User.Queries.GetUsers;

namespace TaskFlow.Api.Controllers.Identity
{
    // Development stage: endpoints are open. Secure later with:
    // [Authorize(Policy = Constants.AuthorizationPolicies.AllRoles)]
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IMediator _mediator;

        public UserController(
            IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("me")]
        public async Task<IActionResult> Me(
            CancellationToken cancellationToken)
        {
            var profile =
                await _mediator.Send(
                    new GetCurrentUserProfileQuery(),
                    cancellationToken);

            return Ok(profile);
        }

        [HttpGet("{userId:int}")]
        public async Task<IActionResult> GetById(
            int userId,
            CancellationToken cancellationToken)
        {
            var user =
                await _mediator.Send(
                    new GetUserByIdQuery(userId),
                    cancellationToken);

            return Ok(user);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            CancellationToken cancellationToken)
        {
            var users =
                await _mediator.Send(
                    new GetUsersQuery(),
                    cancellationToken);

            return Ok(users);
        }
    }
}
