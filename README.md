# Vairas MCP Server

Private Model Context Protocol server (ASP.NET Core 10, stateless HTTP transport at `/mcp`) that lets the Chat
Backend read school data on behalf of one signed-in user through exact-recipient delegated tokens, and — only with
the confirmed-lesson-actions profile — carry out one exact, user-confirmed lesson write. It is not exposed through the
public Gateway catalogue and rejects browser origins. It owns no database.

## Projects

| Project | Responsibility |
|---|---|
| `Vairas.McpServer` | HTTP host: `McpApplication` composition root, `Configuration/Configure*`, tool handlers, owner clients |
| `Vairas.McpServer.Business` | Tool catalogue, rules and delegation contracts |
| `Vairas.McpServer.Tests` | NUnit protocol, delegation and confirmed-transport tests |

`security/` holds the canonical copies of the shared delegation/action policy files that `scripts/sync-policy.py`
distributes to the other services.

## Dependencies

Authentication (token authority, JWKS), User Management, School Management, Scheduling, Vehicle, Learning (owner
reads). Local origin `https://localhost:7900`.

## Essential configuration

Options are bound and validated at startup (`Configuration/ConfigureOptions.cs`). Use `__` in environment variables.

| Section | Notes |
|---|---|
| `Mcp` | HTTPS `Authority` and exact `Resource`; `ToolSeconds` 1-20; `ConfirmedLessonActionsEnabled` |
| `Dependencies` | exactly four private HTTPS `Resources`; HTTPS `AuthBaseUrl`, `SchoolBaseUrl`, `BusinessApiBaseUrl`; `ClientId` `mcp-server`; `ClientSecret` required outside Testing |

## Run locally

```powershell
dotnet run --project Vairas.McpServer --launch-profile https
```

The complete local stack is started from `../local/Start.ps1`.

## Tests

```powershell
dotnet test Vairas.McpServer.slnx
./scripts/verify.ps1 -SkipIntegration           # tests with the coverage gate
```

API exports: `api/`. Extended notes, validation reports and evidence: `../../Documentation/Backend/Vairas.McpServer/`.

## Tests and coverage

Requirements: .NET SDK 10 and Docker (PostgreSQL integration tests start a throw-away container via Testcontainers).

```powershell
dotnet test Vairas.McpServer.slnx                  # unit + integration tests
dotnet test Vairas.McpServer.slnx --collect:"XPlat Code Coverage" --settings coverage.runsettings --results-directory TestResults   # measure coverage
```

**Coverage report:** [docs/coverage/README.md](docs/coverage/README.md) (summary, per-assembly table, least covered files); machine-readable `docs/coverage/coverage-summary.json` and `docs/coverage/index.html` (per-file table). The raw cobertura XML of a local run is written to `TestResults/`.