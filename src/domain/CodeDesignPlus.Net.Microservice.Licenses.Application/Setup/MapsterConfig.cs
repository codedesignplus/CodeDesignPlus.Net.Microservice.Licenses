﻿using CodeDesignPlus.Microservice.Api.Dtos;
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
        //OrderAggreate
        TypeAdapterConfig<OrderAggregate, OrderDto>
            .NewConfig()
            .MapWith(src => new OrderDto
            {
                Id = src.Id,
                Buyer = src.Buyer,
                PaymentMethod = src.PaymentMethod,
                IdLicense = src.IdLicense,
                TenantDetail = src.TenantDetail,
                CreatedAt = src.CreatedAt,
                PaymentResponse = src.PaymentResponse == null ? null : new Domain.ValueObjects.PaymentResponse(
                    src.PaymentResponse.Success,
                    src.PaymentResponse.Status,
                    src.PaymentResponse.TransactionId,
                    src.PaymentResponse.Message,
                    src.PaymentResponse.RedirectUrl,
                    new Domain.ValueObjects.FinancialNetwork(
                        src.PaymentResponse.FinancialNetwork.PaymentNetworkResponseCode,
                        src.PaymentResponse.FinancialNetwork.PaymentNetworkResponseErrorMessage,
                        src.PaymentResponse.FinancialNetwork.TrazabilityCode,
                        src.PaymentResponse.FinancialNetwork.AuthorizationCode,
                        src.PaymentResponse.FinancialNetwork.ResponseCode
                    )
                )
            });

        //License
        TypeAdapterConfig<CreateLicenseDto, CreateLicenseCommand>
            .NewConfig()
            .MapWith(src => new CreateLicenseCommand(src.Id, src.Name, src.ShortDescription, src.Description, src.Modules, src.Prices, src.Icon, src.TermsOfService, src.Attributes, src.IsActive, src.IsPopular, src.ShowInLandingPage));

        TypeAdapterConfig<UpdateLicenseDto, UpdateLicenseCommand>
            .NewConfig()
            .MapWith(src => new UpdateLicenseCommand(src.Id, src.Name, src.ShortDescription, src.Description, src.Modules, src.Prices, src.Icon, src.TermsOfService, src.Attributes, src.IsActive, src.IsPopular, src.ShowInLandingPage));

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
                ShowInLandingPage = src.ShowInLandingPage,
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
        TypeAdapterConfig<PayOrderCommand, InitiatePaymentRequest>
           .NewConfig()
           .Map(dest => dest.Id, src => src.OrderDetail.Id.ToString())
           .Map(dest => dest.Module, src => "Licenses")
           .Map(dest => dest.Payer, src => new Payer
           {
               FullName = src.OrderDetail.Buyer.Name,
               ContactPhone = src.OrderDetail.Buyer.Phone,
               DniNumber = src.OrderDetail.Buyer.Document,
               DniType = src.OrderDetail.Buyer.TypeDocument.Code,
               EmailAddress = src.OrderDetail.Buyer.Email
           })
           .Map(dest => dest.PaymentMethod, src => new CodeDesignPlus.Net.gRpc.Clients.Services.Payment.PaymentMethod
           {
               Type = src.PaymentMethod.Code,
               CreditCard = src.PaymentMethod.CreditCard != null ? new CodeDesignPlus.Net.gRpc.Clients.Services.Payment.CreditCard
               {
                   Number = src.PaymentMethod.CreditCard.Number,
                   ExpirationDate = src.PaymentMethod.CreditCard.ExpirationDate,
                   SecurityCode = src.PaymentMethod.CreditCard.SecurityCode,
                   Name = src.PaymentMethod.CreditCard.CardHolderName,
                   InstallmentsNumber = 1
               } : null,
               Pse = src.PaymentMethod.Pse != null ? new CodeDesignPlus.Net.gRpc.Clients.Services.Payment.Pse
               {
                   PseCode = src.PaymentMethod.Pse.PseCode,
                   PseResponseUrl = src.PaymentMethod.Pse.PseResponseUrl,
                   TypePerson = src.PaymentMethod.Pse.TypePerson
               } : null
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
