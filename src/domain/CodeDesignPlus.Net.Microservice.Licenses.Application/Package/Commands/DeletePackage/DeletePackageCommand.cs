namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Package.Commands.DeletePackage;

/// <summary>
/// Borra un paquete del catálogo: deja de venderse y de renovarse; lo ya comprado sigue hasta su fin (pendings/739).
/// </summary>
/// <param name="Id">El paquete.</param>
[DtoGenerator]
public record DeletePackageCommand(Guid Id) : IRequest;

/// <summary>
/// Valida el comando.
/// </summary>
public class Validator : AbstractValidator<DeletePackageCommand>
{
    /// <summary>
    /// Crea el validador.
    /// </summary>
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
