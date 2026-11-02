using CodeDesignPlus.Net.Core.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Options;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Setup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application
{
    public class Startup : IStartup
    {
        public void Initialize(IServiceCollection services, IConfiguration configuration)
        {
            MapsterConfigLicense.Configure();

            // The page the payment provider returns the buyer to (pendings/660): a frontend URL per environment.
            services.AddSingleton<IValidateOptions<OnlinePaymentOptions>, OnlinePaymentOptionsValidator>();

            services.AddOptions<OnlinePaymentOptions>()
                .Bind(configuration.GetSection(OnlinePaymentOptions.Section))
                .ValidateOnStart();

            // La política de vencimiento de las suscripciones (pendings/735, 736, 737).
            services.AddSingleton<IValidateOptions<SubscriptionOptions>, SubscriptionOptionsValidator>();

            services.AddOptions<SubscriptionOptions>()
                .Bind(configuration.GetSection(SubscriptionOptions.Section))
                .ValidateOnStart();

            services.AddScoped<Order.Services.ISubscriptionCheckout, Order.Services.SubscriptionCheckout>();
            services.AddScoped<Order.Services.ISubscriptionSettlement, Order.Services.SubscriptionSettlement>();
        }
    }
}
