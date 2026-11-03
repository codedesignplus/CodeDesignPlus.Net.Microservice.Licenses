using CodeDesignPlus.Microservice.Api.Dtos;
using CodeDesignPlus.Net.gRpc.Clients.Services.Payment;
using CodeDesignPlus.Net.gRpc.Clients.Services.Tenant;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.CreateLicense;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.UpdateLicense;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.PayOrder;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Commands.ChangeSubscription;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Commands.RenewSubscription;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Commands.PurchasePackage;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.Commands.CreatePackage;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.Commands.UpdatePackage;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Setup;

public static class MapsterConfigLicense
{
    public static void Configure()
    {
        //OrderAggreate
        TypeAdapterConfig<OrderAggregate, Order.DataTransferObjects.OrderDto>
            .NewConfig()
            // El mapeo es manual, asi que un campo que se anade al DTO y no se anade aqui **no viaja** y no
            // rompe nada: simplemente llega con su valor por defecto. Asi es como `recoverOrderState()` en
            // /purchase/processing leyo durante meses un `provisioningStatus` que nunca llego, dejando la
            // pantalla dependiendo por completo de que el evento de SignalR no se perdiera.
            .MapWith(src => new Order.DataTransferObjects.OrderDto
            {
                Id = src.Id,
                PaymentId = src.PaymentId,
                Buyer = src.Buyer,
                License = src.License,
                TenantDetail = src.TenantDetail,
                CreatedAt = src.CreatedAt,
                IsActive = src.IsActive,
                PaymentStatus = src.PaymentStatus,
                ProvisioningStatus = src.ProvisioningStatus,
                ProvisioningHistory = src.ProvisioningHistory,
                Receipt = src.Receipt,
                Type = src.Type,
                PeriodStart = src.PeriodStart,
                PeriodEnd = src.PeriodEnd,
                ProrationCredit = src.ProrationCredit.Amount,
                ProrationCreditTax = src.ProrationCreditTax.Amount,
                CreditBalanceApplied = src.CreditBalanceApplied == null ? 0 : src.CreditBalanceApplied.Amount,
                AmountDue = src.AmountToCharge.Amount,
                SupersededBy = src.SupersededBy,
                AcceptedTermsVersion = src.AcceptedTermsVersion,
                FailureReason = src.FailureReason,
                Error = src.Error,
                Packages = (src.Packages ?? new List<PackageLine>()).Select(x => Package.DataTransferObjects.PackageLineDto.From(x, src.Id, false)).ToList(),
                SubTotal = src.SubTotal.Amount,
                Tax = src.Tax.Amount,
                Total = src.Total.Amount
            });


        //License
        TypeAdapterConfig<CreateLicenseDto, CreateLicenseCommand>
            .NewConfig()
            .MapWith(src => new CreateLicenseCommand(src.Id, src.Name, src.ShortDescription, src.Description, src.Modules, src.Prices, src.Icon, src.TermsOfService, src.Attributes, src.IsActive, src.IsPopular, src.ShowInLandingPage, src.InvoiceConcept, src.OfferedTo));

        TypeAdapterConfig<UpdateLicenseDto, UpdateLicenseCommand>
            .NewConfig()
            .MapWith(src => new UpdateLicenseCommand(src.Id, src.Name, src.ShortDescription, src.Description, src.Modules, src.Prices, src.Icon, src.TermsOfService, src.Attributes, src.IsActive, src.IsPopular, src.ShowInLandingPage, src.InvoiceConcept, src.OfferedTo));

        TypeAdapterConfig<PayOrderDto, PayOrderCommand>
            .NewConfig()
            .MapWith(src => new PayOrderCommand(
                src.Id,
                src.Buyer,
                src.License,
                src.TenantDetail,
                src.AcceptedTermsVersion,
                src.Packages
            ));

        TypeAdapterConfig<RenewSubscriptionDto, RenewSubscriptionCommand>
            .NewConfig()
            .MapWith(src => new RenewSubscriptionCommand(src.Id, src.AcceptedTermsVersion, src.Packages));

        TypeAdapterConfig<PurchasePackageDto, PurchasePackageCommand>
            .NewConfig()
            .MapWith(src => new PurchasePackageCommand(src.Id, src.PackageId, src.Selections, src.AcceptedTermsVersion));

        TypeAdapterConfig<CreatePackageDto, CreatePackageCommand>
            .NewConfig()
            .MapWith(src => new CreatePackageCommand(src.Id, src.Name, src.ShortDescription, src.Description, src.Icon, src.IsActive, src.IsPopular, src.ShowInLandingPage, src.OfferedTo, src.Kind, src.Currency, src.BasePrice, src.MinimumPrice, src.TaxTreatment, src.TaxPercentage, src.TaxLegalBasis, src.InvoiceConcept, src.Attributes, src.Components));

        TypeAdapterConfig<UpdatePackageDto, UpdatePackageCommand>
            .NewConfig()
            .MapWith(src => new UpdatePackageCommand(src.Id, src.Name, src.ShortDescription, src.Description, src.Icon, src.IsActive, src.IsPopular, src.ShowInLandingPage, src.OfferedTo, src.Kind, src.Currency, src.BasePrice, src.MinimumPrice, src.TaxTreatment, src.TaxPercentage, src.TaxLegalBasis, src.InvoiceConcept, src.Attributes, src.Components));

        TypeAdapterConfig<ChangeSubscriptionDto, ChangeSubscriptionCommand>
            .NewConfig()
            .MapWith(src => new ChangeSubscriptionCommand(src.Id, src.LicenseId, src.AcceptedTermsVersion));

        TypeAdapterConfig<LicenseAggregate, License.DataTransferObjects.LicenseDto>
            .NewConfig()
            .MapWith(src => new License.DataTransferObjects.LicenseDto
            {
                Id = src.Id,
                Name = src.Name,
                ShortDescription = src.ShortDescription,
                Description = src.Description,
                Attributes = src.Attributes,
                Icon = src.Icon,
                TermsOfService = src.TermsOfService,
                IsActive = src.IsActive,
                IsPopular = src.IsPopular,
                ShowInLandingPage = src.ShowInLandingPage,
                InvoiceConcept = src.InvoiceConcept,
                OfferedTo = (src.OfferedTo ?? new List<LicenseOffer>()).Select(x => new License.DataTransferObjects.LicenseOfferDto
                {
                    TenantId = x.TenantId,
                    TenantName = x.TenantName,
                    OfferedBy = x.OfferedBy,
                    OfferedAt = x.OfferedAt,
                }).ToList(),
                Modules = src.Modules.Select(x => new License.DataTransferObjects.ModuleDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                }).ToList()
            });



