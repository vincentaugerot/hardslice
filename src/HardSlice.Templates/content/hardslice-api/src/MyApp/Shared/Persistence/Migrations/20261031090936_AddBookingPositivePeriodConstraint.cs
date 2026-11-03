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
            // BookingPeriod truncates its bounds to microseconds, the precision of PostgreSQL, before it checks that the period ends
            // after it starts. A database that holds empty periods stored before that rule fails this constraint: repair those rows
            // explicitly before applying it.
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
