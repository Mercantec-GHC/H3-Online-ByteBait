using System.Net;
using ByteBait.Client.Admin;
using ByteBait.Client.Api;

namespace ByteBait.Api.Tests;

// The admin panel's calls when the API is slow or answers with something unexpected (see FakeApi)
public class ClientAdminTests
{
    private static readonly string Token = """{"token":"t"}""";

    [Fact]
    public async Task A_timeout_on_save_shows_no_connection()
    {
        var admin = new AdminClient(new FakeApi(FakeApi.Answer(HttpStatusCode.OK, Token), FakeApi.TimesOut).Client);

        var error = await admin.CreateAsync(new SpeciesForm { LaneId = 1, Name = "Gedde" });

        Assert.Same(ApiError.NoConnection, error);
    }

    // The page catches these when it loads the lists, so they must count as connection errors
    [Fact]
    public async Task A_timeout_or_an_html_page_when_loading_counts_as_a_connection_error()
    {
        var timeout = await Record.ExceptionAsync(() => new AdminClient(new FakeApi(FakeApi.TimesOut).Client).GetSpeciesAsync());
        var html = await Record.ExceptionAsync(() => new AdminClient(new FakeApi(() =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>Bad gateway</html>") }).Client).GetLanesAsync());

        Assert.True(timeout is not null && ApiError.IsConnectionError(timeout), timeout?.GetType().Name);
        Assert.True(html is not null && ApiError.IsConnectionError(html), html?.GetType().Name);
    }
}
