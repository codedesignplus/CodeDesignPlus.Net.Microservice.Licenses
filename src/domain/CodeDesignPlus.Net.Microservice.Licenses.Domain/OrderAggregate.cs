using CodeDesignPlus.Net.Exceptions;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Financial;
using CodeDesignPlus.Net.ValueObjects.User;
using ProvisioningStep = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.ProvisioningStep;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain;

/// <summary>
/// Represents the Aggregate Root for an Order.
/// Manages the lifecycle of a license purchase, payment tracking, and triggers the tenant provisioning process upon success.
/// This aggregate acts as an immutable snapshot of the license, buyer, and tenant details at the exact moment of purchase.
/// </summary>
/// <param name="id">The unique identifier of the order.</param>
public class OrderAggregate(Guid id) : AggregateRootBase(id)
{
    /// <summary>
    /// Gets the unique identifier of the transaction provided by the external payment gateway.
    /// </summary>
    public Guid PaymentId { get; private set; }

    /// <summary>
    /// Gets the complete immutable snapshot of the license (including modules, attributes, descriptions) purchased in this order.
    /// This snapshot reflects exactly what the customer bought, regardless of later catalog changes.
    /// </summary>
    public License License { get; private set; } = null!;

    /// <summary>
    /// Gets the snapshot of the buyer's information at the time of purchase.
    /// </summary>
    public Buyer Buyer { get; private set; } = null!;

    /// <summary>
    /// Gets the details of the tenant workspace that will be provisioned once the order is successful.
    /// </summary>
    public Tenant TenantDetail { get; private set; } = null!;

    /// <summary>
    /// Gets the error message if the payment or order processing failed.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Indicates whether the order process was successful.
    /// </summary>
    public bool IsSuccess { get; private set; }

    /// <summary>
    /// Gets the current state of the payment.
    /// </summary>
    public PaymentStatus PaymentStatus { get; private set; }

    /// <summary>
    /// Lo que respondió la pasarela al iniciar el cobro, para devolverlo igual a un reintento del mismo formulario
    /// (pendings/589). Lo escribe solo <c>IOrderRepository.RecordInitiatedPaymentAsync</c>, con un <c>$set</c> de este campo,
    /// porque la respuesta del pago puede estar cambiando la orden a la vez. Nulo en las órdenes guardadas antes de existir.
    /// </summary>
    public InitiatedPayment? InitiatedPayment { get; private set; }

    /// <summary>
    /// Gets the overall provisioning status of the order.
    /// </summary>
    public ProvisioningStatus ProvisioningStatus { get; private set; }

    /// <summary>
    /// Gets the history of provisioning steps with their statuses.
    /// </summary>
    public List<ProvisioningStep> ProvisioningHistory { get; private set; } = [];

    /// <summary>
    /// Cuantas veces se ha reintentado el aprovisionamiento de este pedido.
    /// </summary>
    /// <remarks>
    /// No es argumento del constructor a proposito: el driver de Mongo exige que todos los argumentos del
    /// creador esten presentes, y los pedidos ya guardados no traen este campo. Ver regla 31.
    /// </remarks>
    public int ProvisioningAttempts { get; private set; }

    /// <summary>
    /// Cuantos reintentos se conceden antes de darse por vencido.
    /// </summary>
    /// <remarks>
    /// El reintento es la red que salva los fallos transitorios —un pod que se reinicia, un Mongo lento—,
    /// pero sin tope convierte un fallo <b>permanente</b> en un bucle infinito y silencioso sobre un pedido
    /// <b>ya cobrado</b>. Pendiente 139.
    /// </remarks>
    public const int MaxProvisioningAttempts = 5;

    /// <summary>
    /// Indica si ya se agotaron los reintentos concedidos.
    /// </summary>
    public bool HasExhaustedProvisioningAttempts => ProvisioningAttempts >= MaxProvisioningAttempts;

    /// <summary>
    /// La referencia en ms-filestorage del PDF del recibo de compra, una vez generado.
    /// </summary>
    /// <remarks>
    /// Se guarda la referencia y **no la URL firmada**: la firma que devuelve ms-filestorage caduca a los
    /// 7 dias, asi que persistirla dejaria un enlace roto con fecha. Quien lo muestre pide una firma nueva.
    /// </remarks>
    public FileAttachment? Receipt { get; private set; }

    // Los miembros que siguen no van al constructor (regla 31): las órdenes guardadas antes de existir no los traen. Una
    // orden sin tipo es una compra, que es lo que eran todas (pendings/735).

    /// <summary>
    /// Para qué es la orden: la compra que crea la copropiedad, una renovación o un cambio de licencia (pendings/735, 738).
    /// </summary>
    public OrderType Type { get; private set; }

