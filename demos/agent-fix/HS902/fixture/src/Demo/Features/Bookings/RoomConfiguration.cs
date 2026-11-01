using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Demo.Domain.Bookings;

namespace Demo.Features.Bookings;

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("Rooms");
        builder.HasKey(room => room.Id);
        builder.Property(room => room.Id).ValueGeneratedNever();
        builder.Property(room => room.Name).HasMaxLength(100);
        builder.Property(room => room.Capacity);
    }
}
