# price-lookup-service

A small .NET 10 Minimal API microservice for the IRCO technical assessment ([original PDF](docs/InitialRequirements/IRCO%20-%20Technical%20Assessment.pdf), [retelling with requirement IDs](docs/requirements.md)).
`GET /products/{sku}/price` validates the SKU, asks the ERP (SAP S/4HANA, OData V2 over HTTPS with an X.509 client certificate) for the price through the `IErpClient` port, and returns `{ sku, price, currency, lastUpdated }`.
Every error is an RFC 9457 `ProblemDetails` with a machine-readable `code`.
SAP calls get a per-attempt timeout, 3 retries with a constant pause and a total timeout.
SAP is simulated by `PriceLookup.SapStub`, a small separate app whose behaviour is selected by "magic" SKUs. The frontend and the BFF are described in a document, not built.

## Contents

- [Prerequisites](#prerequisites)
- [Quick start](#quick-start)
- [Tests](#tests)
- [The API](#the-api)
- [Configuration](#configuration)
- [Key decisions and trade-offs](#key-decisions-and-trade-offs)
- [Project layout](#project-layout)
- [Not included, next steps](#not-included-next-steps)
- [Documents](#documents)

## Prerequisites

- .NET 10 SDK. `global.json` pins `10.0.401` with `rollForward: latestFeature`, so any 10.0 SDK from 10.0.401 on works (`dotnet --list-sdks`).
- bash and `curl`. On Windows, Git Bash works.

No other tools, no Docker, no database. Nothing calls SAP or Azure.

## Quick start

All commands run from the repository root. Use three terminals.

**1. Build**

```bash
dotnet build PriceLookup.slnx
```

**2. Terminal 1: start the SAP stub** on `http://localhost:5199`

```bash
dotnet run --project tests/PriceLookup.SapStub
```

**3. Terminal 2: start the API** on `http://localhost:5080` (environment `Development`, which points `Sap:BaseUrl` at the stub)

```bash
dotnet run --project src/PriceLookup.Api
```

**4. Terminal 3: call it**

```bash
curl -s -i http://localhost:5080/health
curl -s -i http://localhost:5080/products/ABC-123/price
```

Try every magic SKU in one go:

```bash
for sku in ABC-123 abc-123 XYZ-999 NOPE-1 'bad%20sku' FLAKY-1 FAIL-1 AUTH-1 BROKEN-1 HANG-1 SLOW-1; do
  echo "== $sku"
  curl -s -w '\nHTTP %{http_code} in %{time_total}s\n' "http://localhost:5080/products/$sku/price"
  echo
done
```

Expected results with the default settings (the stub's behaviour is defined in [assumptions.md, A35](docs/assumptions.md#stub-and-testing)):

| SKU | What the stub does | API result |
|---|---|---|
| `ABC-123` | `200`, 19.99 EUR | `200`, `{"sku":"ABC-123","price":19.99,"currency":"EUR",...}` |
| `abc-123` | same SKU after normalisation | `200`, `sku` is `ABC-123` |
| `XYZ-999` | `200`, 1250.00 PLN | `200` |
| `NOPE-1` (any other valid SKU) | `404` with an SAP Gateway error body | `404 sku_not_found` |
| `bad%20sku` | not called | `400 invalid_sku` |
| `FLAKY-1` | `503` on 2 of every 3 calls, otherwise `200`, 5.00 EUR | `200` after 3 attempts (about 0.4 s) |
| `FAIL-1` | always `500` | `502 erp_unavailable` after 4 attempts (about 0.6 s) |
| `AUTH-1` | `401` | `502 erp_auth_failed` after 1 attempt |
| `BROKEN-1` | `200` with malformed JSON | `502 erp_invalid_response` after 1 attempt |
| `HANG-1` | hangs on calls 1, 4, 7, ..., otherwise `200`, 2.50 EUR | `200` after about 3.2 s: the first attempt is cut by the 3 s attempt timeout, the retry succeeds |
| `SLOW-1` | always waits 10 s | `504 erp_timeout` after about 7 s (the total timeout) |

`FLAKY-1` and `HANG-1` depend on the stub's per-SKU call counter, so the timings above hold for a freshly started stub; repeated calls go through the cycle again.
The same requests are in [PriceLookup.Api.http](src/PriceLookup.Api/PriceLookup.Api.http) for the VS Code REST Client or Visual Studio.

The API log shows one Warning per failed SAP attempt (`RetryingErpClient`) and one line per lookup result (`GetProductPriceHandler`). The stub logs every call with the SKU, the call number and the status it returned.

**5. Stop** both apps with `Ctrl+C`. On Windows `dotnet run` can leave the app process alive; clean up with:

```bash
taskkill //F //IM PriceLookup.Api.exe
taskkill //F //IM PriceLookup.SapStub.exe
```

## Tests

```bash
dotnet test PriceLookup.slnx
```

About 195 tests run in a few seconds. They use xUnit v3 on Microsoft.Testing.Platform (set in `global.json`), plain `Assert` and hand-written fakes, no mocking library. No test leaves the machine: the end-to-end tests host the stub in-process on a free loopback port.

| Level | What | Double |
|---|---|---|
| Architecture | `LayeringTests`: Domain, Application, Infrastructure and Api reference only what they may | none (`NetArchTest.Rules`) |
| Domain | `Sku`, `CurrencyCode`, `Price` invariants | none |
| Application | the use case; each resilience decorator; the full decorator chain; option validation | `ScriptedErpClient` (returns a price, fails N times, or hangs) |
| Infrastructure | `SapErpClient` and its parts (request, status classification, OData date and price parsing, TLS handler, certificate loading, SAP option rules) | `StubHttpMessageHandler`, self-signed certificates created in memory |
| Endpoint | status codes, JSON body, `ProblemDetails` with `code` and `traceId`, `/health`, framework errors | `WebApplicationFactory<Program>` with `ScriptedErpClient` |
| Startup | the whole host in `Production` refuses to start without a client certificate | `WebApplicationFactory<Program>` |
| End to end | every magic SKU through the real decorator chain and `SapErpClient`, with call counts | `PriceLookup.SapStub` in-process |

Timeouts in tests are small millisecond values set through options or `UseSetting`, so the timing tests stay fast. More detail: [architecture.md, Testing strategy](docs/architecture.md#testing-strategy).

## The API

### `GET /products/{sku}/price`

The SKU is trimmed and upper-cased, then must match `^[A-Z0-9][A-Z0-9._-]{0,39}$` (an SAP material number, up to 40 characters; [A11](docs/assumptions.md#erp-sap)).

`200 OK`:

```json
{
  "sku": "ABC-123",
  "price": 19.99,
  "currency": "EUR",
  "lastUpdated": "2025-10-19T10:00:00+00:00"
}
```

Errors are `application/problem+json` with the extensions `code` and `traceId`. `404` for an unknown SKU:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "SKU not found",
  "status": 404,
  "detail": "The ERP has no product with SKU 'NOPE-1'.",
  "instance": "GET /products/NOPE-1/price",
  "code": "sku_not_found",
  "traceId": "00-b697f781d250e9823683cefaaab9b6c3-f74615270a470de3-00"
}
```

| Situation | HTTP | `code` |
|---|---|---|
| SKU fails validation | 400 | `invalid_sku` |
| SAP says the SKU does not exist | 404 | `sku_not_found` |
| Retries exhausted on transient errors (SAP `5xx`, `408`, `429`, network error) | 502 | `erp_unavailable` |
| SAP rejects our certificate (`401` / `403`) | 502 | `erp_auth_failed` |
| SAP returns another `4xx` or a body we cannot parse | 502 | `erp_invalid_response` |
| Total timeout expired, or retries ran out and the last attempt hit the attempt timeout | 504 | `erp_timeout` |
| Unknown route | 404 | `route_not_found` |
| Wrong HTTP method | 405 | `method_not_allowed` |
| Unhandled exception | 500 | `internal_error` |
| Any other framework-generated error status | as returned | `http_error` |

The rule between `502` and `504`: the last attempt decides ([A16](docs/assumptions.md#timeouts-and-retries)). How a frontend maps these codes to UI messages: [frontend-bff.md, Error mapping](docs/frontend-bff.md#error-mapping-r11).

### Other routes

- `GET /health`: `200 Healthy`.
- `GET /openapi/v1.json`: the OpenAPI document, in `Development` only.

## Configuration

Locally the settings live in `appsettings.json` and `appsettings.Development.json`. In Azure they live in Key Vault: the only App Service app setting is `KeyVault__Uri`, the managed identity reads the vault at startup, and secret names use `--` as the separator (`Erp--RetryCount` is `Erp:RetryCount`). Key Vault values override `appsettings.json`; a change needs a restart. All options are validated at startup and the service refuses to start with invalid settings.

| Key | Default | Rule | Local | Azure |
|---|---|---|---|---|
| `Erp:TotalTimeoutMs` | `7000` | 1 to 60000 | `appsettings.json` | Key Vault `Erp--TotalTimeoutMs` (optional) |
| `Erp:AttemptTimeoutMs` | `3000` | 1 to 60000, not above `TotalTimeoutMs` | `appsettings.json` | Key Vault `Erp--AttemptTimeoutMs` (optional) |
| `Erp:RetryCount` | `3` (4 attempts) | 0 to 10 | `appsettings.json` | Key Vault `Erp--RetryCount` (optional) |
| `Erp:RetryDelayMs` | `200` | 0 to 10000 | `appsettings.json` | Key Vault `Erp--RetryDelayMs` (optional) |
| `Sap:BaseUrl` | `https://sap.example.invalid` (placeholder) | absolute; `https`, `http` only in `Development` | `appsettings.Development.json`: `http://localhost:5199` (the stub) | Key Vault `Sap--BaseUrl` |
| `Sap:ServicePath` | `/sap/opu/odata/sap/ZSD_PRODUCT_PRICE_SRV/` | not empty | `appsettings.json` | default, or Key Vault `Sap--ServicePath` |
| `Sap:SapClient` | `100` | three digits | `appsettings.json` | default, or Key Vault `Sap--SapClient` |
| `Sap:ClientCertificateBase64` | empty | base64 PFX with a private key; required outside `Development` | empty (the stub ignores certificates) | Key Vault `Sap--ClientCertificateBase64` |
| `Sap:ClientCertificatePassword` | empty | optional | empty | Key Vault `Sap--ClientCertificatePassword` |
| `KeyVault:Uri` | empty | when empty, Key Vault is not used | empty | app setting `KeyVault__Uri` |

The only relaxation is the `Development` environment (plain `http` to the stub, certificate optional). There is no setting that turns authentication off. The repository is public: `appsettings.json` holds defaults and placeholders only.

To see the startup validation, run the API outside `Development`. It refuses to start with `OptionsValidationException: Sap:ClientCertificateBase64 is required outside Development.` (an `http` `Sap:BaseUrl` would be reported too). Validation messages name the setting, never its value:

```bash
ASPNETCORE_ENVIRONMENT=Production dotnet run --project src/PriceLookup.Api --no-launch-profile
```

## Key decisions and trade-offs

- **DDD style in two projects.** `PriceLookup.Core` holds Domain (value objects `Sku`, `CurrencyCode`, `Price`, read model `ProductPrice`), Application (the `IErpClient` port, the use case `GetProductPriceHandler`, the resilience decorators) and Infrastructure (the SAP adapter) as folders. `PriceLookup.Api` is a thin HTTP shell. The domain is deliberately thin: this is a read-only query service. A test enforces the layer rules. ([A25 to A28](docs/assumptions.md#architecture-and-code-style))
- **Three small decorators instead of Polly.** `TimeoutErpClient(RetryingErpClient(AttemptTimeoutErpClient(SapErpClient)))`. The policy is trivial (a fixed retry count, a constant pause), so hand-written code is short and is part of what a reviewer reads. `SapErpClient` makes exactly one HTTP call and has no timeout of its own. ([A17](docs/assumptions.md#timeouts-and-retries))
- **Attempt timeout plus a total timeout that wins.** A hanging attempt is cut after 3 s and retried; the 7 s total budget covers every attempt and every pause and stops everything when it expires. A caller that disconnects is not reported as an ERP error. ([A12 to A18](docs/assumptions.md#timeouts-and-retries))
- **X.509 client certificate only.** TLS 1.2 or 1.3, the SAP server certificate validated with the platform's rules, the certificate loaded once at startup from a base64 PFX in Key Vault. No Basic auth, no API key, no switch to turn it off. ([A6 to A8, A23, A41](docs/assumptions.md#erp-sap))
- **No cache.** A price must be fresh and the assessment does not ask for one. If needed, it is one more decorator. ([A19](docs/assumptions.md#timeouts-and-retries))
- **A stub app, not only an in-memory fake.** The real `SapErpClient` talks HTTP to `PriceLookup.SapStub`, so the whole chain runs locally and in the end-to-end tests. Unit and endpoint tests use the in-memory `ScriptedErpClient`. Neither is ever deployed. ([A34 to A36](docs/assumptions.md#stub-and-testing))
- **Plain HTTP inbound.** The service is internal: no HTTPS redirection, no HSTS, no inbound authentication; network isolation is the control. ([A3](docs/assumptions.md#context-and-deployment))

The full design with Mermaid diagrams (system context, layers, main types, request flow, retry and timeout rules): [docs/architecture.md](docs/architecture.md).

## Project layout

```text
docs/                          requirements, assumptions, architecture, frontend and BFF, deployment, implementation plan
src/PriceLookup.Core/          no ASP.NET dependency
  Domain/                      Sku, CurrencyCode, Price, ProductPrice
  Application/Ports/           IErpClient, ErpException, ErpFailureKind
  Application/GetProductPrice/ the use case and its result types
  Application/Resilience/      the three decorators and ErpResilienceOptions
  Infrastructure/Sap/          SapErpClient, OData parsing, TLS handler, certificate, options validation
  DependencyInjection/         AddCore(configuration): composes everything
src/PriceLookup.Api/           Program.cs, endpoints, ProblemDetails, Key Vault configuration
tests/PriceLookup.Tests/       all automated tests; folders mirror the layers
tests/PriceLookup.SapStub/     the SAP stub app (never deployed)
```

`dotnet publish src/PriceLookup.Api` contains only the API and `PriceLookup.Core`; nothing in `src/` references `tests/`.

## Not included, next steps

- Inbound authentication (Entra ID) on top of network isolation.
- A short in-memory cache, as one more decorator.
- Circuit breaker, rate limiting.
- OpenTelemetry / Application Insights tracing and metrics.
- Bulk lookups and currency conversion.
- Automated certificate rotation.
- The real SAP contract (service, entity set, fields, which price, `sap-client`) agreed with the SAP team, and timeout values from the SAP SLA.
- Frontend code: the frontend and the BFF are described in [docs/frontend-bff.md](docs/frontend-bff.md), not built.
- CI is optional step 12 of the [plan](docs/ImplementationPlan/plan.md). The Azure deployment is described in [docs/deployment.md](docs/deployment.md) but not automated.

## Documents

| Document | What it is |
|---|---|
| [docs/requirements.md](docs/requirements.md) | The assessment retold, with requirement IDs `R1` to `R13` and where each one is met |
| [docs/assumptions.md](docs/assumptions.md) | Everything the assessment does not say: assumptions and decisions `A1` to `A41`, the SAP contract, the magic SKUs |
| [docs/architecture.md](docs/architecture.md) | The design: diagrams, error mapping, configuration, SAP client details, testing strategy |
| [docs/frontend-bff.md](docs/frontend-bff.md) | The Next.js frontend and BFF (written only): Route Handler, Server Action alternative, React hook and component, error mapping |
| [docs/deployment.md](docs/deployment.md) | Azure App Service and Key Vault with `az` CLI commands: resources, managed identity, secrets, deployment, certificate rotation |
| [docs/ImplementationPlan/plan.md](docs/ImplementationPlan/plan.md) | The step-by-step plan the service was built with, and the progress tracker |
| [AGENTS.md](AGENTS.md) | Rules for the coding agents that worked on this repository |
| [IRCO - Technical Assessment.pdf](docs/InitialRequirements/IRCO%20-%20Technical%20Assessment.pdf) | The original assessment |

## License

[MIT](LICENSE)
