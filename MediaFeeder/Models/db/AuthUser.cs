using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace MediaFeeder.Models.db
{
    public class AuthUser : IdentityUser<int>
    {
        public AuthUser()
        {
            AuthUserGroups = new HashSet<AuthUserGroup>();
            AuthUserUserPermissions = new HashSet<AuthUserUserPermission>();
            DjangoAdminLogs = new HashSet<DjangoAdminLog>();
            DynamicPreferencesUsersUserpreferencemodels = new HashSet<DynamicPreferencesUsersUserpreferencemodel>();
            YtManagerAppJobexecutions = new HashSet<YtManagerAppJobexecution>();
            YtManagerAppSubscriptionfolders = new HashSet<YtManagerAppSubscriptionFolder>();
            YtManagerAppSubscriptions = new HashSet<YtManagerAppSubscription>();
            // AuthProviders = new HashSet<AuthProvider>();
        }

        public override int Id { get; set; }
        public string Password { get; set; }
        public DateTime? LastLogin { get; set; }
        public bool IsSuperuser { get; set; }
        public string Username { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public override string Email { get; set; }
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

        [Obsolete]
        public override string UserName
        {
            get => Username;
            set => Username = value;
        }

        public DateTime DateJoined { get; set; }

        public virtual ICollection<AuthUserGroup> AuthUserGroups { get; set; }
        public virtual ICollection<AuthUserUserPermission> AuthUserUserPermissions { get; set; }
        public virtual ICollection<DjangoAdminLog> DjangoAdminLogs { get; set; }
        public virtual ICollection<DynamicPreferencesUsersUserpreferencemodel> DynamicPreferencesUsersUserpreferencemodels { get; set; }
        public virtual ICollection<YtManagerAppJobexecution> YtManagerAppJobexecutions { get; set; }
        public virtual ICollection<YtManagerAppSubscriptionFolder> YtManagerAppSubscriptionfolders { get; set; }
        public virtual ICollection<YtManagerAppSubscription> YtManagerAppSubscriptions { get; set; }
        // public virtual ICollection<AuthProvider> AuthProviders { get; set; }
    }
}
