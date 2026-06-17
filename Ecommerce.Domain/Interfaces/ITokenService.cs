using Ecommerce.Domain.Entities;

namespace Ecommerce.Domain.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user, IList<string> roles);
    string GenerateRefreshToken();
    string HashRefreshToken(string token);
}
