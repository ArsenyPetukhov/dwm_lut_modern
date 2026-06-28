# Windows Build Notes

The public symbol server and UUP payloads are the source of truth for DWM compatibility work.

## Profile Inputs

For each Windows build:

1. Query UUP dump for the build UUID.
2. Download `Microsoft-Win4-Feature.ESD`.
3. Verify SHA-256.
4. Extract with `wimlib-imagex`.
5. Locate `dwmcore.dll`.
6. Read the CodeView PDB identity.
7. Download the matching public PDB from Microsoft.
8. Convert symbol segment:offset values to PE RVAs.
9. Add a `DwmSymbolProfile`.
10. Build and live-test.

## Useful References

- [Microsoft public symbols](https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/microsoft-public-symbols)
- [DbgHelp SymInitialize](https://learn.microsoft.com/en-us/windows/win32/api/dbghelp/nf-dbghelp-syminitialize)
- [DIA SDK](https://learn.microsoft.com/en-us/visualstudio/debugger/debug-interface-access/debug-interface-access-sdk)
- [UUP dump API](https://api.uupdump.net/)
- [wimlib](https://wimlib.net/)
