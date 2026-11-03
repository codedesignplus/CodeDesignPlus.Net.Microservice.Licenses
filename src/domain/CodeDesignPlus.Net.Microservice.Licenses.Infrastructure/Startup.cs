namespace CodeDesignPlus.Net.Microservice.Licenses.Infrastructure
{
    public class Startup : IStartup
    {
        public void Initialize(IServiceCollection services, IConfiguration configuration)
        {
            // Los paquetes del país del despliegue (pendings/739). Solo siembra donde se configura Seed:Country: el worker.
            services.AddHostedService<Seeds.PackageSeedService>();
        }
    }
}
