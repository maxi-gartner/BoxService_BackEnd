using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;

namespace BoxService_BackEnd.DTOs;

// Nunca incluye PasswordHash.
public sealed record UserDto(
    int UserId,
    int? TenantId,
    string? TenantName,
    string Username,
    string Name,
    string? Email,
    string Role,
    bool Active,
    string CreatedAt)
{
    public static UserDto FromModel(UserAccount u, string? tenantName = null) => new(
        UserId: u.UserId,
        TenantId: u.TenantId,
        TenantName: tenantName,
        Username: u.Username,
        Name: u.Name,
        Email: u.Email,
        Role: u.Role,
        Active: u.Active,
        CreatedAt: u.CreatedAt.ToString("O"));

    public static UserDto FromModel(UserWithTenantName row) => FromModel(row.User, row.TenantName);
}
