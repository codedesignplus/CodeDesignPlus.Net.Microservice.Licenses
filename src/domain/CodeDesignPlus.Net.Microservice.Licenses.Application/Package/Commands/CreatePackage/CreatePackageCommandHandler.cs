using CodeDesignPlus.Net.gRpc.Clients.Abstractions;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Package.Commands.CreatePackage;

/// <summary>
/// Crea un paquete del catálogo (pendings/739).
/// </summary>
public class CreatePackageCommandHandler(IPackageRepository repository, IUserContext user, IPubSub pubsub, ICurrencyGrpc currencyGrpc) : IRequestHandler<CreatePackageCommand>
{
    /// <inheritdoc/>
    public async Task Handle(CreatePackageCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        // Ya guardado por un envío anterior del mismo formulario: el reintento termina bien sin repetir nada (regla 66).
        if (await repository.ExistsAsync<PackageAggregate>(request.Id, cancellationToken))
        {
            return;
        }

        var definition = await PackageDefinitions.BuildAsync(currencyGrpc, request.Name, request.ShortDescription, request.Description, request.Icon, request.IsActive, request.IsPopular, request.ShowInLandingPage, request.OfferedTo, request.Kind, request.Currency, request.BasePrice, request.MinimumPrice, request.TaxTreatment, request.TaxPercentage, request.TaxLegalBasis, request.InvoiceConcept, request.Attributes, request.Components, cancellationToken);

        var package = PackageAggregate.Create(request.Id, definition, user.IdUser);

        if (!await repository.CreateIfAbsentAsync(package, cancellationToken))
        {
            return;
        }

        await pubsub.PublishAsync(package.GetAndClearEvents(), cancellationToken);
    }
}
