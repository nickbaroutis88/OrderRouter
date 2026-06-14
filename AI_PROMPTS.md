# AI Prompts

Significant prompts used with Claude (claude-sonnet-4-6) during this exercise, in order.

---

## 1. Solution structure and technology choices

> Set up a .NET 10 solution for a multi-supplier order routing service. I want three projects: a web API project (OrderRouter.Api), a class library for all business logic and data access (OrderRouter.Services), and a test project (OrderRouter.Services.Tests). Use SQLite as the database via EF Core. Keep the project references clean — the API project depends on Services, the test project depends on Services.

---

## 2. Data model and store

> Design the EF Core entity model for the routing service. I need Supplier, Product, SupplierCategory, and SupplierZipCode entities. Suppliers have a many-to-many relationship with categories and a one-to-many with ZIP codes. Add a ServesAllZips boolean flag on Supplier so national suppliers don't need thousands of ZIP rows. Configure unique indexes and cascade deletes in OnModelCreating. Put everything under OrderRouter.Services/Store/.

---

## 3. CSV seeding with defensive parsing

> Implement CSV seeding that reads suppliers and product data from CSV files on startup. The data is messy — the supplier CSV has a header typo (`suplier_name` instead of `supplier_name`), a trailing question mark on `can_mail_order?`, satisfaction scores that say "no ratings yet" instead of a number, and ZIP fields that are either explicit comma-separated lists or ranges like `10001-10100`. Handle all of this defensively without crashing. Treat any ZIP range wider than 5000 entries as national coverage and set ServesAllZips=true instead of expanding the range. Put the parsers and the seeder in OrderRouter.Services/Store/Seeding/.

---

## 4. API models with DataContract serialisation

> Create the request and response models under OrderRouter.Services/Models/. Use DataContract and DataMember attributes with snake_case names so the JSON output matches the spec exactly. Split into separate files: RouteOrderRequest, RouteOrderResponse, OrderItem, SupplierRoute, RoutedItem. Use EmitDefaultValue=false on optional fields so they are omitted from the JSON when null. Add XML doc comments on all public properties explaining their semantics.

---

## 5. Eligibility resolver — separate class, optimised DB access

> I want the supplier eligibility logic in its own class, not mixed into the operation. Create IOrderEligibilityResolver and OrderEligibilityResolver under OrderRouter.Services/Resolvers/. The resolver should hit the database directly via DbContext and resolve eligibility in exactly two queries: one for the requested products, one for all suppliers that have at least one matching category and are reachable (local ZIP match, ServesAllZips, or mail-order eligible). Everything after that should be done in memory.

> Now optimise the in-memory BuildEligibilityMap. The naive approach iterates all suppliers for every product, which is O(P×S). I want a two-pass algorithm: first pass indexes suppliers by category (O(S×C)), second pass resolves each product with a dictionary lookup (O(P)). Document the complexity in a comment so it's clear why this ordering was chosen.

---

## 6. Greedy set cover strategy — optimised with incremental coverage counts

> Implement the routing algorithm as a separate IRoutingStrategy / GreedySetCoverStrategy under OrderRouter.Services/Routing/. Use greedy set cover: at each step pick the supplier that covers the most still-unassigned products.

> Optimise it. The naive approach recomputes each supplier's coverage count from scratch every round, which is O(S×P) per round. Instead, build a forward index (supplierId → products) once at setup in O(P×S), then maintain coverage counts as integers and decrement them incrementally when a product gets assigned. That makes each round a simple integer scan rather than a full eligibility recomputation.

> Tie-breaking should apply priority 3 (higher satisfaction score wins) then priority 4 (local delivery preferred over mail_order when scores are equal). Document the complexity analysis and explain why greedy set cover is the correct algorithm here, not a compromise.

---

## 7. Mapper and operation orchestration

> Add a RoutingMapper under OrderRouter.Services/Mappers/ that converts SupplierCandidate + OrderItem collections into SupplierRoute response objects. Then create RoutingOperation under OrderRouter.Services/Operations/ that orchestrates the full pipeline: validate input → resolve eligibility → run the strategy → map to response. The operation should have no direct database dependency — all DB access stays in the resolver. Wire up AllowPartial logic so partial fulfilment returns feasible=false with a populated routing list and an errors list for skipped items.

---

## 8. Separate Startup class

> Don't inline everything in Program.cs. Create a Startup class that holds ConfigureServices and Configure separately from the entry point. Program.cs should be a 5-line bootstrap that constructs Startup and calls its methods. The reason is that Startup can be instantiated in integration tests to get a fully-configured test host without duplicating wiring logic.

---

## 9. Health checks

