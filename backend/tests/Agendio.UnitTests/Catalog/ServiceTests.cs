using Agendio.Modules.Catalog.Domain;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.UnitTests.Catalog;

public class ServiceTests
{
    private static readonly TenantId Tenant = TenantId.From(Guid.NewGuid());

    [Fact]
    public void Create_Should_Default_DisplayOrder_To_Zero()
    {
        var result = Service.Create(Tenant, "Corte", null, 30, 45.90m, "BRL", null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.DisplayOrder.ShouldBe(0);
        result.Value.ImageUrl.ShouldBeNull();
    }

    [Fact]
    public void Create_Should_Accept_An_Explicit_DisplayOrder()
    {
        var result = Service.Create(Tenant, "Corte", null, 30, 45.90m, "BRL", null, displayOrder: 5);

        result.IsSuccess.ShouldBeTrue();
        result.Value.DisplayOrder.ShouldBe(5);
    }

    [Fact]
    public void Update_Should_Change_DisplayOrder()
    {
        var service = Service.Create(Tenant, "Corte", null, 30, 45.90m, "BRL", null).Value;

        var result = service.Update("Corte", null, 30, 45.90m, "BRL", null, displayOrder: 3);

        result.IsSuccess.ShouldBeTrue();
        service.DisplayOrder.ShouldBe(3);
    }

    [Fact]
    public void SetImage_Should_Set_The_Image_Url()
    {
        var service = Service.Create(Tenant, "Corte", null, 30, 45.90m, "BRL", null).Value;

        var result = service.SetImage("/uploads/service-images/abc.png");

        result.IsSuccess.ShouldBeTrue();
        service.ImageUrl.ShouldBe("/uploads/service-images/abc.png");
    }

    [Fact]
    public void SetImage_Should_Fail_When_Url_Is_Empty()
    {
        var service = Service.Create(Tenant, "Corte", null, 30, 45.90m, "BRL", null).Value;

        var result = service.SetImage("   ");

        result.IsFailure.ShouldBeTrue();
        service.ImageUrl.ShouldBeNull();
    }
}
