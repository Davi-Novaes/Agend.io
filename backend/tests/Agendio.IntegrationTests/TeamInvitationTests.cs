using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Agendio.IntegrationTests;

/// <summary>
/// Cobre o convite de equipe ponta a ponta: quem pode convidar (so Owner),
/// o link do convite realmente cria a conta com a senha escolhida, e um
/// convite ja aceito nao pode ser reaproveitado.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class TeamInvitationTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Owner_Can_Invite_A_New_Member_And_The_Invitation_Can_Be_Accepted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var (tenantId, ownerEmail) = await CreateTenantWithOwnerAsync(client, cancellationToken);
        var ownerAccessToken = await LoginAsync(client, tenantId, ownerEmail, cancellationToken);

        var inviteEmail = $"staff-{Guid.NewGuid():N}@example.com";
        var inviteResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, ownerAccessToken, "/api/team/invitations", new { email = inviteEmail, role = "Staff" }, cancellationToken);
        inviteResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var inviteBody = await inviteResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var token = inviteBody.GetProperty("token").GetString()!;

        var acceptResponse = await client.PostAsJsonAsync(
            $"/api/team/invitations/{token}/accept",
            new { fullName = "Novo Funcionario", password = Password },
            cancellationToken);
        acceptResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Prova real: o convidado consegue logar com a senha que definiu.
        var newMemberLogin = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = inviteEmail, password = Password }, cancellationToken);
        newMemberLogin.StatusCode.ShouldBe(HttpStatusCode.OK);

        var membersResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, ownerAccessToken, "/api/team/members", cancellationToken);
        var members = await membersResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        members.GetArrayLength().ShouldBe(2); // dono + convidado
    }

    [Fact]
    public async Task Accepting_The_Same_Invitation_Twice_Should_Fail_The_Second_Time()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var (tenantId, ownerEmail) = await CreateTenantWithOwnerAsync(client, cancellationToken);
        var ownerAccessToken = await LoginAsync(client, tenantId, ownerEmail, cancellationToken);

        var inviteEmail = $"staff-{Guid.NewGuid():N}@example.com";
        var inviteResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, ownerAccessToken, "/api/team/invitations", new { email = inviteEmail, role = "Staff" }, cancellationToken);
        var inviteBody = await inviteResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var token = inviteBody.GetProperty("token").GetString()!;

        var firstAccept = await client.PostAsJsonAsync(
            $"/api/team/invitations/{token}/accept", new { fullName = "Primeira Vez", password = Password }, cancellationToken);
        firstAccept.StatusCode.ShouldBe(HttpStatusCode.OK);

        var secondAccept = await client.PostAsJsonAsync(
            $"/api/team/invitations/{token}/accept", new { fullName = "Segunda Vez", password = Password }, cancellationToken);
        secondAccept.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_Staff_Member_Should_Not_Be_Able_To_Invite_Anyone()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var (tenantId, ownerEmail) = await CreateTenantWithOwnerAsync(client, cancellationToken);
        var ownerAccessToken = await LoginAsync(client, tenantId, ownerEmail, cancellationToken);

        var staffEmail = $"staff-{Guid.NewGuid():N}@example.com";
        var inviteResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, ownerAccessToken, "/api/team/invitations", new { email = staffEmail, role = "Staff" }, cancellationToken);
        var inviteBody = await inviteResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var token = inviteBody.GetProperty("token").GetString()!;
        await client.PostAsJsonAsync(
            $"/api/team/invitations/{token}/accept", new { fullName = "Funcionario", password = Password }, cancellationToken);

        var staffAccessToken = await LoginAsync(client, tenantId, staffEmail, cancellationToken);

        var forbiddenAttempt = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, staffAccessToken, "/api/team/invitations",
            new { email = $"outro-{Guid.NewGuid():N}@example.com", role = "Staff" }, cancellationToken);

        forbiddenAttempt.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_Request_To_Invite_Should_Be_Unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/team/invitations", new { email = "alguem@example.com", role = "Staff" }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task<(Guid TenantId, string OwnerEmail)> CreateTenantWithOwnerAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var tenantResponse = await client.PostAsJsonAsync("/api/tenants", new
        {
            name = $"Tenant {Guid.NewGuid():N}",
            slug = $"tenant-{Guid.NewGuid():N}",
            businessType = "Other",
            timeZoneId = "America/Sao_Paulo",
        }, cancellationToken);
        tenantResponse.EnsureSuccessStatusCode();
        var tenantBody = await tenantResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var tenantId = tenantBody.GetProperty("id").GetGuid();

        var ownerEmail = $"owner-{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register", new { tenantId, email = ownerEmail, password = Password, fullName = "Dono" }, cancellationToken);
        registerResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (tenantId, ownerEmail);
    }

    private static async Task<string> LoginAsync(HttpClient client, Guid tenantId, string email, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email, password = Password }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("accessToken").GetString()!;
    }
}
