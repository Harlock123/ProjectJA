// SPDX-License-Identifier: BUSL-1.1
using System.Net;
using ProjectJA.IntegrationTests.Fixtures;

namespace ProjectJA.IntegrationTests.Tests;

[Collection(AppCollection.Name)]
public sealed class SmokeTests(AppFactory factory)
{
    [Fact]
    public async Task Login_page_responds_for_anonymous()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Protected_page_redirects_to_login_when_anonymous()
    {
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        var response = await client.GetAsync("/projects");
        // Cookie auth issues a 302 redirect to the configured LoginPath.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login", response.Headers.Location!.OriginalString);
    }
}
