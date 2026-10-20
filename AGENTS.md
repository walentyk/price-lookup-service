# AGENTS.md

Instructions for coding agents (Claude Code) working in this repository.
Read this file and [docs/ImplementationPlan/plan.md](docs/ImplementationPlan/plan.md) before doing anything.

## What this is

`price-lookup-service` is a small .NET 10 Minimal API microservice built for the IRCO interview assessment.
`GET /products/{sku}/price` validates the SKU, asks the ERP (SAP S/4HANA, external, OData V2 over HTTPS with an X.509 client certificate) for the price through the `IErpClient` port, and returns a DTO or a `ProblemDetails` error with a machine-readable `code`.
The code is written in DDD style: two projects, small classes, small methods.

Sources of truth, in this order:
1. [docs/requirements.md](docs/requirements.md) - what the assessment asks for (retelling of the original PDF in `docs/InitialRequirements/`).
2. [docs/assumptions.md](docs/assumptions.md) - everything the assessment does not say and the author decided.
3. [docs/architecture.md](docs/architecture.md) - components, flows, error mapping, configuration (exists after plan step 1).
4. [docs/ImplementationPlan/plan.md](docs/ImplementationPlan/plan.md) - steps, checklists, progress tracker.

If the code and the docs disagree, or a step is ambiguous, stop and ask. Do not silently pick an interpretation.

## Keep it small

This is an interview task. Implement exactly what the current step asks for.
No extra layers, abstractions, packages, endpoints or "nice to have" features unless the step lists them.

## Layout

```text
docs/                              requirements, assumptions, architecture, plan, frontend-bff
src/PriceLookup.Core/              Domain, Application, Infrastructure (the SAP adapter); no ASP.NET dependency
src/PriceLookup.Api/               Minimal API: endpoints, ProblemDetails, configuration, composition root
tests/PriceLookup.Tests/           all automated tests; folders mirror the layers
tests/PriceLookup.SapStub/         tiny HTTP app imitating the SAP OData endpoint (never deployed)
```

## Commands

All commands are bash (the author uses Git Bash on Windows). Do not use PowerShell syntax.

```bash
dotnet build PriceLookup.slnx
dotnet test PriceLookup.slnx
dotnet run --project tests/PriceLookup.SapStub    # SAP stub on http://localhost:5199
dotnet run --project src/PriceLookup.Api          # API on http://localhost:5080 (Development)
curl -s -i http://localhost:5080/products/ABC-123/price
npx @mermaid-js/mermaid-cli@10.8.0 -i <file.mmd> -o <file.svg>   # check that a Mermaid diagram renders
```

The Mermaid CLI version is pinned on purpose: the author's machine has Node 14 and npx 6. The latest mermaid-cli needs a newer Node, and npx 6 has no `-y` flag.

Long-running processes: start them as background tasks and stop them when the check is done.
On Windows `dotnet run` can leave the app process alive; clean up with
`taskkill //F //IM PriceLookup.Api.exe` and `taskkill //F //IM PriceLookup.SapStub.exe` (ignore "not found").

## Hard rules

- **Layers.** `Domain` references nothing. `Application` references `Domain` only (plus `Microsoft.Extensions.Logging.Abstractions`). `Infrastructure` references `Application` and `Domain`. `PriceLookup.Api` references `PriceLookup.Core` only. Nothing in `src/` references anything in `tests/`.
- **Error codes.** Machine-readable codes (`sku_not_found`, ...) are strings that live in `PriceLookup.Api` (`ErrorCodes`). `PriceLookup.Core` knows only the `PriceLookupError` enum and knows nothing about HTTP.
- **Resilience.** Retries live only in `RetryingErpClient`, the per-attempt timeout only in `AttemptTimeoutErpClient`, the total timeout only in `TimeoutErpClient` (all in `Application/Resilience`). `SapErpClient` makes exactly one HTTP call per invocation, never retries and sets no timeout of its own. The decorator chain is `TimeoutErpClient(RetryingErpClient(AttemptTimeoutErpClient(SapErpClient)))`.
- **No Polly**, no `Microsoft.Extensions.Http.Resilience`, no `AddStandardResilienceHandler`. The retry loop is hand-written.
- Every error response is a `ProblemDetails` with a `code` extension. Never return a bare status code or a plain string body.
- **No secrets in the repo.** `appsettings.json` holds defaults and placeholders only. Never log certificates, passwords or private keys.
- Production code has **no "no authentication" switch**. Outside `Development`, `Sap:BaseUrl` must be `https` and the client certificate is required (assumptions A7, A8, A24).
- Only the NuGet packages listed in the plan; versions are pinned centrally in `Directory.Packages.props`. Ask before adding anything else.
- No mocking libraries, no FluentAssertions. Use the fakes and xUnit `Assert`.
- The inbound API is plain HTTP inside the internal network: no `UseHttpsRedirection`, no HSTS, no inbound auth (assumption A3).
- The SAP stub and every fake live only under `tests/` and are never published or deployed.

