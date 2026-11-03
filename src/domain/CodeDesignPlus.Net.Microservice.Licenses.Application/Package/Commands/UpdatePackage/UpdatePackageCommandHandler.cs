using CodeDesignPlus.Net.gRpc.Clients.Abstractions;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Package.Commands.UpdatePackage;

/// <summary>
/// Cambia un paquete del catálogo (pendings/739).
/// </summary>
public class UpdatePackageCommandHandler(IPackageRepository repository, IUserContext user, IPubSub pubsub, ICurrencyGrpc currencyGrpc) : IRequestHandler<UpdatePackageCommand>
{
    /// <inheritdoc/>
    public async Task Handle(UpdatePackageCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var package = await repository.FindAsync<PackageAggregate>(request.Id, cancellationToken);

        ApplicationGuard.IsTrue(package is null || package.IsDeleted, Errors.PackageNotFound);

        var definition = await PackageDefinitions.BuildAsync(currencyGrpc, request.Name, request.ShortDescription, request.Description, request.Icon, request.IsActive, request.IsPopular, request.ShowInLandingPage, request.OfferedTo, request.Kind, request.Currency, request.BasePrice, request.MinimumPrice, request.TaxTreatment, request.TaxPercentage, request.TaxLegalBasis, request.InvoiceConcept, request.Attributes, request.Components, cancellationToken);

        package!.Update(definition, user.IdUser);

        await repository.UpdateAsync(package, cancellationToken);

        await pubsub.PublishAsync(package.GetAndClearEvents(), cancellationToken);
    }
}
