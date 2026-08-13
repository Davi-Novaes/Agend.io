using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Agendio.IntegrationTests;

/// <summary>
/// O portal publico (Sprint 4): navegar servicos/recursos e agendar sem login
/// previo. O ponto mais delicado e o motor de disponibilidade — ele precisa
/// respeitar o horario de trabalho do recurso E excluir horarios ja ocupados,
/// e o isolamento entre tenants precisa se manter mesmo sem JWT (o tenant vem
/// explicito na rota, nao de claim — ver IHasExplicitTenant).
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class PublicPortalTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Public_Endpoints_Only_List_Active_Services_And_Resources()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, accessToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (resourceId, serviceId) = await SetUpBookableResourceAndServiceAsync(client, accessToken, cancellationToken);

        var inactiveServiceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/services",
            new { name = "Servico Inativo", description = (string?)null, durationMinutes = 30, price = 10m, currency = "BRL", category = (string?)null },
            cancellationToken);
        var inactiveServiceBody = await inactiveServiceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var inactiveServiceId = inactiveServiceBody.GetProperty("id").GetGuid();
        var deactivateResponse = await AuthorizedRequestHelpers.PatchAuthorizedAsync(
            client, accessToken, $"/api/services/{inactiveServiceId}/status", new { isActive = false }, cancellationToken);
        deactivateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var servicesResponse = await client.GetAsync($"/api/public/tenants/{tenantId}/services", cancellationToken);
        servicesResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var services = await servicesResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var serviceIds = services.EnumerateArray().Select(s => s.GetProperty("id").GetGuid()).ToList();
        serviceIds.ShouldContain(serviceId);
        serviceIds.ShouldNotContain(inactiveServiceId);

        var resourcesResponse = await client.GetAsync($"/api/public/tenants/{tenantId}/resources", cancellationToken);
        resourcesResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var resources = await resourcesResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        resources.EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).ShouldContain(resourceId);
    }

    [Fact]
    public async Task Public_Services_Endpoint_Exposes_Image_And_Is_Ordered_By_DisplayOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, accessToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var secondResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/services",
            new { name = "Segundo", description = (string?)null, durationMinutes = 30, price = 10m, currency = "BRL", category = (string?)null, displayOrder = 2 },
            cancellationToken);
        var secondId = (await secondResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();

        var firstResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/services",
            new { name = "Primeiro", description = (string?)null, durationMinutes = 30, price = 10m, currency = "BRL", category = (string?)null, displayOrder = 1 },
            cancellationToken);
        var firstId = (await firstResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();

        var servicesResponse = await client.GetAsync($"/api/public/tenants/{tenantId}/services", cancellationToken);
        servicesResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var services = (await servicesResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).EnumerateArray().ToList();

        var orderedIds = services.Select(s => s.GetProperty("id").GetGuid()).ToList();
        orderedIds.IndexOf(firstId).ShouldBeLessThan(orderedIds.IndexOf(secondId));
        services.Single(s => s.GetProperty("id").GetGuid() == firstId).GetProperty("imageUrl").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Public_Resources_Endpoint_Exposes_Photo_And_Specialties()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, accessToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var resourceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/resources",
            new { name = "Dra. Ana", type = "Person", capacity = 1, description = (string?)null }, cancellationToken);
        var resourceId = (await resourceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();

        var specialtiesResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, $"/api/resources/{resourceId}/specialties", new { specialties = new[] { "Corte", "Coloracao" } }, cancellationToken);
        specialtiesResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var resourcesResponse = await client.GetAsync($"/api/public/tenants/{tenantId}/resources", cancellationToken);
        resourcesResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var resources = (await resourcesResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).EnumerateArray().ToList();

        var resource = resources.Single(r => r.GetProperty("id").GetGuid() == resourceId);
        resource.GetProperty("specialties").EnumerateArray().Select(s => s.GetString()).ShouldBe(["Corte", "Coloracao"]);
        resource.GetProperty("photoUrl").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Availability_Respects_Working_Hours_And_Excludes_Booked_Slots()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, accessToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (resourceId, serviceId) = await SetUpBookableResourceAndServiceAsync(client, accessToken, cancellationToken);

        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        await SetWorkingHoursForDateAsync(client, accessToken, resourceId, targetDate, "09:00:00", "12:00:00", cancellationToken);

        var availabilityResponse = await client.GetAsync(
            $"/api/public/tenants/{tenantId}/availability?resourceId={resourceId}&serviceId={serviceId}&date={targetDate:yyyy-MM-dd}",
            cancellationToken);
        availabilityResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var slots = (await availabilityResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken))
            .EnumerateArray().ToList();
        slots.ShouldNotBeEmpty();

        var firstSlotStart = slots[0].GetProperty("startUtc").GetDateTimeOffset();

        var bookingResponse = await client.PostAsJsonAsync($"/api/public/tenants/{tenantId}/appointments", new
        {
            resourceId,
            serviceId,
            startAtUtc = firstSlotStart,
            customerFullName = "Visitante do Portal",
            customerEmail = $"visitante-{Guid.NewGuid():N}@example.com",
            customerPhone = (string?)null,
            notes = (string?)null,
        }, cancellationToken);
        bookingResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var availabilityAfterBookingResponse = await client.GetAsync(
            $"/api/public/tenants/{tenantId}/availability?resourceId={resourceId}&serviceId={serviceId}&date={targetDate:yyyy-MM-dd}",
            cancellationToken);
        var slotsAfterBooking = (await availabilityAfterBookingResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken))
            .EnumerateArray().Select(s => s.GetProperty("startUtc").GetDateTimeOffset()).ToList();

        slotsAfterBooking.ShouldNotContain(firstSlotStart);
    }

    [Fact]
    public async Task Availability_Is_Empty_On_A_Day_The_Resource_Has_TimeOff()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, accessToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (resourceId, serviceId) = await SetUpBookableResourceAndServiceAsync(client, accessToken, cancellationToken);

        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(9));
        await SetWorkingHoursForDateAsync(client, accessToken, resourceId, targetDate, "09:00:00", "18:00:00", cancellationToken);

        var timeOffResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, $"/api/resources/{resourceId}/time-off",
            new { startDate = targetDate, endDate = targetDate, reason = "Ferias" }, cancellationToken);
        timeOffResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var slots = await GetAvailableSlotsAsync(client, tenantId, resourceId, serviceId, targetDate, cancellationToken);

        slots.ShouldBeEmpty();
    }

    [Fact]
    public async Task Public_Booking_On_A_Day_The_Resource_Has_TimeOff_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, accessToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (resourceId, serviceId) = await SetUpBookableResourceAndServiceAsync(client, accessToken, cancellationToken);

        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        await SetWorkingHoursForDateAsync(client, accessToken, resourceId, targetDate, "09:00:00", "18:00:00", cancellationToken);
        await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, $"/api/resources/{resourceId}/time-off",
            new { startDate = targetDate, endDate = targetDate, reason = "Ferias" }, cancellationToken);

        // Chama a confirmacao diretamente (nao passa pela lista de disponibilidade,
        // que ja excluiria o dia) para provar que a rejeicao acontece nos dois pontos.
        var startAtUtc = new DateTimeOffset(targetDate.ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero);
        var response = await client.PostAsJsonAsync($"/api/public/tenants/{tenantId}/appointments", new
        {
            resourceId,
            serviceId,
            startAtUtc,
            customerFullName = "Visitante",
            customerEmail = $"visitante-{Guid.NewGuid():N}@example.com",
            customerPhone = (string?)null,
            notes = (string?)null,
        }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Availability_Is_Empty_On_A_Tenant_Closed_Date()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, accessToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (resourceId, serviceId) = await SetUpBookableResourceAndServiceAsync(client, accessToken, cancellationToken);

        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(11));
        await SetWorkingHoursForDateAsync(client, accessToken, resourceId, targetDate, "09:00:00", "18:00:00", cancellationToken);

        var settingsResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/scheduling-settings",
            new { closedDates = new[] { new { date = targetDate, reason = "Feriado" } }, appointmentBufferMinutes = 0 },
            cancellationToken);
        settingsResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var slots = await GetAvailableSlotsAsync(client, tenantId, resourceId, serviceId, targetDate, cancellationToken);

        slots.ShouldBeEmpty();
    }

    [Fact]
    public async Task Availability_Is_Restricted_To_The_Tenant_Business_Hours_When_Configured()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, accessToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (resourceId, serviceId) = await SetUpBookableResourceAndServiceAsync(client, accessToken, cancellationToken);

        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(12));
        // O recurso trabalha ate 18:00, mas o estabelecimento so abre ate 12:00
        // nesse dia — a disponibilidade efetiva precisa respeitar o menor dos dois.
        await SetWorkingHoursForDateAsync(client, accessToken, resourceId, targetDate, "09:00:00", "18:00:00", cancellationToken);
        var businessHoursResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/business-hours",
            new { entries = new[] { new { dayOfWeek = targetDate.DayOfWeek.ToString(), startTime = "09:00:00", endTime = "12:00:00" } } },
            cancellationToken);
        businessHoursResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var slots = await GetAvailableSlotsAsync(client, tenantId, resourceId, serviceId, targetDate, cancellationToken);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

        slots.ShouldNotBeEmpty();
        slots.ShouldAllBe(s => TimeZoneInfo.ConvertTime(s, timeZone).TimeOfDay < new TimeSpan(12, 0, 0));
    }

    [Fact]
    public async Task Availability_Is_Empty_When_Tenant_Business_Hours_Configured_But_Missing_For_That_Day()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, accessToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (resourceId, serviceId) = await SetUpBookableResourceAndServiceAsync(client, accessToken, cancellationToken);

        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(13));
        await SetWorkingHoursForDateAsync(client, accessToken, resourceId, targetDate, "09:00:00", "18:00:00", cancellationToken);

        // Horario de funcionamento configurado, mas so para um dia diferente do
        // pedido — o estabelecimento esta fechado nesse dia mesmo com o recurso aberto.
        var otherDayOfWeek = targetDate.DayOfWeek == DayOfWeek.Monday ? DayOfWeek.Tuesday : DayOfWeek.Monday;
        var businessHoursResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/business-hours",
            new { entries = new[] { new { dayOfWeek = otherDayOfWeek.ToString(), startTime = "09:00:00", endTime = "18:00:00" } } },
            cancellationToken);
        businessHoursResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var slots = await GetAvailableSlotsAsync(client, tenantId, resourceId, serviceId, targetDate, cancellationToken);

        slots.ShouldBeEmpty();
    }

    [Fact]
    public async Task Availability_Respects_The_Appointment_Buffer_Around_An_Existing_Booking()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, accessToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (resourceId, serviceId) = await SetUpBookableResourceAndServiceAsync(client, accessToken, cancellationToken);

        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14));
        await SetWorkingHoursForDateAsync(client, accessToken, resourceId, targetDate, "09:00:00", "18:00:00", cancellationToken);

        var settingsResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/scheduling-settings",
            new { closedDates = Array.Empty<object>(), appointmentBufferMinutes = 30 },
            cancellationToken);
        settingsResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var slotsBeforeBooking = await GetAvailableSlotsAsync(client, tenantId, resourceId, serviceId, targetDate, cancellationToken);
        var firstSlotStart = slotsBeforeBooking[0];

        var bookingResponse = await client.PostAsJsonAsync($"/api/public/tenants/{tenantId}/appointments", new
        {
            resourceId,
            serviceId,
            startAtUtc = firstSlotStart,
            customerFullName = "Visitante do Portal",
            customerEmail = $"visitante-{Guid.NewGuid():N}@example.com",
            customerPhone = (string?)null,
            notes = (string?)null,
        }, cancellationToken);
        bookingResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Servico de 30min + buffer de 30min dos dois lados: nada entre o inicio
        // do agendamento e o fim (30min) + buffer (30min) depois dele pode ficar
        // disponivel; exatamente nesse ponto (60min depois do inicio) volta a abrir.
        var slotsAfterBooking = await GetAvailableSlotsAsync(client, tenantId, resourceId, serviceId, targetDate, cancellationToken);
        var bufferEnd = firstSlotStart.AddMinutes(30 + 30);

        slotsAfterBooking.ShouldNotContain(firstSlotStart);
        slotsAfterBooking.ShouldNotContain(s => s > firstSlotStart && s < bufferEnd);
        slotsAfterBooking.ShouldContain(bufferEnd);
    }

    [Fact]
    public async Task Public_Booking_Reuses_An_Existing_Customer_By_Email_Instead_Of_Duplicating()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, accessToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (resourceId, serviceId) = await SetUpBookableResourceAndServiceAsync(client, accessToken, cancellationToken);

        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8));
        await SetWorkingHoursForDateAsync(client, accessToken, resourceId, targetDate, "09:00:00", "18:00:00", cancellationToken);

        var slots = await GetAvailableSlotsAsync(client, tenantId, resourceId, serviceId, targetDate, cancellationToken);
        var email = $"repetido-{Guid.NewGuid():N}@example.com";

        var firstBooking = await client.PostAsJsonAsync($"/api/public/tenants/{tenantId}/appointments", new
        {
            resourceId,
            serviceId,
            startAtUtc = slots[0],
            customerFullName = "Cliente Recorrente",
            customerEmail = email,
            customerPhone = (string?)null,
            notes = (string?)null,
        }, cancellationToken);
        firstBooking.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Reconsulta a disponibilidade: o servico dura 30min mas os slots sao
        // gerados a cada 15min, entao slots[1] (adjacente) ainda se sobrepoe ao
        // que acabou de ser reservado — precisa da lista fresca pos-reserva.
        var slotsAfterFirstBooking = await GetAvailableSlotsAsync(client, tenantId, resourceId, serviceId, targetDate, cancellationToken);

        var secondBooking = await client.PostAsJsonAsync($"/api/public/tenants/{tenantId}/appointments", new
        {
            resourceId,
            serviceId,
            startAtUtc = slotsAfterFirstBooking[0],
            customerFullName = "Cliente Recorrente",
            customerEmail = email,
            customerPhone = (string?)null,
            notes = (string?)null,
        }, cancellationToken);
        secondBooking.StatusCode.ShouldBe(HttpStatusCode.Created);

        var customersResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/customers", cancellationToken);
        var customersBody = await customersResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var matching = customersBody.GetProperty("items").EnumerateArray()
            .Count(c => string.Equals(c.GetProperty("email").GetString(), email, StringComparison.OrdinalIgnoreCase));

        matching.ShouldBe(1);
    }

    [Fact]
    public async Task Booking_With_A_Resource_From_Another_Tenant_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var (tenantAId, tenantAToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (_, serviceId) = await SetUpBookableResourceAndServiceAsync(client, tenantAToken, cancellationToken);

        var (tenantBId, tenantBToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (resourceIdOfTenantB, _) = await SetUpBookableResourceAndServiceAsync(client, tenantBToken, cancellationToken);

        // Tenta reservar o recurso do tenant B usando o Id do tenant A na rota.
        var response = await client.PostAsJsonAsync($"/api/public/tenants/{tenantAId}/appointments", new
        {
            resourceId = resourceIdOfTenantB,
            serviceId,
            startAtUtc = DateTimeOffset.UtcNow.AddDays(3),
            customerFullName = "Tentativa Cruzada",
            customerEmail = "cruzado@example.com",
            customerPhone = (string?)null,
            notes = (string?)null,
        }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        tenantBId.ShouldNotBe(tenantAId);
    }

    [Fact]
    public async Task Availability_For_An_Unknown_Tenant_Should_Return_NotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var response = await client.GetAsync(
            $"/api/public/tenants/{Guid.NewGuid()}/availability?resourceId={Guid.NewGuid()}&serviceId={Guid.NewGuid()}&date={DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)):yyyy-MM-dd}",
            cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<List<DateTimeOffset>> GetAvailableSlotsAsync(
        HttpClient client, Guid tenantId, Guid resourceId, Guid serviceId, DateOnly date, CancellationToken cancellationToken)
    {
        var response = await client.GetAsync(
            $"/api/public/tenants/{tenantId}/availability?resourceId={resourceId}&serviceId={serviceId}&date={date:yyyy-MM-dd}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.EnumerateArray().Select(s => s.GetProperty("startUtc").GetDateTimeOffset()).ToList();
    }

    private static async Task SetWorkingHoursForDateAsync(
        HttpClient client, string accessToken, Guid resourceId, DateOnly date, string startTime, string endTime, CancellationToken cancellationToken)
    {
        var response = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, $"/api/resources/{resourceId}/working-hours",
            new { entries = new[] { new { dayOfWeek = date.DayOfWeek.ToString(), startTime, endTime } } },
            cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static async Task<(Guid ResourceId, Guid ServiceId)> SetUpBookableResourceAndServiceAsync(
        HttpClient client, string accessToken, CancellationToken cancellationToken)
    {
        var resourceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/resources",
            new { name = "Sala de Atendimento", type = "Room", capacity = 1, description = (string?)null },
            cancellationToken);
        var resourceBody = await resourceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var resourceId = resourceBody.GetProperty("id").GetGuid();

        var serviceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/services",
            new { name = "Consulta", description = (string?)null, durationMinutes = 30, price = 80m, currency = "BRL", category = (string?)null },
            cancellationToken);
        var serviceBody = await serviceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var serviceId = serviceBody.GetProperty("id").GetGuid();

        return (resourceId, serviceId);
    }

    private static async Task<(Guid TenantId, string AccessToken)> CreateTenantWithOwnerAndLoginAsync(HttpClient client, CancellationToken cancellationToken)
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
