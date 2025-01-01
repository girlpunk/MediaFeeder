using MediaFeeder.Data.db;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Data;

public class MediaFeederDataContext(DbContextOptions<MediaFeederDataContext> options)
    : DbContext(options)
{
    public virtual DbSet<AuthGroup> AuthGroups { get; init; }
    public virtual DbSet<AuthGroupPermission> AuthGroupPermissions { get; init; }
    public virtual DbSet<AuthPermission> AuthPermissions { get; init; }
    public virtual DbSet<AuthProvider> AuthProviders { get; init; }
    public virtual DbSet<AuthUser> AuthUsers { get; init; }
    public virtual DbSet<AuthUserGroup> AuthUserGroups { get; init; }
    public virtual DbSet<AuthUserUserPermission> AuthUserUserPermissions { get; init; }
    public virtual DbSet<DjangoAdminLog> DjangoAdminLogs { get; init; }
    public virtual DbSet<DjangoCeleryResultsChordcounter> DjangoCeleryResultsChordcounters { get; init; }
    public virtual DbSet<DjangoCeleryResultsGroupresult> DjangoCeleryResultsGroupresults { get; init; }
    public virtual DbSet<DjangoCeleryResultsTaskresult> DjangoCeleryResultsTaskresults { get; init; }
    public virtual DbSet<DjangoContentType> DjangoContentTypes { get; init; }
    public virtual DbSet<DjangoMigration> DjangoMigrations { get; init; }
    public virtual DbSet<DjangoSession> DjangoSessions { get; init; }

    public virtual DbSet<DynamicPreferencesGlobalpreferencemodel> DynamicPreferencesGlobalpreferencemodels
    {
        get;
        init;
    }

    public virtual DbSet<DynamicPreferencesUsersUserpreferencemodel> DynamicPreferencesUsersUserpreferencemodels
    {
        get;
        init;
    }

    public virtual DbSet<EasyThumbnailsSource> EasyThumbnailsSources { get; init; }
    public virtual DbSet<EasyThumbnailsThumbnail> EasyThumbnailsThumbnails { get; init; }
    public virtual DbSet<EasyThumbnailsThumbnaildimension> EasyThumbnailsThumbnaildimensions { get; init; }
    public virtual DbSet<YtManagerAppJobexecution> YtManagerAppJobexecutions { get; init; }
    public virtual DbSet<YtManagerAppJobmessage> YtManagerAppJobmessages { get; init; }
    public virtual DbSet<YtManagerAppSubscription> YtManagerAppSubscriptions { get; init; }
    public virtual DbSet<YtManagerAppSubscriptionFolder> YtManagerAppSubscriptionFolders { get; init; }
    public virtual DbSet<YtManagerAppVideo> YtManagerAppVideos { get; init; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder = optionsBuilder.UseLazyLoadingProxies();

        if (!optionsBuilder.IsConfigured)
            optionsBuilder
                .UseLazyLoadingProxies()
                .UseNpgsql(
                    "Host=localhost;Database=ytsm;Username=ytsm;Password=verysecurepassword;port=5432;Timeout=60");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AuthGroup>(static entity =>
        {
            entity.ToTable("auth_group");

            entity.HasIndex(static e => e.Name, "auth_group_name_a6ea08ec_like")
                .HasOperators("varchar_pattern_ops");

            entity.HasIndex(static e => e.Name, "auth_group_name_key")
                .IsUnique();

            entity.Property(static e => e.Id).HasColumnName("id");

            entity.Property(static e => e.Name)
                .IsRequired()
                .HasMaxLength(150)
                .HasColumnName("name");
        });

        modelBuilder.Entity<AuthProvider>(static entity =>
        {
            entity.HasIndex(static e => new { e.LoginProvider, e.ProviderKey })
                .IsUnique();

            entity.Property(static e => e.LoginProvider)
                .IsRequired();

            entity.Property(static e => e.ProviderKey)
                .IsRequired();

            entity.Property(static e => e.UserId)
                .IsRequired();
        });

        modelBuilder.Entity<AuthGroupPermission>(static entity =>
        {
            entity.ToTable("auth_group_permissions");

            entity.HasIndex(static e => e.GroupId, "auth_group_permissions_group_id_b120cbf9");

            entity.HasIndex(static e => new { e.GroupId, e.PermissionId },
                    "auth_group_permissions_group_id_permission_id_0cd325b0_uniq")
                .IsUnique();

            entity.HasIndex(static e => e.PermissionId, "auth_group_permissions_permission_id_84c5c92e");

            entity.Property(static e => e.Id).HasColumnName("id");

            entity.Property(static e => e.GroupId).HasColumnName("group_id");

            entity.Property(static e => e.PermissionId).HasColumnName("permission_id");

            entity.HasOne(static d => d.Group)
                .WithMany(static p => p.AuthGroupPermissions)
                .HasForeignKey(static d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("auth_group_permissions_group_id_b120cbf9_fk_auth_group_id");

            entity.HasOne(static d => d.Permission)
                .WithMany(static p => p.AuthGroupPermissions)
                .HasForeignKey(static d => d.PermissionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("auth_group_permissio_permission_id_84c5c92e_fk_auth_perm");
        });

        modelBuilder.Entity<AuthPermission>(static entity =>
        {
            entity.ToTable("auth_permission");

            entity.HasIndex(static e => e.ContentTypeId, "auth_permission_content_type_id_2f476e4b");

            entity.HasIndex(static e => new { e.ContentTypeId, e.Codename },
                    "auth_permission_content_type_id_codename_01ab375a_uniq")
                .IsUnique();

            entity.Property(static e => e.Id).HasColumnName("id");

            entity.Property(static e => e.Codename)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("codename");

            entity.Property(static e => e.ContentTypeId).HasColumnName("content_type_id");

            entity.Property(static e => e.Name)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("name");

            entity.HasOne(static d => d.ContentType)
                .WithMany(static p => p.AuthPermissions)
                .HasForeignKey(static d => d.ContentTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("auth_permission_content_type_id_2f476e4b_fk_django_co");
        });

        modelBuilder.Entity<AuthUser>(static entity =>
        {
            entity.ToTable("auth_user");

            entity.HasIndex(static e => e.Username, "auth_user_username_6821ab7c_like")
                .HasOperators("varchar_pattern_ops");

            entity.HasIndex(static e => e.Username, "auth_user_username_key")
                .IsUnique();

            entity.Property(static e => e.Id).HasColumnName("id");

            entity.Property(static e => e.DateJoined).HasColumnName("date_joined");

            entity.Property(static e => e.Email)
                .IsRequired()
                .HasMaxLength(254)
                .HasColumnName("email");

            entity.Property(static e => e.FirstName)
                .IsRequired()
                .HasMaxLength(150)
                .HasColumnName("first_name");

            entity.Property(static e => e.IsActive).HasColumnName("is_active");

            entity.Property(static e => e.IsStaff).HasColumnName("is_staff");

            entity.Property(static e => e.IsSuperuser).HasColumnName("is_superuser");

            entity.Property(static e => e.LastLogin).HasColumnName("last_login");

            entity.Property(static e => e.LastName)
                .IsRequired()
                .HasMaxLength(150)
                .HasColumnName("last_name");

            entity.Property(static e => e.Password)
                .IsRequired()
                .HasMaxLength(128)
                .HasColumnName("password");

            entity.Property(static e => e.Username)
                .IsRequired()
                .HasMaxLength(150)
                .HasColumnName("username");

            entity.Ignore(static e => e.AccessFailedCount);
            entity.Ignore(static e => e.ConcurrencyStamp);
            entity.Ignore(static e => e.EmailConfirmed);
            entity.Ignore(static e => e.LockoutEnabled);
            entity.Ignore(static e => e.LockoutEnd);
            entity.Ignore(static e => e.NormalizedEmail);
            entity.Ignore(static e => e.NormalizedUserName);
            entity.Ignore(static e => e.PasswordHash);
            entity.Ignore(static e => e.PhoneNumber);
            entity.Ignore(static e => e.PhoneNumberConfirmed);
            entity.Ignore(static e => e.SecurityStamp);
            entity.Ignore(static e => e.TwoFactorEnabled);
            entity.Ignore(static e => e.UserName);
        });

        modelBuilder.Entity<AuthUserGroup>(static entity =>
        {
            entity.ToTable("auth_user_groups");

            entity.HasIndex(static e => e.GroupId, "auth_user_groups_group_id_97559544");

            entity.HasIndex(static e => e.UserId, "auth_user_groups_user_id_6a12ed8b");

            entity.HasIndex(static e => new { e.UserId, e.GroupId }, "auth_user_groups_user_id_group_id_94350c0c_uniq")
                .IsUnique();

            entity.Property(static e => e.Id).HasColumnName("id");

            entity.Property(static e => e.GroupId).HasColumnName("group_id");

            entity.Property(static e => e.UserId).HasColumnName("user_id");

            entity.HasOne(static d => d.Group)
                .WithMany(static p => p.AuthUserGroups)
                .HasForeignKey(static d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("auth_user_groups_group_id_97559544_fk_auth_group_id");

            entity.HasOne(static d => d.User)
                .WithMany(static p => p.AuthUserGroups)
                .HasForeignKey(static d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("auth_user_groups_user_id_6a12ed8b_fk_auth_user_id");
        });

        modelBuilder.Entity<AuthUserUserPermission>(static entity =>
        {
            entity.ToTable("auth_user_user_permissions");

            entity.HasIndex(static e => e.PermissionId, "auth_user_user_permissions_permission_id_1fbb5f2c");

            entity.HasIndex(static e => e.UserId, "auth_user_user_permissions_user_id_a95ead1b");

            entity.HasIndex(static e => new { e.UserId, e.PermissionId },
                    "auth_user_user_permissions_user_id_permission_id_14a6b632_uniq")
                .IsUnique();

            entity.Property(static e => e.Id).HasColumnName("id");

            entity.Property(static e => e.PermissionId).HasColumnName("permission_id");

            entity.Property(static e => e.UserId).HasColumnName("user_id");

            entity.HasOne(static d => d.Permission)
                .WithMany(static p => p.AuthUserUserPermissions)
                .HasForeignKey(static d => d.PermissionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("auth_user_user_permi_permission_id_1fbb5f2c_fk_auth_perm");

            entity.HasOne(static d => d.User)
                .WithMany(static p => p.AuthUserUserPermissions)
                .HasForeignKey(static d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("auth_user_user_permissions_user_id_a95ead1b_fk_auth_user_id");
        });

        modelBuilder.Entity<DjangoAdminLog>(static entity =>
        {
            entity.ToTable("django_admin_log");

            entity.HasIndex(static e => e.ContentTypeId, "django_admin_log_content_type_id_c4bce8eb");

            entity.HasIndex(static e => e.UserId, "django_admin_log_user_id_c564eba6");

            entity.Property(static e => e.Id).HasColumnName("id");

            entity.Property(static e => e.ActionFlag).HasColumnName("action_flag");

            entity.Property(static e => e.ActionTime).HasColumnName("action_time");

            entity.Property(static e => e.ChangeMessage)
                .IsRequired()
                .HasColumnName("change_message");

            entity.Property(static e => e.ContentTypeId).HasColumnName("content_type_id");

            entity.Property(static e => e.ObjectId).HasColumnName("object_id");

            entity.Property(static e => e.ObjectRepr)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("object_repr");

            entity.Property(static e => e.UserId).HasColumnName("user_id");

            entity.HasOne(static d => d.ContentType)
                .WithMany(static p => p.DjangoAdminLogs)
                .HasForeignKey(static d => d.ContentTypeId)
                .HasConstraintName("django_admin_log_content_type_id_c4bce8eb_fk_django_co");

            entity.HasOne(static d => d.User)
                .WithMany(static p => p.DjangoAdminLogs)
                .HasForeignKey(static d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("django_admin_log_user_id_c564eba6_fk_auth_user_id");
        });

        modelBuilder.Entity<DjangoCeleryResultsChordcounter>(static entity =>
        {
            entity.ToTable("django_celery_results_chordcounter");

            entity.HasIndex(static e => e.GroupId, "django_celery_results_chordcounter_group_id_1f70858c_like")
                .HasOperators("varchar_pattern_ops");

            entity.HasIndex(static e => e.GroupId, "django_celery_results_chordcounter_group_id_key")
                .IsUnique();

            entity.Property(static e => e.Id).HasColumnName("id");

            entity.Property(static e => e.Count).HasColumnName("count");

            entity.Property(static e => e.GroupId)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("group_id");

            entity.Property(static e => e.SubTasks)
                .IsRequired()
                .HasColumnName("sub_tasks");
        });

        modelBuilder.Entity<DjangoCeleryResultsGroupresult>(static entity =>
        {
            entity.ToTable("django_celery_results_groupresult");

            entity.HasIndex(static e => e.DateCreated, "django_cele_date_cr_bd6c1d_idx");

            entity.HasIndex(static e => e.DateDone, "django_cele_date_do_caae0e_idx");

            entity.HasIndex(static e => e.GroupId, "django_celery_results_groupresult_group_id_a085f1a9_like")
                .HasOperators("varchar_pattern_ops");

            entity.HasIndex(static e => e.GroupId, "django_celery_results_groupresult_group_id_key")
                .IsUnique();

            entity.Property(static e => e.Id).HasColumnName("id");

            entity.Property(static e => e.ContentEncoding)
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnName("content_encoding");

            entity.Property(static e => e.ContentType)
                .IsRequired()
                .HasMaxLength(128)
                .HasColumnName("content_type");

            entity.Property(static e => e.DateCreated).HasColumnName("date_created");

            entity.Property(static e => e.DateDone).HasColumnName("date_done");

            entity.Property(static e => e.GroupId)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("group_id");

            entity.Property(static e => e.Result).HasColumnName("result");
        });

        modelBuilder.Entity<DjangoCeleryResultsTaskresult>(static entity =>
        {
            entity.ToTable("django_celery_results_taskresult");

            entity.HasIndex(static e => e.DateCreated, "django_cele_date_cr_f04a50_idx");

            entity.HasIndex(static e => e.DateDone, "django_cele_date_do_f59aad_idx");

            entity.HasIndex(static e => e.Status, "django_cele_status_9b6201_idx");

            entity.HasIndex(static e => e.TaskName, "django_cele_task_na_08aec9_idx");

            entity.HasIndex(static e => e.Worker, "django_cele_worker_d54dd8_idx");

            entity.HasIndex(static e => e.TaskId, "django_celery_results_taskresult_task_id_de0d95bf_like")
                .HasOperators("varchar_pattern_ops");

            entity.HasIndex(static e => e.TaskId, "django_celery_results_taskresult_task_id_key")
                .IsUnique();

            entity.Property(static e => e.Id).HasColumnName("id");

            entity.Property(static e => e.ContentEncoding)
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnName("content_encoding");

            entity.Property(static e => e.ContentType)
                .IsRequired()
                .HasMaxLength(128)
                .HasColumnName("content_type");

            entity.Property(static e => e.DateCreated).HasColumnName("date_created");

            entity.Property(static e => e.DateDone).HasColumnName("date_done");

            entity.Property(static e => e.Meta).HasColumnName("meta");

            entity.Property(static e => e.Result).HasColumnName("result");

            entity.Property(static e => e.Status)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("status");

            entity.Property(static e => e.TaskArgs).HasColumnName("task_args");

            entity.Property(static e => e.TaskId)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("task_id");

            entity.Property(static e => e.TaskKwargs).HasColumnName("task_kwargs");

            entity.Property(static e => e.TaskName)
                .HasMaxLength(255)
                .HasColumnName("task_name");

            entity.Property(static e => e.Traceback).HasColumnName("traceback");

            entity.Property(static e => e.Worker)
                .HasMaxLength(100)
                .HasColumnName("worker");
        });

        modelBuilder.Entity<DjangoContentType>(static entity =>
        {
            entity.ToTable("django_content_type");

            entity.HasIndex(static e => new { e.AppLabel, e.Model }, "django_content_type_app_label_model_76bd3d3b_uniq")
                .IsUnique();

            entity.Property(static e => e.Id).HasColumnName("id");

            entity.Property(static e => e.AppLabel)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("app_label");

            entity.Property(static e => e.Model)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("model");
        });

        modelBuilder.Entity<DjangoMigration>(static entity =>
        {
            entity.ToTable("django_migrations");

            entity.Property(static e => e.Id).HasColumnName("id");

            entity.Property(static e => e.App)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("app");

            entity.Property(static e => e.Applied).HasColumnName("applied");

            entity.Property(static e => e.Name)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("name");
        });

        modelBuilder.Entity<DjangoSession>(static entity =>
        {
            entity.HasKey(static e => e.SessionKey)
                .HasName("django_session_pkey");

            entity.ToTable("django_session");

            entity.HasIndex(static e => e.ExpireDate, "django_session_expire_date_a5c62663");

            entity.HasIndex(static e => e.SessionKey, "django_session_session_key_c0390e0f_like")
                .HasOperators("varchar_pattern_ops");

            entity.Property(static e => e.SessionKey)
                .HasMaxLength(40)
                .HasColumnName("session_key");

            entity.Property(static e => e.ExpireDate).HasColumnName("expire_date");

            entity.Property(static e => e.SessionData)
                .IsRequired()
                .HasColumnName("session_data");
        });

        modelBuilder.Entity<DynamicPreferencesGlobalpreferencemodel>(static entity =>
        {
            entity.ToTable("dynamic_preferences_globalpreferencemodel");

            entity.HasIndex(static e => new { e.Section, e.Name }, "dynamic_preferences_glob_section_name_f4a2439b_uniq")
                .IsUnique();

            entity.HasIndex(static e => e.Name, "dynamic_preferences_globalpreferencemodel_name_033debe0");

            entity.HasIndex(static e => e.Name, "dynamic_preferences_globalpreferencemodel_name_033debe0_like")
                .HasOperators("varchar_pattern_ops");

            entity.HasIndex(static e => e.Section, "dynamic_preferences_globalpreferencemodel_section_c1ee9cc3");

            entity.HasIndex(static e => e.Section, "dynamic_preferences_globalpreferencemodel_section_c1ee9cc3_like")
                .HasOperators("varchar_pattern_ops");

            entity.Property(static e => e.Id).HasColumnName("id");

            entity.Property(static e => e.Name)
                .IsRequired()
                .HasMaxLength(150)
                .HasColumnName("name");

            entity.Property(static e => e.RawValue).HasColumnName("raw_value");

            entity.Property(static e => e.Section)
                .HasMaxLength(150)
                .HasColumnName("section");
        });

        modelBuilder.Entity<DynamicPreferencesUsersUserpreferencemodel>(static entity =>
        {
            entity.ToTable("dynamic_preferences_users_userpreferencemodel");

            entity.HasIndex(static e => new { e.InstanceId, e.Section, e.Name },
                    "dynamic_preferences_user_instance_id_section_name_29814e3f_uniq")
                .IsUnique();

            entity.HasIndex(static e => e.Name, "dynamic_preferences_user_name_11ac488d_like")
                .HasOperators("varchar_pattern_ops");

            entity.HasIndex(static e => e.Section, "dynamic_preferences_user_section_ba869570_like")
                .HasOperators("varchar_pattern_ops");

            entity.HasIndex(static e => e.InstanceId, "dynamic_preferences_users__instance_id_bf1d7718");

            entity.HasIndex(static e => e.Name, "dynamic_preferences_users_userpreferencemodel_name_11ac488d");

            entity.HasIndex(static e => e.Section, "dynamic_preferences_users_userpreferencemodel_section_ba869570");

            entity.Property(static e => e.Id).HasColumnName("id");

            entity.Property(static e => e.InstanceId).HasColumnName("instance_id");

            entity.Property(static e => e.Name)
                .IsRequired()
                .HasMaxLength(150)
                .HasColumnName("name");

            entity.Property(static e => e.RawValue).HasColumnName("raw_value");

            entity.Property(static e => e.Section)
                .HasMaxLength(150)
                .HasColumnName("section");

            entity.HasOne(static d => d.Instance)
                .WithMany(static p => p.DynamicPreferencesUsersUserpreferencemodels)
                .HasForeignKey(static d => d.InstanceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("dynamic_preferences__instance_id_bf1d7718_fk_auth_user");
        });

        modelBuilder.Entity<EasyThumbnailsSource>(static entity =>
        {
            entity.ToTable("easy_thumbnails_source");

            entity.HasIndex(static e => e.Name, "easy_thumbnails_source_name_5fe0edc6");

            entity.HasIndex(static e => e.Name, "easy_thumbnails_source_name_5fe0edc6_like")
                .HasOperators("varchar_pattern_ops");

            entity.HasIndex(static e => e.StorageHash, "easy_thumbnails_source_storage_hash_946cbcc9");

            entity.HasIndex(static e => e.StorageHash, "easy_thumbnails_source_storage_hash_946cbcc9_like")
                .HasOperators("varchar_pattern_ops");

            entity.HasIndex(static e => new { e.StorageHash, e.Name },
                    "easy_thumbnails_source_storage_hash_name_481ce32d_uniq")
                .IsUnique();

            entity.Property(static e => e.Id).HasColumnName("id");

            entity.Property(static e => e.Modified).HasColumnName("modified");

            entity.Property(static e => e.Name)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("name");

            entity.Property(static e => e.StorageHash)
                .IsRequired()
                .HasMaxLength(40)
                .HasColumnName("storage_hash");
        });

        modelBuilder.Entity<EasyThumbnailsThumbnail>(static entity =>
        {
            entity.ToTable("easy_thumbnails_thumbnail");

            entity.HasIndex(static e => new { e.StorageHash, e.Name, e.SourceId },
                    "easy_thumbnails_thumbnai_storage_hash_name_source_fb375270_uniq")
                .IsUnique();

            entity.HasIndex(static e => e.Name, "easy_thumbnails_thumbnail_name_b5882c31");

            entity.HasIndex(static e => e.Name, "easy_thumbnails_thumbnail_name_b5882c31_like")
                .HasOperators("varchar_pattern_ops");

            entity.HasIndex(static e => e.SourceId, "easy_thumbnails_thumbnail_source_id_5b57bc77");

            entity.HasIndex(static e => e.StorageHash, "easy_thumbnails_thumbnail_storage_hash_f1435f49");

            entity.HasIndex(static e => e.StorageHash, "easy_thumbnails_thumbnail_storage_hash_f1435f49_like")
                .HasOperators("varchar_pattern_ops");

            entity.Property(static e => e.Id).HasColumnName("id");

            entity.Property(static e => e.Modified).HasColumnName("modified");

            entity.Property(static e => e.Name)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("name");

            entity.Property(static e => e.SourceId).HasColumnName("source_id");

            entity.Property(static e => e.StorageHash)
                .IsRequired()
                .HasMaxLength(40)
                .HasColumnName("storage_hash");

            entity.HasOne(static d => d.Source)
                .WithMany(static p => p.EasyThumbnailsThumbnails)
                .HasForeignKey(static d => d.SourceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("easy_thumbnails_thum_source_id_5b57bc77_fk_easy_thum");
        });

        modelBuilder.Entity<EasyThumbnailsThumbnaildimension>(static entity =>
        {
            entity.ToTable("easy_thumbnails_thumbnaildimensions");

            entity.HasIndex(static e => e.ThumbnailId, "easy_thumbnails_thumbnaildimensions_thumbnail_id_key")
                .IsUnique();

            entity.Property(static e => e.Id).HasColumnName("id");

            entity.Property(static e => e.Height).HasColumnName("height");

            entity.Property(static e => e.ThumbnailId).HasColumnName("thumbnail_id");

            entity.Property(static e => e.Width).HasColumnName("width");

            entity.HasOne(static d => d.Thumbnail)
                .WithOne(static p => p.EasyThumbnailsThumbnaildimension)
                .HasForeignKey<EasyThumbnailsThumbnaildimension>(static d => d.ThumbnailId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("easy_thumbnails_thum_thumbnail_id_c3a0c549_fk_easy_thum");
        });

        modelBuilder.Entity<YtManagerAppJobexecution>(entity =>
        {
            entity.ToTable("YtManagerApp_jobexecution");

            entity.HasIndex(static e => e.UserId, "YtManagerApp_jobexecution_user_id_60530e6f");

            entity.Property(static e => e.Id).HasColumnName("id");

            entity.Property(static e => e.Description)
                .IsRequired()
                .HasMaxLength(250)
                .HasColumnName("description");

            entity.Property(static e => e.EndDate).HasColumnName("end_date");

            entity.Property(static e => e.StartDate).HasColumnName("start_date");

            entity.Property(static e => e.Status).HasColumnName("status");

            entity.Property(static e => e.UserId).HasColumnName("user_id");

            entity.HasOne(static d => d.User)
                .WithMany(static p => p.YtManagerAppJobexecutions)
                .HasForeignKey(static d => d.UserId)
                .HasConstraintName("YtManagerApp_jobexecution_user_id_60530e6f_fk_auth_user_id");
        });

        modelBuilder.Entity<YtManagerAppJobmessage>(entity =>
        {
            entity.ToTable("YtManagerApp_jobmessage");

            entity.HasIndex(static e => e.JobId, "YtManagerApp_jobmessage_job_id_ec6435ce");

            entity.Property(static e => e.Id).HasColumnName("id");

            entity.Property(static e => e.JobId).HasColumnName("job_id");

            entity.Property(static e => e.Level).HasColumnName("level");

            entity.Property(static e => e.Message)
                .IsRequired()
                .HasMaxLength(1024)
                .HasColumnName("message");

            entity.Property(static e => e.Progress).HasColumnName("progress");

            entity.Property(static e => e.SuppressNotification).HasColumnName("suppress_notification");

            entity.Property(static e => e.Timestamp).HasColumnName("timestamp");

            entity.HasOne(static d => d.Job)
                .WithMany(static p => p.YtManagerAppJobmessages)
                .HasForeignKey(static d => d.JobId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("YtManagerApp_jobmess_job_id_ec6435ce_fk_YtManager");
        });

        modelBuilder.Entity<YtManagerAppSubscription>(entity =>
        {
            entity.ToTable("YtManagerApp_subscription");

            entity.HasIndex(static e => e.ParentFolderId, "YtManagerApp_subscription_parent_folder_id_c4c64c21");

            entity.HasIndex(static e => e.UserId, "YtManagerApp_subscription_user_id_9d38617d");

            entity.Property(static e => e.Id).HasColumnName("id");

            entity.Property(static e => e.AutoDownload).HasColumnName("auto_download");

            entity.Property(static e => e.AutomaticallyDeleteWatched).HasColumnName("automatically_delete_watched");

            entity.Property(static e => e.ChannelId)
                .IsRequired()
                .HasMaxLength(128)
                .HasColumnName("channel_id");

            entity.Property(static e => e.ChannelName)
                .IsRequired()
                .HasMaxLength(1024)
                .HasColumnName("channel_name");

            entity.Property(static e => e.Description)
                .IsRequired()
                .HasColumnName("description");

            entity.Property(static e => e.DownloadLimit).HasColumnName("download_limit");

            entity.Property(static e => e.DownloadOrder)
                .HasMaxLength(128)
                .HasColumnName("download_order");

            entity.Property(static e => e.LastSynchronised).HasColumnName("last_synchronised");

            entity.Property(static e => e.Name)
                .IsRequired()
                .HasMaxLength(1024)
                .HasColumnName("name");

            entity.Property(static e => e.ParentFolderId).HasColumnName("parent_folder_id");

            entity.Property(static e => e.PlaylistId)
                .IsRequired()
                .HasMaxLength(128)
                .HasColumnName("playlist_id");

            entity.Property(static e => e.Provider)
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnName("provider");

            entity.Property(static e => e.RewritePlaylistIndices).HasColumnName("rewrite_playlist_indices");

            entity.Property(static e => e.Thumb)
                .HasMaxLength(100)
                .HasColumnName("thumb");

            entity.Property(static e => e.Thumbnail)
                .IsRequired()
                .HasMaxLength(1024)
                .HasColumnName("thumbnail");

            entity.Property(static e => e.UserId).HasColumnName("user_id");

            entity.HasOne(static d => d.ParentFolder)
                .WithMany(static p => p.YtManagerAppSubscriptions)
                .HasForeignKey(static d => d.ParentFolderId)
                .HasConstraintName("YtManagerApp_subscri_parent_folder_id_c4c64c21_fk_YtManager");

            entity.HasOne(static d => d.User)
                .WithMany(static p => p.YtManagerAppSubscriptions)
                .HasForeignKey(static d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("YtManagerApp_subscription_user_id_9d38617d_fk_auth_user_id");
        });

        modelBuilder.Entity<YtManagerAppSubscriptionFolder>(entity =>
        {
            entity.ToTable("YtManagerApp_subscriptionfolder");

            entity.HasIndex(static e => e.ParentId, "YtManagerApp_subscriptionfolder_parent_id_bd5f4bc1");

            entity.HasIndex(static e => e.UserId, "YtManagerApp_subscriptionfolder_user_id_6fb12da0");

            entity.Property(static e => e.Id).HasColumnName("id");

            entity.Property(static e => e.Name)
                .IsRequired()
                .HasMaxLength(250)
                .HasColumnName("name");

            entity.Property(static e => e.ParentId).HasColumnName("parent_id");

            entity.Property(static e => e.UserId).HasColumnName("user_id");

            entity.HasOne(static d => d.Parent)
                .WithMany(static p => p.InverseParent)
                .HasForeignKey(static d => d.ParentId)
                .HasConstraintName("YtManagerApp_subscri_parent_id_bd5f4bc1_fk_YtManager");

            entity.HasOne(static d => d.User)
                .WithMany(static p => p.YtManagerAppSubscriptionfolders)
                .HasForeignKey(static d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("YtManagerApp_subscri_user_id_6fb12da0_fk_auth_user");
        });

        modelBuilder.Entity<YtManagerAppVideo>(entity =>
        {
            entity.ToTable("YtManagerApp_video");

            entity.HasIndex(static e => e.SubscriptionId, "YtManagerApp_video_subscription_id_720d4227");

            entity.Property(static e => e.Id).HasColumnName("id");

            entity.Property(static e => e.Description)
                .IsRequired()
                .HasColumnName("description");

            entity.Property(static e => e.DownloadedPath).HasColumnName("downloaded_path");

            entity.Property(static e => e.Duration).HasColumnName("duration");

            entity.Property(static e => e.Name)
                .IsRequired()
                .HasColumnName("name");

            entity.Property(static e => e.New).HasColumnName("new");

            entity.Property(static e => e.PlaylistIndex).HasColumnName("playlist_index");

            entity.Property(static e => e.PublishDate).HasColumnName("publish_date");

            entity.Property(static e => e.Rating).HasColumnName("rating");

            entity.Property(static e => e.SubscriptionId).HasColumnName("subscription_id");

            entity.Property(static e => e.Thumb)
                .HasMaxLength(100)
                .HasColumnName("thumb");

            entity.Property(static e => e.Thumbnail)
                .IsRequired()
                .HasColumnName("thumbnail");

            entity.Property(static e => e.UploaderName)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("uploader_name");

            entity.Property(static e => e.VideoId)
                .IsRequired()
                .HasMaxLength(12)
                .HasColumnName("video_id");

            entity.Property(static e => e.Views).HasColumnName("views");

            entity.Property(static e => e.Watched).HasColumnName("watched");

            entity.HasOne(static d => d.Subscription)
                .WithMany(static p => p.YtManagerAppVideos)
                .HasForeignKey(static d => d.SubscriptionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("YtManagerApp_video_subscription_id_720d4227_fk_YtManager");
        });
    }
}