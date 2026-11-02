namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Repositories;

/// <summary>
/// El repositorio de las constancias de los avisos de vencimiento (pendings/736, regla 23).
/// </summary>
public interface ISubscriptionNoticeRepository : IRepositoryBase
{
    /// <summary>
    /// Guarda la constancia si su identificador no existe todavía.
    /// </summary>
    /// <param name="notice">La constancia.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns><c>false</c> si ya existía: el aviso ya se emitió.</returns>
    Task<bool> CreateIfAbsentAsync(SubscriptionNoticeAggregate notice, CancellationToken cancellationToken);

    /// <summary>
    /// Las constancias guardadas cuyo evento no consta como publicado.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Las constancias.</returns>
    Task<List<SubscriptionNoticeAggregate>> FindUnpublishedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Marca la constancia como publicada.
    /// </summary>
    /// <param name="id">La constancia.</param>
    /// <param name="publishedAt">Cuándo se publicó.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task MarkPublishedAsync(Guid id, Instant publishedAt, CancellationToken cancellationToken);
}
