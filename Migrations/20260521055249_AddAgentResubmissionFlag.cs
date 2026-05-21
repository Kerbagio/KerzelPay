using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KerzelPay.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentResubmissionFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsResubmission",
                table: "Agents",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsResubmission",
                table: "Agents");
        }
    }
}
