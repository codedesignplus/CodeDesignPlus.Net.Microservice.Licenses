using System;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public record PaymentResponse(
    bool Success,
    PaymentStatus Status,
    string TransactionId,
    string Message,
    string? RedirectUrl,
    FinancialNetwork FinancialNetwork
);

public record FinancialNetwork(
    string PaymentNetworkResponseCode,
    string PaymentNetworkResponseErrorMessage,
    string TrazabilityCode,
    string AuthorizationCode,
    string ResponseCode
);

public enum PaymentStatus
{
    None = 0,
    Initiated = 1,
    Succeeded = 2,
    Failed = 3,
    Pending = 4
}