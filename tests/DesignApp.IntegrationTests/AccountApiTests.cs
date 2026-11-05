using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DesignApp.Tests;

namespace DesignApp.IntegrationTests;

/// <summary>
/// End-to-end tests of accounts over HTTP: guests are kept apart, signing up or in keeps a guest's
/// rooms, and wrong details are refused. Each client has its own cookies, like a browser.
/// </summary>
/// <param name="factory">The API running against test containers.</param>
public class AccountApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "correct horse battery";

    [Fact]
    public async Task Guests_in_different_browsers_cannot_see_each_others_rooms()
    {
        using var alice = factory.CreateClient();
        using var bob = factory.CreateClient();

        var room = await UploadAsync(alice);

        Assert.Equal(HttpStatusCode.OK, (await alice.GetAsync($"/api/rooms/{room}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/rooms/{room}")).StatusCode);
    }

    [Fact]
    public async Task Signing_up_keeps_the_guests_rooms_and_signing_out_starts_a_new_guest()
    {
        using var browser = factory.CreateClient();
        var email = NewEmail();
        var room = await UploadAsync(browser);
        Assert.False((await AccountAsync(browser)).SignedIn);

        using var signUp = await browser.PostAsJsonAsync("/api/account/sign-up", new { email, password = Password });

        Assert.Equal(HttpStatusCode.OK, signUp.StatusCode);
        Assert.Equal(new AccountDto(true, email), await AccountAsync(browser));
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/api/rooms/{room}")).StatusCode);

        using var signOut = await browser.PostAsync("/api/account/sign-out", null);
        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        Assert.False((await AccountAsync(browser)).SignedIn);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/api/rooms/{room}")).StatusCode);
    }

    [Fact]
    public async Task Signing_in_from_another_browser_brings_both_browsers_rooms_together()
    {
        var email = NewEmail();
        using var laptop = factory.CreateClient();
        var first = await UploadAsync(laptop);
        (await laptop.PostAsJsonAsync("/api/account/sign-up", new { email, password = Password })).EnsureSuccessStatusCode();

        using var phone = factory.CreateClient();
        var second = await UploadAsync(phone);
        using var signIn = await phone.PostAsJsonAsync("/api/account/sign-in", new { email = $"  {email.ToUpperInvariant()} ", password = Password });

        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        foreach (var browser in new[] { laptop, phone })
        {
            Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/api/rooms/{first}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/api/rooms/{second}")).StatusCode);
        }
    }

    [Fact]
    public async Task An_email_can_only_have_one_account_and_short_passwords_are_refused()
    {
        var email = NewEmail();
        using var browser = factory.CreateClient();
        (await browser.PostAsJsonAsync("/api/account/sign-up", new { email, password = Password })).EnsureSuccessStatusCode();

        using var again = factory.CreateClient();
        Assert.Equal("An account with this email already exists. Sign in instead.",
            await ProblemAsync(await again.PostAsJsonAsync("/api/account/sign-up", new { email, password = Password })));
        Assert.Equal("Use a password of at least 8 characters.",
            await ProblemAsync(await again.PostAsJsonAsync("/api/account/sign-up", new { email = NewEmail(), password = "short" })));
        Assert.Equal("Enter a valid email address.",
            await ProblemAsync(await again.PostAsJsonAsync("/api/account/sign-up", new { email = "not an email", password = Password })));
        Assert.Equal("Enter your email.",
            await ProblemAsync(await again.PostAsJsonAsync("/api/account/sign-up", new { email = " ", password = Password })));
    }

    [Fact]
    public async Task Wrong_details_get_one_answer_and_repeated_wrong_passwords_lock_the_account()
    {
        var email = NewEmail();
        using var owner = factory.CreateClient();
        (await owner.PostAsJsonAsync("/api/account/sign-up", new { email, password = Password })).EnsureSuccessStatusCode();

        using var stranger = factory.CreateClient();
        Assert.Equal("Wrong email or password.",
            await ProblemAsync(await stranger.PostAsJsonAsync("/api/account/sign-in", new { email = NewEmail(), password = Password })));
        for (var attempt = 1; attempt < 5; attempt++)
        {
            Assert.Equal("Wrong email or password.",
                await ProblemAsync(await stranger.PostAsJsonAsync("/api/account/sign-in", new { email, password = "guess number " + attempt })));
        }

        using var fifth = await stranger.PostAsJsonAsync("/api/account/sign-in", new { email, password = "guess number 5" });
        Assert.Equal(HttpStatusCode.TooManyRequests, fifth.StatusCode);
        using var evenRight = await stranger.PostAsJsonAsync("/api/account/sign-in", new { email, password = Password });
        Assert.Equal(HttpStatusCode.TooManyRequests, evenRight.StatusCode);
        Assert.False((await AccountAsync(stranger)).SignedIn);
    }

    /// <summary>A fresh email, so tests sharing the database don't collide.</summary>
    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    /// <summary>Uploads a small photo as the browser and returns the new room's id.</summary>
    /// <param name="browser">The client, with its own cookies.</param>
    private static async Task<Guid> UploadAsync(HttpClient browser)
    {
        using var content = new ByteArrayContent(TestImages.Create(800, 600));
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        using var form = new MultipartFormDataContent { { content, "photo", "room.jpg" } };
        using var response = await browser.PostAsync("/api/rooms", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdDto>())!.Id;
    }

    /// <summary>Who the browser is signed in as.</summary>
    /// <param name="browser">The client.</param>
    private static async Task<AccountDto> AccountAsync(HttpClient browser) =>
        (await browser.GetFromJsonAsync<AccountDto>("/api/account"))!;

    /// <summary>The detail of a refused request, checking it was refused with 400.</summary>
    /// <param name="response">The response; disposed here.</param>
    private static async Task<string?> ProblemAsync(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<ProblemDto>())?.Detail;
        }
    }

    /// <summary>The part of a room the tests read.</summary>
    /// <param name="Id">The room's id.</param>
    private sealed record IdDto(Guid Id);

    /// <summary><c>GET /api/account</c> as a client reads it.</summary>
    /// <param name="SignedIn">Whether an account is signed in.</param>
    /// <param name="Email">Its email.</param>
    private sealed record AccountDto(bool SignedIn, string? Email);

    /// <summary>A problem response's detail.</summary>
    /// <param name="Detail">What went wrong.</param>
    private sealed record ProblemDto(string? Detail);
}
