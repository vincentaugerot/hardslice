using Demo.Domain;
using Demo.Domain.Bookings;

namespace Demo.Tests.Domain.Bookings;

public sealed class RoomTests
{
    [Fact]
    public void Open_creates_a_room_with_its_trimmed_name_and_capacity()
    {
        var room = Room.Open("  Blue room ", 6);

        room.Name.ShouldBe("Blue room");
        room.Capacity.ShouldBe(6);
    }

    [Fact]
    public void Open_rejects_a_room_without_a_name()
    {
        Should.Throw<DomainException>(() => Room.Open(" ", 6));
    }

    [Fact]
    public void Open_rejects_a_room_that_holds_nobody()
    {
        Should.Throw<DomainException>(() => Room.Open("Blue room", 0));
    }
}
