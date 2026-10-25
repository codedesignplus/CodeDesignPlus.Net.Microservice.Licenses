using CodeDesignPlus.Net.Security.Abstractions;
using Moq;
using System.Runtime.CompilerServices;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Retry;

/// <summary>
/// Arma un manejador de alta con dobles para todo lo que no mira la prueba de reintento (pendings/589, regla 66).
/// </summary>
/// <remarks>
/// El reintento tiene que terminar en la comprobación del id, antes de leer nada más: los dobles sin preparar devuelven
/// nulos, así que un manejador que siguiera de largo fallaría o guardaría, y la prueba lo vería.
/// </remarks>
internal static class RetryHarness
{
    public static readonly Guid Tenant = Guid.Parse("58958958-9589-4589-8589-589589589589");
    public static readonly Guid UserId = Guid.Parse("742dcc0c-a3d8-4840-85c5-e1d9eb2307f3");
    public static readonly Guid Id = Guid.Parse("c589c589-c589-4589-8589-c589c589c589");

    /// <summary>
    /// El manejador con los dobles que se le pasan y uno vacío para cada dependencia restante.
    /// </summary>
    public static T Build<T>(params object[] doubles) where T : class
    {
        var constructor = typeof(T).GetConstructors().Single();

        var arguments = constructor.GetParameters()
            .Select(parameter => doubles.FirstOrDefault(parameter.ParameterType.IsInstanceOfType) ?? Empty(parameter.ParameterType))
            .ToArray();

        return (T)constructor.Invoke(arguments);
    }

    /// <summary>
    /// Un comando que solo lleva su id: el manejador tiene que pararse antes de leer cualquier otra cosa.
    /// </summary>
    public static T WithId<T>(string property = "Id") where T : class
    {
        var command = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        var target = typeof(T).GetProperty(property)!;

        // Una propiedad de solo lectura (`public Guid Id { get; } = id;`) se escribe por su campo.
        if (target.CanWrite)
            target.SetValue(command, Id);
        else
            typeof(T).GetField($"<{property}>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(command, Id);

        return command;
    }

    /// <summary>
    /// Quien envía el formulario, en la copropiedad de la prueba.
    /// </summary>
    public static IUserContext User()
    {
        var user = new Mock<IUserContext>();
        user.SetupGet(x => x.Tenant).Returns(Tenant);
        user.SetupGet(x => x.IdUser).Returns(UserId);

        return user.Object;
    }

    private static object? Empty(Type type)
    {
        if (type.IsValueType)
            return Activator.CreateInstance(type);

        if (type.IsSealed)
            return null;

        var mock = (Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(type))!;

        return mock.Object;
    }
}
