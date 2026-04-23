using System;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

/// <summary>
/// Represents a license in the order context and this model is used from request.
/// </summary>
public class LicenseDto
{
    /// <summary>
    /// Gets the original unique identifier of the license in the catalog.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets the billing type (e.g., Monthly, Yearly).
    /// </summary>
    public BillingType BillingType { get; set; }
}
