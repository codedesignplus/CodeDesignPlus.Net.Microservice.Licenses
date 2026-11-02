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
    /// El crédito por lo no prestado de la licencia anterior: la suma de <see cref="CreditedPeriods"/>.
    /// </summary>
    public Money ProrationCredit => (CreditedPeriods ?? []).Aggregate(Money.Zero(License.Total.Currency), (sum, period) => sum + period.Amount);

    /// <summary>
    /// Lo que cobra la pasarela por esta orden.
    /// </summary>
    public Money AmountToCharge => AmountDue ?? License.Total;

    /// <summary>
    /// Si la orden espera todavía el resultado de su pago.
    /// </summary>
    public bool IsAwaitingPayment => PaymentStatus is PaymentStatus.Initiated or PaymentStatus.Pending or PaymentStatus.Unknown;

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
    public static OrderAggregate Create(Guid id, Guid paymentId, License license, Buyer buyer, Tenant tenantDetail, Guid createdBy)
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

        return aggregate;
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
    /// <returns>La orden, a la espera del pago.</returns>
    public static OrderAggregate CreateRenewal(Guid id, Guid paymentId, License license, Buyer buyer, Tenant tenantDetail, SubscriptionCharge charge, Guid createdBy)
    {
        DomainGuard.IsFalse(charge.ProrationCredit.Amount == 0, Errors.RenewalCannotCarryProrationCredit);

        var order = Create(id, paymentId, license, buyer, tenantDetail, createdBy);

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
    /// <returns>La orden, a la espera del pago.</returns>
    public static OrderAggregate CreateChange(Guid id, Guid paymentId, License license, Buyer buyer, Tenant tenantDetail, Guid previousLicenseId, List<CreditedPeriod> creditedPeriods, SubscriptionCharge charge, Guid createdBy)
    {
        DomainGuard.GuidIsEmpty(previousLicenseId, Errors.LicenseIdIsRequired);
        DomainGuard.IsNull(creditedPeriods, Errors.CreditedPeriodsAreRequired);

        var order = Create(id, paymentId, license, buyer, tenantDetail, createdBy);

        order.Type = OrderType.Change;
        order.PreviousLicenseId = previousLicenseId;
        order.CreditedPeriods = creditedPeriods;

        var credited = order.ProrationCredit;

        DomainGuard.IsFalse(credited == charge.ProrationCredit, Errors.ChargeDoesNotMatchTheOrder);

        order.ApplyCharge(charge);

        return order;
    }

    private void ApplyCharge(SubscriptionCharge charge)
    {
        DomainGuard.IsNull(charge, Errors.ChargeDoesNotMatchTheOrder);
        DomainGuard.IsFalse(charge.Total == License.Total, Errors.ChargeDoesNotMatchTheOrder);

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
    public void SetPaymentStatus(PaymentStatus paymentStatus, Guid buyerId, Instant? periodStart = null)
    {
        // Idempotent: if already at this status, skip (message redelivery)
        if (PaymentStatus == paymentStatus)
            return;

        PaymentStatus = paymentStatus;

        if (paymentStatus == PaymentStatus.Succeeded)
        {
            IsSuccess = true;

            var now = SystemClock.Instance.GetCurrentInstant();

            PeriodStart = periodStart ?? now;
            PeriodEnd = BillingPeriod.End(PeriodStart.Value, License.BillingType);

            ProvisioningHistory.Add(new ProvisioningStep(ProvisioningStep.Payment, ProvisioningStepStatus.Completed, now));

            if (Type == OrderType.Purchase)
            {
                ProvisioningStatus = ProvisioningStatus.InProgress;

                ProvisioningHistory.Add(new ProvisioningStep("TenantProvisioning", ProvisioningStepStatus.Pending, now));
                ProvisioningHistory.Add(new ProvisioningStep("UserProvisioning", ProvisioningStepStatus.Pending, now));

                AddEvent(OrderPaidAndReadyForProvisioningDomainEvent.Create(this.Id, TenantDetail, ToLicenseTenant(), buyerId));
            }
            else
            {
                // La copropiedad ya existe: no hay nada que aprovisionar, solo aplicarle la licencia.
                ProvisioningStatus = ProvisioningStatus.Completed;

                if (SupersededBy is null)
                {
                    AddSubscriptionEvent(buyerId, now);
                }
            }
        }
        else if (paymentStatus == PaymentStatus.Failed || paymentStatus == PaymentStatus.Expired)
        {
            var now = SystemClock.Instance.GetCurrentInstant();
            ProvisioningStatus = ProvisioningStatus.PaymentFailed;
            ProvisioningHistory.Add(new ProvisioningStep(ProvisioningStep.Payment, ProvisioningStepStatus.Failed, now));
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

        return new SubscriptionBilling(License.SubTotal, License.Tax, License.Total, ProrationCredit, License.Total - AmountToCharge, AmountToCharge, PeriodStart!.Value, PeriodEnd!.Value, paidAt);
    }

    private void AddSubscriptionEvent(Guid buyerId, Instant paidAt)
    {
        if (Type == OrderType.Renewal)
        {
            AddEvent(SubscriptionRenewedDomainEvent.Create(Id, TenantDetail, ToLicenseTenant(), ToBilling(paidAt), buyerId));

            return;
        }

        AddEvent(SubscriptionChangedDomainEvent.Create(Id, TenantDetail, ToLicenseTenant(), PreviousLicenseId!.Value, CreditedPeriods ?? [], ToBilling(paidAt), buyerId));
    }

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
        var required = new[] { "TenantProvisioning", "UserProvisioning" };
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
