using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ByteBait.Api.Players;
using ByteBait.Data;
using ByteBait.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ByteBait.Api.Tests;

// POST /api/account/register, login and logout, GET /api/account/me and the antiforgery token (S2.13)
public sealed class AccountEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private const string Password = TestPlayer.Password;

    private readonly ApiFactory _factory;

    // Has an antiforgery token, set in InitializeAsync
    private HttpClient _client = null!;

    // Unique, so tests running at the same time don't collide
    private readonly string _userName = TestPlayer.NewUserName();

    public AccountEndpointTests(ApiFactory factory)
    {
        _factory = factory;

        if (ApiFactory.HasDatabase)
        {
            using var scope = factory.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ByteBaitDbContext>().Database.Migrate();
        }
    }

    // ---------- Register ----------

    [PostgresFact]
    public async Task Register_creates_the_player_and_logs_in()
    {
        var response = await RegisterAsync(_client);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(_userName, body.GetProperty("userName").GetString());
        Assert.Equal(0, body.GetProperty("money").GetInt32());
        Assert.Empty(body.GetProperty("roles").EnumerateArray());

        var me = await _client.GetFromJsonAsync<JsonElement>("/api/account/me");
        Assert.Equal(body.GetProperty("id").GetGuid(), me.GetProperty("id").GetGuid());
    }

    [PostgresFact]
    public async Task Login_cookie_can_only_be_used_by_our_own_site_over_https()
    {
        var response = await RegisterAsync(_client);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith($"{PlayerAccountExtensions.LoginCookieName}=", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task Register_with_a_taken_name_returns_409()
    {
        await RegisterAsync(_client);

        var response = await RegisterAsync(await CreateClientAsync(), _userName.ToUpperInvariant());

        await AssertProblem(response, HttpStatusCode.Conflict);
    }

    [PostgresFact]
    public async Task Only_one_of_two_players_gets_the_name_at_the_same_time()
    {
        var responses = await Task.WhenAll(RegisterAsync(await CreateClientAsync()), RegisterAsync(await CreateClientAsync()));

        Assert.Equal(
            [HttpStatusCode.Created, HttpStatusCode.Conflict],
            responses.Select(r => r.StatusCode).Order());
    }

    // Identity rejects these two before it looks in the database
    [Fact]
    public async Task Register_with_a_short_password_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/account/register", new { userName = _userName, password = "1234567" });

        await AssertValidationProblem(response, "password");
    }

    [Fact]
    public async Task Register_with_a_space_in_the_name_returns_400()
    {
        var response = await RegisterAsync(_client, "two words");

        await AssertValidationProblem(response, "userName");
    }

    [Fact]
    public async Task Register_without_fields_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/account/register", new { });

        var problem = await AssertValidationProblem(response, "userName");
        Assert.True(problem.GetProperty("errors").TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Register_with_a_too_short_name_returns_400()
    {
        var response = await RegisterAsync(_client, "ab");

        await AssertValidationProblem(response, "userName");
    }

    // ---------- Login and logout ----------

    [PostgresFact]
    public async Task Login_works_with_other_letter_case_in_the_name()
    {
        await RegisterAsync(_client);
        var client = await CreateClientAsync();

        var response = await LoginAsync(client, _userName.ToUpperInvariant(), Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/account/me");
        Assert.Equal(_userName, me.GetProperty("userName").GetString());
    }

    [PostgresFact]
    public async Task Wrong_password_and_unknown_name_get_the_same_answer()
    {
        await RegisterAsync(_client);
        var client = await CreateClientAsync();

        var wrongPassword = await AssertProblem(
            await LoginAsync(client, _userName, "wrong password"), HttpStatusCode.Unauthorized);
        var unknownName = await AssertProblem(
            await LoginAsync(client, "nobody-" + _userName[..20], Password), HttpStatusCode.Unauthorized);

        Assert.Equal(wrongPassword.GetProperty("title").GetString(), unknownName.GetProperty("title").GetString());
        Assert.Equal(wrongPassword.GetProperty("detail").GetString(), unknownName.GetProperty("detail").GetString());
    }

    [PostgresFact]
    public async Task Account_is_locked_after_five_wrong_passwords()
    {
        await RegisterAsync(_client);
        var client = await CreateClientAsync();

        for (var i = 0; i < 5; i++)
        {
            await LoginAsync(client, _userName, "wrong password");
        }

        // Not even the right password works while the account is locked
        await AssertProblem(await LoginAsync(client, _userName, Password), HttpStatusCode.TooManyRequests);
    }

    [PostgresFact]
    public async Task Logout_ends_the_login()
    {
        await RegisterAsync(_client);
        await TestPlayer.RefreshAntiforgeryTokenAsync(_client);

        var response = await _client.PostAsync("/api/account/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertProblem(await _client.GetAsync("/api/account/me"), HttpStatusCode.Unauthorized);
    }

    // ---------- Me ----------

    [Fact]
    public async Task Me_without_login_returns_401_and_no_redirect()
    {
        var response = await _client.GetAsync("/api/account/me");

        await AssertProblem(response, HttpStatusCode.Unauthorized);
        Assert.Null(response.Headers.Location);
    }

    [PostgresFact]
    public async Task Me_shows_the_admin_role()
    {
        await RegisterAsync(_client);
        using (var scope = _factory.Services.CreateScope())
        {
            var players = scope.ServiceProvider.GetRequiredService<UserManager<Player>>();
            await players.AddToRoleAsync((await players.FindByNameAsync(_userName))!, RoleNames.Admin);
        }

        var me = await _client.GetFromJsonAsync<JsonElement>("/api/account/me");

        Assert.Equal(RoleNames.Admin, Assert.Single(me.GetProperty("roles").EnumerateArray()).GetString());
    }

    // ---------- Antiforgery ----------

    [Fact]
    public async Task Antiforgery_cookie_can_only_be_used_by_our_own_site_over_https()
    {
        var response = await TestPlayer.CreateClient(_factory).GetAsync("/api/account/antiforgery");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("token").GetString()));

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"),
            c => c.StartsWith($"{PlayerAccountExtensions.AntiforgeryCookieName}=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    // On Dokploy the request reaches the API as http, because Cloudflare handles HTTPS
    [Theory]
    [InlineData(null)]
    [InlineData("http")]
    public async Task Antiforgery_works_when_the_request_arrives_as_http(string? forwardedProto)
    {
        var client = _factory.CreateClient(new() { BaseAddress = new Uri("http://localhost") });
        if (forwardedProto is not null)
        {
            client.DefaultRequestHeaders.Add("X-Forwarded-Proto", forwardedProto);
        }

        var token = await client.GetAsync("/api/account/antiforgery");
        var logout = await client.PostAsync("/api/account/logout", null);

        Assert.Equal(HttpStatusCode.OK, token.StatusCode);
        // Without the token, so 400 and not 500
        Assert.Equal(HttpStatusCode.BadRequest, logout.StatusCode);
    }

    // Register and login are protected too, so another site can't log a player into its own account
    [Theory]
    [InlineData("/api/account/register")]
    [InlineData("/api/account/login")]
    [InlineData("/api/account/logout")]
    public async Task Post_without_antiforgery_token_returns_400(string url)
    {
        var response = await TestPlayer.CreateClient(_factory)
            .PostAsJsonAsync(url, new { userName = _userName, password = Password });

        var problem = await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Equal("Invalid antiforgery token", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Post_with_a_made_up_antiforgery_token_returns_400()
    {
        var client = await CreateClientAsync();
        client.DefaultRequestHeaders.Remove(PlayerAccountExtensions.AntiforgeryHeaderName);
        client.DefaultRequestHeaders.Add(PlayerAccountExtensions.AntiforgeryHeaderName, "made-up");

        var response = await LoginAsync(client, _userName, Password);

        await AssertProblem(response, HttpStatusCode.BadRequest);
    }

    // The token belongs to the player, so the one from before login no longer works
    [PostgresFact]
    public async Task Token_from_before_login_must_be_replaced_after_login()
    {
        await RegisterAsync(_client);

        await AssertProblem(await _client.PostAsync("/api/account/logout", null), HttpStatusCode.BadRequest);

        await TestPlayer.RefreshAntiforgeryTokenAsync(_client);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsync("/api/account/logout", null)).StatusCode);
    }

    // ---------- Helpers ----------

    // https and with an antiforgery token, because every POST needs one
    private Task<HttpClient> CreateClientAsync() => TestPlayer.CreateClientWithTokenAsync(_factory);

    private Task<HttpResponseMessage> RegisterAsync(HttpClient client, string? userName = null) =>
        client.PostAsJsonAsync("/api/account/register", new { userName = userName ?? _userName, password = Password });

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string userName, string password) =>
        client.PostAsJsonAsync("/api/account/login", new { userName, password });

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

    public async Task InitializeAsync() => _client = await CreateClientAsync();

    public async Task DisposeAsync()
    {
        if (!ApiFactory.HasDatabase) return;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ByteBaitDbContext>();
        await db.Players.Where(p => p.UserName == _userName).ExecuteDeleteAsync();
    }
}
