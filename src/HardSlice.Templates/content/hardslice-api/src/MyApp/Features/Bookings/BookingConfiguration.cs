using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApp.Domain.Bookings;

namespace MyApp.Features.Bookings;

// Module-level mapping, outside any slice: the domain stays free of EF Core attributes and references.
internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings", table =>
            table.HasCheckConstraint("CK_Bookings_Period_Positive", "\"End\" > \"Start\""));

        builder.HasKey(booking => booking.Id);
        builder.Property(booking => booking.Id)
            .HasConversion(id => id.Value, value => new BookingId(value))
            .ValueGeneratedNever();

        builder.Property(booking => booking.RoomId);

        builder.ComplexProperty(booking => booking.Period, period =>
        {
            period.Property(p => p.Start).HasColumnName("Start");
            period.Property(p => p.End).HasColumnName("End");
        });

        builder.Property(booking => booking.Status)
            .HasConversion<string>()
            .HasMaxLength(16);

        // Optimistic concurrency: PostgreSQL changes the xmin system column of a row at each update, and EF Core updates a booking
        // only while its xmin is the one it loaded. A command that changes a booking another request changed since it loaded it
        // saves nothing: TransactionBehavior answers it with 409. A shadow property, so that the domain knows nothing of it.
        builder.Property<uint>("Version").IsRowVersion();

        builder.Ignore(booking => booking.DomainEvents);
    }
}
