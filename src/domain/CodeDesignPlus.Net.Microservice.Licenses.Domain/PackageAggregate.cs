using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain;

/// <summary>
/// Un paquete complementario que se suma a cualquier licencia (pendings/739): fijo, con sus atributos y su precio, o
/// configurable, con componentes de los que sale el precio.
/// </summary>
/// <remarks>
/// El paquete no sabe qué es una asamblea: maneja atributos con su clave, su valor y cómo se combinan con los de la
/// licencia (<see cref="AttributeCombination"/>). Su precio es anual y se cobra completo; su vigencia la decide la
/// licencia de la copropiedad (<see cref="PackageTerm"/>).
/// </remarks>
/// <param name="id">El identificador, generado por el cliente.</param>
public class PackageAggregate(Guid id) : AggregateRootBase(id)
{
    /// <summary>
    /// El nombre, p. ej. «Virtual Básico».
    /// </summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    /// La descripción corta de la tarjeta.
    /// </summary>
    public string ShortDescription { get; private set; } = null!;

    /// <summary>
    /// La descripción completa.
    /// </summary>
    public string Description { get; private set; } = null!;

    /// <summary>
    /// El ícono de la tarjeta.
    /// </summary>
    public Icon Icon { get; private set; } = null!;

    /// <summary>
    /// Si se destaca en la landing.
    /// </summary>
    public bool IsPopular { get; private set; }

    /// <summary>
    /// Si se ve en la landing: un paquete público. Uno privado solo se ofrece a las copropiedades de <see cref="OfferedTo"/>.
    /// </summary>
    public bool ShowInLandingPage { get; private set; }

    /// <summary>
    /// Las copropiedades a las que se ofrece un paquete privado, con quién y cuándo.
    /// </summary>
    public List<LicenseOffer> OfferedTo { get; private set; } = [];

    /// <summary>
    /// Fijo o configurable.
    /// </summary>
    public PackageKind Kind { get; private set; }

    /// <summary>
    /// La moneda de todos sus precios.
    /// </summary>
    public string Currency { get; private set; } = null!;

    /// <summary>
    /// El precio anual del fijo, o la base que se suma a los componentes del configurable (puede ser cero).
    /// </summary>
    public Money BasePrice { get; private set; } = null!;

    /// <summary>
    /// El piso del precio de un configurable: si los componentes suman menos, se cobra esto. Cero si no tiene.
    /// </summary>
    public Money MinimumPrice { get; private set; } = null!;

    /// <summary>
    /// Cómo trata el impuesto el precio: gravado, excluido o exento (como el precio de la licencia, pendings/662).
    /// </summary>
    public TaxTreatment TaxTreatment { get; private set; }

    /// <summary>
    /// La tarifa del impuesto en puntos básicos; cero si no es gravado.
    /// </summary>
    public int TaxBasisPoints { get; private set; }

    /// <summary>
    /// La base legal de un precio excluido o exento.
    /// </summary>
    public string? TaxLegalBasis { get; private set; }

    /// <summary>
    /// El concepto con el que se factura, p. ej. «Sala virtual de reuniones Kappali, paquete Virtual Básico».
    /// </summary>
    public string InvoiceConcept { get; private set; } = null!;

    /// <summary>
    /// Los atributos de un paquete fijo.
    /// </summary>
    public List<PackageAttribute> Attributes { get; private set; } = [];

    /// <summary>
    /// Los componentes de un paquete configurable.
    /// </summary>
    public List<PackageComponent> Components { get; private set; } = [];

    /// <summary>
    /// Si está a la venta para cualquiera: público, activo y no borrado. Es lo único que acepta la compra en la landing.
    /// </summary>
    public bool IsForSale => IsActive && !IsDeleted && ShowInLandingPage;

    /// <summary>
    /// Si se le ofrece a esa copropiedad: el que está a la venta, o el privado que se le ofreció a ella.
    /// </summary>
    /// <param name="tenantId">La copropiedad.</param>
    /// <returns><c>true</c> si se le ofrece.</returns>
    public bool IsOfferedTo(Guid tenantId) => IsForSale || (IsActive && !IsDeleted && OfferedTo.Any(x => x.TenantId == tenantId));

    /// <summary>
    /// Crea el paquete.
    /// </summary>
    /// <param name="id">El identificador.</param>
    /// <param name="definition">Lo que lo define.</param>
    /// <param name="createdBy">Quién lo crea.</param>
    /// <returns>El paquete.</returns>
    public static PackageAggregate Create(Guid id, PackageDefinition definition, Guid createdBy)
    {
        DomainGuard.GuidIsEmpty(id, Errors.PackageIdIsRequired);
        DomainGuard.GuidIsEmpty(createdBy, Errors.CreatedByLicenseIsRequired);

        var now = SystemClock.Instance.GetCurrentInstant();
        var aggregate = new PackageAggregate(id)
        {
            CreatedAt = now,
            CreatedBy = createdBy
        };

        aggregate.Apply(definition, createdBy, now);

        aggregate.AddEvent(PackageCreatedDomainEvent.Create(id, aggregate.Name, aggregate.Kind, aggregate.IsActive, aggregate.ShowInLandingPage));

        return aggregate;
    }

