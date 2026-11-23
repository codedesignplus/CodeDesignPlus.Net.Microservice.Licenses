using CodeDesignPlus.Net.Exceptions;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.ValueObjects.Financial;
using CodeDesignPlus.Net.ValueObjects.Location;
using CodeDesignPlus.Net.ValueObjects.User;
using Grpc.Core;
using TenantContracts = CodeDesignPlus.Net.gRpc.Clients.Services.Tenant;
using TenantSnapshot = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.Tenant;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions;

/// <summary>
/// The tenant as it is today, to be copied into an order that is not the purchase that created it: a renewal, a license
/// change or a package purchase (Kappali pendings/744).
/// </summary>
/// <remarks>
/// The order keeps a copy of the tenant, and its invoice, its receipt and its emails are written from that copy. Copying it
/// from the previous order kept the document, the name and the email of the first purchase forever. ms-tenants owns that
/// data, so every new order asks it.
/// </remarks>
public interface ITenantSnapshots
{
    /// <summary>
    /// Asks ms-tenants for the tenant and builds the copy an order keeps.
    /// </summary>
    /// <param name="tenantId">The tenant the order is for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tenant with the data ms-tenants has now.</returns>
    /// <exception cref="CodeDesignPlusException">Thrown when ms-tenants does not have the tenant, or when its data cannot be copied into an order; the error says which data to correct.</exception>
    Task<TenantSnapshot> TakeAsync(Guid tenantId, CancellationToken cancellationToken);
}

/// <summary>
/// Reads the tenant from ms-tenants with the gRPC client the purchase already uses.
/// </summary>
/// <remarks>
/// The tenant asked for travels in the body of the request, not in the context of the call: the answer is always that of
/// the id given. The document is copied as it comes, without checking its check digit: an order is never refused for it,
/// and the invoice is what waits when it is not valid.
/// </remarks>
/// <param name="tenants">The client of ms-tenants.</param>
public class TenantSnapshots(ITenantGrpc tenants) : ITenantSnapshots
{
    /// <inheritdoc/>
    public async Task<TenantSnapshot> TakeAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        TenantContracts.GetTenantResponse response;

        try
        {
            response = await tenants.GetTenantByIdAsync(new TenantContracts.GetTenantRequest { Id = tenantId.ToString() }, cancellationToken);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
            throw new CodeDesignPlusException(Layer.Application, Errors.TenantOfTheOrderNotFound, exception);
        }

        // An answer about another tenant would invoice the order to someone else.
        ApplicationGuard.IsFalse(Guid.TryParse(response.Id, out var answered) && answered == tenantId, Errors.TenantOfTheOrderNotFound);

        try
        {
            return TenantSnapshot.Create(tenantId, response.Name, string.IsNullOrWhiteSpace(response.Domain) ? null : response.Domain, TypeDocument.Create(response.TypeDocument?.Code!, response.TypeDocument?.Name!), response.NumberDocument, response.Phone, response.Email, LocationOf(response.Location));
        }
        catch (CodeDesignPlusException exception)
        {
            throw new CodeDesignPlusException(Layer.Application, ProblemOf(exception, response), exception);
        }
    }

    /// <summary>
    /// Which data of the tenant has to be corrected, so the error says it instead of a bare «the phone is not valid».
    /// </summary>
    private static Error ProblemOf(CodeDesignPlusException exception, TenantContracts.GetTenantResponse response)
    {
        var code = exception.Code;

        if (code == Domain.Errors.PhoneIsRequired.GetCode() || code == Domain.Errors.PhoneContainsInvalidCharacters.GetCode())
        {
            return Errors.TenantPhoneIsNotValid.With(response.Phone);
        }

        if (code == Domain.Errors.EmailIsRequired.GetCode() || code == Domain.Errors.EmailContainsInvalidCharacters.GetCode())
        {
            return Errors.TenantEmailIsNotValid.With(response.Email);
        }

        return Errors.TenantDataIsNotValid;
    }

    private static Location LocationOf(TenantContracts.Location? location)
    {
        ApplicationGuard.IsNull(location, Errors.TenantDataIsNotValid);

        var country = location!.Country;
        var currency = country?.Currency;

        ApplicationGuard.IsTrue(country is null || currency is null || location.State is null || location.City is null, Errors.TenantDataIsNotValid);

        return Location.Create(
            Country.Create(IdOf(country!.Id), country.Name, country.Alpha2, country.Alpha3, (ushort)country.Code, country.PhoneCode, country.Timezone, Currency.Create(IdOf(currency!.Id), currency.Name, currency.Code, currency.Symbol, (short)currency.DecimalDigits, (short)currency.NumericCode)),
            State.Create(IdOf(location.State.Id), location.State.Name, location.State.Code),
            City.Create(IdOf(location.City.Id), location.City.Name, location.City.Timezone),
            IsEmpty(location.Locality?.Id) ? null : Locality.Create(IdOf(location.Locality!.Id), location.Locality.Name),
            IsEmpty(location.Neighborhood?.Id) ? null : Neighborhood.Create(IdOf(location.Neighborhood!.Id), location.Neighborhood.Name),
            location.Address,
            location.PostalCode);
    }

    private static bool IsEmpty(string? id) => !Guid.TryParse(id, out var value) || value == Guid.Empty;

    private static Guid IdOf(string? id) => Guid.TryParse(id, out var value) ? value : Guid.Empty;
}
