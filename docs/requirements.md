# Requirements

A retelling of the original assessment for agents and reviewers.
Original document: [IRCO - Technical Assessment.pdf](InitialRequirements/IRCO%20-%20Technical%20Assessment.pdf).

- Requirement IDs (`R1`, `R2`, ...) are referenced from the plan and the tests.
- Anything **not** stated in the original document is an assumption or a decision and lives in [assumptions.md](assumptions.md).
- How it is built is described in [architecture.md](architecture.md) (written in plan step 1).

## Goal

A small price lookup service. Given a product SKU, it returns a price from an ERP-like source instead of a hardcoded value.

## Code to write ("keep it small")

| ID | Requirement (from the assessment) | How we meet it |
|---|---|---|
| R1 | A backend endpoint, for example `GET /products/{sku}/price`. | Exactly this route. |
| R2 | The endpoint validates the SKU. | Value object `Sku.TryParse`, `400 invalid_sku`. |
| R3 | The endpoint calls an ERP client **interface**. | `IErpClient` (called by `GetProductPriceHandler`). |
| R4 | The endpoint maps the result to a response DTO. | `PriceResponse { sku, price, currency, lastUpdated }`. |
| R5 | The ERP client interface has a simulated implementation: a fake or in-memory stub that returns a price, a currency and a last-updated timestamp. | The real `SapErpClient` talks to `PriceLookup.SapStub`, a separate small app that imitates the SAP OData endpoint. Unit and endpoint tests also use the in-memory `ScriptedErpClient`. |
| R6 | The simulated implementation can be told to time out or fail. | Magic SKUs in the stub (`SLOW-1`, `FAIL-1`, `AUTH-1`, ...); `ScriptedErpClient` can hang or throw on demand. |
| R7 | The stub is included in the repo. | `tests/PriceLookup.SapStub`. Never deployed. |
| R8 | A small test suite covering at least: a found SKU, an unknown SKU, and an ERP timeout or failure. | We cover found, unknown, timeout **and** failure, plus validation, retries and configuration. |

## Written only (no code needed)

| ID | Requirement (from the assessment) | How we meet it |
|---|---|---|
| R9 | The frontend: SKU input, price display, loading and error states. | [frontend-bff.md](frontend-bff.md) (plan step 10). |
| R10 | The middleware: a BFF route or a server action. | Next.js App Router Route Handler; Server Action as the alternative. |
| R11 | Describe how they call the endpoint, what they handle in the UI, and how errors map from the backend. | Error mapping table in [frontend-bff.md](frontend-bff.md). |

## Architecture sketch

| ID | Requirement (from the assessment) | How we meet it |
|---|---|---|
| R12 | An architecture sketch (Markdown, a hand-drawn diagram, anything) that is accessible to the reviewers. | [architecture.md](architecture.md) with Mermaid diagrams (rendered by GitHub). |

## Submission

| ID | Requirement (from the assessment) | How we meet it |
|---|---|---|
| R13 | A public GitHub repo containing the code, tests, README and any sketch. | `https://github.com/walentyk/price-lookup-service`, branch `2026-10-20-new-vision` merged to `main` via a pull request. |

## Expectations

- The assessment budget is 1 to 2 days. Our own estimate is about 9 hours of agent work plus review time.
- "We don't expect production polish." Production concerns are described in the docs; they are coded only when [assumptions.md](assumptions.md) says so.

## Traceability

| Requirement | Plan step | Verified by |
|---|---|---|
| R1, R2, R3, R4 | Steps 3, 4, 5 | `SkuTests`, `GetProductPriceHandlerTests`, `PriceEndpointTests` |
| R5, R6, R7 | Steps 4, 6 (`ScriptedErpClient`), 7 (`SapErpClient`), 8 (stub) | `AttemptTimeoutErpClientTests`, `RetryingErpClientTests`, `TimeoutErpClientTests`, `StubScenarioTests` |
| R8 | Steps 5, 6, 8 | `PriceEndpointTests`, `StubScenarioTests` |
| R9, R10, R11 | Step 10 | Review of `docs/frontend-bff.md` |
| R12 | Step 1, aligned in Step 11 | Review of `docs/architecture.md` |
| R13 | Steps 11 and 14 | README, pull request to `main` |