    /// <summary>
    /// Cambia el paquete. Lo que ya compró una copropiedad no cambia: la orden guarda su copia.
    /// </summary>
    /// <param name="definition">Lo que lo define.</param>
    /// <param name="updatedBy">Quién lo cambia.</param>
    public void Update(PackageDefinition definition, Guid updatedBy)
    {
        DomainGuard.GuidIsEmpty(updatedBy, Errors.CreatedByLicenseIsRequired);

        var now = SystemClock.Instance.GetCurrentInstant();

        Apply(definition, updatedBy, now);

        UpdatedAt = now;
        UpdatedBy = updatedBy;

        AddEvent(PackageUpdatedDomainEvent.Create(Id, Name, Kind, IsActive, ShowInLandingPage));
    }

    /// <summary>
    /// Borra el paquete: deja de venderse y de renovarse. Lo ya comprado sigue hasta su fin.
    /// </summary>
    /// <param name="deletedBy">Quién lo borra.</param>
    public void Delete(Guid deletedBy)
    {
        DomainGuard.GuidIsEmpty(deletedBy, Errors.CreatedByLicenseIsRequired);

        IsDeleted = true;
        IsActive = false;
        DeletedAt = SystemClock.Instance.GetCurrentInstant();
        DeletedBy = deletedBy;

        AddEvent(PackageDeletedDomainEvent.Create(Id, Name));
    }

    /// <summary>
    /// El precio con su impuesto para una base: el impuesto se calcula como el del precio de la licencia.
    /// </summary>
    /// <param name="subTotal">La base sin impuestos.</param>
    /// <returns>La base, el impuesto y el total.</returns>
    public (Money SubTotal, Money Tax, Money Total) WithTax(Money subTotal)
    {
        // Puntos básicos: 10.000 es el ciento por ciento (regla 05), como Price.Tax.
        var tax = subTotal * (TaxBasisPoints / 10000m);

        return (subTotal, tax, subTotal + tax);
    }

