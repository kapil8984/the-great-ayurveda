using Microsoft.AspNetCore.Identity;
using Ayurveda.Domain.Entities;

namespace Ayurveda.Infrastructure.Identity;

public class BCryptPasswordHasher : PasswordHasher<ApplicationUser>
{
    public override PasswordVerificationResult VerifyHashedPassword(
        ApplicationUser user, 
        string hashedPassword, 
        string providedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword) || string.IsNullOrEmpty(providedPassword))
        {
            return PasswordVerificationResult.Failed;
        }

        // Detect legacy BCrypt hash ($2a$, $2b$, $2y$)
        if (hashedPassword.StartsWith("$2a$") || hashedPassword.StartsWith("$2b$") || hashedPassword.StartsWith("$2y$"))
        {
            try
            {
                if (BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword))
                {
                    // Success! Instruct Identity to rehash and upgrade to standard Identity algorithm
                    return PasswordVerificationResult.SuccessRehashNeeded;
                }
                return PasswordVerificationResult.Failed;
            }
            catch
            {
                return PasswordVerificationResult.Failed;
            }
        }

        // Standard ASP.NET Core Identity verification
        return base.VerifyHashedPassword(user, hashedPassword, providedPassword);
    }
}
