# OrderRouter

A REST API that routes multi-item DME orders to suppliers based on product category, geographic coverage, and customer satisfaction score.

## Prerequisites

- **Docker + Docker Compose** — recommended, no SDK required
- **OR .NET 10 SDK** — for local development without Docker

## Running with Docker

```bash
docker compose up --build
```

The service starts on **http://localhost:8080**. The first startup seeds the database from the CSV files baked into the image — this takes a few seconds. The readiness endpoint reports when it's ready:

```bash
curl http://localhost:8080/health/ready
```

Data is persisted in a named Docker volume (`orderrouter_db`) and survives container restarts.

## Running Locally (without Docker)

```bash
cd OrderRouter.Api
dotnet run
```

Uses `appsettings.Development.json`, which points the database and CSV paths to relative local paths. The SQLite database (`orderrouter.db`) is created in the `OrderRouter.Api/` directory on first run. Swagger opens automatically at **http://localhost:5255/swagger**.

## API Reference

### POST /api/route

Routes an order to one or more suppliers. Always returns **HTTP 200** — check the `feasible` field to determine success.

**Request**

```json
{
  "order_id": "ORD-001",
  "customer_zip": "10014",
  "mail_order": true,
  "allow_partial": false,
  "items": [
    { "product_code": "OX-PORT-024", "quantity": 1 },
    { "product_code": "CM-BED-048", "quantity": 1 }
  ]
}
```

| Field | Type | Default | Description |
|---|---|---|---|
| `order_id` | string | required | Identifier echoed in the response |
| `customer_zip` | string | required | Customer postal code |
| `mail_order` | bool | `true` | When `true`, national mail-order suppliers are eligible in addition to local ones |
| `allow_partial` | bool | `false` | When `true`, route whatever items are possible and list unroutable items in `errors` |
| `items[].product_code` | string | required | Product code from `products.csv` |
| `items[].quantity` | int | `1` | Units requested |

**Successful response** (`feasible: true`)

```json
{
  "order_id": "ORD-001",
  "feasible": true,
  "routing": [
    {
      "supplier_id": "SUP-005",
      "supplier_name": "Respiratory Care Co Co",
      "items": [
        { "product_code": "OX-PORT-024", "quantity": 1, "fulfillment_mode": "local" },
        { "product_code": "CM-BED-048",  "quantity": 1, "fulfillment_mode": "local" }
      ]
    }
  ]
}
```

**Failed response** (`feasible: false`)

```json
{
  "order_id": "ORD-003",
  "feasible": false,
  "errors": [
    "No supplier can fulfill 'WK-STD-009' to ZIP 10001"
  ]
}
```

`fulfillment_mode` is either `"local"` (supplier physically serves the ZIP) or `"mail_order"` (supplier ships nationally). All items within a single supplier entry will always share the same mode — it is determined once at the supplier level, not per product. It is placed on each item by design so that clients can process items independently without needing to look up the parent route.

### GET /health/ready

Readiness probe used by Docker Compose and Kubernetes.

| Status | HTTP | Meaning |
|---|---|---|
| Healthy | 200 | Database accessible and both supplier/product tables populated |
| Degraded | 200 | Database accessible but a table is empty — routing will return infeasible until data is fixed |
| Unhealthy | 503 | Database not accessible |

### GET /swagger

Swagger UI for interactive exploration.

## Sample Orders

`data/sample_orders.json` contains seven sample orders that exercise different routing scenarios:

| Order | Scenario | Expected |
|---|---|---|
| `ORD-001` | Local routing — wheelchair + oxygen at a specific ZIP | `feasible: true`, single supplier |
| `ORD-002` | Multi-category at ZIP 77059 — hospital bed, patient lift, commode, BP monitor | `feasible: true`, greedy consolidation |
| `ORD-003` | Respiratory order with `mail_order: true` — CPAP + nebulizer | `feasible: true`, mail-order supplier |
| `ORD-004` | Two unknown product codes, `allow_partial: false` | `feasible: false`, two `Unknown product` errors |
| `ORD-005` | One valid + one unknown, `allow_partial: false` | `feasible: false`, one `Unknown product` error, valid item not routed |
| `ORD-006` | One valid + one unknown, `allow_partial: true` | `feasible: false`, valid item routed, unknown reports `Unknown product` |
| `ORD-007` | Seven categories, designed for multi-supplier stress test | `feasible: true`, single supplier (see note below) |

**Note on multi-supplier routing**: The current supplier dataset includes national suppliers (e.g. SUP-0460) that cover all 24 product categories with `serves_all_zips: true`. The greedy algorithm will always find one supplier that covers every requested product, so multi-supplier routing never occurs with this data. To validate multi-supplier splitting, use unit tests with controlled mock eligibility data.

**Note on extra fields**: The sample orders include `priority` and `notes` fields that are not part of the `RouteOrderRequest` contract. ASP.NET Core's JSON deserializer ignores unknown properties by default, so these fields are silently dropped when the payload is sent to `POST /api/route`. They serve as inline documentation within the file only.

## Data Files

| File | Description |
|---|---|
| `data/suppliers.csv` | Supplier catalogue — IDs, names, service ZIPs, product categories, satisfaction scores, mail-order flag |
| `data/products.csv` | Product catalogue — product codes, names, and categories |
| `data/sample_orders.json` | Seven example orders covering successful routing, infeasibility, and partial fulfilment |

ZIP ranges in `suppliers.csv` (e.g. `10001-10100`) are expanded on startup. Ranges wider than 5 000 ZIPs are treated as national coverage and stored as a `serves_all_zips` flag rather than individual rows.

## Running Tests

```bash
dotnet test
```

## Architecture

```
OrderRouter.Api          — ASP.NET Core web host, controller, health checks
OrderRouter.Services     — Business logic, EF Core DbContext, CSV seeding
OrderRouter.Services.Tests — Unit tests
```

**Routing algorithm** — Greedy set cover: at each step, pick the supplier that covers the most remaining unassigned products. Tie-breaks favour higher satisfaction scores; among equal scores, local suppliers are preferred over mail-order.

**Database** — SQLite, seeded from CSV on startup. Schema is managed via EF Core migrations (`MigrateAsync` on startup). The database file is stored outside the CSV data directory so a Docker volume mount does not shadow the baked-in seed files.
