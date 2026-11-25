# Vairas.McpServer — code coverage

**Line coverage 78.79 %** (509/646 executable lines), branch coverage 67.59 % — threshold 70 % **met**. Generated 2026-11-25.

| Assembly | Line % | Lines | Branch % |
|---|---:|---:|---:|
| Vairas.McpServer | 72.41 | 315/435 | 58.33 |
| Vairas.McpServer.Business | 91.94 | 194/211 | 83.27 |

## How it was measured

- Tool: coverlet (`XPlat Code Coverage`) driven by `Backend/local/coverage.py`; settings `Vairas.McpServer/coverage.runsettings` (migrations and test assemblies excluded).
- Test projects: `Vairas.McpServer.Tests`; line hits of all test projects of the service are merged (maximum per line), so shared code is never counted twice.
- Filter: `TestCategory!=Broker`. Broker tests need the Service Bus emulator and are executed (and passed) separately by `local/test_all.py`.
- The numbers are measured line coverage, not a statement about functional completeness; acceptance status is tracked in `Documentation/Final-Verification/AC-Traceability-Matrix.xlsx`.

## Reproduce

```powershell
cd Backend
py -3 local/coverage.py --services Vairas.McpServer --filter "TestCategory!=Broker" --threshold 70
```

## Files

`coverage-summary.json` (machine-readable, per file) and `index.html` (per-file table, lowest coverage first) are in this folder.

## Least covered files

| File | Line % | Lines |
|---|---:|---:|
| `Vairas.McpServer.Business/ConflictException.cs` | 0.0 | 0/1 |
| `Vairas.McpServer.Business/UnprocessableEntityException.cs` | 0.0 | 0/1 |
| `Integrations/DelegatedTokens.cs` | 0.0 | 0/17 |
| `Integrations/McpOwners.cs` | 0.0 | 0/58 |
| `Integrations/WorkloadTokens.cs` | 0.0 | 0/15 |
| `Vairas.McpServer.Business/McpErrors.cs` | 27.27 | 3/11 |
| `Configuration/ConfigureKestrel.cs` | 40.0 | 2/5 |
| `Vairas.McpServer.Business/Wire.cs` | 66.67 | 2/3 |