    /// <summary>
    /// El inicio del periodo que paga la orden. Se fija al aprobarse el pago; nulo mientras no se pague.
    /// </summary>
    public Instant? PeriodStart { get; private set; }

    /// <summary>
    /// El fin del periodo que paga la orden. Se fija al aprobarse el pago; nulo mientras no se pague.
    /// </summary>
    public Instant? PeriodEnd { get; private set; }

    /// <summary>
    /// En un cambio de licencia, lo no prestado de cada orden anterior que se acredita (la nota crédito, pendings/738).
    /// </summary>
    public List<CreditedPeriod>? CreditedPeriods { get; private set; }

    /// <summary>
    /// En un cambio de licencia, la licencia que se reemplaza.
    /// </summary>
    public Guid? PreviousLicenseId { get; private set; }

    /// <summary>
    /// El saldo a favor de la copropiedad que se aparta para pagar parte de esta orden. Nulo en una compra.
    /// </summary>
    public Money? CreditBalanceApplied { get; private set; }

    /// <summary>
    /// Lo que se cobra por la pasarela: el precio menos el crédito y el saldo a favor aplicados. Nulo en una compra, que
    /// cobra el total.
    /// </summary>
    public Money? AmountDue { get; private set; }

    /// <summary>
    /// La orden que reemplazó a esta cuando aún no se había pagado: si llega a pagarse, lo pagado queda como saldo a favor y
    /// no cambia la licencia.
    /// </summary>
    public Guid? SupersededBy { get; private set; }

    /// <summary>
    /// La versión de los términos del servicio que aceptó quien pidió la orden (pendings/662). Nula si no se pidió aceptarlos.
    /// </summary>
    public string? AcceptedTermsVersion { get; private set; }

    /// <summary>
    /// Por qué no se aprobó el pago, tal como lo normaliza ms-payments; nulo si se aprobó o si aún no hay resultado
    /// (pendings/738). El texto del banco o de la pasarela queda en <see cref="Error"/>.
    /// </summary>
    public PaymentFailureReason? FailureReason { get; private set; }

    /// <summary>
    /// Los paquetes complementarios de la orden, cada uno con su copia y su precio (pendings/739). Nulo en las órdenes
    /// guardadas antes de existir los paquetes (regla 31).
    /// </summary>
    public List<PackageLine>? Packages { get; private set; }

    /// <summary>
    /// Con qué pagó el comprador en la pasarela, como ella lo nombra (<c>CARD</c>, <c>PSE</c>, <c>NEQUI</c>…); nulo mientras
    /// no se apruebe el pago o si la pasarela no lo dijo. La factura lo traduce al medio de pago de la DIAN (pendings/661).
    /// </summary>
    public string? PaymentMethodType { get; private set; }

    /// <summary>
    /// Si la tarjeta fue de crédito o de débito, como lo dijo la pasarela (<c>CREDIT</c>, <c>DEBIT</c>); nulo si no lo dijo o
    /// si no fue tarjeta. Nunca se adivina (pendings/661).
    /// </summary>
    public string? CardType { get; private set; }

    /// <summary>
    /// La factura electrónica de lo que pagó la orden, emitida por ExcellenceForge en ms-electronicinvoicing; nula mientras
    /// no se haya pedido (pendings/661).
    /// </summary>
    public OrderElectronicDocument? Invoice { get; private set; }

    /// <summary>
    /// En un cambio de licencia o una ampliación de paquete, las notas crédito sobre las facturas de las órdenes cuyo periodo
    /// no prestado se acredita, una por orden (pendings/661, 738, 739). Nula si la orden no acredita nada o aún no se pidieron.
    /// </summary>
    public List<OrderElectronicDocument>? CreditNotes { get; private set; }

    /// <summary>
    /// Cuándo se aprobó el pago: el paso del pago completado en el historial. Nulo si no se ha pagado.
    /// </summary>
    public Instant? PaidAt => ProvisioningHistory.LastOrDefault(x => x.StepName == ProvisioningStep.Payment && x.Status == ProvisioningStepStatus.Completed)?.Timestamp;

    /// <summary>
    /// La base de la orden: la de la licencia más la de sus paquetes.
    /// </summary>
    public Money SubTotal => (Packages ?? []).Aggregate(License.SubTotal, (sum, x) => sum + x.SubTotal);

    /// <summary>
    /// El impuesto de la orden: el de la licencia más el de sus paquetes.
    /// </summary>
    public Money Tax => (Packages ?? []).Aggregate(License.Tax, (sum, x) => sum + x.Tax);

    /// <summary>
    /// El precio completo de la orden con impuestos: la licencia más sus paquetes. Es lo que dice la factura.
    /// </summary>
    public Money Total => (Packages ?? []).Aggregate(License.Total, (sum, x) => sum + x.Total);

