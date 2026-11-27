using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ByteBait.Api.Players;
using ByteBait.Data;
using ByteBait.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ByteBait.Api.Tests;

// A logged-in player with its own client, for tests of endpoints that need login (S2.13).
// The name is unique, so tests don't collide, and the player is deleted again with its casts and catches.
public sealed class TestPlayer : IAsyncDisposable
{
    public const string Password = "a long password";

    private readonly ApiFactory _factory;

    private TestPlayer(ApiFactory factory, HttpClient client, Guid id, string userName)
    {
        _factory = factory;
        Client = client;
        Id = id;
        UserName = userName;
    }

    // Logged in, with a token for the player in the antiforgery header
    public HttpClient Client { get; }

    public Guid Id { get; }

    public string UserName { get; }

    public static string NewUserName() => $"player-{Guid.NewGuid():N}"[..30];

    // https, because the login and antiforgery cookies are never sent over http
    public static HttpClient CreateClient(ApiFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
    });

    // Not logged in, but with an antiforgery token, so POSTs get past the antiforgery check
    public static async Task<HttpClient> CreateClientWithTokenAsync(ApiFactory factory)
    {
        var client = CreateClient(factory);
        await RefreshAntiforgeryTokenAsync(client);
        return client;
    }

    // The token belongs to the player, so the game gets a new one after register, login and logout
    public static async Task RefreshAntiforgeryTokenAsync(HttpClient client)
    {
        var body = await client.GetFromJsonAsync<JsonElement>("/api/account/antiforgery");
        client.DefaultRequestHeaders.Remove(PlayerAccountExtensions.AntiforgeryHeaderName);
        client.DefaultRequestHeaders.Add(PlayerAccountExtensions.AntiforgeryHeaderName, body.GetProperty("token").GetString());
    }

    public static async Task<TestPlayer> RegisterAsync(ApiFactory factory)
    {
        var client = await CreateClientWithTokenAsync(factory);
        var userName = NewUserName();

        var response = await client.PostAsJsonAsync("/api/account/register", new { userName, password = Password });
        if (response.StatusCode != HttpStatusCode.Created)
        {
            throw new InvalidOperationException($"Register failed with {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await RefreshAntiforgeryTokenAsync(client);

        return new TestPlayer(factory, client, body.GetProperty("id").GetGuid(), userName);
    }

    // A player with the admin role. The role is in the login cookie, so the player logs in again after getting it.
    public static async Task<TestPlayer> RegisterAdminAsync(ApiFactory factory)
    {
        var player = await RegisterAsync(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var players = scope.ServiceProvider.GetRequiredService<UserManager<Player>>();
            await players.AddToRoleAsync((await players.FindByIdAsync(player.Id.ToString()))!, RoleNames.Admin);
        }

        var login = await player.Client.PostAsJsonAsync("/api/account/login", new { userName = player.UserName, password = Password });
        if (login.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException($"Login failed with {(int)login.StatusCode}: {await login.Content.ReadAsStringAsync()}");
        }

        await RefreshAntiforgeryTokenAsync(player.Client);
        return player;
    }

    // The player's casts, catches and equipment are deleted with it (cascade)
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ByteBaitDbContext>();
        await db.Players.Where(p => p.Id == Id).ExecuteDeleteAsync();
    }
}
