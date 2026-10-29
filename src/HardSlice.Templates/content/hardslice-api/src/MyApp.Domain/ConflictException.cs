namespace MyApp.Domain;

// A rule broken by the current state of the system rather than by the request alone: the same request may succeed later.
public class ConflictException(string message) : DomainException(message);
