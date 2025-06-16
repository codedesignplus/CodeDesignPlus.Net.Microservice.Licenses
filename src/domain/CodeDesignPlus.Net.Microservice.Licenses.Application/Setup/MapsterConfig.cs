using CodeDesignPlus.Microservice.Api.Dtos;
using CodeDesignPlus.Net.gRpc.Clients.Services.Payment;
using CodeDesignPlus.Net.gRpc.Clients.Services.Tenant;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.CreateLicense;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.UpdateLicense;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.PayOrder;
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
            .MapWith(src => new CreateLicenseCommand(src.Id, src.Name, src.ShortDescription, src.Description, src.Modules, src.Prices, src.Icon, src.TermsOfService, src.Attributes, src.IsActive, src.IsPopular));

        TypeAdapterConfig<UpdateLicenseDto, UpdateLicenseCommand>
            .NewConfig()
            .MapWith(src => new UpdateLicenseCommand(src.Id, src.Name, src.ShortDescription, src.Description, src.Modules, src.Prices, src.Icon, src.TermsOfService, src.Attributes, src.IsActive, src.IsPopular));

        TypeAdapterConfig<PayOrderDto, PayOrderCommand>
            .NewConfig()
            .MapWith(src => new PayOrderCommand(
                src.Id,
                src.OrderDetail,
                src.PaymentMethod,
                src.TenantDetail
            ));

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

        TypeAdapterConfig<gRpc.Clients.Services.Payment.PaymentResponse, Domain.ValueObjects.PaymentResponse>
            .NewConfig()
            .MapWith(src => new Domain.ValueObjects.PaymentResponse(
                Guid.Parse(src.Id),
                src.Provider,
                new Domain.ValueObjects.Response(
                    src.Response.Code,
                    src.Response.Error,
                    new Domain.ValueObjects.ResponseDetails(
                        src.Response.TransactionResponse.OrderId,
                        src.Response.TransactionResponse.TransactionId,
                        src.Response.TransactionResponse.State,
                        src.Response.TransactionResponse.ResponseCode,
                        src.Response.TransactionResponse.PaymentNetworkResponseCode,
                        src.Response.TransactionResponse.PaymentNetworkResponseErrorMessage,
                        src.Response.TransactionResponse.TrazabilityCode,
                        src.Response.TransactionResponse.AuthorizationCode,
                        src.Response.TransactionResponse.ResponseMessage,
                        src.Response.TransactionResponse.ExtraParameters.ToDictionary(x => x.Key, x => x.Value),
                        src.Response.TransactionResponse.AdditionalData.ToDictionary(x => x.Key, x => x.Value)
                    )
                )
            ));

        //Payment gRpc
        TypeAdapterConfig<PayOrderCommand, PayRequest>
           .NewConfig()
           .Map(dest => dest.Id, src => src.OrderDetail.Id.ToString())
           .Map(dest => dest.Transaction, src => new CodeDesignPlus.Net.gRpc.Clients.Services.Payment.Transaction
           {
               Order = new CodeDesignPlus.Net.gRpc.Clients.Services.Payment.Order
               {
                   Buyer = new CodeDesignPlus.Net.gRpc.Clients.Services.Payment.Buyer
                   {
                       FullName = src.OrderDetail.Buyer.Name,
                       ContactPhone = src.OrderDetail.Buyer.Phone,
                       DniNumber = src.OrderDetail.Buyer.Document,
                       EmailAddress = src.OrderDetail.Buyer.Email,
                       ShippingAddress = new Address
                       {
                           Street = src.OrderDetail.Buyer.Address,
                           City = src.OrderDetail.Buyer.City.Name,
                           State = src.OrderDetail.Buyer.State.Name,
                           PostalCode = src.OrderDetail.Buyer.PostalCode,
                           Country = src.OrderDetail.Buyer.Country.Alpha2,
                           Phone = src.OrderDetail.Buyer.Phone
                       }
                   },
               },
               Payer = new Payer
               {
                   FullName = src.OrderDetail.Buyer.Name,
                   ContactPhone = src.OrderDetail.Buyer.Phone,
                   DniNumber = src.OrderDetail.Buyer.Document,
                   DniType = src.OrderDetail.Buyer.TypeDocument,
                   EmailAddress = src.OrderDetail.Buyer.Email,
                   BillingAddress = new Address
                   {
                       Street = src.OrderDetail.Buyer.Address,
                       City = src.OrderDetail.Buyer.City.Name,
                       State = src.OrderDetail.Buyer.State.Name,
                       PostalCode = src.OrderDetail.Buyer.PostalCode,
                       Country = src.OrderDetail.Buyer.Country.Alpha2,
                       Phone = src.OrderDetail.Buyer.Phone
                   }
               },
               PaymentMethod = src.PaymentMethod.Code,
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
           });


        TypeAdapterConfig<Domain.ValueObjects.Tenant, CreateTenantRequest>
            .NewConfig()
            .MapWith(
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
                        State = new gRpc.Clients.Services.Tenant.State
                        {
                            Id = src.Location.State.Id.ToString(),
                            Name = src.Location.State.Name,
                            Code = src.Location.State.Code
                        },
                        City = new gRpc.Clients.Services.Tenant.City
                        {
                            Id = src.Location.City.Id.ToString(),
                            Name = src.Location.City.Name,
                            Timezone = src.Location.City.Timezone
                        },
                        Locality = new gRpc.Clients.Services.Tenant.Locality
                        {
                            Id = src.Location.Locality.Id.ToString(),
                            Name = src.Location.Locality.Name
                        },
                        Neighborhood = new gRpc.Clients.Services.Tenant.Neighborhood
                        {
                            Id = src.Location.Neighborhood.Id.ToString(),
                            Name = src.Location.Neighborhood.Name
                        }
                    }
                }
            );
    }
}
