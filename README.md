# DiscountServer Protocol & Implementation Notes

## Overview
DiscountServer exposes a very small TCP protocol for generating and consuming discount codes. Messages are UTF-8 encoded JSON objects delimited by a single `\n` newline. Carriage returns (`\r`) are ignored. Each inbound line is parsed independently; there is no length prefix. Maximum inbound message size: 16KB. Per-message read timeout: 10s.

## Message Framing
- Transport: TCP
- Encoding: UTF-8
- Delimiter: `\n` (newline). `\r\n` from some clients is tolerated; `\r` stripped.
- Max message size: 16,384 bytes (server closes connection if exceeded).
- Timeout: If a full line isn't received within 10 seconds, the server closes the connection.

## Message Types
Request Type field selects behavior. All messages contain a non-empty string field `Type`.

GenerateRequest JSON Schema (conceptual):
{
  "Type": "Generate",
  "Count": <number>,    // ushort 1..2000 (validated; must also <= 65535)
  "Length": <number>    // byte, allowed values: 7 or 8
}

GenerateResponse:
{
  "Type": "GenerateResponse",
  "Result": <bool>,
  "Codes": [ "AB12CD34", ... ] // OPTIONAL; present ONLY when Result=true AND full requested count created
}

UseRequest:
{
  "Type": "Use",
  "Code": "ABCDEFG" // string length 7 or 8, non-null/non-whitespace
}

UseResponse:
{
  "Type": "UseResponse",
  "Result": <number> // byte status code
}

## Field Types & Ranges (Strict Validation)
- Count: ushort (C# `ushort`) range 1..2000. Reject 0, >2000 or >65535.
- Length: byte (C# `byte`) allowed values: 7 or 8 only.
- Code (UseRequest): non-empty string length 7 or 8. (Alphabet is A-Z0-9 but we only validate length & non-whitespace.)

Invalid requests yield:
- Generate: `{ "Type":"GenerateResponse","Result":false }` (no Codes field)
- Use: `{ "Type":"UseResponse","Result":3 }` (InvalidRequest)

## UseResponse.Result Mapping (byte)
- 0 = OK          (code successfully marked used)
- 1 = NotFound    (code does not exist)
- 2 = AlreadyUsed (code existed but was used previously)
- 3 = InvalidRequest (validation failed or malformed JSON)

## Code Generation Details
- Alphabet: A-Z and digits 0-9 (36 chars).
- Code lengths supported: 7 or 8.
- Generation loop attempts up to 5000 batches to fulfill a request.
- Batch size: `min(need * 2, 500)` codes produced in memory, inserted with `INSERT OR IGNORE` to handle collisions.
- Collisions counted as generated - inserted; logged each attempt.
- Backoff: if zero progress after every 10 attempts, a short 20ms delay.
- Success requires full fulfillment (generated.Count == Count). Partial failure returns Result=false with no Codes.

## SQLite & Concurrency
- WAL mode enabled (`PRAGMA journal_mode=WAL`).
- Short-lived connections per repository method.
- Busy retry: up to 5 attempts with exponential backoff (50,100,200,400,800 ms) on `SQLITE_BUSY` when opening connection.
- Individual busy insert statements are skipped; subsequent attempts keep trying.

## Starting the Server & Client (Visual Studio)
1. Open the solution containing projects:
   - DiscountServer
   - DiscountClient
   - DiscountServer.Tests
2. Set multiple startup projects (Solution Properties > Startup Project > Multiple Startup Projects):
   - DiscountServer: Start
   - DiscountClient: Start (optional if you want interactive usage)
3. Run (F5). Server listens on `localhost:5000`.

### Command-line Interaction (netcat / nc)
You can test manually without the client:
Windows (use ncat from Nmap or PowerShell's built-in `nc` if available):# Connect
nc 127.0.0.1 5000
# Send a generate request (press Enter after line)
{"Type":"Generate","Count":5,"Length":8}
# Receive response line
{"Type":"GenerateResponse","Result":true,"Codes":["ABCDEF12",...]}
# Use one code
{"Type":"Use","Code":"ABCDEF12"}
{"Type":"UseResponse","Result":0}
### Sample Client JSON Sequence
1. `{"Type":"Generate","Count":3,"Length":7}`
2. `{"Type":"Use","Code":"AAAAAAA"}` (replace with returned code)

## Running Tests
From solution directory:dotnet testIncluded tests:
- Unit: CodeGenerator length & charset, repository duplicate insertion, `UseCodeAsync` state transitions.
- Integration: 2,000 code generation, concurrency uniqueness (50 parallel tasks x 200 codes).

## Stress Testing (Example Concept)
A simple parallel generator stress (pseudo):Parallel.For(0, 50, i => {
  // open TcpClient, send Generate 200, collect codes
});
// assert uniquenessFor full TCP stress you can craft a console tool similar to DiscountClient but spawning tasks and aggregating codes into a ConcurrentDictionary for uniqueness verification.

## Example Failure Responses
- Invalid Generate (Length=9): `{"Type":"GenerateResponse","Result":false}`
- Invalid Use (Code length 6): `{"Type":"UseResponse","Result":3}`

## Security & Robustness Considerations
- Max message size (16KB) prevents large payload DoS.
- Per-message timeout (10s) avoids slow-loris style attacks.
- Input parsing wrapped in try/catch; malformed JSON ignored (Generate/Use handlers return an explicit failure when parsed object invalid).
- Codes field omitted on failure to maintain stable schema for strict validation harnesses.

## Extensibility Suggestions
- Add configurable settings (port, max attempts, timeouts) via appsettings.json.
- Structured logging (JSON) for metrics (attempts, collision counts, latency).
- Rate limiting per client IP.
- Add HTTP API alongside TCP for easier integration.

## Versioning & Compatibility
- Accept both 7 and 8 length codes for UseRequest to remain backward compatible. Future versions could enforce a single length with migration plan.

## Quick Start Recap
1. `dotnet build`
2. Run DiscountServer (`dotnet run --project DiscountServer`)
3. Use netcat or DiscountClient to send JSON lines.
4. Run tests: `dotnet test`
