using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace CodeDesignPlus.Net.Microservice.Licenses.Infrastructure.Test.Repositories;

/// <summary>
/// La conciliación recoge también la orden que nunca se actualizó (pendings/738, orden <c>65b77cd4</c>): su
/// <c>UpdatedAt</c> es nulo y Mongo no lo incluye en <c>$lt</c>.
/// </summary>
public class StuckOrdersPipelineTest
{
    private static readonly Instant Cutoff = Instant.FromUtc(2026, 11, 3, 12, 0);

    private static List<BsonDocument> Render()
    {
        CodeDesignPlus.Net.Mongo.Extensions.MongoSerializerRegistration.RegisterSerializers();

        var serializer = BsonSerializer.SerializerRegistry.GetSerializer<OrderAggregate>();
        var pipeline = OrderRepository.StuckOrdersPipeline(ProvisioningStatus.PaymentPending, Cutoff, 20);

        return [.. pipeline.Render(new RenderArgs<OrderAggregate>(serializer, BsonSerializer.SerializerRegistry)).Documents];
    }

    [Fact]
    public void StuckOrdersPipeline_OrderNeverUpdated_MatchesByCreatedAt()
    {
        // Arrange
        var match = Render()[0]["$match"].AsBsonDocument;

        // Act
        var branches = match["$or"].AsBsonArray.Select(x => x.AsBsonDocument).ToList();
        var neverUpdated = branches.Single(x => x.Contains("CreatedAt"));

        // Assert
        Assert.Equal((BsonNull.Value, branches.Single(x => !x.Contains("CreatedAt"))["UpdatedAt"]["$lt"]), (neverUpdated["UpdatedAt"], neverUpdated["CreatedAt"]["$lt"]));
    }

    [Fact]
    public void StuckOrdersPipeline_Sort_UsesUpdatedAtOrCreatedAtAndDropsTheHelperField()
    {
        // Arrange
        var stages = Render();

        // Act
        var effective = stages.Single(x => x.Contains("$addFields"))["$addFields"][OrderRepository.EffectiveUpdatedAtField];
        var sort = stages.Single(x => x.Contains("$sort"))["$sort"].AsBsonDocument;
        var last = stages[^1];

        // Assert
        Assert.Equal(
            (new BsonDocument("$ifNull", new BsonArray { "$UpdatedAt", "$CreatedAt" }), new BsonDocument(OrderRepository.EffectiveUpdatedAtField, 1), new BsonDocument("$project", new BsonDocument(OrderRepository.EffectiveUpdatedAtField, 0))),
            (effective.AsBsonDocument, sort, last));
    }
}
