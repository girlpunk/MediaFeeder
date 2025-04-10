using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaFeeder.Data.Migrations
{
    /// <inheritdoc />
    public partial class add_DisableSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DisableSync",
                table: "YtManagerApp_subscription",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisableSync",
                table: "YtManagerApp_subscription");
        }
    }
}
