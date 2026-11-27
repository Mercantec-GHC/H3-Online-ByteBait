using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ByteBait.Data;
using ByteBait.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ByteBait.Api.Tests;

// /api/admin/species and /api/admin/lanes (S2.11). The tests only change species on their own test lane.
public sealed class AdminSpeciesEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly ApiFactory _factory;

    // Set in InitializeAsync: an admin, and a lane with one species on it
    private TestPlayer _admin = null!;
    private TestLevel _level = null!;

    public AdminSpeciesEndpointTests(ApiFactory factory)
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
            _admin = await TestPlayer.RegisterAdminAsync(_factory);
            _level = await TestLevel.CreateAsync(_factory);
        }
    }

    public async Task DisposeAsync()
    {
        if (_admin is not null)
        {
            await _admin.DisposeAsync();
            await _level.DisposeAsync();
        }
    }

    private HttpClient Admin => _admin.Client;

    // ---------- Who may use it ----------

    [Fact]
    public async Task Admin_endpoints_require_login()
    {
        var client = await TestPlayer.CreateClientWithTokenAsync(_factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/species")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/lanes")).StatusCode);
    }

    [PostgresFact]
    public async Task Players_without_the_admin_role_get_403()
    {
        await using var player = await TestPlayer.RegisterAsync(_factory);

        var list = await player.Client.GetAsync("/api/admin/species");
        var create = await player.Client.PostAsJsonAsync("/api/admin/species", NewSpecies(_level.LaneId));

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    // ---------- Read ----------

    [PostgresFact]
    public async Task List_has_the_seeded_species_and_the_lanes()
    {
        var species = await Admin.GetFromJsonAsync<JsonElement[]>("/api/admin/species");
        var lanes = await Admin.GetFromJsonAsync<JsonElement[]>("/api/admin/lanes");

        Assert.Contains(species!, s => s.GetProperty("name").GetString() == "Pike" && s.GetProperty("laneId").GetInt32() == 1);
        Assert.Contains(species!, s => s.GetProperty("id").GetInt32() == _level.Species!.Id);
        Assert.Equal([1, 2, 3, 4], lanes!.Select(l => l.GetProperty("number").GetInt32()).Where(n => n <= 4));
    }

    [PostgresFact]
    public async Task Unknown_species_returns_404()
    {
        var response = await Admin.GetAsync($"/api/admin/species/{int.MaxValue}");

        await AssertProblem(response, HttpStatusCode.NotFound);
    }

    // ---------- Create ----------

    [PostgresFact]
    public async Task Create_saves_the_species_and_points_to_it()
    {
        var response = await Admin.PostAsJsonAsync("/api/admin/species", NewSpecies(_level.LaneId, name: "Ide"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Ide", body.GetProperty("name").GetString());
        Assert.Equal("rare", body.GetProperty("rarity").GetString());

        var fetched = await Admin.GetFromJsonAsync<JsonElement>(response.Headers.Location);
        Assert.Equal(body.GetProperty("id").GetInt32(), fetched.GetProperty("id").GetInt32());
    }

    [PostgresFact]
    public async Task Same_name_twice_on_a_lane_returns_409()
    {
        var response = await Admin.PostAsJsonAsync("/api/admin/species", NewSpecies(_level.LaneId, name: _level.Species!.Name));

        await AssertProblem(response, HttpStatusCode.Conflict);
    }

    [PostgresFact]
    public async Task Create_on_an_unknown_lane_returns_400()
    {
        var response = await Admin.PostAsJsonAsync("/api/admin/species", NewSpecies(int.MaxValue));

        await AssertValidationProblem(response, "laneId");
    }

    [PostgresFact]
    public async Task Max_below_min_returns_400_for_the_max_field()
    {
        var species = NewSpecies(_level.LaneId);
        species["maxWeightKg"] = 0.5;
        species["maxLengthCm"] = 5;

        var response = await Admin.PostAsJsonAsync("/api/admin/species", species);

        var problem = await AssertValidationProblem(response, "maxWeightKg");
        Assert.True(problem.GetProperty("errors").TryGetProperty("maxLengthCm", out _));
    }

    [PostgresFact]
    public async Task Unknown_rarity_and_negative_price_return_400()
    {
        var species = NewSpecies(_level.LaneId);
        species["rarity"] = "mythical";
        species["price"] = -1;

        var response = await Admin.PostAsJsonAsync("/api/admin/species", species);

        var problem = await AssertValidationProblem(response, "rarity");
        Assert.True(problem.GetProperty("errors").TryGetProperty("price", out _));
    }

    // ---------- Edit ----------

    [PostgresFact]
    public async Task Update_changes_the_species()
    {
        var id = _level.Species!.Id;
        var species = NewSpecies(_level.LaneId, name: "Big Carp", version: await VersionOfAsync(id));
        species["price"] = 75;

        var response = await Admin.PutAsJsonAsync($"/api/admin/species/{id}", species);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<ByteBaitDbContext>().FishSpecies.SingleAsync(s => s.Id == id);
        Assert.Equal("Big Carp", saved.Name);
        Assert.Equal(75, saved.Price);
        Assert.Equal(Rarity.Rare, saved.Rarity);
    }

    // Andreas' case from the review: two admins open the same species. The first changes the price,
    // the second only the name. The second form is from before the price change, so it must not win.
    [PostgresFact]
    public async Task A_form_opened_before_another_save_returns_409_and_keeps_that_save()
    {
        var id = _level.Species!.Id;
        var opened = await VersionOfAsync(id);

        var first = NewSpecies(_level.LaneId, version: opened);
        first["price"] = 80;
        Assert.Equal(HttpStatusCode.OK, (await Admin.PutAsJsonAsync($"/api/admin/species/{id}", first)).StatusCode);

        var second = NewSpecies(_level.LaneId, name: "Renamed Carp", version: opened);
        var response = await Admin.PutAsJsonAsync($"/api/admin/species/{id}", second);

        var problem = await AssertProblem(response, HttpStatusCode.Conflict);
        Assert.Equal(Controllers.AdminSpeciesController.SpeciesChangedTitle, problem.GetProperty("title").GetString());
        var saved = await Admin.GetFromJsonAsync<JsonElement>($"/api/admin/species/{id}");
        Assert.Equal(80, saved.GetProperty("price").GetInt32());
        Assert.Equal("Crucian Carp", saved.GetProperty("name").GetString());
    }

    [PostgresFact]
    public async Task A_save_returns_the_new_version()
    {
        var id = _level.Species!.Id;
        var opened = await VersionOfAsync(id);

        var response = await Admin.PutAsJsonAsync($"/api/admin/species/{id}", NewSpecies(_level.LaneId, version: opened));
        var saved = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.NotEqual(opened, saved.GetProperty("version").GetUInt32());
        Assert.Equal(saved.GetProperty("version").GetUInt32(), await VersionOfAsync(id));
    }

    [PostgresFact]
    public async Task Update_without_a_version_returns_400()
    {
        var response = await Admin.PutAsJsonAsync($"/api/admin/species/{_level.Species!.Id}", NewSpecies(_level.LaneId));

        await AssertValidationProblem(response, "version");
    }

    [PostgresFact]
    public async Task Species_that_has_been_fished_can_still_be_edited_on_its_lane()
    {
        await CastOnTestLevelAsync();
        var species = NewSpecies(_level.LaneId, name: "Old Carp", version: await VersionOfAsync(_level.Species!.Id));
        species["price"] = 90;

        var response = await Admin.PutAsJsonAsync($"/api/admin/species/{_level.Species!.Id}", species);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The old casts were made on this lane's level, so they would point to a fish from another lake
    [PostgresFact]
    public async Task Species_that_has_been_fished_cannot_move_to_another_lane()
    {
        await CastOnTestLevelAsync();
        int otherLaneId;
        using (var scope = _factory.Services.CreateScope())
        {
            otherLaneId = await scope.ServiceProvider.GetRequiredService<ByteBaitDbContext>().Lanes
                .Where(l => l.Id != _level.LaneId)
                .Select(l => l.Id)
                .FirstAsync();
        }

        var response = await Admin.PutAsJsonAsync(
            $"/api/admin/species/{_level.Species!.Id}",
            NewSpecies(otherLaneId, name: "Moved Carp", version: await VersionOfAsync(_level.Species.Id)));

        await AssertProblem(response, HttpStatusCode.Conflict);
        var saved = await Admin.GetFromJsonAsync<JsonElement>($"/api/admin/species/{_level.Species.Id}");
        Assert.Equal(_level.LaneId, saved.GetProperty("laneId").GetInt32());
    }

    [PostgresFact]
    public async Task Update_of_an_unknown_species_returns_404()
    {
        var response = await Admin.PutAsJsonAsync($"/api/admin/species/{int.MaxValue}", NewSpecies(_level.LaneId, version: 1));

        await AssertProblem(response, HttpStatusCode.NotFound);
    }

    // ---------- Delete ----------

    [PostgresFact]
    public async Task Delete_removes_a_species_that_has_not_been_fished()
    {
        var created = await Admin.PostAsJsonAsync("/api/admin/species", NewSpecies(_level.LaneId, name: "Ruffe"));
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var response = await Admin.DeleteAsync($"/api/admin/species/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertProblem(await Admin.GetAsync($"/api/admin/species/{id}"), HttpStatusCode.NotFound);
    }

    [PostgresFact]
    public async Task Species_that_has_been_fished_returns_409_and_stays()
    {
        await CastOnTestLevelAsync();

        var response = await Admin.DeleteAsync($"/api/admin/species/{_level.Species!.Id}");

        await AssertProblem(response, HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.OK, (await Admin.GetAsync($"/api/admin/species/{_level.Species.Id}")).StatusCode);
    }

    [PostgresFact]
    public async Task Delete_without_antiforgery_token_returns_400()
    {
        Admin.DefaultRequestHeaders.Remove(Players.PlayerAccountExtensions.AntiforgeryHeaderName);

        var response = await Admin.DeleteAsync($"/api/admin/species/{_level.Species!.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Helpers ----------

    // The test lane has only one species, so the cast hooks it
    private async Task CastOnTestLevelAsync()
    {
        var cast = await Admin.PostAsJsonAsync("/api/casts", new
        {
            zone = "green",
            strength = 0.8,
            castAt = "2026-11-26T09:15:30Z",
            levelId = _level.LevelId,
        });
        Assert.Equal(HttpStatusCode.Created, cast.StatusCode);
    }

    // version: the version the form was opened with. PUT needs it, POST ignores it.
    private static Dictionary<string, object> NewSpecies(int laneId, string name = "Crucian Carp", uint? version = null)
    {
        var species = new Dictionary<string, object>
        {
            ["laneId"] = laneId,
            ["name"] = name,
            ["rarity"] = "rare",
            ["depth"] = "shallow",
            ["minWeightKg"] = 1.0,
            ["maxWeightKg"] = 2.5,
            ["minLengthCm"] = 20,
            ["maxLengthCm"] = 40,
            ["price"] = 40,
        };
        if (version is { } v)
        {
            species["version"] = v;
        }

        return species;
    }

    // Like the admin panel: the version comes from the list or GET when the form is opened
    private async Task<uint> VersionOfAsync(int id) =>
        (await Admin.GetFromJsonAsync<JsonElement>($"/api/admin/species/{id}")).GetProperty("version").GetUInt32();

    private static async Task<JsonElement> AssertProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> AssertValidationProblem(HttpResponseMessage response, string field)
    {
        var problem = await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), $"Expected an error for '{field}'");
        return problem;
    }
}
