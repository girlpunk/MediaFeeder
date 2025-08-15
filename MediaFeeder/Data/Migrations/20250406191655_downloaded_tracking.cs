using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaFeeder.Data.Migrations
{
    /// <inheritdoc />
    public partial class downloaded_tracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DownloadError",
                table: "YtManagerApp_video",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDownloaded",
                table: "YtManagerApp_video",
                type: "boolean",
                nullable: false,
                computedColumnSql: "downloaded_path IS NOT NULL",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "ytmanagerapp_video_subscription_id_downloaded",
                table: "YtManagerApp_video",
                columns: new[] { "subscription_id", "IsDownloaded" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ytmanagerapp_video_subscription_id_downloaded",
                table: "YtManagerApp_video");

            migrationBuilder.DropColumn(
                name: "IsDownloaded",
                table: "YtManagerApp_video");

            migrationBuilder.DropColumn(
                name: "DownloadError",
                table: "YtManagerApp_video");
        }
    }
}