    /// <summary>
    /// El crédito por lo no prestado de la licencia anterior, con su IVA: la suma de <see cref="CreditedPeriods"/>.
    /// </summary>
    public Money ProrationCredit => (CreditedPeriods ?? []).Aggregate(Money.Zero(License.Total.Currency), (sum, period) => sum + period.Total);

    /// <summary>
    /// El IVA del crédito por lo no prestado: lo que la nota crédito devuelve de impuestos.
    /// </summary>
    public Money ProrationCreditTax => (CreditedPeriods ?? []).Aggregate(Money.Zero(License.Total.Currency), (sum, period) => period.Tax is null ? sum : sum + period.Tax);

    /// <summary>
    /// Lo que cobra la pasarela por esta orden.
    /// </summary>
    public Money AmountToCharge => AmountDue ?? Total;

    /// <summary>
    /// Reparte lo que cobra la pasarela entre base e IVA, para que el pago diga lo mismo que la orden (pendings/738).
    /// </summary>
    /// <remarks>
    /// Al precio completo se le resta el crédito por lo no prestado con su base y su IVA exactos. Lo que paga el saldo a
    /// favor se reparte en la proporción de lo que queda, porque el saldo no distingue base de impuesto. Si el crédito
    /// devuelve más IVA del que lleva el precio (una licencia excluida tras una gravada), lo cobrado es todo base.
    /// </remarks>
    /// <returns>La base y el IVA de <see cref="AmountToCharge"/>; suman exactamente lo cobrado.</returns>
    public (Money SubTotal, Money Tax) ChargeBreakdown()
    {
        var due = AmountToCharge;
        var afterCredit = Total - Money.Min(ProrationCredit, Total);

        if (due.Amount == 0 || afterCredit.Amount == 0)
        {
            return (Money.Zero(due.Currency), Money.Zero(due.Currency));
        }

        var taxAfterCredit = Math.Clamp(Tax.Amount - ProrationCreditTax.Amount, 0, afterCredit.Amount);
        var tax = due == afterCredit ? taxAfterCredit : (long)((decimal)taxAfterCredit * due.Amount / afterCredit.Amount);

        return (Money.FromLong(due.Amount - tax, due.Currency), Money.FromLong(tax, due.Currency));
    }

    /// <summary>
    /// Si la orden espera todavía el resultado de su pago.
    /// </summary>
    public bool IsAwaitingPayment => PaymentStatus is PaymentStatus.Initiated or PaymentStatus.Pending or PaymentStatus.Unknown;

    /// <summary>
    /// Si un usuario, en la copropiedad de su sesión, puede leer esta orden: es de esa copropiedad o la creó él.
    /// </summary>
    /// <remarks>
    /// Al creador también se le permite porque una compra la lee quien la paga antes de que exista la copropiedad que
    /// crea: el token de la pantalla de compra lleva otra copropiedad, o ninguna (pendings/735). Leer cualquier orden es
    /// una vista de plataforma con su propio endpoint, que concede el RBAC.
    /// </remarks>
    /// <param name="tenant">La copropiedad de la sesión.</param>
    /// <param name="user">El usuario de la sesión.</param>
    /// <returns><see langword="true"/> si la orden es de la copropiedad o la creó el usuario.</returns>
    public bool IsVisibleTo(Guid tenant, Guid user) => (tenant != Guid.Empty && TenantDetail.Id == tenant) || (user != Guid.Empty && CreatedBy == user);

    /// <summary>
    /// The license of this order with the period it paid, as ms-tenants applies it to the tenant.
    /// </summary>
    /// <returns>The license snapshot.</returns>
    public LicenseTenant ToLicenseTenant()
    {
        DomainGuard.IsTrue(PeriodStart is null || PeriodEnd is null, Errors.OrderHasNoPeriod);

        return LicenseTenant.Create(License.Id, License.Name, PeriodStart!.Value, PeriodEnd!.Value, License.Modules, License.Attributes);
    }

