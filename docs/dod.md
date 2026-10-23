# Definition of Done

Every task is only complete when **all commands below pass with zero errors**.

Before marking any task complete, run each command in order, wait for it to finish, and paste the full terminal output in your response. A task without this output is **incomplete**.

## Commands

```bash
# Restore all NuGet dependencies for the solution
dotnet restore fluxocaixa.sln

# Build — compiles every project in the solution, surfaces compiler errors/warnings
dotnet build fluxocaixa.sln --no-restore

# Test — runs the full xUnit suite (Lancamentos.UnitTests, ConsolidadoDiario.UnitTests)
dotnet test fluxocaixa.sln --no-build
```

## Rules

- Run all commands even if an earlier one fails — report all failures together.
- Do not suppress, skip, or ignore any failure.
- Fix the root cause and re-run from step 1 until all commands pass.
- If a command is not applicable for the change, explain why in your response — do not silently skip it.
- There is no linter, formatter, or CI pipeline configured in this repository (no `.editorconfig`, no `.github/workflows/`) — `dotnet build` and `dotnet test` are the only gates. Do not invent a `dotnet format` or lint step that isn't actually configured.
- If the change touches EF Core entity configurations or `DbContext` models, manually verify the affected service still starts and `Database.EnsureCreatedAsync()` succeeds (`dotnet run --project src/Lancamentos/Lancamentos.API` or `src/ConsolidadoDiario/ConsolidadoDiario.API`) — there are no EF Core migrations in this project, schema changes are applied via `EnsureCreatedAsync` only.
