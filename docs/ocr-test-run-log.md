# OCR Test Run Log

## Environment
- Command attempted: `dotnet run --project src/Unfollowed.App -- ocr-test`
- Result: `dotnet` was not found in the environment (`bash: command not found: dotnet`).

## Notes
- `ocr-test` relies on Win32 capture/OCR registrations in the runtime stubs, which are Windows-specific.
