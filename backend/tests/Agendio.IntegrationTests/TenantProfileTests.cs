using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Agendio.IntegrationTests;

/// <summary>
/// Cobre o perfil do estabelecimento (Fase 1 — fundacao): contato, endereco,
/// redes sociais e horario de funcionamento.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class TenantProfileTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Owner_Can_Update_And_Read_Back_The_Tenant_Profile()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var updateResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/profile",
            new
            {
                description = "Cortes classicos e modernos",
                phone = "11999998888",
                whatsApp = "11988887777",
                email = "contato@barbearia.com",
                address = "Rua das Flores, 100",
                instagramUrl = "https://instagram.com/barbearia",
                facebookUrl = "https://facebook.com/barbearia",
            },
            cancellationToken);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var profileResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/tenants/profile", cancellationToken);
        profileResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var profile = await profileResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        profile.GetProperty("description").GetString().ShouldBe("Cortes classicos e modernos");
        profile.GetProperty("phone").GetString().ShouldBe("+5511999998888");
        profile.GetProperty("whatsApp").GetString().ShouldBe("+5511988887777");
        profile.GetProperty("email").GetString().ShouldBe("contato@barbearia.com");
        profile.GetProperty("address").GetString().ShouldBe("Rua das Flores, 100");
        profile.GetProperty("instagramUrl").GetString().ShouldBe("https://instagram.com/barbearia");
        profile.GetProperty("facebookUrl").GetString().ShouldBe("https://facebook.com/barbearia");
    }

    [Fact]
    public async Task Updating_Profile_With_An_Invalid_Email_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var response = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/profile", new { email = "nao-e-um-email" }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Owner_Can_Set_And_Read_Back_Business_Hours()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var setResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/business-hours",
            new
            {
                entries = new[]
                {
                    new { dayOfWeek = "Monday", startTime = "09:00:00", endTime = "18:00:00" },
                    new { dayOfWeek = "Saturday", startTime = "10:00:00", endTime = "14:00:00" },
                },
            },
            cancellationToken);
        setResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var profileResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/tenants/profile", cancellationToken);
        var profile = await profileResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var businessHours = profile.GetProperty("businessHours");

        businessHours.GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task Public_By_Slug_Endpoint_Exposes_The_Full_Public_Profile_Without_Authentication()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (slug, accessToken) = await CreateTenantWithOwnerAndLoginAsyncWithSlug(client, cancellationToken);

        await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/profile",
            new
            {
                description = "Cortes classicos e modernos",
                phone = "11999998888",
                whatsApp = "11988887777",
                email = "contato@barbearia.com",
                address = "Rua das Flores, 100",
                instagramUrl = "https://instagram.com/barbearia",
                facebookUrl = "https://facebook.com/barbearia",
            },
            cancellationToken);
        await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/business-hours",
            new { entries = new[] { new { dayOfWeek = "Monday", startTime = "09:00:00", endTime = "18:00:00" } } },
            cancellationToken);

        var response = await client.GetAsync($"/api/tenants/by-slug/{slug}", cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("description").GetString().ShouldBe("Cortes classicos e modernos");
        body.GetProperty("phone").GetString().ShouldBe("+5511999998888");
        body.GetProperty("whatsApp").GetString().ShouldBe("+5511988887777");
        body.GetProperty("address").GetString().ShouldBe("Rua das Flores, 100");
        body.GetProperty("instagramUrl").GetString().ShouldBe("https://instagram.com/barbearia");
        body.GetProperty("businessHours").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Public_By_Slug_Endpoint_For_An_Unknown_Slug_Should_Return_NotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var response = await client.GetAsync($"/api/tenants/by-slug/nao-existe-{Guid.NewGuid():N}", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Owner_Can_Update_And_Read_Back_The_Page_Customization()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (slug, accessToken) = await CreateTenantWithOwnerAndLoginAsyncWithSlug(client, cancellationToken);

        var updateResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/page-customization",
            new
            {
                secondaryColorHex = "#0F172A",
                font = "Poppins",
                buttonStyle = "Pill",
                showAboutSection = false,
                showServicesSection = true,
                showTeamSection = false,
                showHoursSection = true,
                showContactSection = false,
            },
            cancellationToken);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var profileResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/tenants/profile", cancellationToken);
        var profile = await profileResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        profile.GetProperty("secondaryColorHex").GetString().ShouldBe("#0F172A");
        profile.GetProperty("font").GetString().ShouldBe("Poppins");
        profile.GetProperty("buttonStyle").GetString().ShouldBe("Pill");
        profile.GetProperty("showAboutSection").GetBoolean().ShouldBeFalse();
        profile.GetProperty("showServicesSection").GetBoolean().ShouldBeTrue();
        profile.GetProperty("showTeamSection").GetBoolean().ShouldBeFalse();

        // Precisa refletir tambem no perfil publico (sem autenticacao) — e a
        // pagina publica quem consome esses campos de fato.
        var publicResponse = await client.GetAsync($"/api/tenants/by-slug/{slug}", cancellationToken);
        var publicProfile = await publicResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        publicProfile.GetProperty("secondaryColorHex").GetString().ShouldBe("#0F172A");
        publicProfile.GetProperty("font").GetString().ShouldBe("Poppins");
        publicProfile.GetProperty("showAboutSection").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Updating_Page_Customization_With_An_Insufficient_Contrast_Color_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var response = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/page-customization",
            new
            {
                secondaryColorHex = "#FFFF00",
                font = "Default",
                buttonStyle = "Rounded",
                showAboutSection = true,
                showServicesSection = true,
                showTeamSection = true,
                showHoursSection = true,
                showContactSection = true,
            },
            cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Anonymous_Request_To_Update_Page_Customization_Should_Be_Unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var response = await client.PutAsJsonAsync(
            "/api/tenants/page-customization",
            new
            {
                secondaryColorHex = (string?)null,
                font = "Default",
                buttonStyle = "Rounded",
                showAboutSection = true,
                showServicesSection = true,
                showTeamSection = true,
                showHoursSection = true,
                showContactSection = true,
            },
            cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Anonymous_Request_To_Get_Or_Update_Profile_Should_Be_Unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var getResponse = await client.GetAsync("/api/tenants/profile", cancellationToken);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var putResponse = await client.PutAsJsonAsync("/api/tenants/profile", new { description = "x" }, cancellationToken);
        putResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_Staff_Member_Should_Not_Be_Able_To_Update_The_Tenant_Profile()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, ownerToken) = await CreateTenantWithOwnerAndLoginAsyncWithId(client, cancellationToken);

        var staffEmail = $"staff-{Guid.NewGuid():N}@example.com";
        var inviteResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, ownerToken, "/api/team/invitations", new { email = staffEmail, role = "Staff" }, cancellationToken);
        var inviteBody = await inviteResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var token = inviteBody.GetProperty("token").GetString()!;
        await client.PostAsJsonAsync(
            $"/api/team/invitations/{token}/accept", new { fullName = "Funcionario", password = Password }, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = staffEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var staffToken = loginBody.GetProperty("accessToken").GetString()!;

        var response = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, staffToken, "/api/tenants/profile", new { description = "Tentativa nao autorizada" }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Owner_Can_Update_And_Read_Back_Reminder_Settings()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        // Novo tenant nasce com tudo ligado (default sensato) — confirma isso
        // antes de desligar, pra provar o zero-config e nao so o resultado do teste.
        var initialProfileResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/tenants/profile", cancellationToken);
        var initialProfile = await initialProfileResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        initialProfile.GetProperty("reminder24hEnabled").GetBoolean().ShouldBeTrue();
        initialProfile.GetProperty("reminder2hEnabled").GetBoolean().ShouldBeTrue();
        initialProfile.GetProperty("postServiceThankYouEnabled").GetBoolean().ShouldBeTrue();

        var updateResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/reminder-settings",
            new { reminder24hEnabled = false, reminder2hEnabled = true, postServiceThankYouEnabled = false },
            cancellationToken);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var profileResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/tenants/profile", cancellationToken);
        var profile = await profileResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        profile.GetProperty("reminder24hEnabled").GetBoolean().ShouldBeFalse();
        profile.GetProperty("reminder2hEnabled").GetBoolean().ShouldBeTrue();
        profile.GetProperty("postServiceThankYouEnabled").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Owner_Can_Update_And_Read_Back_The_No_Show_Policy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        // Novo tenant nasce com o aviso desligado (zero-config) — confirma isso
        // antes de ligar, pra provar o default sensato e nao so o resultado do teste.
        var initialProfileResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/tenants/profile", cancellationToken);
        var initialProfile = await initialProfileResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        initialProfile.GetProperty("requireDepositAfterNoShows").GetBoolean().ShouldBeFalse();
        initialProfile.GetProperty("noShowThresholdForDeposit").GetInt32().ShouldBe(2);

        var updateResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/no-show-policy",
            new { requireDepositAfterNoShows = true, noShowThresholdForDeposit = 3 },
            cancellationToken);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var profileResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/tenants/profile", cancellationToken);
        var profile = await profileResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        profile.GetProperty("requireDepositAfterNoShows").GetBoolean().ShouldBeTrue();
        profile.GetProperty("noShowThresholdForDeposit").GetInt32().ShouldBe(3);
    }

    [Fact]
    public async Task Updating_No_Show_Policy_With_A_Zero_Threshold_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var response = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/no-show-policy",
            new { requireDepositAfterNoShows = true, noShowThresholdForDeposit = 0 },
            cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Owner_Can_Update_And_Read_Back_Payment_Settings()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        // Novo tenant nasce sem exigir pagamento (zero-config) — confirma isso
        // antes de ligar, pra provar o default sensato e nao so o resultado do teste.
        var initialProfileResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/tenants/profile", cancellationToken);
        var initialProfile = await initialProfileResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        initialProfile.GetProperty("paymentRequired").GetBoolean().ShouldBeFalse();
        initialProfile.GetProperty("depositPercentage").GetInt32().ShouldBe(30);

        var updateResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/payment-settings",
            new { paymentRequired = true, depositPercentage = 40 },
            cancellationToken);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var profileResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/tenants/profile", cancellationToken);
        var profile = await profileResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        profile.GetProperty("paymentRequired").GetBoolean().ShouldBeTrue();
        profile.GetProperty("depositPercentage").GetInt32().ShouldBe(40);
    }

    [Fact]
    public async Task Updating_Payment_Settings_With_An_Out_Of_Range_Percentage_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var response = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/payment-settings",
            new { paymentRequired = true, depositPercentage = 0 },
            cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Owner_Can_Connect_WhatsApp_And_Read_Back_Settings_Without_Exposing_The_Raw_Token()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var updateResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/whatsapp-settings",
            new
            {
                enabled = true,
                phoneNumberId = "1234567890",
                accessToken = "meta-cloud-api-secret-token",
                scheduledTemplate = "Ola {{cliente}}, agendado para {{data}} as {{hora}}.",
                reminderTemplate = (string?)null,
                cancelledTemplate = (string?)null,
                rescheduledTemplate = (string?)null,
                confirmedTemplate = (string?)null,
                completedTemplate = (string?)null,
            },
            cancellationToken);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var profileResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/tenants/profile", cancellationToken);
        var profile = await profileResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        profile.GetProperty("whatsAppIntegrationEnabled").GetBoolean().ShouldBeTrue();
        profile.GetProperty("whatsAppPhoneNumberId").GetString().ShouldBe("1234567890");
        profile.GetProperty("whatsAppAccessTokenConfigured").GetBoolean().ShouldBeTrue();
        profile.GetProperty("whatsAppScheduledTemplate").GetString().ShouldBe("Ola {{cliente}}, agendado para {{data}} as {{hora}}.");

        // O token de acesso e um segredo — a API nunca devolve o valor em texto
        // puro, so o bool "configurado" acima.
        profile.TryGetProperty("whatsAppAccessToken", out _).ShouldBeFalse();
        profile.GetRawText().ShouldNotContain("meta-cloud-api-secret-token");
    }

    [Fact]
    public async Task Enabling_WhatsApp_Without_Credentials_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var response = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/whatsapp-settings",
            new
            {
                enabled = true,
                phoneNumberId = (string?)null,
                accessToken = (string?)null,
                scheduledTemplate = (string?)null,
                reminderTemplate = (string?)null,
                cancelledTemplate = (string?)null,
                rescheduledTemplate = (string?)null,
                confirmedTemplate = (string?)null,
                completedTemplate = (string?)null,
            },
            cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Updating_WhatsApp_Settings_Without_A_New_Token_Keeps_The_Existing_One()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/whatsapp-settings",
            new
            {
                enabled = true,
                phoneNumberId = "1234567890",
                accessToken = "meta-cloud-api-secret-token",
                scheduledTemplate = (string?)null,
                reminderTemplate = (string?)null,
                cancelledTemplate = (string?)null,
                rescheduledTemplate = (string?)null,
                confirmedTemplate = (string?)null,
                completedTemplate = (string?)null,
            },
            cancellationToken);

        // Segunda chamada sem digitar um token novo (nem reenviar o antigo,
        // que a API nunca devolve) — a integracao precisa continuar ativa.
        var secondUpdateResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/whatsapp-settings",
            new
            {
                enabled = true,
                phoneNumberId = "1234567890",
                accessToken = (string?)null,
                scheduledTemplate = "Novo template para {{cliente}}.",
                reminderTemplate = (string?)null,
                cancelledTemplate = (string?)null,
                rescheduledTemplate = (string?)null,
                confirmedTemplate = (string?)null,
                completedTemplate = (string?)null,
            },
            cancellationToken);
        secondUpdateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var profileResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/tenants/profile", cancellationToken);
        var profile = await profileResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        profile.GetProperty("whatsAppIntegrationEnabled").GetBoolean().ShouldBeTrue();
        profile.GetProperty("whatsAppAccessTokenConfigured").GetBoolean().ShouldBeTrue();
        profile.GetProperty("whatsAppScheduledTemplate").GetString().ShouldBe("Novo template para {{cliente}}.");
    }

    [Fact]
    public async Task Anonymous_Request_To_Update_WhatsApp_Settings_Should_Be_Unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var response = await client.PutAsJsonAsync(
            "/api/tenants/whatsapp-settings",
            new
            {
                enabled = false,
                phoneNumberId = (string?)null,
                accessToken = (string?)null,
                scheduledTemplate = (string?)null,
                reminderTemplate = (string?)null,
                cancelledTemplate = (string?)null,
                rescheduledTemplate = (string?)null,
                confirmedTemplate = (string?)null,
                completedTemplate = (string?)null,
            },
            cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task<string> CreateTenantWithOwnerAndLoginAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var (_, accessToken) = await CreateTenantWithOwnerAndLoginAsyncWithId(client, cancellationToken);
        return accessToken;
    }

    private static async Task<(string Slug, string AccessToken)> CreateTenantWithOwnerAndLoginAsyncWithSlug(
        HttpClient client, CancellationToken cancellationToken)
    {
        var slug = $"tenant-{Guid.NewGuid():N}";

        var tenantResponse = await client.PostAsJsonAsync("/api/tenants", new
        {
            name = $"Tenant {Guid.NewGuid():N}",
            slug,
            businessType = "Other",
            timeZoneId = "America/Sao_Paulo",
        }, cancellationToken);
        tenantResponse.EnsureSuccessStatusCode();
        var tenantBody = await tenantResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var tenantId = tenantBody.GetProperty("id").GetGuid();

        var ownerEmail = $"owner-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync(
            "/api/auth/register", new { tenantId, email = ownerEmail, password = Password, fullName = "Dono" }, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return (slug, loginBody.GetProperty("accessToken").GetString()!);
    }

    private static async Task<(Guid TenantId, string AccessToken)> CreateTenantWithOwnerAndLoginAsyncWithId(
        HttpClient client, CancellationToken cancellationToken)
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
        await client.PostAsJsonAsync(
            "/api/auth/register", new { tenantId, email = ownerEmail, password = Password, fullName = "Dono" }, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return (tenantId, loginBody.GetProperty("accessToken").GetString()!);
    }
}
