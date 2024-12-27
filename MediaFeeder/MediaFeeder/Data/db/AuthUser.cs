using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace MediaFeeder.Data.db;

public class AuthUser : IdentityUser<int>
{
    public override int Id { get; set; }

    [MaxLength(128)] public string Password { get; set; }

    public DateTime? LastLogin { get; set; }
    public bool IsSuperuser { get; set; }

    [MaxLength(150)] public string Username { get; set; }

    [MaxLength(150)] public string FirstName { get; set; }

    [MaxLength(150)] public string LastName { get; set; }

    [MaxLength(254)] public override string Email { get; set; }

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

    public virtual ICollection<AuthUserGroup> AuthUserGroups { get; init; } = default!;
    public virtual ICollection<AuthUserUserPermission> AuthUserUserPermissions { get; init; } = default!;
    public virtual ICollection<DjangoAdminLog> DjangoAdminLogs { get; init; } = default!;

    public virtual ICollection<DynamicPreferencesUsersUserpreferencemodel> DynamicPreferencesUsersUserpreferencemodels
    {
        get;
        init;
    } = default!;

    public virtual ICollection<YtManagerAppJobexecution> YtManagerAppJobexecutions { get; init; } = default!;

    public virtual ICollection<YtManagerAppSubscriptionFolder> YtManagerAppSubscriptionfolders { get; init; } =
        default!;

    public virtual ICollection<YtManagerAppSubscription> YtManagerAppSubscriptions { get; init; } = default!;
    public virtual ICollection<AuthProvider> AuthProviders { get; init; } = default!;
}