using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyApp.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingNoOverlapConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF Core does not model exclusion constraints. This one keeps the Booking overlap rule true under concurrency:
            // two confirmed bookings of the same room cannot have overlapping [Start, End) periods.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql(
                """
                ALTER TABLE "Bookings" ADD CONSTRAINT "EX_Bookings_RoomId_Period"
                    EXCLUDE USING gist ("RoomId" WITH =, tstzrange("Start", "End", '[)') WITH &&)
                    WHERE ("Status" = 'Confirmed');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE "Bookings" DROP CONSTRAINT "EX_Bookings_RoomId_Period";""");
            // btree_gist is database-wide and may predate this migration or be shared by other objects.
            // Leave it installed when rolling back the application-owned constraint.
        }
    }
}
