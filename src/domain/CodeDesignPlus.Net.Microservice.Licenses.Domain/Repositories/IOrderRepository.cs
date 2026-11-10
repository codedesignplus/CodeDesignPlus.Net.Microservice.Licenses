using CodeDesignPlus.Net.Core.Abstractions.Models.Pager;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Repositories;

/// <summary>
/// Define el contrato del repositorio para la persistencia y recuperación del agregado
/// <see cref="OrderAggregate"/>.
/// </summary>
public interface IOrderRepository : IRepositoryBase
{
    /// <summary>
    /// Busca la orden exitosa más reciente para un tenant dado.
    /// Utilizado por el servicio gRPC para obtener el snapshot de licencia al provisionar
    /// o para validar la licencia activa en el middleware de seguridad.
    /// </summary>
    /// <param name="tenantId">El identificador del tenant.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>La orden exitosa más reciente, o null si no existe.</returns>
    Task<OrderAggregate?> FindSucceededOrderByTenantAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Busca la orden exitosa activa para un tenant y una licencia específica.
    /// Retorna la más reciente si existen múltiples órdenes (upgrades/renovaciones).
    /// </summary>
    /// <param name="tenantId">El identificador del tenant.</param>
    /// <param name="licenseId">El identificador de la licencia.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>La orden exitosa más reciente para la combinación tenant+licencia, o null si no existe.</returns>
    Task<OrderAggregate?> FindActiveSubscriptionAsync(Guid tenantId, Guid licenseId, CancellationToken cancellationToken);

    /// <summary>
    /// Completa un paso de provisioning de forma atómica usando FindOneAndUpdate.
    /// Evita race conditions cuando múltiples handlers actualizan la misma orden concurrentemente.
    /// </summary>
    Task<OrderAggregate?> CompleteProvisioningStepAtomicAsync(Guid orderId, string stepName, CancellationToken cancellationToken);

    // ESCRITURAS PARCIALES. Desde que se publica OrderPaidAndReadyForProvisioning, ms-tenants y ms-users marcan
    // sus pasos sobre la orden con CompleteProvisioningStepAtomicAsync. Un UpdateAsync del documento entero,
    // hecho con una copia leida antes, borra esos pasos: asi se quedaban las compras «en curso» hasta la
    // conciliacion (pendings/080). Por eso todo cambio de la orden despues del pago escribe solo sus campos, y
    // con un filtro que dice si el cambio aplica: el efecto (evento, recibo, correo) se hace solo si aplico.

    /// <summary>
    /// Guarda el resultado del pago que ya se aplico sobre <paramref name="order"/>, solo si la orden sigue en
    /// <paramref name="previousStatus"/>.
    /// </summary>
    /// <returns><c>true</c> si esta llamada aplico el cambio; <c>false</c> si otra (el webhook o la
    /// conciliacion) ya lo habia hecho.</returns>
    Task<bool> ApplyPaymentStatusAsync(OrderAggregate order, PaymentStatus previousStatus, CancellationToken cancellationToken);

    /// <summary>
    /// Guarda la factura pedida de la orden y sus notas crédito, solo si la orden todavía no tiene factura (pendings/661).
    /// </summary>
    /// <returns><c>true</c> si esta llamada las guardó; <c>false</c> si otra entrega ya lo había hecho.</returns>
    Task<bool> RequestElectronicDocumentsAsync(OrderAggregate order, CancellationToken cancellationToken);

    /// <summary>
    /// Guarda lo que ms-electronicinvoicing dijo de un documento de la orden (la factura o una de sus notas), solo si ese
    /// documento sigue siendo de la orden y no está ya validado: un rechazo tardío no deshace una validación (pendings/661).
    /// </summary>
    /// <returns><c>true</c> si el documento es de la orden y no estaba validado; una reentrega del mismo rechazo también
    /// devuelve <c>true</c>, para que el aviso que no alcanzó a salir se mande con el mismo id.</returns>
    Task<bool> RecordElectronicDocumentAsync(Guid orderId, OrderElectronicDocument document, CancellationToken cancellationToken);

