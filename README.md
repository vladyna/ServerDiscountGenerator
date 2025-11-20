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

ListRequest:
{
  "Type":"List",
  "Limit": <number>,
}

ListResponse:
{
  "Type":"List",
  "Result": <bool>,
  "Codes": [ "AB12CD34", ... ]
}

Invalid requests yield:
- Generate: `{ "Type":"GenerateResponse","Result":false }` (no Codes field)
- Use: `{ "Type":"UseResponse","Result":3 }` (InvalidRequest)

## Code Generation Details
- Alphabet: A-Z and digits 0-9 (36 chars).
- Code lengths supported: 7 or 8.
- Generation loop attempts up to 5000 batches to fulfill a request.
- Batch size: `min(need * 2, 500)` codes produced in memory, inserted with `INSERT OR IGNORE` to handle collisions.
- Collisions counted as generated - inserted; logged each attempt.
- Backoff: if zero progress after every 10 attempts, a short 20ms delay.
- Success requires full fulfillment (generated.Count == Count). Partial failure returns Result=false with no Codes.

## Starting the Server & Client (Visual Studio)
1. Open the solution containing projects:
   - DiscountServer
   - DiscountClient
   - DiscountServer.Tests
2. Set multiple startup projects (Solution Properties > Startup Project > Multiple Startup Projects):
   - DiscountServer: Start
   - DiscountClient: Start (optional if you want interactive usage)
3. Run (F5). Server listens on `localhost:5000`.


## Running Tests
From solution directory:dotnet testIncluded tests:
- Unit: CodeGenerator length & charset, repository duplicate insertion, `UseCodeAsync` state transitions.
- Integration: 2,000 code generation, concurrency uniqueness (50 parallel tasks x 200 codes).

## Security & Robustness Considerations
- Max message size (16KB) prevents large payload DoS.
- Per-message timeout (10s) avoids slow-loris style attacks.
- Input parsing wrapped in try/catch; malformed JSON ignored (Generate/Use handlers return an explicit failure when parsed object invalid).
- Codes field omitted on failure to maintain stable schema for strict validation harnesses.

## Quick Start Recap
1. `dotnet build`
2. Run DiscountServer (`dotnet run --project DiscountServer`)
3. Use netcat or DiscountClient to send JSON lines.
4. Run tests: `dotnet test`
