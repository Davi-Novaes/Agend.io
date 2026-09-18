using System.Net;
using System.Net.Http.Json;

namespace Agendio.IntegrationTests;

/// <summary>
/// Cobre a rejeicao de slug reservado no cadastro de tenant (ver ADR 0009 —
/// subdominio-por-tenant: um slug reservado colidiria com um subdominio da
/// propria plataforma, tipo api.agendiobr.com.br ou www.agendiobr.com.br).
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class TenantSlugTests(IntegrationTestFixture fixture)
{
    [Theory]
    [InlineData("www")]
    [InlineData("api")]
    [InlineData("admin")]
    public async Task Should_Reject_Tenant_Creation_When_Slug_Is_Reserved(string reservedSlug)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/tenants", new
        {
            name = $"Tenant {Guid.NewGuid():N}",
            slug = reservedSlug,
            businessType = "Other",
            timeZoneId = "America/Sao_Paulo",
        }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_Accept_Tenant_Creation_When_Slug_Is_Not_Reserved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/tenants", new
        {
            name = $"Tenant {Guid.NewGuid():N}",
            slug = $"barbearia-{Guid.NewGuid():N}",
            businessType = "Other",
            timeZoneId = "America/Sao_Paulo",
        }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
