using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Agendio.IntegrationTests;

/// <summary>
/// Regressao do BL-05 (docs/BACKLOG.md): antes do GlobalExceptionHandler +
/// DefaultPolicy exigindo a claim tenant_id, uma excecao nao tratada (JSON
/// malformado, ou um token valido mas sem tenant_id) vazava stack trace
/// completo — incluindo caminho de arquivo do servidor e, no caso do token
/// sem tenant_id, os proprios headers da requisicao (Bearer token incluso) —
/// no corpo da resposta. Reproduzido pelo QA audit em 3 endpoints diferentes
/// e pelo Backend audit via curl manual; aqui vira teste automatizado.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class ExceptionHandlingTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Malformed_DueDate_On_Create_Payable_Should_Return_Clean_ProblemDetails_Not_A_Stack_Trace()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        // Mesma reproducao exata do achado do QA audit (C5-bad/F3/F4): formato
        // de data que o System.Text.Json rejeita ANTES do FluentValidation
        // rodar, disparando BadHttpRequestException nao tratada.
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/financeiro/contas-a-pagar")
        {
            Content = new StringContent(
                """{"description":"x","amount":100,"dueDate":"31-31-9999","category":"Other"}""",
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        body.ShouldNotContain("System.FormatException");
        body.ShouldNotContain("Agendio.Api");
        body.ShouldNotContain(" at ");
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Authenticated_Token_Without_TenantId_Claim_Should_Be_Rejected_With_401_Not_500()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        // JWT valido (mesma chave/issuer/audience de teste configurada em
        // IntegrationTestFixture) mas SEM a claim tenant_id — simula tanto um
        // bug de emissao quanto um token manualmente forjado.
        var forgedToken = BuildTokenWithoutTenantClaim();

        var readRequest = new HttpRequestMessage(HttpMethod.Get, "/api/customers");
        readRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", forgedToken);
        var readResponse = await client.SendAsync(readRequest, cancellationToken);
        // Antes da correcao: 200 com lista vazia (mascarava o problema). Agora:
        // a DefaultPolicy barra na camada de autorizacao, antes do handler —
        // 403 (nao 401) porque a assinatura/issuer/audience sao validos, so a
        // claim exigida pela policy que falta (autenticado, mas nao autorizado).
        readResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var writeRequest = new HttpRequestMessage(HttpMethod.Post, "/api/customers")
        {
            Content = JsonContent.Create(new { fullName = "Sem Tenant" }),
        };
        writeRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", forgedToken);
        var writeResponse = await client.SendAsync(writeRequest, cancellationToken);
        // Antes da correcao: 500 com stack trace + headers (Bearer token
        // incluso) refletidos no corpo. Agora: mesmo 403 limpo do caminho de leitura.
        writeResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var writeBody = await writeResponse.Content.ReadAsStringAsync(cancellationToken);
        writeBody.ShouldNotContain("InvalidOperationException");
        writeBody.ShouldNotContain("Bearer ");
    }

    private static string BuildTokenWithoutTenantClaim()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Email, "sem-tenant@example.com"),
            new Claim(ClaimTypes.Role, "Owner"),
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("integration-test-signing-key-min-32-bytes-long!!"));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "https://agendio.test",
            audience: "agendio-tests",
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
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
        var tenantBody = await tenantResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(cancellationToken);
        var tenantId = tenantBody.GetProperty("id").GetGuid();

        var ownerEmail = $"owner-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync(
            "/api/auth/register", new { tenantId, email = ownerEmail, password = Password, fullName = "Dono" }, cancellationToken);
        await fixture.ConfirmEmailDirectlyAsync(tenantId, ownerEmail, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(cancellationToken);

        return loginBody.GetProperty("accessToken").GetString()!;
    }
}
