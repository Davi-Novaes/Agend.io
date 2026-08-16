using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace Agendio.IntegrationTests;

/// <summary>
/// Requisito inegociavel do CLAUDE.md: "dado sensivel (CPF, saude) criptografado
/// em coluna" (ver docs/adr/0007). Le a coluna CRUA (bypassando API e EF Core)
/// para provar que o valor persistido no Postgres nao e o texto plano — se
/// alguem trocar EncryptedStringConverter por uma conversao passthrough por
/// engano, este teste (nao so o roundtrip via API) pega o erro.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class CustomerEncryptionTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";
    private const string ValidCpf = "529.982.247-25";
    private const string HealthNotes = "Alergia a penicilina; convenio Unimed.";

    [Fact]
    public async Task Should_Persist_Cpf_And_Health_Notes_Encrypted_At_Rest_When_Customer_Created()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/customers",
            new { fullName = "Carlos Souza", cpf = ValidCpf, healthNotes = HealthNotes },
            cancellationToken);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var customerId = createBody.GetProperty("id").GetGuid();

        await using var connection = new NpgsqlConnection(fixture.DatabaseOwnerConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT cpf, health_notes FROM customers.customers WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", customerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        var rawCpf = reader.GetString(0);
        var rawHealthNotes = reader.GetString(1);

        // Nao e o texto plano...
        rawCpf.ShouldNotBe("52998224725");
        rawCpf.ShouldNotContain("529");
        rawHealthNotes.ShouldNotBe(HealthNotes);
        rawHealthNotes.ShouldNotContain("penicilina");

        // ...mas e um base64 valido (formato do EncryptedStringConverter: nonce+tag+ciphertext).
        Should.NotThrow(() => Convert.FromBase64String(rawCpf));
        Should.NotThrow(() => Convert.FromBase64String(rawHealthNotes));
    }

    [Fact]
    public async Task Should_Return_Decrypted_Cpf_And_Health_Notes_When_Fetching_Customer_By_Id()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/customers",
            new { fullName = "Beatriz Lima", cpf = ValidCpf, healthNotes = HealthNotes },
            cancellationToken);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var customerId = createBody.GetProperty("id").GetGuid();

        var getResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, $"/api/customers/{customerId}", cancellationToken);
        var getBody = await getResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        getBody.GetProperty("cpf").GetString().ShouldBe("52998224725");
        getBody.GetProperty("healthNotes").GetString().ShouldBe(HealthNotes);
    }

    [Fact]
    public async Task Should_Reject_Customer_Creation_When_Cpf_Has_Invalid_Check_Digit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/customers",
            new { fullName = "Cpf Invalido", cpf = "529.982.247-26" },
            cancellationToken);

        createResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_Redact_Cpf_And_Health_Notes_In_Audit_Log_When_Customer_Created()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/customers",
            new { fullName = "Renata Dias", cpf = ValidCpf, healthNotes = HealthNotes },
            cancellationToken);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var customerId = createBody.GetProperty("id").GetGuid();

        var auditResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, $"/api/customers/{customerId}/audit-log", cancellationToken);
        var auditBody = await auditResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        var created = auditBody.GetProperty("items").EnumerateArray()
            .Single(e => e.GetProperty("action").GetString() == "Created");
        var after = created.GetProperty("after").GetString()!;

        after.ShouldNotContain("52998224725");
        after.ShouldNotContain("penicilina");

        var afterJson = JsonDocument.Parse(after).RootElement;
        afterJson.GetProperty("Cpf").GetString().ShouldBe("***");
        afterJson.GetProperty("HealthNotes").GetString().ShouldBe("***");
    }

    private async Task<string> CreateTenantWithOwnerAndLoginAsync(HttpClient client, CancellationToken cancellationToken)
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
        await fixture.ConfirmEmailDirectlyAsync(tenantId, ownerEmail, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return loginBody.GetProperty("accessToken").GetString()!;
    }
}
