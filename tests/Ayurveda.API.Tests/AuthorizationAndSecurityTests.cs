using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Ayurveda.API.Tests;

public class AuthorizationAndSecurityTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthorizationAndSecurityTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/admin/get-pending-doctor")]
    [InlineData("/api/admin/pending-doctors")]
    [InlineData("/api/admin/dashboard")]
    public async Task AdminEndpoints_AnonymousUser_ReturnsUnauthorized(string url)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/admin/get-pending-doctor")]
    [InlineData("/api/admin/pending-doctors")]
    [InlineData("/api/admin/dashboard")]
    public async Task AdminEndpoints_CustomerUser_ReturnsForbidden(string url)
    {
        var client = _factory.CreateAuthenticatedClient(role: "User", id: 42, email: "customer@ayurveda.com");

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoints_AdminUser_ReturnsOk()
    {
        var client = _factory.CreateAuthenticatedClient(role: "Admin", id: 1, email: "admin@ayurveda.com");

        var response = await client.GetAsync("/api/admin/pending-doctors");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PublicAdminSignup_EndpointRemoved_ReturnsNotFound()
    {
        // Rule b: Public admin signup must be completely removed
        var client = _factory.CreateClient();

        var response1 = await client.PostAsJsonAsync("/api/auth/admin/signup", new { email = "hacker@test.com", password = "Password123" });
        var response2 = await client.PostAsJsonAsync("/admin/sign-up", new { email = "hacker@test.com", password = "Password123" });

        Assert.Equal(HttpStatusCode.NotFound, response1.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, response2.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_CustomerUser_ReturnsForbidden()
    {
        // Rule a: User deletion must be admin-only
        var client = _factory.CreateAuthenticatedClient(role: "User", id: 99);

        var response = await client.DeleteAsync("/api/users/99");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateOrderStatus_CustomerUser_ReturnsForbidden()
    {
        // Rule a: Order status update must be admin-only
        var client = _factory.CreateAuthenticatedClient(role: "User", id: 99);

        var response = await client.PutAsJsonAsync("/api/orders/1/status", new { status = "Delivered" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
