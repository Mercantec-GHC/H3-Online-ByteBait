using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ByteBait.Api.Players;
using ByteBait.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ByteBait.Api.Tests;

// POST /api/levels/{levelId}/start, POST /api/casts with levelId and POST /api/catches (S2.7).
// Every test runs as its own logged-in player (S2.13), so they need the database.
public sealed class LevelAndCatchEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly ApiFactory _factory;

    // Set in InitializeAsync. The client is the player's: logged in and with an antiforgery token.
    private TestPlayer _player = null!;
    private HttpClient _client = null!;

    public LevelAndCatchEndpointTests(ApiFactory factory)
    {
        _factory = factory;

        if (ApiFactory.HasDatabase)
        {
            using var scope = factory.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ByteBaitDbContext>().Database.Migrate();
        }
    }

    public async Task InitializeAsync()
    {
        if (ApiFactory.HasDatabase)
        {
            _player = await TestPlayer.RegisterAsync(_factory);
            _client = _player.Client;
        }
    }

    public async Task DisposeAsync()
    {
        if (_player is not null)
        {
            await _player.DisposeAsync();
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
        Assert.Equal(_player.Id, saved.PlayerId);
    }

    [PostgresFact]
    public async Task Cast_on_a_level_hooks_a_fish_and_saves_it_on_the_cast()
    {
        await using var level = await TestLevel.CreateAsync(_factory);
        var species = level.Species!;

        var response = await _client.PostAsJsonAsync("/api/casts", Cast(level.LevelId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var fish = body.GetProperty("fish");
        Assert.Equal(species.Id, fish.GetProperty("speciesId").GetInt32());
        Assert.Equal(species.Name, fish.GetProperty("name").GetString());
        Assert.Equal("common", fish.GetProperty("rarity").GetString());
        var weight = fish.GetProperty("weightKg").GetDouble();
        var length = fish.GetProperty("lengthCm").GetDouble();
        Assert.InRange(weight, TestLevel.MinWeightKg, TestLevel.MaxWeightKg);
        Assert.InRange(length, TestLevel.MinLengthCm, TestLevel.MaxLengthCm);

        // The same fish is saved on the cast
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ByteBaitDbContext>();
        var saved = await db.Casts.SingleAsync(c => c.Id == body.GetProperty("id").GetInt32());
        Assert.Equal(species.Id, saved.FishSpeciesId);
        Assert.Equal(weight, saved.WeightKg);
        Assert.Equal(length, saved.LengthCm);
    }

    [PostgresFact]
    public async Task Cast_on_a_lane_without_species_has_no_fish()
    {
        await using var level = await TestLevel.CreateAsync(_factory, withSpecies: false);

        var response = await _client.PostAsJsonAsync("/api/casts", Cast(level.LevelId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("fish").ValueKind);
    }

    [PostgresFact]
    public async Task Cast_with_unknown_levelId_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/casts", Cast(levelId: int.MaxValue));

        await AssertValidationProblem(response, "levelId");
    }

    [PostgresFact]
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
        // The cast is deleted with the test player
        var castId = await SaveCastAsync(levelId: null);

        var response = await _client.PostAsJsonAsync("/api/catches", new { castId, result = "caught" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ---------- Login (S2.13) ----------

    [Fact]
    public async Task Game_endpoints_require_login()
    {
        // Has a token, so it is the missing login that stops the requests
        var client = await TestPlayer.CreateClientWithTokenAsync(_factory);

        var responses = new[]
        {
            await client.PostAsync("/api/levels/1/start", null),
            await client.PostAsJsonAsync("/api/casts", Cast(levelId: 1)),
            await client.PostAsJsonAsync("/api/catches", new { castId = 1, result = "caught" }),
        };

        Assert.All(responses, r =>
        {
            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
            Assert.Null(r.Headers.Location);
        });
    }

    [PostgresFact]
    public async Task Cast_without_antiforgery_token_returns_400()
    {
        _client.DefaultRequestHeaders.Remove(PlayerAccountExtensions.AntiforgeryHeaderName);

        var response = await _client.PostAsJsonAsync("/api/casts", Cast(levelId: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Invalid antiforgery token", problem.GetProperty("title").GetString());
    }

    [PostgresFact]
    public async Task Players_only_see_their_own_casts_and_progress()
    {
        await using var level = await TestLevel.CreateAsync(_factory);
        await using var other = await TestPlayer.RegisterAsync(_factory);
        var castId = await SaveCastAsync(level.LevelId);
        await _client.PostAsJsonAsync("/api/catches", new { castId, result = "caught" });

        // Another player's cast is just as unknown as one that doesn't exist
        var response = await other.Client.PostAsJsonAsync("/api/catches", new { castId, result = "lost" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // And the other player's progress on the level doesn't include the fish
        var start = await other.Client.PostAsync($"/api/levels/{level.LevelId}/start", null);
        var body = await start.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("caughtFish").GetInt32());
    }

    [PostgresFact]
    public async Task Cast_without_fish_can_be_lost_but_not_caught()
    {
        await using var level = await TestLevel.CreateAsync(_factory, withSpecies: false);
        var castId = await SaveCastAsync(level.LevelId);

        var caught = await _client.PostAsJsonAsync("/api/catches", new { castId, result = "caught" });

        Assert.Equal(HttpStatusCode.Conflict, caught.StatusCode);
        Assert.Equal("application/problem+json", caught.Content.Headers.ContentType?.MediaType);
        var problem = await caught.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Cast has no fish", problem.GetProperty("title").GetString());

        var lost = await _client.PostAsJsonAsync("/api/catches", new { castId, result = "lost" });
        Assert.Equal(HttpStatusCode.Created, lost.StatusCode);
    }

    [PostgresFact]
    public async Task Catch_with_invalid_result_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/catches", new { castId = 1, result = "won" });

        await AssertValidationProblem(response, "result");
    }

    [PostgresFact]
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
