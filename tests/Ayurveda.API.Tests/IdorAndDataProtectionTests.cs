using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ayurveda.API.Data;
using Ayurveda.API.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ayurveda.API.Tests;

public class IdorAndDataProtectionTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public IdorAndDataProtectionTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CartEndpoints_IgnoreBodyUserId_DerivesFromAuthenticatedPrincipal()
    {
        // Setup a product in the database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AyurvedaDbContext>();
            if (!db.Products.Any(p => p.Id == 101))
            {
                var cat = new Category { Id = 101, Name = "IdorTestCat", Description = "Test" };
                db.Categories.Add(cat);
                db.Products.Add(new Product { Id = 101, Title = "Idor Product", Price = 100, Stock = 50, CategoryId = 101 });
                await db.SaveChangesAsync();
            }
        }

        // Customer Alice (ID: 10) makes a request with request body claiming userId: 99 (Bob)
        var clientAlice = _factory.CreateAuthenticatedClient(role: "User", id: 10, email: "alice@test.com", name: "Alice");

        var addPayload = new
        {
            userId = 99, // Attempted IDOR injection
            productId = 101,
            quantity = 2
        };

        var addResponse = await clientAlice.PostAsJsonAsync("/api/cart/items", addPayload);
        Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);

        // Verify that the cart was created for Alice (ID: 10), NOT Bob (ID: 99)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AyurvedaDbContext>();
            var aliceCart = db.Carts.FirstOrDefault(c => c.UserId == 10);
            var bobCart = db.Carts.FirstOrDefault(c => c.UserId == 99);

            Assert.NotNull(aliceCart);
            Assert.Null(bobCart);
        }
    }

    [Fact]
    public async Task UserProfile_OtherUserProfileLookup_ReturnsForbidden()
    {
        // Customer 10 tries to view Customer 20's profile
        var client = _factory.CreateAuthenticatedClient(role: "User", id: 10);

        var response = await client.GetAsync("/api/users/20");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OrderDetails_OtherUserOrderLookup_ReturnsForbidden()
    {
        // Seed order belonging to User 20
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AyurvedaDbContext>();
            if (!db.Orders.Any(o => o.Id == 501))
            {
                db.Orders.Add(new Order
                {
                    Id = 501,
                    UserId = 20,
                    TotalAmount = 500,
                    Status = "Placed",
                    OrderDate = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
        }

        // User 10 tries to view User 20's order
        var client = _factory.CreateAuthenticatedClient(role: "User", id: 10);

        var response = await client.GetAsync("/api/orders/501");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
