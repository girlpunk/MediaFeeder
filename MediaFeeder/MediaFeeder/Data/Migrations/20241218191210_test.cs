using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaFeeder.Data.Migrations
{
    /// <inheritdoc />
    public partial class test : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessFailedCount",
                table: "auth_user");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                table: "auth_user");

            migrationBuilder.DropColumn(
                name: "EmailConfirmed",
                table: "auth_user");

            migrationBuilder.DropColumn(
                name: "LockoutEnabled",
                table: "auth_user");

            migrationBuilder.DropColumn(
                name: "LockoutEnd",
                table: "auth_user");

            migrationBuilder.DropColumn(
                name: "NormalizedEmail",
                table: "auth_user");

            migrationBuilder.DropColumn(
                name: "NormalizedUserName",
                table: "auth_user");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "auth_user");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "auth_user");

            migrationBuilder.DropColumn(
                name: "PhoneNumberConfirmed",
                table: "auth_user");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "auth_user");

            migrationBuilder.DropColumn(
                name: "TwoFactorEnabled",
                table: "auth_user");

            migrationBuilder.DropColumn(
                name: "UserName",
                table: "auth_user");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccessFailedCount",
                table: "auth_user",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyStamp",
                table: "auth_user",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmailConfirmed",
                table: "auth_user",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LockoutEnabled",
                table: "auth_user",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockoutEnd",
                table: "auth_user",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedEmail",
                table: "auth_user",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedUserName",
                table: "auth_user",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "auth_user",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "auth_user",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PhoneNumberConfirmed",
                table: "auth_user",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "auth_user",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TwoFactorEnabled",
                table: "auth_user",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "UserName",
                table: "auth_user",
                type: "text",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "django_admin_log_content_type_id_c4bce8eb_fk_django_co",
                table: "django_admin_log",
                column: "content_type_id",
                principalTable: "django_content_type",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "YtManagerApp_jobexecution_user_id_60530e6f_fk_auth_user_id",
                table: "YtManagerApp_jobexecution",
                column: "user_id",
                principalTable: "auth_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "YtManagerApp_subscri_parent_folder_id_c4c64c21_fk_YtManager",
                table: "YtManagerApp_subscription",
                column: "parent_folder_id",
                principalTable: "YtManagerApp_subscriptionfolder",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
