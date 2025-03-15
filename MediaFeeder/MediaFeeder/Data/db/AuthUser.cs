using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace MediaFeeder.Data.db;

public class AuthUser : IdentityUser<int>
{
    public override int Id { get; set; }

    [MaxLength(128)] public string? Password { get; set; }

    public DateTime? LastLogin { get; set; }
    public bool IsSuperuser { get; set; }

    [MaxLength(150)] public required string Username { get; set; }

    [MaxLength(150)] public required string FirstName { get; set; }

    [MaxLength(150)] public required string LastName { get; set; }

    [MaxLength(254)] public override required string Email { get; set; }

    public bool IsStaff { get; set; }
    public bool IsActive { get; set; }

    public override bool LockoutEnabled
    {
        get => !IsActive;
        set => IsActive = !value;
    }

    public override string NormalizedEmail
    {
        get => Email;
        set => Email = value;
    }

    public override string NormalizedUserName
    {
        get => Username;
        set => Username = value;
    }

    public override string UserName
    {
        get => Username;
        set => Username = value;
    }

    public DateTime DateJoined { get; set; }

    public virtual ICollection<AuthUserGroup>? AuthUserGroups { get; init; }
    public virtual ICollection<AuthUserUserPermission>? AuthUserUserPermissions { get; init; }
    public virtual ICollection<DjangoAdminLog>? DjangoAdminLogs { get; init; }

    public virtual ICollection<DynamicPreferencesUsersUserpreferencemodel>? DynamicPreferencesUsersUserpreferencemodels
    {
        get;
        init;
    }

    public virtual ICollection<JobExecution>? JobExecutions { get; init; }
    public virtual ICollection<Folder>? Folders { get; init; }
    public virtual ICollection<Subscription>? Subscriptions { get; init; }
    public virtual ICollection<AuthProvider>? AuthProviders { get; init; }
}
