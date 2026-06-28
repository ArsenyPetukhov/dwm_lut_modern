# Tests

This project cannot fully unit-test DWM injection in CI because DWM internals, GPU state, and desktop sessions are machine-specific.

The repository therefore uses three layers:

1. Build checks.
2. Package/static artifact checks.
3. Manual runtime validation against exact Windows builds.
