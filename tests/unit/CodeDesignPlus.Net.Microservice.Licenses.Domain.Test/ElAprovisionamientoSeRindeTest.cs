using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.ValueObjects.Payment;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Test;

/// <summary>
/// Cubre que el aprovisionamiento de un pedido deje de reintentarse alguna vez.
/// </summary>
/// <remarks>
/// <c>OrderReconciliationJob</c> republica el evento de aprovisionamiento de un pedido atascado cada cinco
/// minutos. Ese reintento es la red que salva los fallos transitorios, pero <b>no tenia tope</b>: un fallo
/// permanente —un dato invalido, una colision de nombre, una cuota agotada— se reintentaba indefinidamente
/// sobre un pedido <b>ya cobrado</b>, y nadie se enteraba.
/// <para>
/// Rendirse importa por donde deja el pedido: el job solo recoge <c>PaymentPending</c> e <c>InProgress</c>,
/// asi que <c>PartiallyFailed</c> es lo que lo saca del bucle y lo deja visible en
/// <c>/management/system/orders</c>. Pendiente 139.
/// </para>
/// </remarks>
public class ElAprovisionamientoSeRindeTest
{
    private static OrderAggregate EnCurso()
    {
        var orden = OrdenDePrueba.Nueva();

        orden.SetPaymentStatus(PaymentStatus.Succeeded, OrdenDePrueba.BuyerId);
        orden.GetAndClearEvents();

        return orden;
    }

    [Fact]
    public void UnPedidoNuevoNoHaGastadoIntentos()
    {
        var orden = OrdenDePrueba.Nueva();

        Assert.Equal(0, orden.ProvisioningAttempts);
        Assert.False(orden.HasExhaustedProvisioningAttempts);
    }

    [Fact]
    public void CadaReintentoCuenta()
    {
        var orden = EnCurso();

        Assert.Equal(1, orden.RegisterProvisioningAttempt());
        Assert.Equal(2, orden.RegisterProvisioningAttempt());
        Assert.Equal(2, orden.ProvisioningAttempts);
    }

    [Fact]
    public void AlQuintoIntentoSeAgotan()
    {
        // La cifra de control: cinco, no cuatro ni seis.
        var orden = EnCurso();

        for (var i = 0; i < OrderAggregate.MaxProvisioningAttempts - 1; i++)
        {
            orden.RegisterProvisioningAttempt();
            Assert.False(orden.HasExhaustedProvisioningAttempts);
        }

        orden.RegisterProvisioningAttempt();

        Assert.True(orden.HasExhaustedProvisioningAttempts);
        Assert.Equal(5, OrderAggregate.MaxProvisioningAttempts);
    }

    [Fact]
    public void RendirseSacaAlPedidoDelBucle()
    {
        // El job solo recoge PaymentPending e InProgress. Mientras siga en InProgress, vuelve cada ciclo.
        var orden = EnCurso();

        Assert.Equal(ProvisioningStatus.InProgress, orden.ProvisioningStatus);

        orden.FailProvisioningStep("Provisioning", "no se pudo", OrdenDePrueba.BuyerId);

        Assert.Equal(ProvisioningStatus.PartiallyFailed, orden.ProvisioningStatus);
    }

    [Fact]
    public void RendirseDejaEscritoElMotivo()
    {
        // Sin el motivo en el historial, quien abra el pedido en la pantalla ve un estado y ninguna pista.
        var orden = EnCurso();

        orden.FailProvisioningStep("Provisioning", "Pasos pendientes: UserProvisioning.", OrdenDePrueba.BuyerId);

        var paso = Assert.Single(orden.ProvisioningHistory, x => x.Status == ProvisioningStepStatus.Failed);

        Assert.Equal("Provisioning", paso.StepName);
        Assert.Contains("UserProvisioning", paso.Error);
    }

    [Fact]
    public void RendirseAvisaAlMundo()
    {
        var orden = EnCurso();

        orden.FailProvisioningStep("Provisioning", "no se pudo", OrdenDePrueba.BuyerId);

        Assert.Single(orden.GetAndClearEvents().OfType<OrderProvisioningFailedDomainEvent>());
    }

    [Fact]
    public void ElContadorNoEstorbaAlCaminoFeliz()
    {
        // Gastar intentos no puede impedir que el pedido se complete si los pasos acaban llegando.
        var orden = EnCurso();

        orden.RegisterProvisioningAttempt();
        orden.RegisterProvisioningAttempt();

        orden.CompleteProvisioningStep("TenantProvisioning", OrdenDePrueba.UserId);
        orden.CompleteProvisioningStep("UserProvisioning", OrdenDePrueba.UserId);

        Assert.Equal(ProvisioningStatus.Completed, orden.ProvisioningStatus);
    }
}
