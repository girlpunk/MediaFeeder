using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MediaFeeder.Data.Migrations
{
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "wfc");

            migrationBuilder.CreateTable(
                name: "auth_group",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table => { table.PrimaryKey("auth_group_pkey", x => x.id); });

            migrationBuilder.CreateTable(
                name: "auth_user",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    password = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    last_login = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_superuser = table.Column<bool>(type: "boolean", nullable: false),
                    username = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    first_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    last_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    is_staff = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    date_joined = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),

                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NormalizedEmail = table.Column<string>(type: "text", nullable: true),
                    NormalizedUserName = table.Column<string>(type: "text", nullable: true),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    UserName = table.Column<string>(type: "text", nullable: true),
                },
                constraints: table => { table.PrimaryKey("auth_user_pkey", x => x.id); });

            migrationBuilder.CreateTable(
                name: "django_celery_results_chordcounter",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    group_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    sub_tasks = table.Column<string>(type: "text", nullable: false),
                    count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table => { table.PrimaryKey("django_celery_results_chordcounter_pkey", x => x.id); });

            migrationBuilder.CreateTable(
                name: "django_celery_results_groupresult",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    group_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    date_created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_done = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    content_type =
                        table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    content_encoding =
                        table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    result = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table => { table.PrimaryKey("django_celery_results_groupresult_pkey", x => x.id); });

            migrationBuilder.CreateTable(
                name: "django_celery_results_taskresult",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    task_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    content_type =
                        table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    content_encoding =
                        table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    result = table.Column<string>(type: "text", nullable: true),
                    date_done = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    traceback = table.Column<string>(type: "text", nullable: true),
                    meta = table.Column<string>(type: "text", nullable: true),
                    task_args = table.Column<string>(type: "text", nullable: true),
                    task_kwargs = table.Column<string>(type: "text", nullable: true),
                    task_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    worker = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    date_created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => { table.PrimaryKey("django_celery_results_taskresult_pkey", x => x.id); });

            migrationBuilder.CreateTable(
                name: "django_content_type",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    app_label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table => { table.PrimaryKey("django_content_type_pkey", x => x.id); });

            migrationBuilder.CreateTable(
                name: "django_migrations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    app = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    applied = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => { table.PrimaryKey("django_migrations_pkey", x => x.id); });

            migrationBuilder.CreateTable(
                name: "django_session",
                columns: table => new
                {
                    session_key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    session_data = table.Column<string>(type: "text", nullable: false),
                    expire_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => { table.PrimaryKey("django_session_pkey", x => x.session_key); });

            migrationBuilder.CreateTable(
                name: "dynamic_preferences_globalpreferencemodel",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    section = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    raw_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("dynamic_preferences_globalpreferencemodel_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "easy_thumbnails_source",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    storage_hash = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => { table.PrimaryKey("easy_thumbnails_source_pkey", x => x.id); });

            migrationBuilder.CreateTable(
                name: "Event",
                schema: "wfc",
                columns: table => new
                {
                    PersistenceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventData = table.Column<string>(type: "text", nullable: true),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EventName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EventTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_Event", x => x.PersistenceId); });

            migrationBuilder.CreateTable(
                name: "ExecutionError",
                schema: "wfc",
                columns: table => new
                {
                    PersistenceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ErrorTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExecutionPointerId =
                        table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Message = table.Column<string>(type: "text", nullable: true),
                    WorkflowId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table => { table.PrimaryKey("PK_ExecutionError", x => x.PersistenceId); });

            migrationBuilder.CreateTable(
                name: "ScheduledCommand",
                schema: "wfc",
                columns: table => new
                {
                    PersistenceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommandName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Data = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExecuteTime = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_ScheduledCommand", x => x.PersistenceId); });

            migrationBuilder.CreateTable(
                name: "Subscription",
                schema: "wfc",
                columns: table => new
                {
                    PersistenceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EventName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StepId = table.Column<int>(type: "integer", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SubscribeAsOf = table.Column<DateTime>(type: "timestamp with time zone", nullable: false,
                        defaultValueSql: "'-infinity'::timestamp with time zone"),
                    SubscriptionData = table.Column<string>(type: "text", nullable: true),
                    ExecutionPointerId =
                        table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExternalToken =
                        table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExternalTokenExpiry = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExternalWorkerId =
                        table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table => { table.PrimaryKey("PK_Subscription", x => x.PersistenceId); });

            migrationBuilder.CreateTable(
                name: "Workflow",
                schema: "wfc",
                columns: table => new
                {
                    PersistenceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompleteTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Data = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    NextExecution = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    WorkflowDefinitionId =
                        table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table => { table.PrimaryKey("PK_Workflow", x => x.PersistenceId); });

            migrationBuilder.CreateTable(
                name: "auth_user_groups",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    group_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("auth_user_groups_pkey", x => x.id);
                    table.ForeignKey(
                        name: "auth_user_groups_group_id_97559544_fk_auth_group_id",
                        column: x => x.group_id,
                        principalTable: "auth_group",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "auth_user_groups_user_id_6a12ed8b_fk_auth_user_id",
                        column: x => x.user_id,
                        principalTable: "auth_user",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "dynamic_preferences_users_userpreferencemodel",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    section = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    raw_value = table.Column<string>(type: "text", nullable: true),
                    instance_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("dynamic_preferences_users_userpreferencemodel_pkey", x => x.id);
                    table.ForeignKey(
                        name: "dynamic_preferences__instance_id_bf1d7718_fk_auth_user",
                        column: x => x.instance_id,
                        principalTable: "auth_user",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "YtManagerApp_jobexecution",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("YtManagerApp_jobexecution_pkey", x => x.id);
                    table.ForeignKey(
                        name: "YtManagerApp_jobexecution_user_id_60530e6f_fk_auth_user_id",
                        column: x => x.user_id,
                        principalTable: "auth_user",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "YtManagerApp_subscriptionfolder",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    parent_id = table.Column<int>(type: "integer", nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("YtManagerApp_subscriptionfolder_pkey", x => x.id);
                    table.ForeignKey(
                        name: "YtManagerApp_subscri_parent_id_bd5f4bc1_fk_YtManager",
                        column: x => x.parent_id,
                        principalTable: "YtManagerApp_subscriptionfolder",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "YtManagerApp_subscri_user_id_6fb12da0_fk_auth_user",
                        column: x => x.user_id,
                        principalTable: "auth_user",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "auth_permission",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type_id = table.Column<int>(type: "integer", nullable: false),
                    codename = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("auth_permission_pkey", x => x.id);
                    table.ForeignKey(
                        name: "auth_permission_content_type_id_2f476e4b_fk_django_co",
                        column: x => x.content_type_id,
                        principalTable: "django_content_type",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "django_admin_log",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    action_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    object_id = table.Column<string>(type: "text", nullable: true),
                    object_repr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    action_flag = table.Column<short>(type: "smallint", nullable: false),
                    change_message = table.Column<string>(type: "text", nullable: false),
                    content_type_id = table.Column<int>(type: "integer", nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("django_admin_log_pkey", x => x.id);
                    table.ForeignKey(
                        name: "django_admin_log_content_type_id_c4bce8eb_fk_django_co",
                        column: x => x.content_type_id,
                        principalTable: "django_content_type",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "django_admin_log_user_id_c564eba6_fk_auth_user_id",
                        column: x => x.user_id,
                        principalTable: "auth_user",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "easy_thumbnails_thumbnail",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    storage_hash = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("easy_thumbnails_thumbnail_pkey", x => x.id);
                    table.ForeignKey(
                        name: "easy_thumbnails_thum_source_id_5b57bc77_fk_easy_thum",
                        column: x => x.source_id,
                        principalTable: "easy_thumbnails_source",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "ExecutionPointer",
                schema: "wfc",
                columns: table => new
                {
                    PersistenceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EventData = table.Column<string>(type: "text", nullable: true),
                    EventKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EventName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EventPublished = table.Column<bool>(type: "boolean", nullable: false),
                    Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PersistenceData = table.Column<string>(type: "text", nullable: true),
                    SleepUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StepId = table.Column<int>(type: "integer", nullable: false),
                    StepName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WorkflowId = table.Column<long>(type: "bigint", nullable: false),
                    Children = table.Column<string>(type: "text", nullable: true),
                    ContextItem = table.Column<string>(type: "text", nullable: true),
                    PredecessorId =
                        table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Outcome = table.Column<string>(type: "text", nullable: true),
                    Scope = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionPointer", x => x.PersistenceId);
                    table.ForeignKey(
                        name: "FK_ExecutionPointer_Workflow_WorkflowId",
                        column: x => x.WorkflowId,
                        principalSchema: "wfc",
                        principalTable: "Workflow",
                        principalColumn: "PersistenceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YtManagerApp_jobmessage",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    progress = table.Column<double>(type: "double precision", nullable: true),
                    message = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    suppress_notification = table.Column<bool>(type: "boolean", nullable: false),
                    job_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("YtManagerApp_jobmessage_pkey", x => x.id);
                    table.ForeignKey(
                        name: "YtManagerApp_jobmess_job_id_ec6435ce_fk_YtManager",
                        column: x => x.job_id,
                        principalTable: "YtManagerApp_jobexecution",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "YtManagerApp_subscription",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    playlist_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    thumbnail = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    auto_download = table.Column<bool>(type: "boolean", nullable: true),
                    download_limit = table.Column<int>(type: "integer", nullable: true),
                    download_order =
                        table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    automatically_delete_watched = table.Column<bool>(type: "boolean", nullable: true),
                    parent_folder_id = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    channel_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    channel_name =
                        table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    rewrite_playlist_indices = table.Column<bool>(type: "boolean", nullable: false),
                    last_synchronised = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    thumb = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("YtManagerApp_subscription_pkey", x => x.id);
                    table.ForeignKey(
                        name: "YtManagerApp_subscri_parent_folder_id_c4c64c21_fk_YtManager",
                        column: x => x.parent_folder_id,
                        principalTable: "YtManagerApp_subscriptionfolder",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "YtManagerApp_subscription_user_id_9d38617d_fk_auth_user_id",
                        column: x => x.user_id,
                        principalTable: "auth_user",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "auth_group_permissions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    permission_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("auth_group_permissions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "auth_group_permissio_permission_id_84c5c92e_fk_auth_perm",
                        column: x => x.permission_id,
                        principalTable: "auth_permission",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "auth_group_permissions_group_id_b120cbf9_fk_auth_group_id",
                        column: x => x.group_id,
                        principalTable: "auth_group",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "auth_user_user_permissions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    permission_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("auth_user_user_permissions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "auth_user_user_permi_permission_id_1fbb5f2c_fk_auth_perm",
                        column: x => x.permission_id,
                        principalTable: "auth_permission",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "auth_user_user_permissions_user_id_a95ead1b_fk_auth_user_id",
                        column: x => x.user_id,
                        principalTable: "auth_user",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "easy_thumbnails_thumbnaildimensions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    thumbnail_id = table.Column<int>(type: "integer", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: true),
                    height = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("easy_thumbnails_thumbnaildimensions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "easy_thumbnails_thum_thumbnail_id_c3a0c549_fk_easy_thum",
                        column: x => x.thumbnail_id,
                        principalTable: "easy_thumbnails_thumbnail",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "ExtensionAttribute",
                schema: "wfc",
                columns: table => new
                {
                    PersistenceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AttributeKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AttributeValue = table.Column<string>(type: "text", nullable: true),
                    ExecutionPointerId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtensionAttribute", x => x.PersistenceId);
                    table.ForeignKey(
                        name: "FK_ExtensionAttribute_ExecutionPointer_ExecutionPointerId",
                        column: x => x.ExecutionPointerId,
                        principalSchema: "wfc",
                        principalTable: "ExecutionPointer",
                        principalColumn: "PersistenceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YtManagerApp_video",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    video_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    watched = table.Column<bool>(type: "boolean", nullable: false),
                    downloaded_path = table.Column<string>(type: "text", nullable: true),
                    playlist_index = table.Column<int>(type: "integer", nullable: true),
                    publish_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    thumbnail = table.Column<string>(type: "text", nullable: true),
                    subscription_id = table.Column<int>(type: "integer", nullable: false),
                    rating = table.Column<double>(type: "double precision", nullable: true),
                    uploader_name =
                        table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    views = table.Column<int>(type: "integer", nullable: true),
                    @new = table.Column<bool>(name: "new", type: "boolean", nullable: false),
                    duration = table.Column<int>(type: "integer", nullable: true),
                    thumb = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("YtManagerApp_video_pkey", x => x.id);
                    table.ForeignKey(
                        name: "YtManagerApp_video_subscription_id_720d4227_fk_YtManager",
                        column: x => x.subscription_id,
                        principalTable: "YtManagerApp_subscription",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                    name: "auth_group_name_a6ea08ec_like",
                    table: "auth_group",
                    column: "name")
                .Annotation("Npgsql:IndexOperators", new[] { "varchar_pattern_ops" });

            migrationBuilder.CreateIndex(
                name: "auth_group_name_key",
                table: "auth_group",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "auth_group_permissions_group_id_b120cbf9",
                table: "auth_group_permissions",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "auth_group_permissions_group_id_permission_id_0cd325b0_uniq",
                table: "auth_group_permissions",
                columns: new[] { "group_id", "permission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "auth_group_permissions_permission_id_84c5c92e",
                table: "auth_group_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "auth_permission_content_type_id_2f476e4b",
                table: "auth_permission",
                column: "content_type_id");

            migrationBuilder.CreateIndex(
                name: "auth_permission_content_type_id_codename_01ab375a_uniq",
                table: "auth_permission",
                columns: new[] { "content_type_id", "codename" },
                unique: true);

            migrationBuilder.CreateIndex(
                    name: "auth_user_username_6821ab7c_like",
                    table: "auth_user",
                    column: "username")
                .Annotation("Npgsql:IndexOperators", new[] { "varchar_pattern_ops" });

            migrationBuilder.CreateIndex(
                name: "auth_user_username_key",
                table: "auth_user",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "auth_user_groups_group_id_97559544",
                table: "auth_user_groups",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "auth_user_groups_user_id_6a12ed8b",
                table: "auth_user_groups",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "auth_user_groups_user_id_group_id_94350c0c_uniq",
                table: "auth_user_groups",
                columns: new[] { "user_id", "group_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "auth_user_user_permissions_permission_id_1fbb5f2c",
                table: "auth_user_user_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "auth_user_user_permissions_user_id_a95ead1b",
                table: "auth_user_user_permissions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "auth_user_user_permissions_user_id_permission_id_14a6b632_uniq",
                table: "auth_user_user_permissions",
                columns: new[] { "user_id", "permission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "django_admin_log_content_type_id_c4bce8eb",
                table: "django_admin_log",
                column: "content_type_id");

            migrationBuilder.CreateIndex(
                name: "django_admin_log_user_id_c564eba6",
                table: "django_admin_log",
                column: "user_id");

            migrationBuilder.CreateIndex(
                    name: "django_celery_results_chordcounter_group_id_1f70858c_like",
                    table: "django_celery_results_chordcounter",
                    column: "group_id")
                .Annotation("Npgsql:IndexOperators", new[] { "varchar_pattern_ops" });

            migrationBuilder.CreateIndex(
                name: "django_celery_results_chordcounter_group_id_key",
                table: "django_celery_results_chordcounter",
                column: "group_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "django_cele_date_cr_bd6c1d_idx",
                table: "django_celery_results_groupresult",
                column: "date_created");

            migrationBuilder.CreateIndex(
                name: "django_cele_date_do_caae0e_idx",
                table: "django_celery_results_groupresult",
                column: "date_done");

            migrationBuilder.CreateIndex(
                    name: "django_celery_results_groupresult_group_id_a085f1a9_like",
                    table: "django_celery_results_groupresult",
                    column: "group_id")
                .Annotation("Npgsql:IndexOperators", new[] { "varchar_pattern_ops" });

            migrationBuilder.CreateIndex(
                name: "django_celery_results_groupresult_group_id_key",
                table: "django_celery_results_groupresult",
                column: "group_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "django_cele_date_cr_f04a50_idx",
                table: "django_celery_results_taskresult",
                column: "date_created");

            migrationBuilder.CreateIndex(
                name: "django_cele_date_do_f59aad_idx",
                table: "django_celery_results_taskresult",
                column: "date_done");

            migrationBuilder.CreateIndex(
                name: "django_cele_status_9b6201_idx",
                table: "django_celery_results_taskresult",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "django_cele_task_na_08aec9_idx",
                table: "django_celery_results_taskresult",
                column: "task_name");

            migrationBuilder.CreateIndex(
                name: "django_cele_worker_d54dd8_idx",
                table: "django_celery_results_taskresult",
                column: "worker");

            migrationBuilder.CreateIndex(
                    name: "django_celery_results_taskresult_task_id_de0d95bf_like",
                    table: "django_celery_results_taskresult",
                    column: "task_id")
                .Annotation("Npgsql:IndexOperators", new[] { "varchar_pattern_ops" });

            migrationBuilder.CreateIndex(
                name: "django_celery_results_taskresult_task_id_key",
                table: "django_celery_results_taskresult",
                column: "task_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "django_content_type_app_label_model_76bd3d3b_uniq",
                table: "django_content_type",
                columns: new[] { "app_label", "model" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "django_session_expire_date_a5c62663",
                table: "django_session",
                column: "expire_date");

            migrationBuilder.CreateIndex(
                    name: "django_session_session_key_c0390e0f_like",
                    table: "django_session",
                    column: "session_key")
                .Annotation("Npgsql:IndexOperators", new[] { "varchar_pattern_ops" });

            migrationBuilder.CreateIndex(
                name: "dynamic_preferences_glob_section_name_f4a2439b_uniq",
                table: "dynamic_preferences_globalpreferencemodel",
                columns: new[] { "section", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "dynamic_preferences_globalpreferencemodel_name_033debe0",
                table: "dynamic_preferences_globalpreferencemodel",
                column: "name");

            migrationBuilder.CreateIndex(
                    name: "dynamic_preferences_globalpreferencemodel_name_033debe0_like",
                    table: "dynamic_preferences_globalpreferencemodel",
                    column: "name")
                .Annotation("Npgsql:IndexOperators", new[] { "varchar_pattern_ops" });

            migrationBuilder.CreateIndex(
                name: "dynamic_preferences_globalpreferencemodel_section_c1ee9cc3",
                table: "dynamic_preferences_globalpreferencemodel",
                column: "section");

            migrationBuilder.CreateIndex(
                    name: "dynamic_preferences_globalpreferencemodel_section_c1ee9cc3_like",
                    table: "dynamic_preferences_globalpreferencemodel",
                    column: "section")
                .Annotation("Npgsql:IndexOperators", new[] { "varchar_pattern_ops" });

            migrationBuilder.CreateIndex(
                name: "dynamic_preferences_user_instance_id_section_name_29814e3f_uniq",
                table: "dynamic_preferences_users_userpreferencemodel",
                columns: new[] { "instance_id", "section", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                    name: "dynamic_preferences_user_name_11ac488d_like",
                    table: "dynamic_preferences_users_userpreferencemodel",
                    column: "name")
                .Annotation("Npgsql:IndexOperators", new[] { "varchar_pattern_ops" });

            migrationBuilder.CreateIndex(
                    name: "dynamic_preferences_user_section_ba869570_like",
                    table: "dynamic_preferences_users_userpreferencemodel",
                    column: "section")
                .Annotation("Npgsql:IndexOperators", new[] { "varchar_pattern_ops" });

            migrationBuilder.CreateIndex(
                name: "dynamic_preferences_users__instance_id_bf1d7718",
                table: "dynamic_preferences_users_userpreferencemodel",
                column: "instance_id");

            migrationBuilder.CreateIndex(
                name: "dynamic_preferences_users_userpreferencemodel_name_11ac488d",
                table: "dynamic_preferences_users_userpreferencemodel",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "dynamic_preferences_users_userpreferencemodel_section_ba869570",
                table: "dynamic_preferences_users_userpreferencemodel",
                column: "section");

            migrationBuilder.CreateIndex(
                name: "easy_thumbnails_source_name_5fe0edc6",
                table: "easy_thumbnails_source",
                column: "name");

            migrationBuilder.CreateIndex(
                    name: "easy_thumbnails_source_name_5fe0edc6_like",
                    table: "easy_thumbnails_source",
                    column: "name")
                .Annotation("Npgsql:IndexOperators", new[] { "varchar_pattern_ops" });

            migrationBuilder.CreateIndex(
                name: "easy_thumbnails_source_storage_hash_946cbcc9",
                table: "easy_thumbnails_source",
                column: "storage_hash");

            migrationBuilder.CreateIndex(
                    name: "easy_thumbnails_source_storage_hash_946cbcc9_like",
                    table: "easy_thumbnails_source",
                    column: "storage_hash")
                .Annotation("Npgsql:IndexOperators", new[] { "varchar_pattern_ops" });

            migrationBuilder.CreateIndex(
                name: "easy_thumbnails_source_storage_hash_name_481ce32d_uniq",
                table: "easy_thumbnails_source",
                columns: new[] { "storage_hash", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "easy_thumbnails_thumbnai_storage_hash_name_source_fb375270_uniq",
                table: "easy_thumbnails_thumbnail",
                columns: new[] { "storage_hash", "name", "source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "easy_thumbnails_thumbnail_name_b5882c31",
                table: "easy_thumbnails_thumbnail",
                column: "name");

            migrationBuilder.CreateIndex(
                    name: "easy_thumbnails_thumbnail_name_b5882c31_like",
                    table: "easy_thumbnails_thumbnail",
                    column: "name")
                .Annotation("Npgsql:IndexOperators", new[] { "varchar_pattern_ops" });

            migrationBuilder.CreateIndex(
                name: "easy_thumbnails_thumbnail_source_id_5b57bc77",
                table: "easy_thumbnails_thumbnail",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "easy_thumbnails_thumbnail_storage_hash_f1435f49",
                table: "easy_thumbnails_thumbnail",
                column: "storage_hash");

            migrationBuilder.CreateIndex(
                    name: "easy_thumbnails_thumbnail_storage_hash_f1435f49_like",
                    table: "easy_thumbnails_thumbnail",
                    column: "storage_hash")
                .Annotation("Npgsql:IndexOperators", new[] { "varchar_pattern_ops" });

            migrationBuilder.CreateIndex(
                name: "easy_thumbnails_thumbnaildimensions_thumbnail_id_key",
                table: "easy_thumbnails_thumbnaildimensions",
                column: "thumbnail_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Event_EventId",
                schema: "wfc",
                table: "Event",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Event_EventName_EventKey",
                schema: "wfc",
                table: "Event",
                columns: new[] { "EventName", "EventKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Event_EventTime",
                schema: "wfc",
                table: "Event",
                column: "EventTime");

            migrationBuilder.CreateIndex(
                name: "IX_Event_IsProcessed",
                schema: "wfc",
                table: "Event",
                column: "IsProcessed");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPointer_WorkflowId",
                schema: "wfc",
                table: "ExecutionPointer",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_ExtensionAttribute_ExecutionPointerId",
                schema: "wfc",
                table: "ExtensionAttribute",
                column: "ExecutionPointerId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledCommand_CommandName_Data",
                schema: "wfc",
                table: "ScheduledCommand",
                columns: new[] { "CommandName", "Data" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledCommand_ExecuteTime",
                schema: "wfc",
                table: "ScheduledCommand",
                column: "ExecuteTime");

            migrationBuilder.CreateIndex(
                name: "IX_Subscription_EventKey",
                schema: "wfc",
                table: "Subscription",
                column: "EventKey");

            migrationBuilder.CreateIndex(
                name: "IX_Subscription_EventName",
                schema: "wfc",
                table: "Subscription",
                column: "EventName");

            migrationBuilder.CreateIndex(
                name: "IX_Subscription_SubscriptionId",
                schema: "wfc",
                table: "Subscription",
                column: "SubscriptionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workflow_InstanceId",
                schema: "wfc",
                table: "Workflow",
                column: "InstanceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workflow_NextExecution",
                schema: "wfc",
                table: "Workflow",
                column: "NextExecution");

            migrationBuilder.CreateIndex(
                name: "YtManagerApp_jobexecution_user_id_60530e6f",
                table: "YtManagerApp_jobexecution",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "YtManagerApp_jobmessage_job_id_ec6435ce",
                table: "YtManagerApp_jobmessage",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "YtManagerApp_subscription_parent_folder_id_c4c64c21",
                table: "YtManagerApp_subscription",
                column: "parent_folder_id");

            migrationBuilder.CreateIndex(
                name: "YtManagerApp_subscription_user_id_9d38617d",
                table: "YtManagerApp_subscription",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "YtManagerApp_subscriptionfolder_parent_id_bd5f4bc1",
                table: "YtManagerApp_subscriptionfolder",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "YtManagerApp_subscriptionfolder_user_id_6fb12da0",
                table: "YtManagerApp_subscriptionfolder",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "YtManagerApp_video_subscription_id_720d4227",
                table: "YtManagerApp_video",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "ytmanagerapp_video_subscription_id_idx",
                table: "YtManagerApp_video",
                columns: new[] { "subscription_id", "watched" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auth_group_permissions");

            migrationBuilder.DropTable(
                name: "auth_user_groups");

            migrationBuilder.DropTable(
                name: "auth_user_user_permissions");

            migrationBuilder.DropTable(
                name: "AuthProviders");

            migrationBuilder.DropTable(
                name: "django_admin_log");

            migrationBuilder.DropTable(
                name: "django_celery_results_chordcounter");

            migrationBuilder.DropTable(
                name: "django_celery_results_groupresult");

            migrationBuilder.DropTable(
                name: "django_celery_results_taskresult");

            migrationBuilder.DropTable(
                name: "django_migrations");

            migrationBuilder.DropTable(
                name: "django_session");

            migrationBuilder.DropTable(
                name: "dynamic_preferences_globalpreferencemodel");

            migrationBuilder.DropTable(
                name: "dynamic_preferences_users_userpreferencemodel");

            migrationBuilder.DropTable(
                name: "easy_thumbnails_thumbnaildimensions");

            migrationBuilder.DropTable(
                name: "Event",
                schema: "wfc");

            migrationBuilder.DropTable(
                name: "ExecutionError",
                schema: "wfc");

            migrationBuilder.DropTable(
                name: "ExtensionAttribute",
                schema: "wfc");

            migrationBuilder.DropTable(
                name: "ScheduledCommand",
                schema: "wfc");

            migrationBuilder.DropTable(
                name: "Subscription",
                schema: "wfc");

            migrationBuilder.DropTable(
                name: "YtManagerApp_jobmessage");

            migrationBuilder.DropTable(
                name: "YtManagerApp_video");

            migrationBuilder.DropTable(
                name: "auth_group");

            migrationBuilder.DropTable(
                name: "auth_permission");

            migrationBuilder.DropTable(
                name: "easy_thumbnails_thumbnail");

            migrationBuilder.DropTable(
                name: "ExecutionPointer",
                schema: "wfc");

            migrationBuilder.DropTable(
                name: "YtManagerApp_jobexecution");

            migrationBuilder.DropTable(
                name: "YtManagerApp_subscription");

            migrationBuilder.DropTable(
                name: "django_content_type");

            migrationBuilder.DropTable(
                name: "easy_thumbnails_source");

            migrationBuilder.DropTable(
                name: "Workflow",
                schema: "wfc");

            migrationBuilder.DropTable(
                name: "YtManagerApp_subscriptionfolder");

            migrationBuilder.DropTable(
                name: "auth_user");
        }
    }
}