    /// <summary>
    /// Creates a new Order instance with the initial status of 'Initiated'.
    /// </summary>
    /// <param name="id">The order id, generated by the client.</param>
    /// <param name="paymentId">The payment id in ms-payments.</param>
    /// <param name="license">The license snapshot.</param>
    /// <param name="buyer">The buyer.</param>
    /// <param name="tenantDetail">The tenant.</param>
    /// <param name="createdBy">Who creates it.</param>
    /// <param name="packages">Los paquetes que se compran con la licencia, en la misma orden y el mismo pago (pendings/739).</param>
    /// <returns>The order.</returns>
    public static OrderAggregate Create(Guid id, Guid paymentId, License license, Buyer buyer, Tenant tenantDetail, Guid createdBy, List<PackageLine>? packages = null)
    {
        DomainGuard.GuidIsEmpty(id, Errors.IdOrderIsRequired);
        DomainGuard.GuidIsEmpty(paymentId, Errors.IdPaymentIsRequired);
        DomainGuard.IsNull(license, Errors.LicenseIdIsRequired);
        DomainGuard.IsNull(buyer, Errors.BuyerIsRequired);
        DomainGuard.IsNull(tenantDetail, Errors.TenantDetailIsRequired);

        var aggregate = new OrderAggregate(id)
        {
            PaymentId = paymentId,
            License = license,
            Buyer = buyer,
            PaymentStatus = PaymentStatus.Initiated,
            ProvisioningStatus = ProvisioningStatus.PaymentPending,
            TenantDetail = tenantDetail,
            IsActive = true,
            CreatedAt = SystemClock.Instance.GetCurrentInstant(),
            CreatedBy = createdBy
        };

        aggregate.SetPackages(packages);

        return aggregate;
    }

    private void SetPackages(List<PackageLine>? packages)
    {
        if (packages is null || packages.Count == 0)
        {
            return;
        }

        DomainGuard.IsTrue(packages.Any(x => x is null || x.Total.Currency != License.Total.Currency), Errors.PackageLineIsInvalid);
        DomainGuard.IsTrue(packages.GroupBy(x => x.PackageId).Any(g => g.Count() > 1) || packages.GroupBy(x => x.LineId).Any(g => g.Count() > 1), Errors.PackageIsRepeatedInOrder);

        Packages = [.. packages];
    }

    /// <summary>
    /// Crea la orden que renueva la licencia vigente de una copropiedad que ya existe (pendings/735).
    /// </summary>
    /// <param name="id">El identificador, generado por el cliente.</param>
    /// <param name="paymentId">El identificador del cobro en ms-payments.</param>
    /// <param name="license">La copia de la licencia vigente con el precio vigente del catálogo.</param>
    /// <param name="buyer">Quien paga.</param>
    /// <param name="tenantDetail">La copropiedad.</param>
    /// <param name="charge">El cobro, con el saldo a favor que se aplica.</param>
    /// <param name="createdBy">Quien la pide.</param>
    /// <param name="packages">Los paquetes que se renuevan con la licencia anual (pendings/739).</param>
    /// <returns>La orden, a la espera del pago.</returns>
    public static OrderAggregate CreateRenewal(Guid id, Guid paymentId, License license, Buyer buyer, Tenant tenantDetail, SubscriptionCharge charge, Guid createdBy, List<PackageLine>? packages = null)
    {
        DomainGuard.IsFalse(charge.ProrationCredit.Amount == 0, Errors.RenewalCannotCarryProrationCredit);

        var order = Create(id, paymentId, license, buyer, tenantDetail, createdBy, packages);

        order.Type = OrderType.Renewal;
        order.ApplyCharge(charge);

        return order;
    }

    /// <summary>
    /// Crea la orden que cambia la licencia de una copropiedad por una mayor, por su precio completo (pendings/738).
    /// </summary>
    /// <param name="id">El identificador, generado por el cliente.</param>
    /// <param name="paymentId">El identificador del cobro en ms-payments.</param>
    /// <param name="license">La copia de la licencia destino, con sus módulos y atributos y su precio completo.</param>
    /// <param name="buyer">Quien paga.</param>
    /// <param name="tenantDetail">La copropiedad.</param>
    /// <param name="previousLicenseId">La licencia que se reemplaza.</param>
    /// <param name="creditedPeriods">Lo no prestado de las órdenes anteriores, que se acredita al pagarse.</param>
    /// <param name="charge">El cobro, calculado con ese crédito y el saldo a favor.</param>
    /// <param name="createdBy">Quien lo pide.</param>
    /// <param name="packages">Los paquetes vigentes recotizados al periodo nuevo, con licencia anual (pendings/739).</param>
    /// <returns>La orden, a la espera del pago.</returns>
    public static OrderAggregate CreateChange(Guid id, Guid paymentId, License license, Buyer buyer, Tenant tenantDetail, Guid previousLicenseId, List<CreditedPeriod> creditedPeriods, SubscriptionCharge charge, Guid createdBy, List<PackageLine>? packages = null)
    {
        DomainGuard.GuidIsEmpty(previousLicenseId, Errors.LicenseIdIsRequired);
        DomainGuard.IsNull(creditedPeriods, Errors.CreditedPeriodsAreRequired);

        var order = Create(id, paymentId, license, buyer, tenantDetail, createdBy, packages);

        order.Type = OrderType.Change;
        order.PreviousLicenseId = previousLicenseId;
        order.CreditedPeriods = creditedPeriods;

        var credited = order.ProrationCredit;

        DomainGuard.IsFalse(credited == charge.ProrationCredit, Errors.ChargeDoesNotMatchTheOrder);

        order.ApplyCharge(charge);

        return order;
    }

