namespace Ayurveda.API.DTOs;

public record UserRegisterDto(string Username, string Email, string Password, string Contact);
public record UserLoginDto(string Email, string Password);

public record DoctorRegisterDto(string DoctorName, string Email, string Password, string ContactNumber);
public record DoctorLoginDto(string Email, string Password);

public record AdminRegisterDto(string AdminName, string Email, string Password, string ContactNumber);
public record AdminLoginDto(string Email, string Password);

public record AuthResponseDto(
    bool Success,
    string Message,
    string? Token,
    object? User,
    string? Role
);

public record UserDto(
    int Id,
    string Username,
    string Email,
    string Contact,
    string? ProfileImage,
    string Role,
    DateTime CreatedAt
);

public record DoctorDto(
    int Id,
    string DoctorName,
    string Email,
    string ContactNumber,
    string? ProfileImage,
    string Role,
    DateTime CreatedAt,
    DoctorDetailDto? DoctorDetail
);

public record AdminDto(
    int Id,
    string AdminName,
    string Email,
    string ContactNumber,
    string? ProfileImage,
    string Role,
    DateTime CreatedAt
);

public record ChangePasswordDto(int? DoctorId, string OldPassword, string NewPassword);
public record ForgotPasswordDto(string Email);
public record ResetPasswordDto(string Email, string OtpCode, string NewPassword);
public record UpdateUserProfileDto(string? Username, string? Contact);