    private void Apply(PackageDefinition definition, Guid by, Instant now)
    {
        DomainGuard.IsNull(definition, Errors.PackageNameIsRequired);
        DomainGuard.IsTrue(string.IsNullOrWhiteSpace(definition.Name), Errors.PackageNameIsRequired);
        DomainGuard.IsFalse(LicenseName.IsValid(definition.Name.Trim()), Errors.LicenseNameIsInvalid.With(FieldLength.Name));
        DomainGuard.IsTrue(string.IsNullOrWhiteSpace(definition.Description) || string.IsNullOrWhiteSpace(definition.ShortDescription), Errors.PackageDescriptionIsRequired);
        DomainGuard.IsNull(definition.Icon, Errors.IconLicenseIsRequired);
        DomainGuard.IsFalse(Enum.IsDefined(definition.Kind), Errors.PackageKindIsInvalid);
        DomainGuard.IsTrue(string.IsNullOrWhiteSpace(definition.Currency), Errors.PackageCurrencyIsRequired);
        DomainGuard.IsTrue(string.IsNullOrWhiteSpace(definition.InvoiceConcept), Errors.InvoiceConceptIsRequired);

        EnsureTax(definition.TaxTreatment, definition.TaxBasisPoints, definition.TaxLegalBasis);
        EnsurePrices(definition);
        PackageRules.EnsureContent(definition);
        EnsureOffers(definition.ShowInLandingPage, definition.OfferedTo);

        Name = definition.Name.Trim();
        ShortDescription = definition.ShortDescription.Trim();
        Description = definition.Description.Trim();
        Icon = definition.Icon;
        IsActive = definition.IsActive;
        IsPopular = definition.IsPopular;
        ShowInLandingPage = definition.ShowInLandingPage;
        Kind = definition.Kind;
        Currency = definition.Currency.Trim();
        BasePrice = definition.BasePrice;
        MinimumPrice = definition.MinimumPrice;
        TaxTreatment = definition.TaxTreatment;
        TaxBasisPoints = definition.TaxBasisPoints;
        TaxLegalBasis = string.IsNullOrWhiteSpace(definition.TaxLegalBasis) ? null : definition.TaxLegalBasis.Trim();
        InvoiceConcept = definition.InvoiceConcept.Trim();
        Attributes = [.. definition.Attributes.Select(x => x with { Key = x.Key.Trim(), Value = x.Value.Trim() })];
        Components = [.. definition.Components.Select(x => x with { Key = x.Key.Trim(), PerUnitOf = Blank(x.PerUnitOf), EnableKey = Blank(x.EnableKey), Tiers = [.. (x.Tiers ?? []).OrderBy(t => t.Value)] })];

        ApplyOffers(definition.OfferedTo, by, now);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Las mismas reglas que el precio de la licencia (pendings/662): un gravado lleva tarifa y ninguna base legal; un
    /// excluido o exento, tarifa cero y su base legal.
    /// </summary>
    private static void EnsureTax(TaxTreatment treatment, int basisPoints, string? legalBasis)
    {
        DomainGuard.IsFalse(Enum.IsDefined(treatment), Errors.TaxTreatmentIsInvalid);
        DomainGuard.IsGreaterThan(basisPoints, Price.MaxBasisPoints, Errors.TaxLicenseCannotExceedOneHundredPercent);

        var hasLegalBasis = !string.IsNullOrWhiteSpace(legalBasis);

        if (treatment == TaxTreatment.Taxed)
        {
            DomainGuard.IsTrue(basisPoints <= 0, Errors.TaxedPriceRequiresARate);
            DomainGuard.IsTrue(hasLegalBasis, Errors.TaxedPriceCannotCarryALegalBasis);

            return;
        }

        DomainGuard.IsTrue(basisPoints != 0, Errors.UntaxedPriceCannotCarryARate);
        DomainGuard.IsFalse(hasLegalBasis, Errors.UntaxedPriceRequiresALegalBasis);
    }

    private static void EnsurePrices(PackageDefinition definition)
    {
        var currency = definition.Currency.Trim();
        var prices = new List<Money?> { definition.BasePrice, definition.MinimumPrice };

        prices.AddRange(definition.Components.Select(x => (Money?)x.UnitPrice));
        prices.AddRange(definition.Components.SelectMany(x => x.Tiers ?? []).Select(x => (Money?)x.Price));

        DomainGuard.IsTrue(prices.Any(x => x is null || x.Amount < 0 || x.Currency != currency), Errors.PackagePriceCannotBeNegative);
        DomainGuard.IsTrue(definition.Kind == PackageKind.Fixed && definition.BasePrice.Amount <= 0, Errors.FixedPackageRequiresAPrice);
    }

    /// <summary>
    /// Un paquete público ya se le ofrece a todas: solo uno privado lleva copropiedades, y cada una una vez.
    /// </summary>
    private static void EnsureOffers(bool showInLandingPage, List<OfferedTenant> offeredTo)
    {
        DomainGuard.IsNull(offeredTo, Errors.OfferedTenantIsRequired);
        DomainGuard.IsTrue(showInLandingPage && offeredTo.Count > 0, Errors.PublicLicenseCannotBeOfferedToTenants);
        DomainGuard.IsTrue(offeredTo.Any(x => x is null || x.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(x.TenantName)), Errors.OfferedTenantIsRequired);
        DomainGuard.IsTrue(offeredTo.GroupBy(x => x.TenantId).Any(g => g.Count() > 1), Errors.TenantIsOfferedTwice);
    }

    private void ApplyOffers(List<OfferedTenant> offeredTo, Guid offeredBy, Instant now)
    {
        var current = OfferedTo.ToDictionary(x => x.TenantId);
        var offers = new List<LicenseOffer>();

        foreach (var tenant in offeredTo)
        {
            var name = tenant.TenantName.Trim();

            if (current.TryGetValue(tenant.TenantId, out var existing))
            {
                offers.Add(existing with { TenantName = name });

                continue;
            }

            offers.Add(new LicenseOffer(tenant.TenantId, name, offeredBy, now));

            AddEvent(PackageOfferedDomainEvent.Create(Id, Name, tenant.TenantId, name, offeredBy));
        }

        OfferedTo = offers;
    }
}

/// <summary>
/// Lo que define un paquete al crearlo o cambiarlo (pendings/739).
/// </summary>
/// <param name="Name">El nombre.</param>
/// <param name="ShortDescription">La descripción corta.</param>
/// <param name="Description">La descripción.</param>
/// <param name="Icon">El ícono.</param>
/// <param name="IsActive">Si está activo.</param>
/// <param name="IsPopular">Si se destaca.</param>
/// <param name="ShowInLandingPage">Si es público.</param>
/// <param name="OfferedTo">Las copropiedades a las que se ofrece si es privado.</param>
/// <param name="Kind">Fijo o configurable.</param>
/// <param name="Currency">La moneda.</param>
/// <param name="BasePrice">El precio del fijo o la base del configurable.</param>
/// <param name="MinimumPrice">El piso del configurable.</param>
/// <param name="TaxTreatment">El tratamiento del impuesto.</param>
/// <param name="TaxBasisPoints">La tarifa en puntos básicos.</param>
/// <param name="TaxLegalBasis">La base legal de un excluido o exento.</param>
/// <param name="InvoiceConcept">El concepto de la factura.</param>
/// <param name="Attributes">Los atributos del fijo.</param>
/// <param name="Components">Los componentes del configurable.</param>
public sealed record PackageDefinition(string Name, string ShortDescription, string Description, Icon Icon, bool IsActive, bool IsPopular, bool ShowInLandingPage, List<OfferedTenant> OfferedTo, PackageKind Kind, string Currency, Money BasePrice, Money MinimumPrice, TaxTreatment TaxTreatment, int TaxBasisPoints, string? TaxLegalBasis, string InvoiceConcept, List<PackageAttribute> Attributes, List<PackageComponent> Components);
