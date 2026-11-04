using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Services;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.DeleteLicense;

public class DeleteLicenseCommandHandler(ILicenseRepository repository, IOrderRepository orders, IUserContext user, IPubSub pubsub, ICacheManager cacheManager) : IRequestHandler<DeleteLicenseCommand>
{
    public async Task Handle(DeleteLicenseCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var aggregate = await repository.FindAsync<LicenseAggregate>(request.Id, cancellationToken);

        ApplicationGuard.IsNull(aggregate, Errors.LicenseNotFound);

        // Ni con la licencia inactiva se borra la de quien la tomó: borrada ya no se renueva (pendings/464).
        await LicenseOffers.EnsureCanWithdrawAsync(aggregate!.Id, aggregate.OfferedTo ?? [], orders, cancellationToken);

        aggregate.Delete(user.IdUser);

        await repository.DeleteAsync<LicenseAggregate>(aggregate.Id, cancellationToken);

        await pubsub.PublishAsync(aggregate.GetAndClearEvents(), cancellationToken);

        // Sin esto, GET /License/{id} seguia sirviendo la licencia borrada desde la cache (plan 057 de pendings/).
        await cacheManager.RemoveAsync(aggregate.Id.ToString());
    }
}