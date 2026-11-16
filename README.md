# Global Logistics Management System

GLMS is a .NET 10 application split into an ASP.NET Core MVC user interface and a separate ASP.NET Core Web API. Only the API owns SQL Server persistence and application rules; MVC calls it over HTTP and keeps the browser session in an encrypted authentication cookie.

## Architecture

- `GLMS.Core` contains the EF Core model, business rules, agreement storage, OTP verification and shared API contracts.
- `GLMS.Api` owns Identity, SQL Server, migrations, JWT authentication and the client, contract, service-request and dashboard endpoints.
- `GLMS.Web` renders the UI and uses typed HTTP clients. Its API delegating handler sends the JWT associated with the MVC cookie; MVC does not access EF Core or Identity directly.
- `GLMS.Tests` contains business-rule tests and API integration tests using an isolated in-memory database.

The API is the only migration owner. Apply schema changes through `GLMS.Api`, not the MVC project.

## Run locally

Prerequisites: .NET 10 SDK, SQL Server LocalDB, and the `dotnet-ef` tool.

1. In `GLMS.Api`, set `ConnectionStrings:DefaultConnection` using user secrets (or edit the local development setting):

   ```powershell
   dotnet user-secrets init --project GLMS.Api
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=GLMS;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True" --project GLMS.Api
   ```

2. Apply the API-owned migrations:

   ```powershell
   dotnet ef database update --project GLMS.Api --startup-project GLMS.Api
   ```

3. Run the API and MVC in separate terminals:

   ```powershell
   dotnet run --project GLMS.Api --launch-profile http
   dotnet run --project GLMS.Web --launch-profile http
   ```

   The API listens on `http://localhost:5265`; MVC listens on `http://localhost:5272`.

In Development, the API writes registration verification codes to its console instead of sending email. This is only for local testing. Other environments use SMTP; configure `SmtpEmail__Host`, `SmtpEmail__Port`, `SmtpEmail__EnableSsl`, `SmtpEmail__FromAddress`, `SmtpEmail__Username` and `SmtpEmail__Password` on the **API**. Keep credentials in user secrets or a secret store.

## Run with Docker Compose

Docker Compose starts SQL Server, the API and MVC, uses service-to-service networking, applies pending migrations from the API at startup and persists SQL data and agreement PDFs in named volumes.

```powershell
Copy-Item .env.example .env
# Edit .env and replace both sample values with unique local-only secrets.
docker compose up --build
```

Open `http://localhost:5081`. The API is available at `http://localhost:5080`; SQL Server is mapped to local port `14333`. Use a strong SQL password without semicolons because Compose injects it into a local connection string. In the Compose Development environment, verification codes appear in the API container logs (`docker compose logs -f api`). Do not use the example secrets outside a disposable local environment.

Stop the containers with `docker compose down`. The named volumes are retained; add `-v` only when intentionally deleting the local SQL database and agreement files.

## Tests and CI

Run all unit and API integration tests with:

```powershell
dotnet test GLMS.sln
```

The integration tests verify anonymous API protection, resource create/read behavior, and contract status/date filtering. GitHub Actions runs the solution tests, validates the Compose configuration and builds both container images.

## Test the API with Postman

Import `postman/GLMS API.postman_collection.json` and `postman/GLMS Local.postman_environment.json` in Postman, then select the **GLMS Local** environment. Start the API using the local setup above. In Postman's environment editor, set `email` to an address you can use for a new local test account and set `password` to a password that meets the Identity policy (at least 10 characters, upper- and lowercase letters, a digit, and a symbol).

Run **1 - API status / Health check** first; expect `200`. The next request verifies that an unauthenticated API call receives `401`. Then send **2 - Account / Register account**. In the API console, copy the `DEVELOPMENT ONLY` verification code into the environment's `otpCode` value and send **Verify email with OTP**. That request stores the JWT in the environment. The **Login** request can also obtain a token after verification. Protected client, contract, service request and dashboard requests use that token automatically.

Run the client requests in order: **List clients**, **Create client**, then **Get created client**. To test contracts, first create a client; then open **Create active contract (select a PDF)**, choose a real PDF in the `Agreement` form-data field, and send it. This request generates an active date range and unique reference. The contract ID is saved for the agreement download and service-request requests. Send **Change contract status to OnHold** only after testing service-request creation, because requests require an active contract. The invalid date filter request intentionally returns `400`. Requests that call the external exchange-rate provider need internet access.

Use a local development database for these tests. The default local connection is in `GLMS.Api/appsettings.json`; apply migrations using the command in the local setup section. If you choose Azure SQL instead, configure the connection on the API (not MVC), and ensure the Azure SQL firewall allows the API host. Never store real database credentials in the collection or environment export.

## POE Part 3 technical reflection

This implementation separates presentation from persistence: MVC consumes HTTP contracts rather than injecting the database context, and the API owns validation, authentication, business rules and storage. This lets the API be tested independently and allows the MVC and API containers to communicate through Compose without machine-specific addresses. Automated integration tests now exercise actual HTTP routing, authentication and data persistence instead of only isolated business rules. The multi-container setup also makes local dependencies explicit and repeatable.

The trade-offs are an additional service boundary, HTTP failure handling and JWT/cookie coordination. Development uses console OTP delivery and a local SQL container; production still requires managed secret storage, production email delivery, HTTPS/forwarded-header configuration, database access controls and durable agreement storage. Docker execution should be verified on a machine with Docker Desktop because it is not installed in every development environment.

## Azure deployment notes

Deploy the API and MVC as separate container images. Give the API a managed identity with database permissions, run migrations as a separate deployment step using an identity with DDL permissions, and keep runtime identities limited to required data access. Place the SQL and API endpoints on an appropriately secured network, store SMTP/JWT configuration in a secret store, and mount durable storage for agreement PDFs. Do not expose SQL Server publicly or widen its firewall to arbitrary client IP ranges.
