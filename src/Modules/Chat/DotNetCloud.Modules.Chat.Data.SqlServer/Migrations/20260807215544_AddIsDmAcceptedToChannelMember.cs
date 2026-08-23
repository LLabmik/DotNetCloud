using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Chat.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDmAcceptedToChannelMember : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDmAccepted",
                schema: "core",
                table: "ChannelMembers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDmAccepted",
                schema: "core",
                table: "ChannelMembers");
        }
    }
}
