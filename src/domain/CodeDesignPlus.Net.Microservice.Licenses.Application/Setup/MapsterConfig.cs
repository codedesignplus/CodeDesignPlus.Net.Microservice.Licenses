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
            .ConstructUsing(src => new CreateLicenseCommand(src.Id, src.Name, src.ShortDescription, src.Description, src.Modules, src.Prices, src.Icon, src.TermsOfService, src.Attributes, src.IsActive, src.IsPopular));

        TypeAdapterConfig<UpdateLicenseDto, UpdateLicenseCommand>
            .NewConfig()
            .ConstructUsing(src => new UpdateLicenseCommand(src.Id, src.Name, src.ShortDescription, src.Description, src.Modules, src.Prices, src.Icon, src.TermsOfService, src.Attributes, src.IsActive, src.IsPopular));

        TypeAdapterConfig<LicenseAggregate, LicenseDto>
            .NewConfig()
            .MapWith(src => new LicenseDto
            {
                Id = src.Id,
                Name = src.Name,
                ShortDescription = src.ShortDescription,
                Description = src.Description,
                Attributes = src.Attributes,
                Prices = src.Prices.Select(x => Price.Create(x.BillingType, Currency.Create(x.Currency.Name, x.Currency.Code, x.Currency.Symbol), x.Pricing, x.BillingModel)).ToList(),
                Icon = src.Icon,
                TermsOfService = src.TermsOfService,
                IsActive = src.IsActive,
                IsPopular = src.IsPopular,
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
