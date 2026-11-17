using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ByteBait.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ByteBait.Api.Tests;

public class CastsEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public CastsEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        if (ApiFactory.HasDatabase)
        {
            using var scope = factory.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ByteBaitDbContext>().Database.Migrate();
        }
    }

    [PostgresFact]
    public async Task Valid_cast_returns_201_and_is_saved()
    {
        var response = await _client.PostAsJsonAsync("/api/casts", new
        {
            zone = "green",
            strength = 0.8,
            castAt = "2026-11-17T10:15:30.123+01:00",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetInt32();
        Assert.Equal("green", body.GetProperty("zone").GetString());
        Assert.Equal(0.8, body.GetProperty("strength").GetDouble());
        // The time is converted to UTC before it is saved
        Assert.Equal("2026-11-17T09:15:30.123Z", body.GetProperty("castAt").GetString());
        Assert.True(body.TryGetProperty("createdAt", out _));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ByteBaitDbContext>();
        var saved = await db.Casts.SingleAsync(c => c.Id == id);
        Assert.Equal(new DateTime(2026, 11, 17, 9, 15, 30, 123, DateTimeKind.Utc), saved.CastAt);

        db.Casts.Remove(saved);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Invalid_zone_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/casts", new
        {
            zone = "blue",
            strength = 0.5,
            castAt = "2026-11-17T09:15:30Z",
        });

        await AssertValidationProblem(response, "zone");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.5)]
    public async Task Strength_outside_0_to_1_returns_400(double strength)
    {
        var response = await _client.PostAsJsonAsync("/api/casts", new
        {
            zone = "green",
            strength,
            castAt = "2026-11-17T09:15:30Z",
        });

        await AssertValidationProblem(response, "strength");
    }

    [Fact]
    public async Task Missing_fields_return_400()
    {
        var response = await _client.PostAsJsonAsync("/api/casts", new { });

        var problem = await AssertValidationProblem(response, "zone");
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("strength", out _));
        Assert.True(errors.TryGetProperty("castAt", out _));
    }

    private static async Task<JsonElement> AssertValidationProblem(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), $"Expected an error for '{field}'");
        return problem;
    }
}