    /// <summary>
    /// Asocia el recibo a la orden sin tocar el resto del documento y, si el correo de la compra espera a la factura
    /// electrónica, hasta cuándo (pendings/661, paso 4).
    /// </summary>
    Task AttachReceiptAsync(Guid orderId, FileAttachment receipt, Instant? emailWaitsUntil, Guid updatedBy, CancellationToken cancellationToken);

    /// <summary>
    /// Reserva el envío del correo del comprobante: solo si aún no salió y, si lleva la factura, si la factura tampoco.
    /// Escritura condicional: de dos que lo intentan a la vez (el consumidor de la factura y el job), envía uno.
    /// </summary>
    /// <returns><c>true</c> si este proceso lo envía.</returns>
    Task<bool> ClaimReceiptEmailAsync(Guid orderId, bool withInvoice, Instant sentAt, CancellationToken cancellationToken);

    /// <summary>
    /// Reserva el envío del correo aparte con la factura: solo si el del comprobante ya salió y la factura no.
    /// </summary>
    /// <returns><c>true</c> si este proceso lo envía.</returns>
    Task<bool> ClaimInvoiceEmailAsync(Guid orderId, Instant sentAt, CancellationToken cancellationToken);

    /// <summary>
    /// Devuelve la reserva de un correo que no se pudo publicar, para que el siguiente intento lo envíe. Solo la que hizo
    /// este proceso (la del mismo instante).
    /// </summary>
    Task ReleaseReceiptEmailAsync(Guid orderId, bool withInvoice, Instant sentAt, CancellationToken cancellationToken);

    /// <summary>
    /// Devuelve la reserva del correo aparte con la factura que no se pudo publicar.
    /// </summary>
    Task ReleaseInvoiceEmailAsync(Guid orderId, Instant sentAt, CancellationToken cancellationToken);