    /// <summary>
    /// Crea la orden que compra o amplía un paquete para una copropiedad que ya existe (pendings/739). No toca la licencia:
    /// lleva su copia con precio cero solo para saber su periodicidad y su moneda.
    /// </summary>
    /// <param name="id">El identificador, generado por el cliente.</param>
    /// <param name="paymentId">El identificador del cobro en ms-payments.</param>
    /// <param name="license">La copia de la licencia vigente con precio cero.</param>
    /// <param name="buyer">Quien paga.</param>
    /// <param name="tenantDetail">La copropiedad.</param>
    /// <param name="package">La línea del paquete.</param>
    /// <param name="creditedPeriods">En una ampliación, lo no usado de la línea que se reemplaza.</param>
    /// <param name="charge">El cobro, con ese crédito y el saldo a favor.</param>
    /// <param name="createdBy">Quien la pide.</param>
    /// <returns>La orden, a la espera del pago.</returns>
    public static OrderAggregate CreatePackagePurchase(Guid id, Guid paymentId, License license, Buyer buyer, Tenant tenantDetail, PackageLine package, List<CreditedPeriod> creditedPeriods, SubscriptionCharge charge, Guid createdBy)
    {
        DomainGuard.IsNull(package, Errors.PackageOrderRequiresOneLine);
        DomainGuard.IsTrue(license.Total.Amount != 0, Errors.PackageOrderRequiresOneLine);
        DomainGuard.IsNull(creditedPeriods, Errors.CreditedPeriodsAreRequired);

        var order = Create(id, paymentId, license, buyer, tenantDetail, createdBy, [package]);

        order.Type = OrderType.Package;
        order.CreditedPeriods = creditedPeriods;

        DomainGuard.IsFalse(order.ProrationCredit == charge.ProrationCredit, Errors.ChargeDoesNotMatchTheOrder);

        order.ApplyCharge(charge);

        return order;
    }

    /// <summary>
    /// Deja constancia de la versión de los términos que aceptó quien pide la orden (pendings/662).
    /// </summary>
    /// <param name="version">La versión aceptada; vacía si la pantalla no los pidió.</param>
    public void RecordAcceptedTerms(string? version)
    {
        AcceptedTermsVersion = string.IsNullOrWhiteSpace(version) ? null : version.Trim();
    }

    private void ApplyCharge(SubscriptionCharge charge)
    {
        DomainGuard.IsNull(charge, Errors.ChargeDoesNotMatchTheOrder);
        DomainGuard.IsFalse(charge.Total == Total, Errors.ChargeDoesNotMatchTheOrder);

        CreditBalanceApplied = charge.CreditBalanceApplied;
        AmountDue = charge.AmountDue;
    }

    /// <summary>
    /// Deja la orden sin efecto porque otra de la misma copropiedad la reemplazó antes de que se pagara.
    /// </summary>
    /// <param name="replacedBy">La orden que la reemplaza.</param>
    /// <param name="updatedBy">Quien pidió la nueva.</param>
    public void Supersede(Guid replacedBy, Guid updatedBy)
    {
        DomainGuard.IsFalse(IsAwaitingPayment, Errors.OnlyAnUnpaidOrderCanBeSuperseded);

        SupersededBy = replacedBy;
        IsActive = false;
        UpdatedAt = SystemClock.Instance.GetCurrentInstant();
        UpdatedBy = updatedBy;
    }

