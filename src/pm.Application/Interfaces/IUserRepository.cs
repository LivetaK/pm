using pm.Domain.Entities;

namespace pm.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<User> CreateAsync(User user);
    Task UpdateAsync(User user);
    Task UpdateLastLoginAsync(Guid userId, DateTime loginAt);
    Task<UserSession?> GetSessionByTokenHashAsync(string tokenHash);
    Task CreateSessionAsync(UserSession session);
    Task RevokeSessionAsync(string tokenHash);
    Task RevokeAllUserSessionsAsync(Guid userId);
}
