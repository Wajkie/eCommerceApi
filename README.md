# eCommerceApi

Multi-tenant e-commerce REST API built with ASP.NET Core (.NET 10). Designed to serve any store at any scale — from a developer managing ten small shops to a retailer running a high-traffic global operation — without changing a line of platform code.

Each store is a fully isolated tenant with its own API key, data scope, webhook endpoint, and optional identity provider. The platform handles the infrastructure; stores plug in their own auth and receive events over webhooks.
[Store template](https://wajkiestoretemplate.netlify.app/)
## Stack

- **ASP.NET Core (.NET 10)** — REST API
- **Entity Framework Core + Pomelo** — MySQL via two contexts (central registry + tenant data)
- **SignalR** — real-time metrics and admin session management
- **OtpNet** — TOTP for admin authentication
- **Scalar** — API explorer (development)
- **xUnit + Moq** — 88 passing tests

## Architecture

### Multi-tenancy

Every request carries two headers:

| Header | Purpose |
|--------|---------|
| `X-Store-Id` | Identifies the tenant (GUID) |
| `X-Api-Key` | Authenticates write operations (HMAC-SHA256 hashed at rest) |

`TenantMiddleware` resolves the store from these headers on every request, scoping all downstream queries to that store's data. Tenant isolation is enforced at the database level — queries that reach the wrong store are structurally impossible.

### Databases

| Context | Purpose |
|---------|---------|
| `CentralContext` | Store registry, admin config |
| `EcommerceContext` | Per-tenant data (products, orders, customers, carts) |

Both databases run migrations on startup automatically.

### Request Pipeline

```
CORS → GlobalExceptionHandler → MetricsMiddleware → TenantMiddleware
→ JwtTenantMiddleware → RateLimiter (60 req/min) → OutputCache → Controllers
```

### BYOIDP (Bring Your Own Identity Provider)

Stores can provide a `JwksUri` on onboarding. `JwtTenantMiddleware` validates Bearer tokens against that endpoint, allowing stores to use any OIDC-compatible identity provider (Auth0, Clerk, Cognito, etc.) without the platform touching credentials.

### Webhooks

`WebhookDispatchService` runs as a singleton hosted service, consuming events from an in-memory `Channel<WebhookEvent>`. Each delivery is HMAC-SHA256 signed with the store's webhook secret. Failed deliveries retry at 0 s / 2 s / 8 s automatically.

### Admin Authentication

Admin routes are protected by TOTP (Google Authenticator).

- `POST /api/Admin/setup` — one-time setup, returns a QR URI for Authenticator scanning
- The dashboard connects to `AdminHub` over WebSocket, authenticates once with a TOTP code, and receives a session token
- All subsequent admin HTTP requests use `X-Admin-Session` — no rotating codes needed while the tab is open
- Session is revoked automatically on WebSocket disconnect (tab close = sign out)
- Fallback: `X-Admin-Code` for Scalar / Postman access

## Getting Started

### Prerequisites

- .NET 10 SDK
- MySQL 5.7+
- dotnet user-secrets

### Configuration

The connection string is managed via user secrets and never stored in `appsettings.json`:

```powershell
cd eCommerceApi/eCommerceApi
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=...;Database=...;User=...;Password=...;"
```

### Run

```powershell
cd eCommerceApi/eCommerceApi
dotnet run
```

- API: `http://localhost:5041`
- Scalar UI: `http://localhost:5041/scalar/v1`
- Health check: `http://localhost:5041/health`

Migrations run on startup — no manual setup needed.

### Admin Setup

On first run, call the setup endpoint to link Google Authenticator:

```
POST /api/Admin/setup
```

Returns an `otpauth://` QR URI. Scan it with Google Authenticator. This endpoint disables itself after first use.

## API Reference

### Stores

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `POST` | `/api/Stores/Onboard` | Admin session | Onboard a new store, returns API key |
| `GET` | `/api/Stores` | Admin session | List all stores |
| `GET` | `/api/Stores/{id}/Metrics` | `X-Api-Key` | Store-level metrics |

Onboard body:
```json
{
  "storeName": "My Shop",
  "jwksUri": "https://idp.example.com/.well-known/jwks.json",
  "webhookUrl": "https://myshop.com/webhooks",
  "webhookSecret": "at-least-32-characters"
}
```

The API key is returned once on onboarding and never stored in plaintext.

---

### Products

`X-Store-Id` required. Write operations additionally require `X-Api-Key`.

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/Products` | Paginated list, filterable by slug / tag / search / price range. Cached 60 s per store. |
| `GET` | `/api/Products/{id}` | Get by ID |
| `POST` | `/api/Products` | Create |
| `PUT` | `/api/Products/{id}` | Update |
| `DELETE` | `/api/Products/{id}` | Delete |

---

### Carts

`X-Store-Id` required.

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/Carts/current?customerId=&storeId=` | Get or create active cart |
| `GET` | `/api/Carts/{cartId}` | Get cart |
| `POST` | `/api/Carts/{cartId}/items` | Add item |
| `PUT` | `/api/Carts/{cartId}/items/{itemId}` | Update quantity |
| `DELETE` | `/api/Carts/{cartId}/items/{itemId}` | Remove item |
| `DELETE` | `/api/Carts/{cartId}/items` | Clear cart |
| `POST` | `/api/Carts/{cartId}/checkout` | Convert cart to order, decrements stock |

---

### Orders

`X-Store-Id` + `X-Api-Key` required.

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/Orders` | Paginated list |
| `GET` | `/api/Orders/{id}` | Get by ID |
| `POST` | `/api/Orders` | Create (accepts optional `idempotencyKey`) |
| `PATCH` | `/api/Orders/{id}/status` | Update status and tracking number |

Orders support idempotency — submit the same `idempotencyKey` (GUID) twice and you get the original order back, never a duplicate.

---

### Customers

`X-Store-Id` required. List and order history additionally require `X-Api-Key`.

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/Customers` | Paginated list |
| `GET` | `/api/Customers/{id}` | Get by ID |
| `POST` | `/api/Customers` | Create |
| `GET` | `/api/Customers/{id}/orders` | Order history (paginated) |

---

### Metrics & Health

No tenant headers required.

| Route | Description |
|-------|-------------|
| `GET /api/Metrics/recent` | Recent requests |
| `GET /api/Metrics/endpoint/{endpoint}/stats` | Avg / p95 / p99 / error rate per endpoint |
| `GET /api/Metrics/health` | All service health statuses |
| `GET /api/Metrics/health/{serviceName}` | Single service health |
| `GET /api/Metrics/security/blocked-ips` | Blocked IPs |
| `POST /api/Metrics/security/block-ip/{ip}` | Block IP |
| `POST /api/Metrics/security/unblock-ip/{ip}` | Unblock IP |
| `GET /health` | Overall health check (DB + cache) |

---

### SignalR Hubs

| Hub | Route | Description |
|-----|-------|-------------|
| MetricsHub | `/hubs/metrics` | Streams live request metrics to the admin dashboard |
| AdminHub | `/hubs/admin` | TOTP authentication and session management |

## Error Handling

All errors return RFC 9457 `ProblemDetails`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "NOT_FOUND",
  "status": 404,
  "detail": "Product not found"
}
```

The `title` field carries a machine-readable error code (`NOT_FOUND`, `BAD_REQUEST`, `INTERNAL_ERROR`). Controllers have zero catch blocks — domain exceptions propagate to `GlobalExceptionHandler`.

## Roadmap

The platform is functional but not yet production-hardened for horizontal scaling. The following are planned and architecturally accounted for — the swap points are already isolated in the codebase.

### Redis (not implemented)

**Session and cache distribution**
Currently `IMemoryCache` is used for store lookups and output caching. This works on a single instance but breaks under a load balancer. The plan is to swap to `IDistributedCache` (StackExchange.Redis) with a managed provider (Upstash, Redis Cloud, etc.). The cache layer is already abstracted behind interfaces, so this is a targeted swap with no controller or service changes.

**Webhook retry persistence**
Pending webhook retries live in `Channel<WebhookEvent>` (in-memory). A process restart loses any queued events. The fix is a Redis-backed queue with a polling service that survives restarts and can be spread across instances. The `WebhookDispatchService` is already a singleton hosted service — the channel swap is self-contained.

**Cart stock reservation**
When two customers race to buy the last unit of a product, the current flow only rejects at order creation time — by then, the losing customer has already filled in a checkout form. The right fix is a short-lived Redis reservation: when a product is added to a cart, a TTL key (`reserve:{storeId}:{externalId}`) decrements the available counter atomically. The product catalog and cart UI read from this counter so items appear unavailable before anyone reaches checkout. Reservations expire automatically (e.g. 15 min) if the cart is abandoned, releasing stock back to the pool. This requires a Redis connection from the store frontend (Upstash HTTP API is a good fit for edge/serverless environments) and a small reservation Netlify Function. The API's final stock check at `POST /api/Orders` remains as the authoritative guard.

### Structured logging (not implemented)

Default `Microsoft.Extensions.Logging` is in place. Serilog with an appropriate sink (disk, database, or cloud logging) will be added once the deployment target is decided — the sink choice is a deployment concern, not a code concern.

### Store frontend template (not implemented)

The `WebhookUrl` and `JwksUri` fields on store onboarding require a deployable store frontend to be meaningful. A Vite + React store template is planned, intended for one-click deploy to Netlify or similar. This unblocks real end-to-end onboarding flows.

### API versioning (not implemented)

No `v1/v2` prefix on routes today. Will be introduced when the first breaking change requires it.

---

## Tests

```powershell
cd eCommerceApi.Tests
dotnet test
```

88 passing tests across: shopping cart flows, order idempotency, customer management, product catalog, store onboarding, TOTP admin auth, JWT middleware, webhook dispatch, security validation, and global exception handling.
