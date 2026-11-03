namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Package.Commands.DeletePackage;

/// <summary>
/// Borra un paquete del catálogo (pendings/739).
/// </summary>
public class DeletePackageCommandHandler(IPackageRepository repository, IUserContext user, IPubSub pubsub) : IRequestHandler<DeletePackageCommand>
{
    /// <inheritdoc/>
    public async Task Handle(DeletePackageCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var aggregate = await repository.FindAsync<PackageAggregate>(request.Id, cancellationToken);

        ApplicationGuard.IsNull(aggregate, Errors.PackageNotFound);

        aggregate!.Delete(user.IdUser);

        // Se guarda borrado y no se elimina: la renovación tiene que saber que ya no se vende.
        await repository.UpdateAsync(aggregate, cancellationToken);

        await pubsub.PublishAsync(aggregate.GetAndClearEvents(), cancellationToken);
    }
}
