namespace Demo.Domain.Bookings;

// A room that bookings reserve, with the number of people it holds.
public sealed class Room
{
    private Room(Guid id, string name, int capacity)
    {
        Id = id;
        Name = name;
        Capacity = capacity;
    }

    // Rehydration only: persistence restores the state of a stored room. Business code calls Open.
    private Room()
    {
        Name = null!;
    }

    public Guid Id { get; }

    public string Name { get; }

    public int Capacity { get; }

    public static Room Open(string name, int capacity)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("A room has a name.");
        if (capacity < 1)
            throw new DomainException("A room holds at least one person.");

        return new Room(Guid.CreateVersion7(), name.Trim(), capacity);
    }
}
