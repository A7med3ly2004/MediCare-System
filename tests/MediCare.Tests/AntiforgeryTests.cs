using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MediCare.Tests;

public class AntiforgeryTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AntiforgeryTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseSeeded();
    }

    [Fact]
    public async Task PostWithoutAntiforgeryToken_IsRejectedWithBadRequest()
    {
        // Act as Admin but omit the Anti-Forgery Request Token
        var client = _factory.CreateClientForRole("Admin", "test-admin-1");

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["name"] = "Cardiovascular Surgery"
        });

        // POST to /Admin/CreateSpecialization which has [ValidateAntiForgeryToken]
        var response = await client.PostAsync("/Admin/CreateSpecialization", content);

        // Global AutoValidateAntiforgeryTokenAttribute or [ValidateAntiForgeryToken] returns 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousPostToProtectedAction_IsRejected()
    {
        var client = _factory.CreateClientForRole("Anonymous");

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = "1"
        });

        var response = await client.PostAsync("/Admin/ToggleDoctorSuspend", content);

        // Rejected: either 400 (antiforgery) or 302 (redirect to login) or 401
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Redirect ||
            response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected rejection for anonymous POST, got {response.StatusCode}");
    }
}
