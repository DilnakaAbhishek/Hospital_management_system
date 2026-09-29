using HospitalManagementSystem.Api.Models;

namespace HospitalManagementSystem.Api.Services;

public record JwtTokenResult(string Token, DateTime ExpiresAt);

public interface IJwtTokenService
{
    JwtTokenResult Create(User user, int? doctorId);
}
