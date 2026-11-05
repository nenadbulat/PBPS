# Payment Batch Processing Service

Ripple Treasury — Payment Batch Processing Service

A .NET 10 service for accepting and asynchronously processing batches of outbound payments.

The implementation demonstrates tenant-scoped batch creation, request idempotency, persistent payment state, simulated bank submission and an audit trail of payment attempts and status transitions.

This is a focused technical implementation rather than a production-ready banking integration.

For the architecture, design decisions and production considerations, see [SYSTEM_DESIGN.md](SYSTEM_DESIGN.md).

## 1. Prerequisites

To build and run the application locally, you will need:

- .NET 10 SDK
- Visual Studio 2026 (optional; recommended for Windows development)
- SQL Server Express LocalDB
- Postman or another HTTP client

The application uses SQL Server LocalDB for development and a separate LocalDB database for integration tests.

## 2. Database Setup

### 2.1 Connection string

The development connection string is configured in `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=PBPSDB;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

The development database is named `PBPSDB`.

LocalDB must be installed and available on the machine.

### 2.2 Apply database migrations

Open the solution in Visual Studio.

Navigate to **Tools → NuGet Package Manager → Package Manager Console**.

Select `PaymentBatchProcessingService.Infrastructure` as the **Default project**.

Run:

```powershell
Update-Database -StartupProject PaymentBatchProcessingService -Context PaymentsDbContext
```

This applies the existing EF Core migrations and creates or updates the `PBPSDB` database.

The development seed data is inserted when the application starts in the Development environment.

### 2.3 Development seed data

The application provides development-only sample data for testing tenant and legal-entity ownership.

| Entity | Identifier |
|---|---|
| Tenant A | `11111111-1111-1111-1111-111111111111` |
| Tenant B | `22222222-2222-2222-2222-222222222222` |
| Legal Entity A | `33333333-3333-3333-3333-333333333333` |
| Source Bank Account A | `44444444-4444-4444-4444-444444444444` |
| Legal Entity B | `77777777-7777-7777-7777-777777777777` |
| Source Bank Account B | `88888888-8888-8888-8888-888888888888` |

Tenant A's source account uses `EUR`. Tenant B's source account uses `USD`.

The seed operation is idempotent and intended for local development only.

## 3. Build and Run

### 3.1 Build the solution

From the solution directory:

```powershell
dotnet restore
dotnet build
```

Alternatively, build the solution directly in Visual Studio.

### 3.2 Start the API

Set `PaymentBatchProcessingService` as the startup project.

Run the application using Visual Studio, or from the solution directory:

```powershell
dotnet run --project PaymentBatchProcessingService --environment Development
```

Use the HTTP or HTTPS base address shown in the application output.

The application hosts both the Minimal API and a background payment processing worker.

The worker polls for accepted payments approximately every five seconds when idle.

### 3.3 Simulated bank behaviour

In the Development environment, the application uses `FakeBankPaymentGateway`.

It accepts payment submissions and generates a reference in the following format:

```text
FAKE-{paymentId}
```

No real banking partner is contacted and no funds are transferred.

The fake gateway is registered only in Development. The application deliberately fails at startup in other environments because a real bank gateway has not been configured. This prevents accidental use of simulated payment submission in a production environment.

## 4. Testing the API with Postman

The following walkthrough uses the development seed data.

Replace `{{baseUrl}}` with the URL shown when the API starts, for example `https://localhost:<port>`.

### 4.1 Create a payment batch

**Method:** `POST`

**URL:**

```text
{{baseUrl}}/payment-batches
```

**Headers:**

| Header | Value |
|---|---|
| `Content-Type` | `application/json` |
| `X-Tenant-Id` | `11111111-1111-1111-1111-111111111111` |
| `Idempotency-Key` | `demo-batch-001` |

**Body → raw → JSON:**

```json
{
  "legalEntityId": "33333333-3333-3333-3333-333333333333",
  "sourceBankAccountId": "44444444-4444-4444-4444-444444444444",
  "currency": "EUR",
  "payments": [
    {
      "amount": 125.50,
      "beneficiaryName": "Example Supplier Ltd",
      "beneficiaryAccountIdentifier": "GB29NWBK60161331926819",
      "reference": "INV-1001"
    },
    {
      "amount": 250.00,
      "beneficiaryName": "Example Services Ltd",
      "beneficiaryAccountIdentifier": "GB82WEST12345698765432",
      "reference": "INV-1002"
    }
  ]
}
```

**Expected result:**

- HTTP `201 Created`.
- A new batch identifier.
- The batch is initially created with status Accepted, but asynchronous processing may advance its status before the response is received.

Copy the returned batch identifier for the next requests.

### 4.2 Retrieve the batch

**Method:** `GET`

**URL:**

```text
{{baseUrl}}/payment-batches/{{batchId}}
```

**Header:**

```text
X-Tenant-Id: 11111111-1111-1111-1111-111111111111
```

**Expected result:**

HTTP `200 OK`, with the current batch status.

Depending on when the request is made, the batch may be `Accepted`, `Processing` or `Completed`.

Wait a few seconds and repeat the request until the batch reaches `Completed`.

**Important:** `Completed` means submission processing has finished. It does not mean that the payments have settled.

### 4.3 Retrieve payments within the batch

**Method:** `GET`

**URL:**

```text
{{baseUrl}}/payment-batches/{{batchId}}/payments
```

**Header:**

```text
X-Tenant-Id: 11111111-1111-1111-1111-111111111111
```

**Expected result:**

HTTP `200 OK`, containing the payments and their current statuses.

With the simulated bank adapter, both payments should eventually reach `Submitted` and receive generated bank references.

