# Mock Payment Provider

## What this is
A stand-in for an external payment vendor. The Zain Recharge API sends it payments and it answers with the simulated results from the assignment. It has its own database and shares no code with the Zain solution.

```
Zain Recharge API (:5200)  -->  Mock Payment Provider (:5100)  -->  MockPaymentProviderDb
```

## Prerequisites
- .NET 10 SDK
- SQL Server LocalDB (installed with Visual Studio)
- `dotnet-ef` tool: `dotnet tool install --global dotnet-ef`
- Seq for logs, listening on http://localhost:5341

## Setup
1. Clone this repository and open a terminal in its folder.
2. Start Seq and check http://localhost:5341 opens.
3. Create the database:
   ```
   dotnet ef database update --project src/MockPaymentProvider.Infrastructure --startup-project src/MockPaymentProvider.Api
   ```
4. Run:
   ```
   dotnet run --project src/MockPaymentProvider.Api
   ```
5. Open Swagger at http://localhost:5100/swagger

## Configuration
Real secrets never go into Git. `appsettings.json` has empty values; `appsettings.Development.json` has fake values that only work on a developer laptop.

| Setting | Meaning | Example |
|---|---|---|
| ConnectionStrings:Default | The provider database | Server=(localdb)\MSSQLLocalDB;Database=MockPaymentProviderDb;Trusted_Connection=True;TrustServerCertificate=True |
| Security:ApiKey | The value callers must send in X-Api-Key | dev-only-provider-api-key |
| Security:ClientId | Client id accepted by /api/auth/token | zain-recharge-dev |
| Security:ClientSecret | Client secret accepted by /api/auth/token | dev-only-client-secret |
| Security:JwtSigningKey | Signs the bearer tokens (at least 32 characters) | dev-only-signing-key-change-me-at-least-32-chars |
| Security:TokenLifetimeSeconds | How long a token lives | 300 |
| Simulation:MaxSuccessAmount | Largest amount that succeeds | 20 |
| Simulation:OutageMobileNumber | Number that gives 503 | 0790000000 |
| Simulation:SlowMobileNumber | Number that answers slowly | 0799999999 |
| Simulation:SlowDelaySeconds | The delay for that number | 10 |

In a real environment the values come from environment variables or a secret store.

## Ports
| Service | Port |
|---|---|
| Mock Payment Provider | 5100 |
| Zain Recharge API | 5200 |
| Seq | 5341 |

## API contract
Security has two layers, checked in this order: the `X-Api-Key` header, then the bearer token, then the request body.

| Method | URL | Needs | Purpose |
|---|---|---|---|
| POST | /api/auth/token | X-Api-Key | Exchange client id and secret for a bearer token |
| POST | /api/payments/process | X-Api-Key and bearer token | Process a payment |
| GET | /api/payments/{transactionId} | X-Api-Key and bearer token | Status inquiry |
| GET | /health | nothing | Service and database up |

Token request and response:
```
POST /api/auth/token
X-Api-Key: dev-only-provider-api-key
Content-Type: application/json

{ "clientId": "zain-recharge-dev", "clientSecret": "dev-only-client-secret" }
```
```json
{ "accessToken": "eyJhbGciOi...", "tokenType": "Bearer", "expiresInSeconds": 300 }
```

Payment request and response:
```
POST /api/payments/process
X-Api-Key: dev-only-provider-api-key
Authorization: Bearer eyJhbGciOi...
Content-Type: application/json

{ "transactionId": "TXN-1001", "mobileNumber": "0791234567", "amount": 5.00 }
```
```json
{
  "transactionId": "TXN-1001",
  "paymentReference": "PAY-50001",
  "status": "SUCCESS",
  "message": "Payment completed successfully"
}
```
The `transactionId` is chosen by the caller (the Zain API) and works as an idempotency key.

## Status codes
| Endpoint | Case | Code | Saved |
|---|---|---|---|
| POST /api/auth/token | Valid key and credentials | 200 | nothing |
| | Missing or wrong X-Api-Key | 401 | nothing |
| | Wrong clientId or clientSecret | 401 | nothing |
| | Missing field in the body | 400 | nothing |
| POST /api/payments/process | Amount from 1 to 20 | 200, SUCCESS | payment SUCCESS |
| | Amount above 20 | 200, FAILED | payment FAILED |
| | Same transactionId and same body again | 200, the stored result | nothing new |
| | Same transactionId, different body | 409 | nothing new |
| | Amount 0 or less, bad number, empty transactionId | 400 | nothing |
| | Mobile number 0790000000 | 503 | nothing |
| | Mobile number 0799999999 | 200 after 10 seconds | payment as usual |
| | Missing or wrong X-Api-Key | 401 | nothing |
| | Missing, invalid or expired token | 401 | nothing |
| | Unexpected exception | 500 | nothing |
| GET /api/payments/{transactionId} | Payment exists / does not exist | 200 / 404 | nothing |

## Business rules
- Amount from 1 to 20: saved as SUCCESS with a payment reference (`PAY-` plus 50000 plus the identity number).
- Amount above 20: saved as FAILED, no reference.
- A repeated request with the same transactionId and the same body returns the stored result and creates nothing new.
- The same transactionId with a different body is a conflict (409).
- Mobile numbers are masked in logs (0791***567).

## Logging
Serilog writes to the console and to Seq (http://localhost:5341). The `X-Correlation-Id` sent by the Zain API is attached to every log line, so one payment can be followed across both systems. Secrets are never logged. Useful searches:
```
Application = 'MockPaymentProvider'
TransactionId = 'TXN-1001'
@Level = 'Error'
```

## Testing
- Unit tests: `dotnet test`
- Postman: import `postman/MockPaymentProvider.postman_collection.json` and `postman/Local.postman_environment.json`, select the Local environment, and run the folder.
- Database script: `docs/schema.sql`

## Assumptions
- The provider decides the final status; the caller never sends one.
- Amounts of 0 or less are validation errors (400), not declined payments.
- The outage and slow numbers come from the assignment and are configurable.

## Known limitations
- A single client id and secret; no client management.
- No refresh tokens: the caller asks for a new token when the old one expires.
- Payments are simulated; no real money moves.