    /// <summary>
    /// Updates the payment status. When succeeded, fixes the period it pays and dispatches its domain event: the
    /// provisioning of a purchase, or the renewal or the change of the license of an existing tenant.
    /// The license snapshot (including modules and attributes) is already complete from order creation.
    /// </summary>
    /// <param name="paymentStatus">The new payment status.</param>
    /// <param name="buyerId">The buyer's identifier.</param>
    /// <param name="periodStart">
    /// When the paid period starts; <c>null</c> is now. A renewal paid in advance starts where the paid period ends.
    /// </param>
    /// <param name="failureReason">Por qué no se aprobó, si no se aprobó.</param>
    /// <param name="providerMessage">Lo que dijo el banco o la pasarela, si no se aprobó.</param>
    /// <param name="licensePeriodEnd">
    /// En una orden de paquete con licencia anual, el fin del periodo de la licencia en curso: ahí vence el paquete
    /// (pendings/739). Se ignora en las demás.
    /// </param>
    /// <param name="paymentMethodType">Con qué pagó el comprador, como lo nombra la pasarela; se guarda solo si se aprobó.</param>
    /// <param name="cardType">Si la tarjeta fue de crédito o de débito, como lo dijo la pasarela; se guarda solo si se aprobó.</param>
    public void SetPaymentStatus(PaymentStatus paymentStatus, Guid buyerId, Instant? periodStart = null, PaymentFailureReason? failureReason = null, string? providerMessage = null, Instant? licensePeriodEnd = null, string? paymentMethodType = null, string? cardType = null)
    {
        // Idempotent: if already at this status, skip (message redelivery)
        if (PaymentStatus == paymentStatus)
            return;

        PaymentStatus = paymentStatus;

        if (paymentStatus == PaymentStatus.Succeeded)
        {
            IsSuccess = true;
            PaymentMethodType = string.IsNullOrWhiteSpace(paymentMethodType) ? null : paymentMethodType.Trim();
            CardType = string.IsNullOrWhiteSpace(cardType) ? null : cardType.Trim();

            var now = SystemClock.Instance.GetCurrentInstant();

            PeriodStart = periodStart ?? now;
            PeriodEnd = Type == OrderType.Package
                ? PackageTerm.End(PeriodStart.Value, License.BillingType, licensePeriodEnd)
                : BillingPeriod.End(PeriodStart.Value, License.BillingType);

            // Con licencia anual el paquete va con el periodo de la orden; con mensual, un año desde que empieza (pendings/739).
            var packagesEnd = Type == OrderType.Package ? PeriodEnd.Value : PackageTerm.End(PeriodStart.Value, License.BillingType, PeriodEnd);

            Packages = Packages?.Select(x => x.WithPeriod(PeriodStart.Value, packagesEnd)).ToList();

            ProvisioningHistory.Add(new ProvisioningStep(ProvisioningStep.Payment, ProvisioningStepStatus.Completed, now));

            if (Type == OrderType.Purchase)
            {
                ProvisioningStatus = ProvisioningStatus.InProgress;

                ProvisioningHistory.Add(new ProvisioningStep(ProvisioningStep.TenantProvisioning, ProvisioningStepStatus.Pending, now));
                ProvisioningHistory.Add(new ProvisioningStep(ProvisioningStep.UserProvisioning, ProvisioningStepStatus.Pending, now));
            }
            else
            {
                // La copropiedad ya existe: no hay nada que aprovisionar, solo aplicarle la licencia.
                ProvisioningStatus = ProvisioningStatus.Completed;
            }

            AddPaidEvent(buyerId, now);
        }
        else if (paymentStatus == PaymentStatus.Failed || paymentStatus == PaymentStatus.Expired)
        {
            var now = SystemClock.Instance.GetCurrentInstant();
            ProvisioningStatus = ProvisioningStatus.PaymentFailed;
            var reason = failureReason ?? PaymentFailureReason.None;

            // Una sesión vencida sin motivo publicado venció: no se queda sin explicación.
            FailureReason = reason == PaymentFailureReason.None && paymentStatus == PaymentStatus.Expired ? PaymentFailureReason.Expired : reason;
            Error = string.IsNullOrWhiteSpace(providerMessage) ? null : providerMessage.Trim();
            ProvisioningHistory.Add(new ProvisioningStep(ProvisioningStep.Payment, ProvisioningStepStatus.Failed, now, Error));
        }

        UpdatedAt = SystemClock.Instance.GetCurrentInstant();
    }

    /// <summary>
    /// Lo que necesita la factura electrónica de esta orden pagada (pendings/661): el precio completo, el anticipo aplicado
    /// y lo cobrado.
    /// </summary>
    /// <param name="paidAt">Cuándo se aprobó el pago.</param>
    /// <returns>Los datos de la factura.</returns>
    public SubscriptionBilling ToBilling(Instant paidAt)
    {
        DomainGuard.IsTrue(PeriodStart is null || PeriodEnd is null, Errors.OrderHasNoPeriod);

        var lines = new List<BillingLine>();

        if (Type != OrderType.Package)
        {
            lines.Add(new BillingLine(License.InvoiceConcept ?? License.Name, null, License.SubTotal, License.Tax, License.Total, License.TaxTreatment, License.TaxBasisPoints, License.TaxLegalBasis, PeriodStart!.Value, PeriodEnd!.Value));
        }

        lines.AddRange((Packages ?? []).Select(x => new BillingLine(x.InvoiceConcept ?? x.Name, x.LineId, x.SubTotal, x.Tax, x.Total, x.TaxTreatment, x.TaxBasisPoints, x.TaxLegalBasis, x.PeriodStart ?? PeriodStart!.Value, x.PeriodEnd ?? PeriodEnd!.Value)));

        // Los importes son los de la orden entera (licencia y paquetes); cada línea dice su parte (pendings/739).
        return new SubscriptionBilling(SubTotal, Tax, Total, ProrationCredit, Total - AmountToCharge, AmountToCharge, PeriodStart!.Value, PeriodEnd!.Value, paidAt, License.TaxTreatment, License.TaxBasisPoints, License.TaxLegalBasis, License.InvoiceConcept) { Lines = lines };
    }

