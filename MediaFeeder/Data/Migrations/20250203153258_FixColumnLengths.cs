using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaFeeder.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixColumnLengths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "video_id",
                table: "YtManagerApp_video",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(12)",
                oldMaxLength: 12);

            migrationBuilder.AlterColumn<string>(
                name: "thumbnail",
                table: "YtManagerApp_video",
                type: "text",
                maxLength: 1000000000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldMaxLength: 1000000000);

            migrationBuilder.AlterColumn<string>(
                name: "thumbnail",
                table: "YtManagerApp_subscription",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "auth_group",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "video_id",
                table: "YtManagerApp_video",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "thumbnail",
                table: "YtManagerApp_video",
                type: "text",
                maxLength: 1000000000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldMaxLength: 1000000000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "thumbnail",
                table: "YtManagerApp_subscription",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "auth_group",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150,
                oldNullable: true);
        }
    }
}
