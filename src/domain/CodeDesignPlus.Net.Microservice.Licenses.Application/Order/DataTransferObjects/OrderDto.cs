using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.User;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

public class OrderDto : IDtoBase
{
    public required Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public Buyer Buyer { get; set; } = null!;
    public Domain.ValueObjects.License License { get; set; } = null!;
    public Tenant TenantDetail { get; set; } = null!;
    public PaymentStatus PaymentStatus { get; set; }
    public ProvisioningStatus ProvisioningStatus { get; set; }
    public List<Domain.ValueObjects.ProvisioningStep> ProvisioningHistory { get; set; } = [];

    /// <summary>
    /// La referencia en ms-filestorage del recibo de compra, si ya se genero.
    /// </summary>
    /// <remarks>
    /// Es la referencia, no la URL: quien lo muestre pide una firma nueva, porque la de ms-filestorage
    /// caduca a los 7 dias.
    /// </remarks>
    public Domain.ValueObjects.FileAttachment? Receipt { get; set; }
    public Instant CreatedAt { get; set; }
    public bool IsActive { get; set; }

    /// <summary>
    /// Para qué es: la compra, una renovación o un cambio de licencia (pendings/735, 738).
    /// </summary>
    public OrderType Type { get; set; }

    /// <summary>
    /// El periodo que paga; nulo mientras no se pague.
    /// </summary>
    public Instant? PeriodStart { get; set; }

    /// <summary>
    /// El fin del periodo que paga; nulo mientras no se pague.
    /// </summary>
    public Instant? PeriodEnd { get; set; }

    /// <summary>
    /// En un cambio, el crédito por lo no prestado de la licencia anterior, en unidades mínimas.
    /// </summary>
    public long ProrationCredit { get; set; }

    /// <summary>
    /// El saldo a favor aplicado, en unidades mínimas.
    /// </summary>
    public long CreditBalanceApplied { get; set; }

    /// <summary>
    /// Lo que cobró o cobra la pasarela, en unidades mínimas.
    /// </summary>
    public long AmountDue { get; set; }

    /// <summary>
    /// La orden que reemplazó a esta antes de pagarse, si alguna.
    /// </summary>
    public Guid? SupersededBy { get; set; }
}