namespace CodeDesignPlus.Net.Microservice.Licenses.Infrastructure.Repositories;

public class OrderRepository(IServiceProvider serviceProvider, IOptions<MongoOptions> mongoOptions, ILogger<OrderRepository> logger) 
    : RepositoryBase(serviceProvider, mongoOptions, logger), IOrderRepository
{
   
}