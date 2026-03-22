using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaFeeder.Data.Migrations
{
    /// <inheritdoc />
    public partial class fix_delete_subscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "auth_group_permissio_permission_id_84c5c92e_fk_auth_perm",
                table: "auth_group_permissions");

            migrationBuilder.DropForeignKey(
                name: "auth_group_permissions_group_id_b120cbf9_fk_auth_group_id",
                table: "auth_group_permissions");

            migrationBuilder.DropForeignKey(
                name: "auth_permission_content_type_id_2f476e4b_fk_django_co",
                table: "auth_permission");

            migrationBuilder.DropForeignKey(
                name: "auth_user_groups_group_id_97559544_fk_auth_group_id",
                table: "auth_user_groups");

            migrationBuilder.DropForeignKey(
                name: "auth_user_groups_user_id_6a12ed8b_fk_auth_user_id",
                table: "auth_user_groups");

            migrationBuilder.DropForeignKey(
                name: "auth_user_user_permi_permission_id_1fbb5f2c_fk_auth_perm",
                table: "auth_user_user_permissions");

            migrationBuilder.DropForeignKey(
                name: "auth_user_user_permissions_user_id_a95ead1b_fk_auth_user_id",
                table: "auth_user_user_permissions");

            migrationBuilder.DropForeignKey(
                name: "dynamic_preferences__instance_id_bf1d7718_fk_auth_user",
                table: "dynamic_preferences_users_userpreferencemodel");

            migrationBuilder.DropForeignKey(
                name: "easy_thumbnails_thum_source_id_5b57bc77_fk_easy_thum",
                table: "easy_thumbnails_thumbnail");

            migrationBuilder.DropForeignKey(
                name: "easy_thumbnails_thum_thumbnail_id_c3a0c549_fk_easy_thum",
                table: "easy_thumbnails_thumbnaildimensions");

            migrationBuilder.DropForeignKey(
                name: "YtManagerApp_jobmess_job_id_ec6435ce_fk_YtManager",
                table: "YtManagerApp_jobmessage");

            migrationBuilder.DropForeignKey(
                name: "YtManagerApp_subscription_user_id_9d38617d_fk_auth_user_id",
                table: "YtManagerApp_subscription");

            migrationBuilder.DropForeignKey(
                name: "YtManagerApp_subscri_user_id_6fb12da0_fk_auth_user",
                table: "YtManagerApp_subscriptionfolder");

            migrationBuilder.DropForeignKey(
                name: "YtManagerApp_video_subscription_id_720d4227_fk_YtManager",
                table: "YtManagerApp_video");

            migrationBuilder.AddForeignKey(
                name: "auth_group_permissio_permission_id_84c5c92e_fk_auth_perm",
                table: "auth_group_permissions",
                column: "permission_id",
                principalTable: "auth_permission",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "auth_group_permissions_group_id_b120cbf9_fk_auth_group_id",
                table: "auth_group_permissions",
                column: "group_id",
                principalTable: "auth_group",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "auth_permission_content_type_id_2f476e4b_fk_django_co",
                table: "auth_permission",
                column: "content_type_id",
                principalTable: "django_content_type",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "auth_user_groups_group_id_97559544_fk_auth_group_id",
                table: "auth_user_groups",
                column: "group_id",
                principalTable: "auth_group",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "auth_user_groups_user_id_6a12ed8b_fk_auth_user_id",
                table: "auth_user_groups",
                column: "user_id",
                principalTable: "auth_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "auth_user_user_permi_permission_id_1fbb5f2c_fk_auth_perm",
                table: "auth_user_user_permissions",
                column: "permission_id",
                principalTable: "auth_permission",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "auth_user_user_permissions_user_id_a95ead1b_fk_auth_user_id",
                table: "auth_user_user_permissions",
                column: "user_id",
                principalTable: "auth_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "dynamic_preferences__instance_id_bf1d7718_fk_auth_user",
                table: "dynamic_preferences_users_userpreferencemodel",
                column: "instance_id",
                principalTable: "auth_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "easy_thumbnails_thum_source_id_5b57bc77_fk_easy_thum",
                table: "easy_thumbnails_thumbnail",
                column: "source_id",
                principalTable: "easy_thumbnails_source",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "easy_thumbnails_thum_thumbnail_id_c3a0c549_fk_easy_thum",
                table: "easy_thumbnails_thumbnaildimensions",
                column: "thumbnail_id",
                principalTable: "easy_thumbnails_thumbnail",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "YtManagerApp_jobmess_job_id_ec6435ce_fk_YtManager",
                table: "YtManagerApp_jobmessage",
                column: "job_id",
                principalTable: "YtManagerApp_jobexecution",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "YtManagerApp_subscription_user_id_9d38617d_fk_auth_user_id",
                table: "YtManagerApp_subscription",
                column: "user_id",
                principalTable: "auth_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "YtManagerApp_subscri_user_id_6fb12da0_fk_auth_user",
                table: "YtManagerApp_subscriptionfolder",
                column: "user_id",
                principalTable: "auth_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "YtManagerApp_video_subscription_id_720d4227_fk_YtManager",
                table: "YtManagerApp_video",
                column: "subscription_id",
                principalTable: "YtManagerApp_subscription",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "auth_group_permissio_permission_id_84c5c92e_fk_auth_perm",
                table: "auth_group_permissions");

            migrationBuilder.DropForeignKey(
                name: "auth_group_permissions_group_id_b120cbf9_fk_auth_group_id",
                table: "auth_group_permissions");

            migrationBuilder.DropForeignKey(
                name: "auth_permission_content_type_id_2f476e4b_fk_django_co",
                table: "auth_permission");

            migrationBuilder.DropForeignKey(
                name: "auth_user_groups_group_id_97559544_fk_auth_group_id",
                table: "auth_user_groups");

            migrationBuilder.DropForeignKey(
                name: "auth_user_groups_user_id_6a12ed8b_fk_auth_user_id",
                table: "auth_user_groups");

            migrationBuilder.DropForeignKey(
                name: "auth_user_user_permi_permission_id_1fbb5f2c_fk_auth_perm",
                table: "auth_user_user_permissions");

            migrationBuilder.DropForeignKey(
                name: "auth_user_user_permissions_user_id_a95ead1b_fk_auth_user_id",
                table: "auth_user_user_permissions");

            migrationBuilder.DropForeignKey(
                name: "dynamic_preferences__instance_id_bf1d7718_fk_auth_user",
                table: "dynamic_preferences_users_userpreferencemodel");

            migrationBuilder.DropForeignKey(
                name: "easy_thumbnails_thum_source_id_5b57bc77_fk_easy_thum",
                table: "easy_thumbnails_thumbnail");

            migrationBuilder.DropForeignKey(
                name: "easy_thumbnails_thum_thumbnail_id_c3a0c549_fk_easy_thum",
                table: "easy_thumbnails_thumbnaildimensions");

            migrationBuilder.DropForeignKey(
                name: "YtManagerApp_jobmess_job_id_ec6435ce_fk_YtManager",
                table: "YtManagerApp_jobmessage");

            migrationBuilder.DropForeignKey(
                name: "YtManagerApp_subscription_user_id_9d38617d_fk_auth_user_id",
                table: "YtManagerApp_subscription");

            migrationBuilder.DropForeignKey(
                name: "YtManagerApp_subscri_user_id_6fb12da0_fk_auth_user",
                table: "YtManagerApp_subscriptionfolder");

            migrationBuilder.DropForeignKey(
                name: "YtManagerApp_video_subscription_id_720d4227_fk_YtManager",
                table: "YtManagerApp_video");

            migrationBuilder.AddForeignKey(
                name: "auth_group_permissio_permission_id_84c5c92e_fk_auth_perm",
                table: "auth_group_permissions",
                column: "permission_id",
                principalTable: "auth_permission",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "auth_group_permissions_group_id_b120cbf9_fk_auth_group_id",
                table: "auth_group_permissions",
                column: "group_id",
                principalTable: "auth_group",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "auth_permission_content_type_id_2f476e4b_fk_django_co",
                table: "auth_permission",
                column: "content_type_id",
                principalTable: "django_content_type",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "auth_user_groups_group_id_97559544_fk_auth_group_id",
                table: "auth_user_groups",
                column: "group_id",
                principalTable: "auth_group",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "auth_user_groups_user_id_6a12ed8b_fk_auth_user_id",
                table: "auth_user_groups",
                column: "user_id",
                principalTable: "auth_user",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "auth_user_user_permi_permission_id_1fbb5f2c_fk_auth_perm",
                table: "auth_user_user_permissions",
                column: "permission_id",
                principalTable: "auth_permission",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "auth_user_user_permissions_user_id_a95ead1b_fk_auth_user_id",
                table: "auth_user_user_permissions",
                column: "user_id",
                principalTable: "auth_user",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "dynamic_preferences__instance_id_bf1d7718_fk_auth_user",
                table: "dynamic_preferences_users_userpreferencemodel",
                column: "instance_id",
                principalTable: "auth_user",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "easy_thumbnails_thum_source_id_5b57bc77_fk_easy_thum",
                table: "easy_thumbnails_thumbnail",
                column: "source_id",
                principalTable: "easy_thumbnails_source",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "easy_thumbnails_thum_thumbnail_id_c3a0c549_fk_easy_thum",
                table: "easy_thumbnails_thumbnaildimensions",
                column: "thumbnail_id",
                principalTable: "easy_thumbnails_thumbnail",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "YtManagerApp_jobmess_job_id_ec6435ce_fk_YtManager",
                table: "YtManagerApp_jobmessage",
                column: "job_id",
                principalTable: "YtManagerApp_jobexecution",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "YtManagerApp_subscription_user_id_9d38617d_fk_auth_user_id",
                table: "YtManagerApp_subscription",
                column: "user_id",
                principalTable: "auth_user",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "YtManagerApp_subscri_user_id_6fb12da0_fk_auth_user",
                table: "YtManagerApp_subscriptionfolder",
                column: "user_id",
                principalTable: "auth_user",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "YtManagerApp_video_subscription_id_720d4227_fk_YtManager",
                table: "YtManagerApp_video",
                column: "subscription_id",
                principalTable: "YtManagerApp_subscription",
                principalColumn: "id");
        }
    }
}
