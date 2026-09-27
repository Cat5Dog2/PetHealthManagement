using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Services;

public interface IGuestAccountService
{
    Task<GuestAccount> CreateAsync(CancellationToken cancellationToken = default);
}

public sealed record GuestAccount(ApplicationUser User, DateTimeOffset ExpiresAt);
