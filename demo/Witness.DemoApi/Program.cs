using System.Text.Json;
using Witness.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── In-memory product catalog ──────────────────────────────────────────────
var products = new Dictionary<int, Product>
{
    [1] = new(1, "Widget", 9.99m, 100),
    [2] = new(2, "Gadget", 24.50m, 42),
    [3] = new(3, "Doohickey", 4.75m, 200),
};

// ── Register HttpClient with Witness outbound capture ──────────────────────
// Every call made through "external-api" is automatically recorded to witness-store.
builder.Services.AddHttpClient("external-api", client =>
{
    client.BaseAddress = new Uri("https://jsonplaceholder.typicode.com");
})
.AddWitnessCapture(opt =>
{
    opt.SessionId = "demo-api-outbound";
    opt.Tag = "outbound";
    opt.StorePath = "./witness-store";
});

var app = builder.Build();

// ── Witness middleware: enables record/replay of inbound + outbound calls ──
// Send X-Witness-Mode: record to capture outbound calls with the inbound request.
// Send X-Witness-Mode: replay + X-Witness-Id: <id> to replay with stubbed outbound.
app.UseWitnessMiddleware(opt => opt.StorePath = "./witness-store");

// ═══════════════════════════════════════════════════════════════════════════
// INBOUND-ONLY ENDPOINTS
// These can be recorded by calling witness_record against this API.
// ═══════════════════════════════════════════════════════════════════════════

app.MapGet("/api/products", () => Results.Ok(products.Values));

app.MapGet("/api/products/{id:int}", (int id) =>
    products.TryGetValue(id, out var product)
        ? Results.Ok(product)
        : Results.NotFound(new { error = "Product not found", id }));

// ═══════════════════════════════════════════════════════════════════════════
// INBOUND + OUTBOUND ENDPOINTS
// Calling these triggers outbound HTTP calls captured by WitnessCaptureHandler.
// ═══════════════════════════════════════════════════════════════════════════

// GET /api/users/{id}/profile
// Fetches a user from JSONPlaceholder (outbound call captured by Witness).
app.MapGet("/api/users/{id:int}/profile", async (int id, IHttpClientFactory httpFactory) =>
{
    var client = httpFactory.CreateClient("external-api");
    var user = await client.GetFromJsonAsync<JsonElement>($"/users/{id}");
    return Results.Ok(new { source = "jsonplaceholder", user });
});

// POST /api/orders
// Creates an order for a product; enriches it by fetching the product's
// "comments" from an external API (outbound call captured by Witness).
app.MapPost("/api/orders", async (OrderRequest order, IHttpClientFactory httpFactory) =>
{
    if (!products.TryGetValue(order.ProductId, out var product))
        return Results.NotFound(new { error = "Product not found", order.ProductId });

    if (order.Quantity > product.Stock)
        return Results.BadRequest(new { error = "Insufficient stock", available = product.Stock });

    // Outbound call — fetch related comments as a stand-in for a pricing service
    var client = httpFactory.CreateClient("external-api");
    var reviews = await client.GetFromJsonAsync<JsonElement>($"/posts/{order.ProductId}/comments");

    var total = product.Price * order.Quantity;
    var response = new
    {
        orderId = Random.Shared.Next(1000, 9999),
        productId = product.Id,
        productName = product.Name,
        quantity = order.Quantity,
        unitPrice = product.Price,
        total,
        status = "confirmed",
        reviewCount = reviews.GetArrayLength()
    };

    return Results.Created($"/api/orders/{response.orderId}", response);
});

app.Run();

// ── Models ──────────────────────────────────────────────────────────────────
record Product(int Id, string Name, decimal Price, int Stock);
record OrderRequest(int ProductId, int Quantity);
