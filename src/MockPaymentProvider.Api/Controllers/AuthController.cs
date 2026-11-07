using Microsoft.AspNetCore.Mvc;
using MockPaymentProvider.Application.Auth;

namespace MockPaymentProvider.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>Exchange the client id and secret for a short-lived bearer token.</summary>
    [HttpPost("token")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public ActionResult<TokenResponse> CreateToken([FromBody] TokenRequest request) =>
        Ok(authService.IssueToken(request));
}