    /// <summary>
    /// Las órdenes cuyo correo esperaba a la factura y ya no espera más: venció la espera y no ha salido.
    /// </summary>
    Task<List<OrderAggregate>> FindReceiptEmailsOverdueAsync(Instant now, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Guarda en un documento ya validado los archivos que ms-electronicinvoicing consiguió después de validarlo.
    /// </summary>
    /// <returns><c>true</c> si el documento era de la orden y estaba validado.</returns>
    Task<bool> RecordElectronicDocumentFilesAsync(Guid orderId, OrderElectronicDocument document, CancellationToken cancellationToken);

    /// <summary>
    /// Pasa la orden de <c>InProgress</c> a <c>Completed</c>.
    /// </summary>
    /// <returns><c>true</c> si esta llamada la completo; <c>false</c> si ya no estaba en curso.</returns>
    Task<bool> MarkProvisioningCompletedAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Anota un paso fallido y deja la orden en <c>PartiallyFailed</c>, solo si seguia en curso.
    /// </summary>
    /// <returns><c>true</c> si esta llamada la marco; <c>false</c> si ya no estaba en curso.</returns>
    Task<bool> FailProvisioningAsync(Guid orderId, ProvisioningStep failedStep, Guid updatedBy, CancellationToken cancellationToken);

    /// <summary>
    /// Suma un intento de aprovisionamiento y mueve <c>UpdatedAt</c>, solo si la orden sigue en curso.
    /// </summary>
    /// <returns>El numero de intentos tras sumar, o <c>null</c> si la orden ya no estaba en curso.</returns>
    Task<int?> RegisterProvisioningAttemptAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Mueve <c>UpdatedAt</c> de una orden que sigue esperando su pago, para que la conciliación pase a las siguientes del
    /// lote y vuelva a ella en otro ciclo (pendings/738).
    /// </summary>
    /// <param name="orderId">La orden.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns><c>true</c> si la orden seguía esperando su pago.</returns>
    Task<bool> TouchPaymentPendingAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Finds orders stuck in a given provisioning status for longer than the specified duration.
    /// Used by the reconciliation job to detect and recover failed provisioning flows.
    /// </summary>
    /// <param name="status">The provisioning status to filter by.</param>
    /// <param name="stuckDuration">The minimum time the order must have been stuck.</param>
    /// <param name="limit">The maximum number of orders to return.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>A list of stuck orders, oldest first by UpdatedAt or, if never updated, by CreatedAt.</returns>
    Task<List<OrderAggregate>> FindStuckOrdersAsync(ProvisioningStatus status, Duration stuckDuration, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves all orders across all tenants with criteria-based filtering, sorting, and pagination.
    /// Bypasses tenant isolation for administrative views.
    /// </summary>
    /// <param name="criteria">The criteria for filtering, sorting, and paginating orders.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>A paginated list of orders.</returns>
    Task<Pagination<OrderAggregate>> GetAllOrdersAsync(CodeDesignPlus.Net.Core.Abstractions.Models.Criteria.Criteria criteria, CancellationToken cancellationToken);

    /// <summary>
    /// Guarda la orden si su id no existe todavía (pendings/589, regla 66).
    /// </summary>
    /// <param name="order">La orden nueva.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns><c>false</c> si otro envío del mismo formulario ya la guardó: el segundo choca contra la clave primaria.</returns>
    Task<bool> CreateIfAbsentAsync(OrderAggregate order, CancellationToken cancellationToken);

    /// <summary>
    /// Anota en la orden lo que respondió la pasarela al iniciar su cobro, sin reemplazar el resto del documento
    /// (pendings/589).
    /// </summary>
    /// <remarks>
    /// Va con un <c>$set</c> de ese campo y no con un reemplazo: con tarjeta, la respuesta del pago puede estar actualizando
    /// la orden a la vez, y un reemplazo con la copia de aquí borraría ese cambio.
    /// </remarks>
    /// <param name="id">La orden.</param>
    /// <param name="payment">La respuesta de la pasarela.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task RecordInitiatedPaymentAsync(Guid id, InitiatedPayment payment, CancellationToken cancellationToken);

    /// <summary>
    /// Las órdenes pagadas y activas de una copropiedad: con ellas se arma su suscripción (pendings/735).
    /// </summary>
    /// <param name="tenantId">La copropiedad.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Las órdenes, en cualquier orden.</returns>
    Task<List<OrderAggregate>> FindPaidByTenantAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Las órdenes pagadas de una copropiedad que traen paquetes, activas o no: un cambio de licencia inactiva la orden de la
    /// compra, pero sus paquetes siguen si no se recotizaron (pendings/739).
    /// </summary>
    /// <param name="tenantId">La copropiedad.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Las órdenes.</returns>
    Task<List<OrderAggregate>> FindPaidWithPackagesByTenantAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Las órdenes pagadas de todas las copropiedades que traen paquetes, para los avisos de vencimiento (pendings/739).
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Las órdenes.</returns>
    Task<List<OrderAggregate>> FindAllPaidWithPackagesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Las compras o ampliaciones de paquete de una copropiedad que esperan todavía su pago (pendings/739).
    /// </summary>
    /// <param name="tenantId">La copropiedad.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Las órdenes.</returns>
    Task<List<OrderAggregate>> FindAwaitingPackagePaymentAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Las órdenes pagadas y activas de todas las copropiedades, para el job de avisos de vencimiento (pendings/736).
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Las órdenes, en cualquier orden.</returns>
    Task<List<OrderAggregate>> FindAllPaidAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Las renovaciones y los cambios de licencia de una copropiedad que esperan todavía su pago.
    /// </summary>
    /// <param name="tenantId">La copropiedad.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Las órdenes.</returns>
    Task<List<OrderAggregate>> FindAwaitingSubscriptionPaymentAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Deja sin efecto una orden que otra reemplazó, solo si su pago sigue pendiente.
    /// </summary>
    /// <param name="order">La orden, ya marcada con <c>Supersede</c>.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns><c>true</c> si esta llamada la reemplazó; <c>false</c> si su pago ya tenía resultado.</returns>
    Task<bool> SupersedeAsync(OrderAggregate order, CancellationToken cancellationToken);

    /// <summary>
    /// Inactiva las órdenes cuyo periodo no prestado se acreditó al cambiar de licencia (pendings/738).
    /// </summary>
    /// <param name="orderIds">Las órdenes.</param>
    /// <param name="updatedBy">Quien pagó el cambio.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task DeactivateAsync(IReadOnlyCollection<Guid> orderIds, Guid updatedBy, CancellationToken cancellationToken);
}
