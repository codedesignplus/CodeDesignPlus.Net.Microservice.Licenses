namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Test;

/// <summary>
/// Cubre que la orden conserve la referencia del recibo de compra.
/// </summary>
/// <remarks>
/// El PDF del recibo ya se generaba y se subia a ms-filestorage, pero la referencia se perdia: solo
/// viajaba adjunta al correo, y la URL firmada se calculaba en una variable que nadie leia. La pantalla de
/// compra no tenia de donde sacarlo, asi que su boton "ver recibo" no aparecia nunca.
/// <para>
/// Se guarda la referencia y **no la URL firmada**: la firma caduca a los 7 dias, asi que una URL
/// persistida seria un enlace roto con fecha de caducidad. Quien lo muestre pide una firma nueva.
/// </para>
/// <para>
/// Se reutiliza <see cref="FileAttachment"/>, que es exactamente la misma referencia que el handler ya
/// construia para adjuntar el PDF al correo. Un value object nuevo para lo mismo habria sido un segundo
/// sitio donde equivocarse.
/// </para>
/// </remarks>
public class OrderReceiptTest
{
    private static readonly Guid UserId = OrdenDePrueba.UserId;
    private static readonly Guid FileId = Guid.Parse("11000000-0000-4000-8000-000000000006");
    private static readonly Guid OtherFileId = Guid.Parse("22000000-0000-4000-8000-000000000007");

    private static readonly string FileName = $"PurchaseReceipt-{FileId}.pdf";
    private const string Target = "licenses-pdf/dd000000-0000-4000-8000-000000000004";

    [Fact]
    public void ANewOrderHasNoReceipt()
    {
        var order = OrdenDePrueba.Nueva();

        Assert.Null(order.Receipt);
    }

    [Fact]
    public void AttachingTheReceiptKeepsTheStorageReference()
    {
        var order = OrdenDePrueba.Nueva();

        order.AttachReceipt(FileId, FileName, Target, UserId);

        Assert.NotNull(order.Receipt);
        Assert.Equal(FileId, order.Receipt!.Id);
        Assert.Equal(FileName, order.Receipt.Name);
        Assert.Equal(Target, order.Receipt.Target);
    }

    [Fact]
    public void TheReceiptDoesNotKeepASignedUrl()
    {
        // La firma que devuelve ms-filestorage caduca a los 7 dias. Persistirla dejaria un enlace roto con
        // fecha, asi que el value object no tiene donde guardarla ni debe tenerlo.
        var order = OrdenDePrueba.Nueva();

        order.AttachReceipt(FileId, FileName, Target, UserId);

        Assert.DoesNotContain(
            order.Receipt!.GetType().GetProperties(),
            property => property.Name.Contains("Url", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RegeneratingTheReceiptReplacesTheReference()
    {
        // El webhook de la pasarela reentrega y el recibo se vuelve a generar con otro fileId: la orden
        // tiene que apuntar al ultimo, no al primero, o el boton abriria un PDF huerfano.
        var order = OrdenDePrueba.Nueva();
        order.AttachReceipt(FileId, FileName, Target, UserId);

        order.AttachReceipt(OtherFileId, $"PurchaseReceipt-{OtherFileId}.pdf", Target, UserId);

        Assert.Equal(OtherFileId, order.Receipt!.Id);
    }

    [Fact]
    public void AttachingTheReceiptLeavesTheAuditTrail()
    {
        var order = OrdenDePrueba.Nueva();

        order.AttachReceipt(FileId, FileName, Target, UserId);

        Assert.Equal(UserId, order.UpdatedBy);
        Assert.NotNull(order.UpdatedAt);
    }
}
