using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Services;
using Ductus.FluentDocker.Services.Extensions;

namespace CodeDesignPlus.Net.Microservice.Licenses.Infrastructure.Test.Helpers;

/// <summary>
/// Starts a disposable Mongo for the repository tests and exposes its port.
/// </summary>
/// <remarks>
/// The container is declared here and not taken from the SDK: <c>CodeDesignPlus.Net.xUnit</c> is internal to the SDK and
/// must not be referenced from a microservice. Same shape as the fixture of ms-electronicinvoicing.
/// <para>
/// Docker assigns the host port (port 0 is exposed) so that two suites in parallel, or a local Mongo already listening
/// on 27017, do not collide.
/// </para>
/// <para>
/// <b>Requires Docker.</b> Without it these tests do not run.
/// </para>
/// </remarks>
public sealed class MongoContainerFixture : IDisposable
{
    /// <summary>
    /// The name of the collection of tests that share the container.
    /// </summary>
    public const string Collection = "Mongo Licenses Collection";

    private readonly IContainerService container;

    /// <summary>
    /// Host port where the Mongo of the tests answers.
    /// </summary>
    public int Port { get; }

    /// <summary>
    /// Starts the container.
    /// </summary>
    public MongoContainerFixture()
    {
        this.container = new Builder()
            .UseContainer()
            .UseImage("mongo:8.0")
            .ExposePort(0, 27017)
            // It waits for the log and not for the port: on Docker Desktop for Windows, WaitForPort probes the internal
            // IP of the container, unreachable from the host, and times out even when Mongo is ready.
            .WaitForMessageInLog("Waiting for connections", TimeSpan.FromSeconds(90))
            .Build()
            .Start();

        this.Port = this.container.ToHostExposedEndpoint("27017/tcp").Port;
    }

    /// <summary>
    /// The connection string of the container.
    /// </summary>
    public string ConnectionString => $"mongodb://localhost:{this.Port}";

    /// <inheritdoc/>
    public void Dispose()
    {
        this.container.Dispose();
    }
}

/// <summary>
/// Shares a single Mongo container among all the repository tests.
/// </summary>
[CollectionDefinition(MongoContainerFixture.Collection)]
public class MongoCollectionDefinition : ICollectionFixture<MongoContainerFixture>
{
}