> Add two health checks under OrderRouter.Api/HealthChecks/: DatabaseReadinessCheck (verifies the DB connection is alive) and DataSeedingCheck (verifies both the Suppliers and Products tables have rows). Expose them on GET /health/ready. Map Unhealthy to 503 so Kubernetes stops routing traffic when the database is down. Map Degraded to 200 so traffic continues when data is missing but the DB is reachable — every routing request will return infeasible until the data is fixed, but at least the service is observable. Add XML comments explaining the Degraded=200 decision.

---

## 10. Docker

> Containerise the service with a multi-stage Dockerfile: SDK stage for build and publish, aspnet runtime stage for the final image. Bake the CSV data files into the image with COPY data/ /app/data/ so seeding runs automatically on startup without needing a volume mount for the data. Add a docker-compose.yml with a healthcheck on /health/ready.

---

## 11. Unit test suite

> Add a unit test suite to `OrderRouter.Services.Tests` using MSTest (`[TestClass]`, `[TestInitialize]`, `[TestMethod]`, `[DataRow]`) with Shouldly for assertions. Remove xUnit. Organise tests under `UnitTests/` with subfolders matching the service layer: `Mappers/`, `Operations/`, `Resolvers/`, `Routing/`, `Store/Seeding/`.

> **CSV parsers** (`SupplierCsvParserTests`, `ProductCsvParserTests`): parsers only expose `Parse(string filePath)`, so each test writes its CSV content to a temp file via `Path.GetTempFileName()`, calls `Parse`, and deletes the file in a `finally` block. Use `NullLogger<T>.Instance` from `Microsoft.Extensions.Logging.Abstractions`. Define a `Headers` constant for the happy-path header row; tests that intentionally deviate (typo normalisation, missing column) must not use it and should carry a comment explaining why.

> **Greedy strategy** (`GreedySetCoverStrategyTests`): `GreedySetCoverStrategy` has no dependencies — instantiate it directly. Test the three in-algorithm priorities: the greedy algorithm picks the supplier with the highest coverage count first (fewest shipments), tie-breaking by satisfaction score, and tie-breaking by local-over-mail_order when scores are equal. Also test the single-supplier and two-supplier assignment paths. Priority 1 (feasibility) is enforced upstream and is not tested here.

> **Routing operation** (`RoutingOperationTests`): mock all three dependencies (`IOrderEligibilityResolver`, `IRoutingStrategy`, `IRoutingMapper`) with Moq. Cover all four input validation paths, ZIP zero-padding, duplicate product code merging (verify via `Callback`), unknown-code infeasibility with both `allow_partial` settings, infeasible-product errors, single-supplier happy path, and a two-supplier happy path that verifies the response contains two routes — this is the multi-supplier scenario promised in section 12.

> **Eligibility resolver** (`OrderEligibilityResolverTests`): the interesting logic lives in `BuildEligibilityMap`, a pure in-memory function with no database dependency. Change its visibility to `internal static` and add `<InternalsVisibleTo Include="OrderRouter.Services.Tests" />` to the Services csproj. Tests call it directly with plain `Product` and `Supplier` objects — no SQLite connection, no EF Core InMemory provider. Cover local supplier (correct ZIP → `"local"` mode), local supplier wrong ZIP, mail-order supplier with the flag both on and off, national supplier (`ServesAllZips=true` → always `"local"`), wrong category, multiple eligible suppliers, and mixed known/unknown codes.

---

## 12. Design decisions and fixes (sessions 2–3)

Key decisions and fixes made during refinement — not prompts, but recorded here so the reasoning is traceable.

- Controller always returns HTTP 200; `feasible` field signals success or failure
- Removed redundant data annotations from service-layer contract models
- Replaced custom CSV parser with CsvHelper 33.0.1; added header normalisation and ZIP deduplication
- Replaced `EnsureCreatedAsync` with `MigrateAsync`; added `IDesignTimeDbContextFactory` so `dotnet ef` works from the class library
- Added `JsonNamingPolicy.SnakeCaseLower` to `AddJsonOptions` — STJ ignores `[DataMember]` attributes, so the naming policy is required for correct snake_case serialisation and deserialisation
- Docker DB moved to `/app/db/` with a named volume so it persists without shadowing the baked-in CSV files; curl added to base image for the healthcheck
- Route hardcoded to `api/route` (lowercase); `[controller]` token expands to `Route`
- `fulfillment_mode` on each `RoutedItem` is by design — mode is supplier-level but placed per item so clients can process items independently
- Infeasibility in `data/sample_orders.json` is tested via unknown product codes; multi-supplier routing cannot be forced with the current dataset because one national supplier (SUP-0460) covers all 24 categories — multi-supplier scenarios will be covered by unit tests with mock eligibility data