    /// <summary>
    /// Publica lo que sigue al pago aprobado: el aprovisionamiento de una compra, o la renovación o el cambio de la licencia
    /// de una copropiedad que ya existe.
    /// </summary>
    /// <remarks>
    /// Si la licencia de la orden no se puede convertir en la que se le aplica a la copropiedad, el pago queda registrado
    /// igual y la orden en <see cref="ProvisioningStatus.PartiallyFailed"/> con el motivo en el historial y
    /// <see cref="OrderProvisioningFailedDomainEvent"/>, como cuando la conciliación se rinde. Antes la excepción subía
    /// hasta el consumidor del pago, que no guardaba nada: el pago aprobado se perdía y cada reentrega volvía a fallar
    /// (pendings/738, «Élite Plus · Conjunto Prueba Licencias»).
    /// </remarks>
    private void AddPaidEvent(Guid buyerId, Instant paidAt)
    {
        try
        {
            if (Type == OrderType.Purchase)
            {
                AddEvent(OrderPaidAndReadyForProvisioningDomainEvent.Create(Id, TenantDetail, ToLicenseTenant(), buyerId, ToPackageTenants(), ToBilling(paidAt)));

                return;
            }

            if (SupersededBy is null)
            {
                AddSubscriptionEvent(buyerId, paidAt);
            }
        }
        catch (CodeDesignPlusException exception) when (exception.Layer == Layer.Domain)
        {
            var error = $"{exception.Code}: {exception.Message}";

            ProvisioningStatus = ProvisioningStatus.PartiallyFailed;
            ProvisioningHistory.Add(new ProvisioningStep(ProvisioningStep.LicenseApplication, ProvisioningStepStatus.Failed, paidAt, error));

            AddEvent(OrderProvisioningFailedDomainEvent.Create(Id, ProvisioningStep.LicenseApplication, error, buyerId));
        }
    }

    /// <summary>
    /// Si el pago se aprobó pero la licencia no se pudo aplicar a la copropiedad: lo que sigue al pago (inactivar la licencia
    /// anterior, el comprobante y el correo) espera a que se resuelva (pendings/738).
    /// </summary>
    public bool LicenseApplicationFailed => ProvisioningHistory.Any(x => x.StepName == ProvisioningStep.LicenseApplication && x.Status == ProvisioningStepStatus.Failed);

    private void AddSubscriptionEvent(Guid buyerId, Instant paidAt)
    {
        if (Type == OrderType.Package)
        {
            AddEvent(SubscriptionPackagesPurchasedDomainEvent.Create(Id, TenantDetail, ToPackageTenants(), CreditedPeriods ?? [], ToBilling(paidAt), buyerId));

            return;
        }

        if (Type == OrderType.Renewal)
        {
            AddEvent(SubscriptionRenewedDomainEvent.Create(Id, TenantDetail, ToLicenseTenant(), ToBilling(paidAt), buyerId, ToPackageTenants()));

            return;
        }

        AddEvent(SubscriptionChangedDomainEvent.Create(Id, TenantDetail, ToLicenseTenant(), PreviousLicenseId!.Value, CreditedPeriods ?? [], ToBilling(paidAt), buyerId, ToPackageTenants()));
    }

    /// <summary>
    /// Los paquetes de la orden pagada como los aplica ms-tenants (pendings/739).
    /// </summary>
    /// <returns>Los paquetes con su periodo; vacío si la orden no tiene.</returns>
    public List<PackageTenant> ToPackageTenants() => [.. (Packages ?? []).Select(PackageTenant.Of)];

    /// <summary>
    /// Deja pedidas la factura de la orden y, en un cambio o una ampliación, sus notas crédito (pendings/661). Se escribe
    /// antes de llamar a ms-electronicinvoicing, para que su respuesta encuentre la orden.
    /// </summary>
    /// <param name="invoice">La factura, pedida.</param>
    /// <param name="creditNotes">Las notas crédito, pedidas o para revisión; vacía si la orden no acredita nada.</param>
    public void RequestElectronicDocuments(OrderElectronicDocument invoice, List<OrderElectronicDocument> creditNotes)
    {
        DomainGuard.IsNull(invoice, Errors.ElectronicDocumentIdIsRequired);
        DomainGuard.IsTrue(PaymentStatus != PaymentStatus.Succeeded, Errors.OnlyAPaidOrderIsInvoiced);

        Invoice = invoice;
        CreditNotes = creditNotes is { Count: > 0 } ? [.. creditNotes] : null;
    }

