# Definition of Done

Every task is only complete when **all commands below pass with zero errors**.

Before marking any task complete, run each command in order, wait for it to finish, and paste the full terminal output in your response. A task without this output is **incomplete**.

## Commands

```bash
# 1. Restore — resolves NuGet packages for all 11 projects in the solution
dotnet restore fluxocaixa.sln

# 2. Build — compiles every project (Domain/Application/Infrastructure/API × 2 bounded
#    contexts + SharedKernel + both test projects) with warnings-as-errors off but
#    Nullable enabled, so nullability warnings surface here
dotnet build fluxocaixa.sln --no-restore

# 3. Test — runs every [Fact]/[Theory] in both xUnit test projects
dotnet test fluxocaixa.sln --no-build
```

## Rules

- Run all three commands even if an earlier one fails — report all failures together.
- Do not suppress, skip, or ignore any failure, including `Nullable`-related compiler warnings introduced by your change (the project has `<Nullable>enable</Nullable>` everywhere).
- Fix the root cause and re-run from step 1 until all commands pass.
- **Lint step is intentionally absent:** this repository has no `.editorconfig`, no `dotnet format` configuration, and no CI pipeline enforcing style — there is no project-defined lint gate to run. Do not invent one; instead, manually check new/changed code against [code-conventions.md](code-conventions.md) (naming, Allman braces, sealed classes, private-ctor + static factory, file-scoped namespaces, Portuguese domain language / English pattern suffixes) before calling the task done.
- If you touched `*.Domain` or `*.Application` code for either bounded context, confirm the corresponding `tests/<Context>.UnitTests` project still reflects the behavior (add/update `[Fact]`s per [testing-standards.md](testing-standards.md) naming convention) — new behavior without a matching test is not done.
- If you touched an endpoint in `Lancamentos.API` or `ConsolidadoDiario.API`, confirm the route, status codes, and `.WithSummary()`/`.WithDescription()`/`.Produces<T>()` annotations still match [api-standards.md](api-standards.md) — these drive the Swagger docs the README links to.
- If a command is not applicable for the change (e.g. a docs-only change under `docs/`), explain why in your response — do not silently skip it.
