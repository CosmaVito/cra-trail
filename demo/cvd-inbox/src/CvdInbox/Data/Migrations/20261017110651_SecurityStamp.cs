using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CvdInbox.Data.Migrations
{
    /// <inheritdoc />
    public partial class SecurityStamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "Operators",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE Operators SET SecurityStamp = lower(hex(randomblob(16)));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Operators");
        }
    }
}