        //Module
        TypeAdapterConfig<License.DataTransferObjects.ModuleDto, ModuleEntity>
            .NewConfig()
            .TwoWays();

        //Payment gRpc
        TypeAdapterConfig<OrderAggregate, InitiatePaymentRequest>
           .NewConfig()
           .Map(dest => dest.Module, src => "Licenses")
           .Map(dest => dest.Buyer, src => new Buyer
           {
               BuyerId = src.Buyer.BuyerId.ToString(),
               Name = src.Buyer.Name,
               Phone = src.Buyer.Phone,
               // El comprador de una renovación copia los datos de la orden anterior, que pueden no traer documento.
               Document = src.Buyer.Document ?? string.Empty,
               TypeDocument = src.Buyer.TypeDocument == null ? null : new gRpc.Clients.Services.Payment.TypeDocument()
               {
                   Code = src.Buyer.TypeDocument.Code,
                   Name = src.Buyer.TypeDocument.Name,
               },
               Email = src.Buyer.Email
           });

        TypeAdapterConfig<InitiatePaymentResponse, Order.DataTransferObjects.PaymentResponse>
            .NewConfig()
            .MapWith(src => new Order.DataTransferObjects.PaymentResponse
            {
                Success = src.Success,
                PaymentId = Guid.Parse(src.PaymentId),
                NextAction = (Order.DataTransferObjects.NextActionType)src.NextAction,
                Metadata = src.Metadata.ToDictionary(k => k.Key, v => v.Value),
                RedirectUrl = src.RedirectUrl,

            });

        TypeAdapterConfig<Domain.ValueObjects.Tenant, CreateTenantRequest>
            .NewConfig()
            .MapWith(
                src => new CreateTenantRequest
                {
                    Id = src.Id.ToString(),
                    Name = src.Name,
                    Email = src.Email,
                    Phone = src.Phone,
                    TypeDocument = new gRpc.Clients.Services.Tenant.TypeDocument
                    {
                        Name = src.TypeDocument.Name,
                        Code = src.TypeDocument.Code
                    },
                    NumbreDocument = src.NumberDocument,
                    IsActive = true,
                    Domain = src.Web,
                    Location = new gRpc.Clients.Services.Tenant.Location()
                    {
                        Address = src.Location.Address,
                        PostalCode = src.Location.PostalCode,
                        Country = new CodeDesignPlus.Net.gRpc.Clients.Services.Tenant.Country
                        {
                            Id = src.Location.Country.Id.ToString(),
                            Name = src.Location.Country.Name,
                            Code = src.Location.Country.Code,
                            Timezone = src.Location.Country.Timezone,
                            Currency = new CodeDesignPlus.Net.gRpc.Clients.Services.Tenant.Currency
                            {
                                Id = src.Location.Country.Currency.CurrencyId.ToString(),
                                Name = src.Location.Country.Currency.Name,
                                Code = src.Location.Country.Currency.Code,
                                Symbol = src.Location.Country.Currency.Symbol
                                //TODO modificar el currency
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
                        // Localidad y barrio son opcionales: la mayoría de los municipios no tiene (pendings/130).
                        Locality = src.Location.Locality == null ? null : new gRpc.Clients.Services.Tenant.Locality
                        {
                            Id = src.Location.Locality.Id.ToString(),
                            Name = src.Location.Locality.Name
                        },
                        Neighborhood = src.Location.Neighborhood == null ? null : new gRpc.Clients.Services.Tenant.Neighborhood
                        {
                            Id = src.Location.Neighborhood.Id.ToString(),
                            Name = src.Location.Neighborhood.Name
                        }
                    }
                }
            );
    }
}