    /// <summary>
    /// El documento electrónico de esta orden con ese id: la factura o una de sus notas crédito.
    /// </summary>
    /// <param name="documentId">El id del documento en ms-electronicinvoicing.</param>
    /// <returns>El documento, o nulo si no es de esta orden.</returns>
    public OrderElectronicDocument? ElectronicDocumentOf(Guid documentId) => Invoice?.DocumentId == documentId ? Invoice : CreditNotes?.Find(x => x.DocumentId == documentId);

    public void CompleteProvisioningStep(string stepName, Guid updatedBy)
    {
        var existingCompleted = ProvisioningHistory.Any(s => s.StepName == stepName && s.Status == ProvisioningStepStatus.Completed);
        if (existingCompleted) return;

        ProvisioningHistory.Add(new ProvisioningStep(stepName, ProvisioningStepStatus.Completed, SystemClock.Instance.GetCurrentInstant()));
        UpdatedAt = SystemClock.Instance.GetCurrentInstant();
        EvaluateCompletion();
    }

    /// <summary>
    /// Anota un reintento del aprovisionamiento y devuelve cuantos van.
    /// </summary>
    /// <remarks>
    /// Lo llama <c>OrderReconciliationJob</c> antes de republicar el evento. Mueve <c>UpdatedAt</c> porque
    /// el propio job usa esa marca para no reprocesar el mismo pedido en el ciclo siguiente.
    /// </remarks>
    public int RegisterProvisioningAttempt()
    {
        ProvisioningAttempts++;
        UpdatedAt = SystemClock.Instance.GetCurrentInstant();

        return ProvisioningAttempts;
    }

    public void FailProvisioningStep(string stepName, string error, Guid updatedBy)
    {
        ProvisioningHistory.Add(new ProvisioningStep(stepName, ProvisioningStepStatus.Failed, SystemClock.Instance.GetCurrentInstant(), error));
        ProvisioningStatus = ProvisioningStatus.PartiallyFailed;
        UpdatedAt = SystemClock.Instance.GetCurrentInstant();
        AddEvent(OrderProvisioningFailedDomainEvent.Create(Id, stepName, error, updatedBy));
    }

    /// <summary>
    /// Asocia el recibo de compra generado tras el pago.
    /// </summary>
    /// <param name="fileId">El identificador del fichero en ms-filestorage.</param>
    /// <param name="fileName">El nombre con el que se subio, ej. <c>PurchaseReceipt-{id}.pdf</c>.</param>
    /// <param name="target">El contenedor donde vive, ej. <c>licenses-pdf/{tenant}</c>.</param>
    /// <param name="updatedBy">El identificador del usuario o proceso que lo asocia.</param>
    /// <remarks>
    /// El PDF ya se generaba y se subia, pero la referencia solo viajaba adjunta al correo: la pantalla de
    /// compra no tenia de donde sacarla y su boton de "ver recibo" no aparecia nunca.
    /// <para>
    /// Si el recibo se regenera —el webhook de la pasarela reentrega— la referencia apunta al ultimo, o el
    /// boton abriria un PDF huerfano.
    /// </para>
    /// </remarks>
    public void AttachReceipt(Guid fileId, string fileName, string target, Guid updatedBy)
    {
        Receipt = new FileAttachment(fileId, fileName, target);

        UpdatedAt = SystemClock.Instance.GetCurrentInstant();
        UpdatedBy = updatedBy;
    }

    public void SetProvisioningCompleted()
    {
        if (ProvisioningStatus == ProvisioningStatus.Completed)
            return;

        ProvisioningStatus = ProvisioningStatus.Completed;
        UpdatedAt = SystemClock.Instance.GetCurrentInstant();
        AddEvent(OrderProvisioningCompletedDomainEvent.Create(Id, Buyer.BuyerId, TenantDetail.Id));
    }

    private void EvaluateCompletion()
    {
        var required = ProvisioningStep.RequiredForPurchase;
        var completed = ProvisioningHistory
            .Where(s => s.Status == ProvisioningStepStatus.Completed)
            .Select(s => s.StepName)
            .ToHashSet();

        if (required.All(completed.Contains))
        {
            ProvisioningStatus = ProvisioningStatus.Completed;
            AddEvent(OrderProvisioningCompletedDomainEvent.Create(Id, Buyer.BuyerId, TenantDetail.Id));
        }
    }
}
