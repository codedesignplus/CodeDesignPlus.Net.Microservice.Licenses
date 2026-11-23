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
    /// <exception cref="CodeDesignPlusException">Thrown when ms-tenants does not have the tenant, or when its data cannot be copied into an order; the error says which data to correct, or that the problem is of the platform.</exception>
    Task<TenantSnapshot> TakeAsync(Guid tenantId, CancellationToken cancellationToken);
}

/// <summary>
/// Reads the tenant from ms-tenants with the gRPC client the purchase already uses.
/// </summary>
/// <remarks>
/// <para>
/// The tenant asked for travels in the body of the request, not in the context of the call: the answer is always that of
/// the id given. The document is copied as it comes, without checking its check digit: an order is never refused for it,
/// and the invoice is what waits when it is not valid.
/// </para>
/// <para>
/// When the answer cannot be copied, two kinds of failure are told apart. What the copropiedad writes itself (its name, its
/// document, its phone, its email, its website, its address) is named and sent to be corrected where it is edited. What
/// comes from the catalogs of the platform (the country, its currency, the state, the city and their ids) is not something
/// the copropiedad can correct: the error says the problem is of the platform and which part of the answer failed, for
/// support. Nothing missing is made up: on 2026-11-23 ms-tenants answered the currency without its id and every renewal
/// was told to review its data, which were right.
/// </para>
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

        var location = LocationOf(response.Location);

        ApplicationGuard.IsTrue(string.IsNullOrWhiteSpace(response.TypeDocument?.Code) || string.IsNullOrWhiteSpace(response.NumberDocument), Errors.TenantDocumentIsMissing);

        try
        {
            return TenantSnapshot.Create(tenantId, response.Name, string.IsNullOrWhiteSpace(response.Domain) ? null : response.Domain, TypeDocument.Create(response.TypeDocument!.Code, response.TypeDocument.Name), response.NumberDocument, response.Phone, response.Email, location);
        }
        catch (CodeDesignPlusException exception)
        {
            throw new CodeDesignPlusException(Layer.Application, ProblemOf(exception, response), exception);
        }
    }

    /// <summary>
    /// Which data of the tenant has to be corrected, so the error says it instead of a bare «the phone is not valid». A
    /// failure that is not of something the copropiedad writes is of the platform.
    /// </summary>
    private static Error ProblemOf(CodeDesignPlusException exception, TenantContracts.GetTenantResponse response)
    {
        var code = exception.Code;

        if (code == Domain.Errors.NameIsRequired.GetCode() || code == Domain.Errors.NameIsTooLong.GetCode())
        {
            return Errors.TenantNameIsNotValid.With(response.Name);
        }

        if (code == Domain.Errors.PhoneIsRequired.GetCode() || code == Domain.Errors.PhoneContainsInvalidCharacters.GetCode())
        {
            return Errors.TenantPhoneIsNotValid.With(response.Phone);
        }

        if (code == Domain.Errors.EmailIsRequired.GetCode() || code == Domain.Errors.EmailContainsInvalidCharacters.GetCode())
        {
            return Errors.TenantEmailIsNotValid.With(response.Email);
        }

        if (code == Domain.Errors.WebContainsInvalidCharacters.GetCode())
        {
            return Errors.TenantWebIsNotValid.With(response.Domain);
        }

        if (code == Domain.Errors.TypeDocumentIsRequired.GetCode() || code == Domain.Errors.NumberDocumentIsRequired.GetCode())
        {
            return Errors.TenantDocumentIsMissing;
        }

        return PlatformProblem("tenant", exception);
    }

    /// <summary>
    /// The location, built part by part so a failure says which part of the answer it was.
    /// </summary>
    private static Location LocationOf(TenantContracts.Location? location)
    {
        var country = Required("location.country", Required("location", location).Country);
        var currency = Required("location.country.currency", country.Currency);
        var state = Required("location.state", location!.State);
        var city = Required("location.city", location.City);

        var builtCurrency = Built("location.country.currency", () => Currency.Create(IdOf(currency.Id), currency.Name, currency.Code, currency.Symbol, (short)currency.DecimalDigits, (short)currency.NumericCode));
        var builtCountry = Built("location.country", () => Country.Create(IdOf(country.Id), country.Name, country.Alpha2, country.Alpha3, (ushort)country.Code, country.PhoneCode, country.Timezone, builtCurrency));
        var builtState = Built("location.state", () => State.Create(IdOf(state.Id), state.Name, state.Code));
        var builtCity = Built("location.city", () => City.Create(IdOf(city.Id), city.Name, city.Timezone));
        var locality = IsEmpty(location.Locality?.Id) ? null : Built("location.locality", () => Locality.Create(IdOf(location.Locality!.Id), location.Locality.Name));
        var neighborhood = IsEmpty(location.Neighborhood?.Id) ? null : Built("location.neighborhood", () => Neighborhood.Create(IdOf(location.Neighborhood!.Id), location.Neighborhood.Name));

        // The address and the postal code are what the copropiedad writes of its location; the rest comes from the catalogs.
        ApplicationGuard.IsTrue(string.IsNullOrWhiteSpace(location.Address) || string.IsNullOrWhiteSpace(location.PostalCode), Errors.TenantAddressIsMissing);

        return Built("location", () => Location.Create(builtCountry, builtState, builtCity, locality, neighborhood, location.Address, location.PostalCode));
    }

    private static T Required<T>(string part, T? value) where T : class
    {
        if (value is null)
        {
            throw new CodeDesignPlusException(Layer.Application, Errors.TenantCouldNotBeRead.With($"{part} is missing"));
        }

        return value;
    }

    private static T Built<T>(string part, Func<T> build)
    {
        try
        {
            return build();
        }
        catch (CodeDesignPlusException exception)
        {
            throw new CodeDesignPlusException(Layer.Application, PlatformProblem(part, exception), exception);
        }
    }

    /// <summary>
    /// A failure the copropiedad cannot correct, with the part of the answer and what was wrong with it, for support.
    /// </summary>
    private static Error PlatformProblem(string part, CodeDesignPlusException exception) => Errors.TenantCouldNotBeRead.With($"{part}: {exception.Message}");

    private static bool IsEmpty(string? id) => !Guid.TryParse(id, out var value) || value == Guid.Empty;

    private static Guid IdOf(string? id) => Guid.TryParse(id, out var value) ? value : Guid.Empty;
}
