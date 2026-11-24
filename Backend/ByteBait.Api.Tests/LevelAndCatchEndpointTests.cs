using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ByteBait.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ByteBait.Api.Tests;

// POST /api/levels/{levelId}/start, POST /api/casts with levelId and POST /api/catches (S2.7).
public class LevelAndCatchEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public LevelAndCatchEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        if (ApiFactory.HasDatabase)
        {
            using var scope = factory.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ByteBaitDbContext>().Database.Migrate();
        }
    }

    // ---------- Start a level ----------

    [PostgresFact]
    public async Task Start_level_returns_the_requirement_and_progress()
    {
        await using var level = await TestLevel.CreateAsync(_factory);

        var response = await _client.PostAsync($"/api/levels/{level.LevelId}/start", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(level.LevelId, body.GetProperty("levelId").GetInt32());
        Assert.Equal(level.LaneId, body.GetProperty("laneId").GetInt32());
        Assert.Equal(level.LaneNumber, body.GetProperty("laneNumber").GetInt32());
        Assert.Equal(1, body.GetProperty("levelNumber").GetInt32());
        Assert.Equal(TestLevel.RequiredFish, body.GetProperty("requiredFish").GetInt32());
        Assert.Equal(0, body.GetProperty("caughtFish").GetInt32());
        Assert.False(body.GetProperty("completed").GetBoolean());
    }

    [PostgresFact]
    public async Task Start_unknown_level_returns_404()
    {
        var response = await _client.PostAsync($"/api/levels/{int.MaxValue}/start", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // ---------- Cast with levelId ----------

    [PostgresFact]
    public async Task Cast_with_levelId_is_saved_on_the_level()
    {
        await using var level = await TestLevel.CreateAsync(_factory);

        var castId = await SaveCastAsync(level.LevelId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ByteBaitDbContext>();
        var saved = await db.Casts.SingleAsync(c => c.Id == castId);
        Assert.Equal(level.LevelId, saved.LevelId);
        Assert.Null(saved.PlayerId); // until player accounts (#76)
    }

    [PostgresFact]
    public async Task Cast_with_unknown_levelId_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/casts", Cast(levelId: int.MaxValue));

        await AssertValidationProblem(response, "levelId");
    }

    [Fact]
    public async Task Cast_with_levelId_0_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/casts", Cast(levelId: 0));

        await AssertValidationProblem(response, "levelId");
    }

    // ---------- Save a catch ----------

    [PostgresFact]
    public async Task Caught_fish_count_toward_the_level_and_lost_fish_do_not()
    {
        await using var level = await TestLevel.CreateAsync(_factory);

        var first = await _client.PostAsJsonAsync("/api/catches", new { castId = await SaveCastAsync(level.LevelId), result = "caught" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("caught", firstBody.GetProperty("result").GetString());
        Assert.True(firstBody.TryGetProperty("resolvedAt", out _));
        Assert.Equal(1, firstBody.GetProperty("progress").GetProperty("caughtFish").GetInt32());
        Assert.False(firstBody.GetProperty("progress").GetProperty("completed").GetBoolean());

        var lost = await _client.PostAsJsonAsync("/api/catches", new { castId = await SaveCastAsync(level.LevelId), result = "lost" });
        var lostBody = await lost.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, lostBody.GetProperty("progress").GetProperty("caughtFish").GetInt32());

        // The second caught fish completes the level (RequiredFish = 2)
        var second = await _client.PostAsJsonAsync("/api/catches", new { castId = await SaveCastAsync(level.LevelId), result = "caught" });
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, secondBody.GetProperty("progress").GetProperty("caughtFish").GetInt32());
        Assert.True(secondBody.GetProperty("progress").GetProperty("completed").GetBoolean());

        // Starting the level again shows the same progress
        var start = await _client.PostAsync($"/api/levels/{level.LevelId}/start", null);
        var startBody = await start.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, startBody.GetProperty("caughtFish").GetInt32());
    }

    [PostgresFact]
    public async Task The_same_fight_can_only_be_saved_once()
    {
        await using var level = await TestLevel.CreateAsync(_factory);
        var castId = await SaveCastAsync(level.LevelId);

        await _client.PostAsJsonAsync("/api/catches", new { castId, result = "caught" });
        var again = await _client.PostAsJsonAsync("/api/catches", new { castId, result = "lost" });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("application/problem+json", again.Content.Headers.ContentType?.MediaType);
    }

    [PostgresFact]
    public async Task Catch_for_unknown_cast_returns_404()
    {
        var response = await _client.PostAsJsonAsync("/api/catches", new { castId = int.MaxValue, result = "caught" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [PostgresFact]
    public async Task Catch_for_cast_without_level_returns_409()
    {
        var castId = await SaveCastAsync(levelId: null);
        try
        {
            var response = await _client.PostAsJsonAsync("/api/catches", new { castId, result = "caught" });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ByteBaitDbContext>();
            await db.Casts.Where(c => c.Id == castId).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task Catch_with_invalid_result_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/catches", new { castId = 1, result = "won" });

        await AssertValidationProblem(response, "result");
    }

    [Fact]
    public async Task Catch_without_fields_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/catches", new { });

        var problem = await AssertValidationProblem(response, "castId");
        Assert.True(problem.GetProperty("errors").TryGetProperty("result", out _));
    }

    // ---------- Helpers ----------

    private static object Cast(int? levelId) => new
    {
        zone = "green",
        strength = 0.6,
        castAt = "2026-11-24T09:15:30Z",
        levelId,
    };

    private async Task<int> SaveCastAsync(int? levelId)
    {
        var response = await _client.PostAsJsonAsync("/api/casts", Cast(levelId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (levelId is not null)
        {
            Assert.Equal(levelId, body.GetProperty("levelId").GetInt32());
        }

        return body.GetProperty("id").GetInt32();
    }

    private static async Task<JsonElement> AssertValidationProblem(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), $"Expected an error for '{field}'");
        return problem;
    }
}
