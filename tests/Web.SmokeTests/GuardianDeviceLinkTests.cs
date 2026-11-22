using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Settings;
using K7.Server.Infrastructure.Database.Context.Identity;
using K7.Server.Web.Services;
using K7.Shared.Dtos.Users;
using K7.Shared.Json;
using K7.Tests.Helpers.Smoke;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace K7.Server.Web.SmokeTests;

/// <summary>
/// End to end: a guardian signed in on the device-link page opens the session of an account
/// they manage, through the real device authorization flow.
/// The device-link endpoint allows 10 posts per minute: keep the posts of this fixture under that.
/// </summary>
[TestFixture]
public class GuardianDeviceLinkTests
{
    private const string ChildPin = "4826";

    private static readonly JsonSerializerOptions Json = K7JsonSerializerOptions.CreateDefault();

    private SmokeWebApplicationFactory _factory = null!;
    private WebApplicationFactory<Program> _app = null!;
    private Account _guardian = null!;
    private Account _stranger = null!;
    private Account _child = null!;
    private Account _childWithoutPin = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new SmokeWebApplicationFactory();

        // The smoke factory replaces the current user with a fixed one. These tests check
        // which account a token belongs to, so they need the real one.
        _app = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUser>();
            services.AddScoped<IUser, CurrentUser>();
        }));
        await SeedAsync();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _app.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task Verify_ShouldOpenManagedAccountSession_WhenGuardianGivesItsPin()
    {
        var client = CreateClient();
        var device = await StartDeviceFlowAsync(client);

        var response = await VerifyAsync(client, _guardian, device.UserCode, _child.UserId, ChildPin);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/link?approved=true");

        var accessToken = await RedeemAsync(client, device.DeviceCode);
        ReadSubject(accessToken).Should().Be(_child.IdentityUserId);

        // The device now acts as the managed account, not as the guardian.
        var me = await GetCurrentUserAsync(client, accessToken);
        me.Id.Should().Be(_child.UserId);
    }

    [Test]
    public async Task Verify_ShouldOpenManagedAccountSession_WhenAccountHasNoPin()
    {
        var client = CreateClient();
        var device = await StartDeviceFlowAsync(client);

        var response = await VerifyAsync(client, _guardian, device.UserCode, _childWithoutPin.UserId, pin: null);

        response.Headers.Location!.OriginalString.Should().Be("/link?approved=true");
        var accessToken = await RedeemAsync(client, device.DeviceCode);
        ReadSubject(accessToken).Should().Be(_childWithoutPin.IdentityUserId);
    }

    [Test]
    public async Task Verify_ShouldOpenGuardianOwnSession_WhenNoAccountIsChosen()
    {
        var client = CreateClient();
        var device = await StartDeviceFlowAsync(client);

        // The "Me" radio posts an empty value.
        var response = await VerifyAsync(client, _guardian, device.UserCode, targetUserId: "", pin: null);

        response.Headers.Location!.OriginalString.Should().Be("/link?approved=true");
        var accessToken = await RedeemAsync(client, device.DeviceCode);
        ReadSubject(accessToken).Should().Be(_guardian.IdentityUserId);
    }

    [Test]
    public async Task Verify_ShouldRefuseThenAcceptSameCode_WhenPinIsWrongThenRight()
    {
        var client = CreateClient();
        var device = await StartDeviceFlowAsync(client);

        var refused = await VerifyAsync(client, _guardian, device.UserCode, _child.UserId, "0000");

        // The redirect keeps the code and the chosen account, so the retry stays on the child.
        var location = refused.Headers.Location!.OriginalString;
        location.Should().StartWith("/link?error=invalid_pin");
        location.Should().Contain($"target_user_id={_child.UserId}");
        location.Should().Contain($"user_code={Uri.EscapeDataString(device.UserCode)}");
        (await RedeemRawAsync(client, device.DeviceCode)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var accepted = await VerifyAsync(client, _guardian, device.UserCode, _child.UserId, ChildPin);

        accepted.Headers.Location!.OriginalString.Should().Be("/link?approved=true");
        var accessToken = await RedeemAsync(client, device.DeviceCode);
        ReadSubject(accessToken).Should().Be(_child.IdentityUserId);
    }

    [Test]
    public async Task Verify_ShouldRefuse_WhenPinIsMissing()
    {
        var client = CreateClient();
        var device = await StartDeviceFlowAsync(client);

        var response = await VerifyAsync(client, _guardian, device.UserCode, _child.UserId, pin: null);

        response.Headers.Location!.OriginalString.Should().StartWith("/link?error=invalid_pin");
        await AssertNoSessionAsync(client, device.DeviceCode);
    }

    [Test]
    public async Task Verify_ShouldRefuse_WhenCallerIsNotGuardianOfTheAccount()
    {
        var client = CreateClient();
        var device = await StartDeviceFlowAsync(client);

        // The right PIN does not help someone who is not a guardian of that account.
        var response = await VerifyAsync(client, _stranger, device.UserCode, _child.UserId, ChildPin);

        response.Headers.Location!.OriginalString.Should().StartWith("/link?error=access_denied");
        await AssertNoSessionAsync(client, device.DeviceCode);
    }

    [Test]
    public async Task Verify_ShouldRefuse_WhenManagedAccountAsksForItsGuardian()
    {
        var client = CreateClient();
        var device = await StartDeviceFlowAsync(client);

        var response = await VerifyAsync(client, _child, device.UserCode, _guardian.UserId, pin: null);

        response.Headers.Location!.OriginalString.Should().StartWith("/link?error=access_denied");
        await AssertNoSessionAsync(client, device.DeviceCode);
    }

    [Test]
    public async Task Verify_ShouldRefuse_WhenTargetIsNotAnAccountId()
    {
        var client = CreateClient();
        var device = await StartDeviceFlowAsync(client);

        var response = await VerifyAsync(client, _guardian, device.UserCode, targetUserId: "not-a-guid", pin: null);

        response.Headers.Location!.OriginalString.Should().Be("/link?error=access_denied");
        await AssertNoSessionAsync(client, device.DeviceCode);
    }

    [Test]
    public async Task ManagedAccounts_ShouldListOnlyAccountsOfTheSignedInGuardian()
    {
        var client = CreateClient();

        var forGuardian = await GetManagedAccountsAsync(client, _guardian);
        var forStranger = await GetManagedAccountsAsync(client, _stranger);
        var forChild = await GetManagedAccountsAsync(client, _child);

        forGuardian.Select(a => a.Id).Should().BeEquivalentTo([_child.UserId, _childWithoutPin.UserId]);
        forGuardian.Single(a => a.Id == _child.UserId).HasPin.Should().BeTrue();
        forGuardian.Single(a => a.Id == _childWithoutPin.UserId).HasPin.Should().BeFalse();
        forStranger.Should().BeEmpty();
        forChild.Should().BeEmpty();
    }

    private HttpClient CreateClient() => _app.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = false,
        BaseAddress = new Uri("https://localhost")
    });

    private static async Task<(string DeviceCode, string UserCode)> StartDeviceFlowAsync(HttpClient client)
    {
        var response = await client.PostAsync("/connect/device", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = "k7-native",
            ["scope"] = "openid profile roles api offline_access"
        }));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (
            document.RootElement.GetProperty("device_code").GetString()!,
            document.RootElement.GetProperty("user_code").GetString()!);
    }

    private Task<HttpResponseMessage> VerifyAsync(
        HttpClient client,
        Account signedIn,
        string userCode,
        Guid targetUserId,
        string? pin) =>
        VerifyAsync(client, signedIn, userCode, targetUserId.ToString(), pin);

    private async Task<HttpResponseMessage> VerifyAsync(
        HttpClient client,
        Account signedIn,
        string userCode,
        string targetUserId,
        string? pin)
    {
        var form = new Dictionary<string, string>
        {
            ["user_code"] = userCode,
            ["target_user_id"] = targetUserId
        };
        if (pin is not null)
            form["pin"] = pin;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/connect/verify")
        {
            Content = new FormUrlEncodedContent(form)
        };
        request.Headers.Add("Cookie", await CreateSessionCookieAsync(signedIn));

        return await client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> RedeemRawAsync(HttpClient client, string deviceCode) =>
        client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
            ["client_id"] = "k7-native",
            ["device_code"] = deviceCode
        }));

    private static async Task<string> RedeemAsync(HttpClient client, string deviceCode)
    {
        var response = await RedeemRawAsync(client, deviceCode);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("access_token").GetString()!;
    }

    // The device keeps polling and gets nothing: the code was not approved for anyone.
    private static async Task AssertNoSessionAsync(HttpClient client, string deviceCode)
    {
        var response = await RedeemRawAsync(client, deviceCode);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("error").GetString().Should().Be("authorization_pending");
    }

    private static string ReadSubject(string accessToken)
    {
        var payload = accessToken.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');

        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        return document.RootElement.GetProperty("sub").GetString()!;
    }

    private static async Task<UserDto> GetCurrentUserAsync(HttpClient client, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<UserDto>(Json))!;
    }

    private async Task<List<ManagedAccountDto>> GetManagedAccountsAsync(HttpClient client, Account signedIn)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me/managed-accounts");
        request.Headers.Add("Cookie", await CreateSessionCookieAsync(signedIn));

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<ManagedAccountDto>>(Json))!;
    }

    // The browser session of a signed-in user, as the sign-in page would have issued it.
    private async Task<string> CreateSessionCookieAsync(Account account)
    {
        using var scope = _app.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var principalFactory = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>();
        var options = scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        var user = (await userManager.FindByIdAsync(account.IdentityUserId))!;
        var now = DateTimeOffset.UtcNow;
        var ticket = new AuthenticationTicket(
            await principalFactory.CreateAsync(user),
            new AuthenticationProperties { IssuedUtc = now, ExpiresUtc = now.AddHours(1) },
            IdentityConstants.ApplicationScheme);

        return $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}";
    }

    private async Task SeedAsync()
    {
        using var scope = _app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<IServerSettingsService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await settings.SetAsync(ServerSettingKeys.SetupCompleted, true);

        _guardian = await AddAccountAsync(userManager, context, "guardian", pin: null);
        _stranger = await AddAccountAsync(userManager, context, "stranger", pin: null);
        _child = await AddAccountAsync(userManager, context, "child", ChildPin);
        _childWithoutPin = await AddAccountAsync(userManager, context, "toddler", pin: null);

        context.UserGuardians.AddRange(
            new UserGuardian { Id = Guid.NewGuid(), UserId = _child.UserId, GuardianUserId = _guardian.UserId },
            new UserGuardian { Id = Guid.NewGuid(), UserId = _childWithoutPin.UserId, GuardianUserId = _guardian.UserId });

        await context.SaveChangesAsync(CancellationToken.None);
    }

    private static async Task<Account> AddAccountAsync(
        UserManager<ApplicationUser> userManager,
        IApplicationDbContext context,
        string userName,
        string? pin)
    {
        var identity = new ApplicationUser { UserName = userName };
        (await userManager.CreateAsync(identity)).Succeeded.Should().BeTrue();
        (await userManager.AddToRoleAsync(identity, Roles.User)).Succeeded.Should().BeTrue();

        var user = new User
        {
            Id = Guid.NewGuid(),
            IdentityUserId = identity.Id,
            DisplayName = userName,
            PinHash = pin is null ? null : PinHashHelper.Hash(pin)
        };
        context.Users.Add(user);

        return new Account(user.Id, identity.Id);
    }

    private sealed record Account(Guid UserId, string IdentityUserId);
}
