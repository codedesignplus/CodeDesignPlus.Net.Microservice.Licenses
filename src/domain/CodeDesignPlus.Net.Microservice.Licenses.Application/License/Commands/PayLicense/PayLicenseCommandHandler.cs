using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.gRpc.Clients.Services.Payment;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.PayLicense;

public class PayLicenseCommandHandler(ILicenseRepository repository, IUserContext user, IPubSub pubsub, IMapper mapper, IPayment payment) : IRequestHandler<PayLicenseCommand>
{
    public async Task Handle(PayLicenseCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var exist = await repository.ExistsAsync<LicenseAggregate>(request.Id, cancellationToken);

        ApplicationGuard.IsFalse(exist, Errors.LicenseNotFound);

        var aggregate = await repository.FindAsync<LicenseAggregate>(request.Id, cancellationToken);

        var payRequest = mapper.Map<PayRequest>(request);

        // var amount = aggregate.Prices
        //     .Where(x => x.BillingType == request.PaymentMethod.BillingType)
        //     .Select(x => x.Pricing)
        //     .FirstOrDefault();

        // payRequest.Transaction.Order.Ammount = new Amount
        // {
        //     Currency = ag
        // };

        await payment.PayAsync(payRequest, cancellationToken);

        var paymentResponse = await payment.GetPayByIdAsync(new GetPaymentRequest { Id = request.Order.Id.ToString() }, cancellationToken);

        var license = PaymentAggregate.Create(request.Order.Id, request.Id, request.PaymentMethod, request.Order.Buyer, request.Organization, user.Tenant, true, "Error", true, user.IdUser );

        await repository.CreateAsync(license, cancellationToken);

        await pubsub.PublishAsync(license.GetAndClearEvents(), cancellationToken);
    }
}