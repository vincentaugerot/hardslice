Cancelling a booking must record a BookingCancelled domain event, as confirming one records BookingConfirmed: a reaction will free the room for its waitlist. The event and its test are written, but the build fails.

Finish this change. Read AGENTS.md first and follow it.
