using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyApp.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingPositivePeriodConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Bookings_Period_Positive",
                table: "Bookings",
                sql: "\"End\" > \"Start\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Bookings_Period_Positive",
                table: "Bookings");
        }
    }
}
