using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyApp.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // xmin is a system column of every PostgreSQL table: Npgsql generates no SQL for it, and this migration only records the
            // concurrency token of BookingConfiguration in the model.
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Bookings",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Bookings");
        }
    }
}
