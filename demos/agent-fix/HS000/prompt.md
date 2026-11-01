I simplified the GetBooking handler: it now loads the booking with FindAsync and maps it, instead of writing a projection by hand. The build fails since that change.

Finish this change. Read AGENTS.md first and follow it.
