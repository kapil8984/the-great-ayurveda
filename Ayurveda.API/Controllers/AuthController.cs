using Ayurveda.API.DTOs;
using Ayurveda.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ayurveda.API.Controllers;

[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    // Customer / User Endpoints
    [HttpPost("api/auth/user/signup")]
    [HttpPost("user/signup")]
    public async Task<IActionResult> UserSignUp([FromBody] UserRegisterDto dto)
    {
        var result = await _authService.RegisterUserAsync(dto);
        if (!result.Success)
        {
            return BadRequest(new { message = result.Message, error = result.Message });
        }
        return Ok(new { message = result.Message, user = result.User, token = result.Token });
    }

    [HttpPost("api/auth/user/signin")]
    [HttpPost("user/signin")]
    public async Task<IActionResult> UserSignIn([FromBody] UserLoginDto dto)
    {
        var result = await _authService.LoginUserAsync(dto);
        if (!result.Success)
        {
            return Unauthorized(new { message = result.Message, error = result.Message });
        }
        return Ok(new { message = result.Message, user = result.User, token = result.Token });
    }

    // Doctor Endpoints
    [HttpPost("api/auth/doctor/signup")]
    [HttpPost("doctor/sign-up")]
    public async Task<IActionResult> DoctorSignUp([FromBody] DoctorRegisterDto dto)
    {
        var result = await _authService.RegisterDoctorAsync(dto);
        if (!result.Success)
        {
            return BadRequest(new { message = result.Message, error = result.Message });
        }
        return Ok(new { message = result.Message, doctor = result.User, token = result.Token });
    }

    [HttpPost("api/auth/doctor/signin")]
    [HttpPost("doctor/sign-in")]
    public async Task<IActionResult> DoctorSignIn([FromBody] DoctorLoginDto dto)
    {
        var result = await _authService.LoginDoctorAsync(dto);
        if (!result.Success)
        {
            return Unauthorized(new { message = result.Message, error = result.Message });
        }
        return Ok(new { message = result.Message, doctor = result.User, token = result.Token });
    }

    // Admin Endpoints - Public admin registration is REMOVED completely (Rule b)
    // Admin login is retained with lockout and rate limiting.
    [HttpPost("api/auth/admin/signin")]
    [HttpPost("admin/sign-in")]
    public async Task<IActionResult> AdminSignIn([FromBody] AdminLoginDto dto)
    {
        var result = await _authService.LoginAdminAsync(dto);
        if (!result.Success)
        {
            return Unauthorized(new { message = result.Message, error = result.Message });
        }
        return Ok(new { message = result.Message, admin = result.User, token = result.Token });
    }

    // Password Reset with generic non-enumerating response (Rule g)
    [HttpPost("api/auth/forgot-password")]
    [HttpPost("user/forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _authService.ForgotPasswordAsync(dto.Email, ip);
        return Ok(new { message = "If the account exists, a verification OTP has been dispatched to the email." });
    }

    [HttpPost("api/auth/reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        var success = await _authService.ResetPasswordAsync(dto.Email, dto.OtpCode, dto.NewPassword);
        if (!success)
        {
            return BadRequest(new { message = "Invalid or expired OTP code or maximum attempts exceeded." });
        }
        return Ok(new { message = "Password reset successfully. You can now log in." });
    }
}
