using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public record ProvisioningStep(string StepName, ProvisioningStepStatus Status, Instant Timestamp, string? Error = null);