`Submitted` indicates bank submission acceptance, not settlement.

### 4.4 Verify idempotent batch creation

Repeat the original POST request with:

- The same tenant.
- The same `Idempotency-Key`.
- The same JSON body.

**Expected result:**

HTTP `200 OK`, returning the previously created batch rather than creating another one.

Now change one payment amount but keep the same idempotency key.

**Expected result:**

HTTP `409 Conflict`, because the same key cannot be reused for a different request.

For a genuinely new batch, use a new idempotency key, such as `demo-batch-002`.

### 4.5 Verify tenant isolation

Repeat the GET request for Tenant A's batch, but change the header to:

```text
X-Tenant-Id: 22222222-2222-2222-2222-222222222222
```

**Expected result:**

HTTP `404 Not Found`.

The batch must not be accessible through another tenant's context.

Note that the `X-Tenant-Id` header is a development-only mechanism and does not provide production authentication.

### 4.6 Inspect persisted audit data

The database also stores payment attempts and status history.

After both payments have been processed successfully, the expected database state is:

| Record | Expected result |
|---|---|
| Payment batch | `Completed` |
| Payments | 2 × `Submitted` |
| Payment attempts | 2 × `Accepted` |
| Payment status history | 4 records |

Each payment produces two status transitions:

```text
Accepted → Processing → Submitted
```

The public API exposes batch and payment status. Payment attempts and full status history can be inspected in SQL Server or through integration tests; they are not exposed through separate API endpoints in this MVP.

## 5. Running Automated Tests

The solution includes two NUnit test projects:

- `PaymentBatchProcessingService.UnitTests`
- `PaymentBatchProcessingService.IntegrationTests`

### 5.1 Unit tests

Unit tests exercise domain rules, application behaviour and processing outcomes using test doubles.

Run them from the solution directory:

```powershell
dotnet test PaymentBatchProcessingService.UnitTests
```

### 5.2 Integration tests

Integration tests exercise the real EF Core persistence implementation using SQL Server LocalDB.

They use a dedicated database:

```text
PaymentBatchProcessingService_IntegrationTests
```

This is separate from the development database `PBPSDB`. A database guard validates the configured database name and LocalDB instance before a test context is created.

Run:

```powershell
dotnet test PaymentBatchProcessingService.IntegrationTests
```

The integration tests apply EF Core migrations to their test database and create the records needed for each scenario.

The tests cover:

- Successful payment processing, including persisted payment attempts, status history and batch completion.
- Uncertain bank outcomes, including timeouts, accepted responses without a valid bank reference and missing bank responses.
- Prevention of automatic resubmission for payments with `Unknown` outcomes.
- Persistence of a known bank response after worker cancellation.
- Concurrent batch creation using the same tenant and idempotency key with matching request fingerprints, verifying that only one batch, payment and idempotency record are persisted.
- Concurrent batch creation using the same tenant and idempotency key with different request fingerprints, verifying that one creation succeeds, the other results in an idempotency conflict, and no duplicate records are persisted.
- Transaction rollback when a database foreign key constraint is violated, verifying that no partial batch, payment or idempotency records remain.

The concurrency tests verify database-backed idempotency during concurrent batch creation attempts. They do not establish safe payment claiming across multiple worker instances or guarantee that a database uniqueness conflict occurs during every test execution.

### 5.3 Run all tests

From the solution directory:

```powershell
dotnet test
```

Tests can also be run through Visual Studio Test Explorer.

SQL Server LocalDB must be available for the integration tests.

## 6. Key Design Decisions

The implementation makes several deliberate trade-offs:

**Asynchronous processing:** Batch creation returns before bank submission, avoiding long-running HTTP requests.

**SQL Server persistence:** Relational constraints and transactions protect related financial records.

**Request idempotency:** A tenant-scoped idempotency key prevents duplicate batch creation.

**Explicit audit records:** Payment attempts and status history are persisted separately from the current payment state.

**Unknown outcomes:** A bank timeout or uncertain response does not trigger an automatic retry, because the bank may already have received the instruction.

**Single-instance worker:** Processing is sequential. The current database work-selection logic is not safe for multiple active workers.

These decisions and their alternatives are discussed in [SYSTEM_DESIGN.md](SYSTEM_DESIGN.md).

## 7. Assumptions and Limitations

This implementation is intended to demonstrate the core payment processing workflow.

It has the following limitations:

- Bank submission is simulated; no real money is moved.
- The background worker is designed for a single active instance.
- Work claiming is not atomic across multiple workers.
- Payments left in `Processing` after a process crash are not automatically recovered.
- Unknown bank outcomes require reconciliation before any safe resubmission.
- Real bank-side idempotency is not implemented.
- Authentication and authorisation are not production-ready.
- The tenant header is intended only for local development.
- Settlement confirmation, foreign exchange and payment approval are outside the implemented scope.
- Batch `Completed` means submission processing is finished, not that every payment succeeded or settled.

The production roadmap in [SYSTEM_DESIGN.md](SYSTEM_DESIGN.md) describes how these limitations would be addressed.

## 8. Further Documentation

See [SYSTEM_DESIGN.md](SYSTEM_DESIGN.md) for:

- System architecture and component responsibilities.
- Data model and transaction boundaries.
- Idempotency, concurrency and consistency.
- Failure handling and reconciliation.
- Security and observability.
- Proposed Azure deployment.
- Production roadmap.

## 9. AI Tooling

ChatGPT was used to support code review, explore architectural alternatives, refine validation and failure handling, develop test cases, and improve documentation clarity.

AI-assisted changes were reviewed during development. I remain responsible for the design decisions, implementation, and verification.