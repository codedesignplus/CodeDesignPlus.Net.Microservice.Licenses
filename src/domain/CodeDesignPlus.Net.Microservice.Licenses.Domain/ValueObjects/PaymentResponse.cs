using System;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public record PaymentResponse(Guid Id, string Provider, Response Reponse);

public record Response(string Code, string? Error, ResponseDetails Details);

public record ResponseDetails(
    string OrderId,
    string TransactionId,
    string State,
    string ResponseCode,
    string PaymentNetworkResponseCode,
    string PaymentNetworkResponseErrorMessage,
    string TrazabilityCode,
    string AuthorizationCode,
    string ResponseMessage,
    Dictionary<string, string> ExtraParameters,
    Dictionary<string, string> AdditionalInfo
);