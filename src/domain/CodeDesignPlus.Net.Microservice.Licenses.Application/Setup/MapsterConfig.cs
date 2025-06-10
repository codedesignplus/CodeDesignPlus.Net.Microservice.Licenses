using CodeDesignPlus.Microservice.Api.Dtos;
using CodeDesignPlus.Net.gRpc.Clients.Services.Payment;
using CodeDesignPlus.Net.gRpc.Clients.Services.Tenant;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.CreateLicense;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.PayLicense;
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

        TypeAdapterConfig<PayLicenseDto, PayLicenseCommand>
            .NewConfig()
            .ConstructUsing(src => new PayLicenseCommand(src.Id, src.Order, src.PaymentMethod, src.Tenant));

        TypeAdapterConfig<LicenseAggregate, LicenseDto>
            .NewConfig()
            .MapWith(src => new LicenseDto
            {
                Id = src.Id,
                Name = src.Name,
                ShortDescription = src.ShortDescription,
                Description = src.Description,
                Attributes = src.Attributes,
                Prices = src.Prices.Select(x => Price.Create(x.BillingType, Domain.ValueObjects.Currency.Create(x.Currency.Id, x.Currency.Name, x.Currency.Code, x.Currency.Symbol), x.Pricing, x.BillingModel, x.DiscountPercentage, x.TaxPercentage)).ToList(),
                Icon = src.Icon,
                TermsOfService = src.TermsOfService,
                IsActive = src.IsActive,
                IsPopular = src.IsPopular,
                Modules = src.Modules.Select(x => new ModuleDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                }).ToList()
            });

        //Module
        TypeAdapterConfig<ModuleDto, ModuleEntity>
            .NewConfig()
            .TwoWays();

        //Payment gRpc
        TypeAdapterConfig<PayLicenseCommand, PayRequest>
            .NewConfig()
            .ConstructUsing(src => new PayRequest
            {
                Id = src.Order.Id.ToString(),
                Transaction = new CodeDesignPlus.Net.gRpc.Clients.Services.Payment.Transaction
                {
                    Order = new CodeDesignPlus.Net.gRpc.Clients.Services.Payment.Order
                    {
                        Buyer = new CodeDesignPlus.Net.gRpc.Clients.Services.Payment.Buyer
                        {
                            FullName = src.Order.Buyer.Name,
                            ContactPhone = src.Order.Buyer.Phone,
                            DniNumber = src.Order.Buyer.Document,
                            EmailAddress = src.Order.Buyer.Email,
                            ShippingAddress = new Address
                            {
                                Street = src.Order.Buyer.Address,
                                City = src.Order.Buyer.City.Name,
                                State = src.Order.Buyer.State.Name,
                                PostalCode = src.Order.Buyer.PostalCode,
                                Country = src.Order.Buyer.Country.Name
                            }
                        },
                        Amount = null!,
                        Description = null!,
                        Tax = null!,
                        TaxReturnBase = null!
                    },
                    Payer = new Payer
                    {
                        FullName = src.Order.Buyer.Name,
                        ContactPhone = src.Order.Buyer.Phone,
                        DniNumber = src.Order.Buyer.Document,
                        EmailAddress = src.Order.Buyer.Email,
                        BillingAddress = new Address
                        {
                            Street = src.Order.Buyer.Address,
                            City = src.Order.Buyer.City.Name,
                            State = src.Order.Buyer.State.Name,
                            PostalCode = src.Order.Buyer.PostalCode,
                            Country = src.Order.Buyer.Country.Name
                        }
                    },
                    Cookie = null!,
                    UserAgent = null!,
                    DeviceSessionId = null!,
                    IpAddress = null!,
                    CreditCard = src.PaymentMethod.CreditCard != null ? new CodeDesignPlus.Net.gRpc.Clients.Services.Payment.CreditCard
                    {
                        Number = src.PaymentMethod.CreditCard.Number,
                        ExpirationDate = src.PaymentMethod.CreditCard.ExpirationDate,
                        SecurityCode = src.PaymentMethod.CreditCard.SecurityCode,
                        Name = src.PaymentMethod.CreditCard.CardHolderName,
                    } : null,
                    Pse = src.PaymentMethod.Pse != null ? new CodeDesignPlus.Net.gRpc.Clients.Services.Payment.Pse
                    {
                        PseCode = src.PaymentMethod.Pse.PseCode,
                        PseResponseUrl = null,
                        TypePerson = src.PaymentMethod.Pse.TypePerson,
                    } : null,
                }
            });


        TypeAdapterConfig<Domain.ValueObjects.Tenant, CreateTenantRequest>
            .NewConfig()
            .ConstructUsing(
                src => new CreateTenantRequest
                {
                    Id = src.Id.ToString(),
                    Name = src.Name,
                    Domain = src.Web,
                    Location = new gRpc.Clients.Services.Tenant.Location()
                    {
                        Country = new CodeDesignPlus.Net.gRpc.Clients.Services.Tenant.Country
                        {
                            Id = src.Location.Country.Id.ToString(),
                            Name = src.Location.Country.Name,
                            Code = src.Location.Country.Code,
                            Timezone = src.Location.Country.Timezone,
                            Currency = new CodeDesignPlus.Net.gRpc.Clients.Services.Tenant.Currency
                            {
                                Id = src.Location.Country.Currency.Id.ToString(),
                                Name = src.Location.Country.Currency.Name,
                                Code = src.Location.Country.Currency.Code,
                                Symbol = src.Location.Country.Currency.Symbol
                            }
                        },
                    }
                }
            );
    }
}
