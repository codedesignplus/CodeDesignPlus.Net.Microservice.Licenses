using System;
using CodeDesignPlus.Net.ValueObjects.User;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

/// <summary>
/// Represents a buyer in the system.
/// </summary>
public class BuyerDto
{
    /// <summary>
    /// Gets the name of the buyer.
    /// </summary>
    public string Name { get; set; } = null!;
    /// <summary>
    /// Gets the phone number of the buyer.
    /// </summary>
    public string Phone { get; set; } = null!;
    /// <summary>
    /// Gets the email address of the buyer.
    /// </summary>
    public string Email { get; set; } = null!;
    /// <summary>
    /// Gets the type of document of the buyer.
    /// </summary>
    public TypeDocument TypeDocument { get; set; } = null!;
    /// <summary>
    /// Gets the document number of the buyer.
    /// </summary>
    public string Document { get; set; } = null!;
}
