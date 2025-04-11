using CodeDesignPlus.Microservice.Api.Dtos;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.CreateLicense;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.UpdateLicense;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Setup;

public static class MapsterConfigLicense
{
    public static void Configure()
    {
        //License
        TypeAdapterConfig<CreateLicenseDto, CreateLicenseCommand>
            .NewConfig()
            .ConstructUsing(src => new CreateLicenseCommand(src.Id, src.Name, src.Description, src.Modules, src.BillingType, Currency.Create(src.Currency.Name, src.Currency.Code, src.Currency.Symbol), src.Price, src.Attributes));

        TypeAdapterConfig<UpdateLicenseDto, UpdateLicenseCommand>
            .NewConfig()
            .ConstructUsing(src => new UpdateLicenseCommand(src.Id, src.Name, src.Description, src.Modules, src.BillingType, Currency.Create(src.Currency.Name, src.Currency.Code, src.Currency.Symbol), src.Price, src.Attributes));

        TypeAdapterConfig<LicenseAggregate, LicenseDto>
            .NewConfig()
            .MapWith(src => new LicenseDto
            {
                Id = src.Id,
                Name = src.Name,
                Description = src.Description,
                Attributes = src.Attributes,
                BillingType = src.BillingType,
                Currency = Currency.Create(src.Currency.Name, src.Currency.Code, src.Currency.Symbol),
                Price = src.Price,
                IsActive = src.IsActive,
                Modules = src.Modules.Select(x => new ModuleDto
                {
                    Id = x.Id,
                    Name = x.Name
                }).ToList()
            });

        //Module
        TypeAdapterConfig<ModuleDto, ModuleEntity>
            .NewConfig()
            .TwoWays();
    }
}