## Code shape (small classes, small methods)

- A method is up to about 15 lines. If it does two things, split it. Early returns instead of nesting; at most two levels of nesting.
- A class is up to about 120 lines and has one reason to change. One public type per file; a small companion record or enum may share the file.
- Prefer at most three parameters. Use primary constructors for dependency injection and `sealed` classes by default.
- Names come from the ubiquitous language: `Sku`, `Price`, `ProductPrice`, `Erp`, `Sap`. No `Helper`, `Manager`, `Util`, `Common`, and no `*Service` god classes.
- **No god class.** The MVP branch had a `PriceService` with the cache, the retry loop, two cancellation sources, five `catch` blocks, status decisions and logging in one class; this rewrite must not. A cross-cutting policy (retry, timeout) is a decorator of `IErpClient`. The use case (`GetProductPriceHandler`) only parses, calls the port and maps the outcome: it never creates a `CancellationTokenSource`, never calls `Task.Delay`, never loops.
- A method has at most one `try` and at most two `catch` blocks. More means it does several jobs: split it.
- No static mutable state, no service locator, no `dynamic`, no reflection in production code.
- Comments explain why, not what. XML doc comments only on `IErpClient` and the options classes.

## C# style

- .NET 10, C# latest, nullable enabled, warnings are errors. Fix warnings; do not suppress them.
- File-scoped namespaces. `var` wherever the type is obvious from the right-hand side.
- Do not use explicit `Func<...>` / `Action<...>` types. Prefer local functions or lambdas with inferred types (`var parse = (string s) => ...;`), and design APIs around small interfaces or classes instead of delegate parameters.
- `record`s for DTOs and value objects.
- Async all the way: `...Async` suffix, pass `CancellationToken` through, never `.Result` or `.Wait()`.
- Parse and format numbers and dates with `CultureInfo.InvariantCulture`.
- Public types are fine (tests use them directly); no `InternalsVisibleTo`.

## Tests

- xUnit v3 + `Assert`. Names describe the scenario and the result, e.g. `Unknown_sku_returns_404_sku_not_found`.
- `dotnet test` runs on Microsoft.Testing.Platform via the `"test": { "runner": "Microsoft.Testing.Platform" }` block in `global.json` (required by xunit.v3 4.x on the .NET 10 SDK); do not remove it.
- Every test is fast (well under 1 s). Timeouts and pauses in tests use small millisecond values set through options or `UseSetting`.
- Endpoint tests use `WebApplicationFactory<Program>` and replace `IErpClient` with `ScriptedErpClient` via `ConfigureTestServices`. Use a fresh factory and a fresh fake per test.
- `SapErpClient` is unit-tested with a stub `HttpMessageHandler`; end-to-end tests use the in-process SAP stub on the loopback interface. No other network in tests.

## Docs

- Markdown in English; diagrams in Mermaid.
- Use a plain hyphen `-`. Do not use em dashes or en dashes, in docs, code, comments, commit messages or reports.
- When the implementation deviates from `docs/architecture.md` or `docs/assumptions.md`, update the doc in the same step and say so in the report.
- After writing `README.md` to the author's computer, read it back and confirm that the content really landed. If it did not, write it again (up to 3 attempts) and report what happened.

## Language

Everything in the project is English: code, comments, docs, commit messages, and the reports and questions that agents write to the author.

## Workflow

- Branch `2026-10-20-new-vision`. One plan step at a time, one commit per step, then a separate tracker commit.
- Do not commit until the author replies "Approved". Do not push unless the step says so.
- Commit messages: Conventional Commits, exactly the message suggested in the step.
- The report format and the step prompts are in the plan, section "How to run a step".
