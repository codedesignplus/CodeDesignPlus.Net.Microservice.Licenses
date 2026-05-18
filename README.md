# 📜 Licenses Microservice

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=.net)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-LGPL%20v3-blue.svg)](LICENSE.md)
[![Tests](https://img.shields.io/badge/tests-passing-success)](tests/)
[![Coverage](https://img.shields.io/badge/coverage-80%25-green)]()
[![Docker](https://img.shields.io/badge/docker-ready-2496ED?logo=docker)](Dockerfile)

A production-ready microservice for managing software licenses, pricing tiers, and subscription plans built with .NET 9. Implements Clean Architecture, DDD, and CQRS patterns for flexible license and module management.

---

## What is this microservice?

The Licenses microservice manages the subscription plans that organizations purchase to use the platform. It defines what each plan includes (which modules are available, pricing per currency, billing period) and drives the commercial funnel from plan selection through payment to automatic tenant provisioning. Potential customers interact with it indirectly when browsing available plans on the landing page, and the system uses it during the purchase flow to determine what features the new organization will have access to. It connects directly with the Payments microservice (to process purchases) and the Tenants microservice (to provision new organizations after payment).

---

## 📋 Table of Contents

- [Overview](#-overview)
- [Key Features](#-key-features)
- [Technology Stack](#️-technology-stack)
- [Prerequisites](#️-prerequisites)
- [Getting Started](#-getting-started)
- [API Endpoints](#-api-endpoints)
- [License Model](#-license-model)
- [Configuration](#️-configuration)
- [Use Cases & Scenarios](#-use-cases--scenarios)
- [Architecture](#️-architecture)
- [Testing](#-testing)
- [Best Practices](#-best-practices)
- [Troubleshooting](#-troubleshooting)
- [Pricing Strategies](#-pricing-strategies)
- [Module Management](#-module-management)
- [FAQ](#-faq)
- [Contributing](#-contributing)
- [License](#-license)

---

## 🎯 Overview

The Licenses microservice provides a centralized system for managing software licenses, subscription tiers, and pricing models. It's designed for SaaS platforms, multi-tenant applications, and products requiring flexible licensing.

- **License Management**: Create and manage subscription tiers (Free, Pro, Enterprise)
- **Module System**: Define feature modules and control access per license
- **Pricing Flexibility**: Support multiple currencies, billing models, and pricing strategies
- **Multi-Tenancy**: Isolate licenses by organization or tenant
- **Landing Page Integration**: Flag licenses for public display
- **Custom Attributes**: Extensible metadata for license limits and features
- **Icon Support**: Visual branding for each license tier

### 🚀 Quick Start

```bash
# 1. Start infrastructure services
git clone https://github.com/codedesignplus/CodeDesignPlus.Environment.Dev
cd CodeDesignPlus.Environment.Dev/resources
docker-compose up -d

# 2. Configure Vault secrets
cd ../../tools/vault
./config-vault.sh

# 3. Run the microservice
dotnet run --project src/entrypoints/CodeDesignPlus.Net.Microservice.Licenses.Rest

# 4. Access Swagger UI
open http://localhost:5000/swagger
```

### 📊 High-Level Architecture

```
┌─────────────┐
│   Client    │
│ Application │
└──────┬──────┘
       │ HTTPS + JWT
       │
┌──────▼──────────────────────────────────────────────┐
│         Licenses Microservice (REST API)            │
│  ┌──────────────┐  ┌─────────────┐  ┌────────────┐ │
│  │ Controllers  │  │  MediatR    │  │  Handlers  │ │
│  │   (API)      │─▶│   (CQRS)    │─▶│ (Business) │ │
│  └──────────────┘  └─────────────┘  └────────────┘ │
└───────┬──────────────────┬──────────────────┬───────┘
        │                  │                  │
   ┌────▼────┐      ┌──────▼──────┐    ┌─────▼─────┐
   │ MongoDB │      │   Redis     │    │ RabbitMQ  │
   │(License │      │  (Cache)    │    │ (Events)  │
   │  Data)  │      │             │    │           │
   └─────────┘      └─────────────┘    └───────────┘
```

## 🚀 Key Features

### Core Capabilities

- ✅ **License Tiers**: Create Free, Basic, Pro, Enterprise, and custom tiers
- ✅ **Module Management**: Add/remove feature modules per license
- ✅ **Flexible Pricing**: Support multiple pricing strategies per license
- ✅ **Multi-Currency**: Handle USD, EUR, COP, and custom currencies
- ✅ **Billing Models**: One-time, recurring, metered, tiered, volume-based
- ✅ **Custom Attributes**: Key-value metadata for limits (MaxUsers, MaxProjects, etc.)
- ✅ **Visual Branding**: Icon and color customization per tier
- ✅ **Landing Page Control**: Flag licenses for public visibility
- ✅ **Popular Marking**: Highlight recommended tiers
- ✅ **Terms of Service**: Per-license legal agreements
- ✅ **Domain Events**: Publish lifecycle events to RabbitMQ

### Technical Features

- Clean Architecture with DDD and CQRS
- Domain events for state changes
- MongoDB for persistence
- RabbitMQ for event publishing
- Redis for distributed caching
- OAuth2/OpenID Connect security
- Multi-tenancy support
- Swagger/OpenAPI documentation
- Docker containerization
- Comprehensive test coverage (Unit, Integration)

## 🛠️ Technology Stack

### Core
- **.NET 9** - Runtime and framework
- **ASP.NET Core** - Web API framework
- **C# 13** - Programming language

### Storage & Data
- **MongoDB** - License persistence and queries
- **Redis** - Distributed caching

### Messaging & Events
- **RabbitMQ** - Event publishing

### Architecture & Patterns
- **MediatR** - CQRS command/query handling
- **FluentValidation** - Input validation
- **Mapster** - Object mapping
- **NodaTime** - Date/time handling

### Security & Configuration
- **Vault** - Secret management
- **OAuth2/OpenID Connect** - Authentication
- **JWT Bearer** - Token-based security

### DevOps & Testing
- **Docker** - Containerization
- **xUnit** - Unit/integration testing
- **Swagger/OpenAPI** - API documentation

## ⚙️ Prerequisites

### Required
- **.NET 9 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Docker & Docker Compose** - For infrastructure services
- **MongoDB 6.0+** - Document database
- **Redis 7.0+** - Caching layer
- **RabbitMQ 3.12+** - Message broker

### Optional
- **Vault** - Secret management (can use appsettings for local dev)

## 🚀 Getting Started

1. Clone the repository:
```bash
git clone <repository-url>
cd CodeDesignPlus.Net.Microservice.Licenses
```

2. Run the MongoDB, Redis, and RabbitMQ services:
```bash
git clone https://github.com/codedesignplus/CodeDesignPlus.Environment.Dev
cd CodeDesignPlus.Environment.Dev/resources
docker-compose up -d
```

3. Configure Vault:
```bash
cd tools/vault
./config-vault.sh
```

4. Build the solution:
```bash
dotnet build
```

5. Run the REST API:
```bash
dotnet run --project src/entrypoints/CodeDesignPlus.Net.Microservice.Licenses.Rest
```

6. Access Swagger UI at `http://localhost:5000/swagger`

## 📡 API Endpoints

### License Operations

#### Create License
```http
POST /api/license
Content-Type: application/json
Authorization: Bearer {token}
X-Tenant: {tenant-id}

{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Pro Plan",
  "shortDescription": "For growing teams",
  "description": "Perfect for teams that need advanced features and integrations.",
  "modules": [
    {
      "id": "mod-analytics",
      "name": "Advanced Analytics",
      "description": "Real-time analytics and reporting",
      "isEnabled": true
    },
    {
      "id": "mod-api",
      "name": "API Access",
      "description": "Full REST API access",
      "isEnabled": true
    }
  ],
  "prices": [
    {
      "basePrice": {
        "amount": 49.99,
        "currency": "USD"
      },
      "billingModel": "Recurring",
      "billingType": "Monthly",
      "discountPercentage": 0
    },
    {
      "basePrice": {
        "amount": 499.99,
        "currency": "USD"
      },
      "billingModel": "Recurring",
      "billingType": "Annually",
      "discountPercentage": 16.67
    }
  ],
  "icon": {
    "name": "rocket",
    "color": "#4F46E5"
  },
  "termsOfService": "https://example.com/terms/pro",
  "attributes": {
    "MaxUsers": "25",
    "MaxProjects": "100",
    "MaxStorage": "500GB",
    "SupportLevel": "Priority"
  },
  "isActive": true,
  "isPopular": true,
  "showInLandingPage": true
}
```

**Response**: `200 OK` with created license

#### Get License by ID
```http
GET /api/license/{id}
Authorization: Bearer {token}
X-Tenant: {tenant-id}
```

**Response**: `200 OK` with license details
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Pro Plan",
  "shortDescription": "For growing teams",
  "description": "Perfect for teams that need advanced features...",
  "modules": [
    {
      "id": "mod-analytics",
      "name": "Advanced Analytics",
      "isEnabled": true
    }
  ],
  "prices": [
    {
      "basePrice": {
        "amount": 49.99,
        "currency": "USD"
      },
      "billingModel": "Recurring",
      "billingType": "Monthly"
    }
  ],
  "icon": {
    "name": "rocket",
    "color": "#4F46E5"
  },
  "attributes": {
    "MaxUsers": "25",
    "MaxProjects": "100"
  },
  "isPopular": true,
  "showInLandingPage": true,
  "createdAt": "2026-05-11T10:00:00Z"
}
```

#### List Licenses (Paginated)
```http
GET /api/license?limit=50&skip=0&filter=isActive eq true&orderby=name asc
Authorization: Bearer {token}
X-Tenant: {tenant-id}
```

**Query Parameters**:
- `limit` (optional): Items per page (default: 100)
- `skip` (optional): Items to skip (default: 0)
- `filter` (optional): OData filter expression
- `orderby` (optional): OData order expression

**Response**: `200 OK` with paginated results
```json
{
  "data": [...],
  "totalCount": 5,
  "limit": 50,
  "skip": 0
}
```

#### Update License
```http
PUT /api/license/{id}
Content-Type: application/json
Authorization: Bearer {token}
X-Tenant: {tenant-id}

{
  "name": "Pro Plan - Updated",
  "shortDescription": "New description",
  // ... all required fields
}
```

**Response**: `200 OK` or `204 No Content`

#### Add Module to License
```http
POST /api/license/{id}/module
Content-Type: application/json
Authorization: Bearer {token}
X-Tenant: {tenant-id}

{
  "moduleId": "mod-integrations",
  "name": "Third-party Integrations",
  "description": "Connect with 100+ services",
  "isEnabled": true
}
```

**Response**: `200 OK`

#### Remove Module from License
```http
DELETE /api/license/{id}/module/{moduleId}
Authorization: Bearer {token}
X-Tenant: {tenant-id}
```

**Response**: `204 No Content`

### Landing Page Endpoint

#### Get Public Licenses
```http
GET /api/license/landing
```

**Response**: `200 OK` with public licenses (no auth required)
```json
{
  "data": [
    {
      "id": "...",
      "name": "Free",
      "isPopular": false
    },
    {
      "id": "...",
      "name": "Pro",
      "isPopular": true  // Highlighted
    },
    {
      "id": "...",
      "name": "Enterprise",
      "isPopular": false
    }
  ]
}
```

### Error Responses

All errors follow RFC 7807 Problem Details:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "License not found.",
  "extensions": {
    "layer": "Application",
    "error_code": "LIC-404",
    "traceId": "0HMVJ3K7S5Q2K:00000001"
  }
}
```

**Common Status Codes**:
- `200 OK` - Success
- `201 Created` - License created
- `204 No Content` - Update successful
- `400 Bad Request` - Invalid input
- `401 Unauthorized` - Missing/invalid token
- `404 Not Found` - License not found
- `500 Internal Server Error` - Server error

## 📜 License Model

### License Structure

```csharp
public class LicenseAggregate
{
    public Guid Id { get; }
    public string Name { get; }                      // "Pro Plan"
    public string Description { get; }               // Full description
    public string ShortDescription { get; }          // Brief summary
    public bool IsPopular { get; }                   // Highlight flag
    public List<ModuleEntity> Modules { get; }       // Feature modules
    public List<Price> Prices { get; }               // Pricing strategies
    public Dictionary<string,string> Attributes { get; } // Custom metadata
    public Icon Icon { get; }                        // Visual branding
    public string TermsOfService { get; }            // Legal agreement URL
    public bool ShowInLandingPage { get; }           // Public visibility
}
```

### Module Structure

```csharp
public class ModuleEntity
{
    public string Id { get; }            // "mod-analytics"
    public string Name { get; }          // "Advanced Analytics"
    public string Description { get; }   // Feature description
    public bool IsEnabled { get; }       // Enabled for this license
}
```

### Price Structure

```csharp
public class Price
{
    public Money BasePrice { get; }      // Amount + Currency
    public BillingModel BillingModel { get; }  // OneTime, Recurring, Metered
    public BillingType BillingType { get; }    // Monthly, Annually, PerUse
    public decimal DiscountPercentage { get; } // Optional discount
}
```

### Icon Structure

```csharp
public class Icon
{
    public string Name { get; }    // Icon name (e.g., "rocket", "star")
    public string Color { get; }   // Hex color code (e.g., "#4F46E5")
}
```

## ⚙️ Configuration

### Database Configuration

```json
{
  "Mongo": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "licenses"
  }
}
```

### Security Configuration

```json
{
  "Security": {
    "Authority": "https://your-identity-server.com",
    "Audience": "licenses-api",
    "RequireHttpsMetadata": true
  }
}
```

### Multi-tenancy

Each request must include a tenant header:

```http
X-Tenant: 9588813a-7bc0-4be4-a169-293061881cc3
```

Licenses are isolated by tenant in MongoDB.

### Environment Variables

```bash
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:5000
MONGO_CONNECTION_STRING=mongodb://mongo:27017
REDIS_CONNECTION_STRING=redis:6379
RABBITMQ_HOST=rabbitmq
VAULT_ADDRESS=http://vault:8200
VAULT_TOKEN=your-vault-token
```

## 🎯 Use Cases & Scenarios

### 1. SaaS Pricing Page
Display license tiers on public pricing page:

```bash
# Fetch public licenses
GET /api/license/landing

Response:
- Free (MaxUsers: 3, MaxProjects: 5)
- Pro (Popular) (MaxUsers: 25, MaxProjects: 100)
- Enterprise (Unlimited)

# User selects "Pro" → Redirect to checkout
# Checkout service references license ID
```

### 2. Feature Flag System
Control feature access based on license:

```bash
# Check if user's license includes module
GET /api/license/{userLicenseId}

if (license.Modules.Contains("mod-analytics"))
{
    // Enable analytics dashboard
}
else
{
    // Show upgrade prompt
}
```

### 3. Dynamic Pricing Calculator
Calculate price based on billing cycle:

```bash
# Get license prices
GET /api/license/{id}

prices:
- Monthly: $49.99
- Annually: $499.99 (save 16.67%)

# Display savings calculation
annualSavings = (monthly * 12) - annual
```

### 4. Marketplace License Management
Multi-tenant SaaS marketplace:

```bash
# Vendor A creates custom license
POST /api/license
Headers: X-Tenant: vendor-a-id
- Name: "Vendor A - Pro"
- Modules: [custom-module-1, custom-module-2]

# Vendor B creates separate license
POST /api/license
Headers: X-Tenant: vendor-b-id
- Name: "Vendor B - Enterprise"

# Licenses are completely isolated
```

### 5. Module-Based Upgrades
Add modules to existing license:

```bash
# License starts with basic modules
Modules: [mod-core, mod-support]

# Customer purchases API access
POST /api/license/{id}/module
- moduleId: "mod-api"
- name: "API Access"

# Updated license
Modules: [mod-core, mod-support, mod-api]

# System grants API access immediately
Event: LicenseModuleAddedDomainEvent
```

## 🏗️ Architecture

### Clean Architecture Layers

```
src/
├── domain/
│   ├── Domain/
│   │   ├── LicenseAggregate.cs
│   │   ├── Entities/
│   │   │   └── ModuleEntity.cs
│   │   ├── ValueObjects/
│   │   │   ├── Price.cs
│   │   │   └── Icon.cs
│   │   ├── Enums/
│   │   │   ├── BillingModel.cs
│   │   │   └── BillingType.cs
│   │   ├── DomainEvents/
│   │   │   ├── LicenseCreatedDomainEvent.cs
│   │   │   ├── LicenseUpdatedDomainEvent.cs
│   │   │   └── LicenseModuleAddedDomainEvent.cs
│   │   └── Repositories/
│   │       └── ILicenseRepository.cs
│   ├── Application/
│   │   ├── Commands/
│   │   │   ├── CreateLicense/
│   │   │   ├── UpdateLicense/
│   │   │   ├── AddModule/
│   │   │   └── RemoveModule/
│   │   ├── Queries/
│   │   │   ├── GetLicenseById/
│   │   │   └── GetAllLicenses/
│   │   └── DTOs/
│   │       └── LicenseDto.cs
│   └── Infrastructure/
│       └── Repositories/
│           └── LicenseRepository.cs (MongoDB)
└── entrypoints/
    └── Rest/
        ├── Controllers/
        │   └── LicenseController.cs
        └── Program.cs
```

### CQRS Pattern

**Commands** (Write operations):
- `CreateLicenseCommand` - Create new license
- `UpdateLicenseCommand` - Update license details
- `AddModuleCommand` - Add module to license
- `RemoveModuleCommand` - Remove module from license

**Queries** (Read operations):
- `GetLicenseByIdQuery` - Get single license
- `GetAllLicensesQuery` - List with pagination

### Domain Events

- `LicenseCreatedDomainEvent` - New license created
- `LicenseUpdatedDomainEvent` - License updated
- `LicenseModuleAddedDomainEvent` - Module added
- `LicenseModuleRemovedDomainEvent` - Module removed

### Data Flow

```
Client Request
     ↓
[Controller] → Validates request
     ↓
[MediatR] → Dispatches Command/Query
     ↓
[Handler] → Business logic
     ↓
[Repository] → MongoDB persistence
     ↓
[IPubSub] → Publishes events to RabbitMQ
     ↓
Response to Client
```

## 🧪 Testing

### Unit & Integration Tests
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/unit/CodeDesignPlus.Net.Microservice.Licenses.Domain.Test

# Run with coverage
dotnet test /p:CollectCoverage=true
```

## 💡 Best Practices

### License Design

#### ✅ DO: Use clear, descriptive names
```csharp
// Good
Name: "Professional Plan"
Name: "Enterprise with Advanced Support"

// Bad
Name: "Plan A"
Name: "Tier 2"
```

#### ✅ DO: Define attribute limits clearly
```csharp
Attributes: {
    "MaxUsers": "25",
    "MaxProjects": "100",
    "MaxStorage": "500GB",
    "APIRateLimit": "1000/hour"
}
```

#### ✅ DO: Use consistent pricing strategies
```csharp
// All licenses should offer similar billing cycles
Free: Monthly only
Pro: Monthly, Annually
Enterprise: Monthly, Annually, Custom
```

#### ❌ DON'T: Create duplicate pricing strategies
```csharp
// Bad: Same currency + billing model + type
prices: [
    { USD, Recurring, Monthly, $49.99 },
    { USD, Recurring, Monthly, $59.99 }  // ❌ Duplicate!
]

// Good: Different billing types
prices: [
    { USD, Recurring, Monthly, $49.99 },
    { USD, Recurring, Annually, $499.99 }  // ✅ Different
]
```

#### ❌ DON'T: Store sensitive data in attributes
```csharp
// Bad
Attributes: {
    "APIKey": "secret123",  // ❌ Never store secrets
    "Password": "admin"     // ❌ Security violation
}

// Good
Attributes: {
    "MaxAPIKeys": "5",      // ✅ Limit configuration
    "HasSSO": "true"        // ✅ Feature flag
}
```

### Module Management

1. **Use meaningful module IDs**: `mod-analytics`, `mod-api`, `mod-integrations`
2. **Group related features**: Bundle features logically
3. **Document dependencies**: If Module A requires Module B
4. **Version modules**: Consider module versioning for breaking changes

## 🐛 Troubleshooting

### Common Issues

#### Issue: Duplicate pricing strategy error
**Cause**: Multiple prices with same Currency + BillingModel + BillingType.

**Solution**:
```csharp
// Ensure each price combination is unique
prices: [
    { USD, Recurring, Monthly },    // ✅
    { USD, Recurring, Annually },   // ✅ Different BillingType
    { EUR, Recurring, Monthly }     // ✅ Different Currency
]
```

#### Issue: Module not appearing in license
**Cause**: Module not properly added or isEnabled = false.

**Solution**:
```bash
# Verify module was added
GET /api/license/{id}

# Check module list
if (!license.Modules.Any(m => m.Id == "mod-api"))
{
    // Add module
    POST /api/license/{id}/module
}
```

#### Issue: License not showing on landing page
**Cause**: `ShowInLandingPage = false` or `IsActive = false`.

**Solution**:
```csharp
// Update license
PUT /api/license/{id}
{
    "showInLandingPage": true,
    "isActive": true
}
```

## 💰 Pricing Strategies

### Billing Models

1. **OneTime**: Single purchase (lifetime license)
2. **Recurring**: Subscription-based
3. **Metered**: Pay-per-use
4. **Tiered**: Graduated pricing tiers
5. **Volume**: Bulk discounts

### Billing Types

1. **Monthly**: Billed every month
2. **Annually**: Billed every year (usually discounted)
3. **Quarterly**: Billed every 3 months
4. **PerUse**: Based on usage
5. **PerSeat**: Per user pricing

### Example Pricing Configurations

```json
// Simple subscription
{
  "basePrice": { "amount": 29.99, "currency": "USD" },
  "billingModel": "Recurring",
  "billingType": "Monthly"
}

// Annual discount
{
  "basePrice": { "amount": 299.99, "currency": "USD" },
  "billingModel": "Recurring",
  "billingType": "Annually",
  "discountPercentage": 16.67  // Save ~$60
}

// One-time purchase
{
  "basePrice": { "amount": 999.99, "currency": "USD" },
  "billingModel": "OneTime",
  "billingType": "Lifetime"
}
```

## 📦 Module Management

### Module Best Practices

1. **Granular features**: Each module = one feature area
2. **Clear dependencies**: Document module requirements
3. **Consistent naming**: Use `mod-` prefix
4. **Enable/disable**: Control per license tier

### Example Module Structure

```json
{
  "modules": [
    {
      "id": "mod-core",
      "name": "Core Features",
      "description": "Essential functionality",
      "isEnabled": true
    },
    {
      "id": "mod-analytics",
      "name": "Advanced Analytics",
      "description": "Real-time dashboards and reports",
      "isEnabled": true
    },
    {
      "id": "mod-api",
      "name": "API Access",
      "description": "Full REST API with 1000 req/hour",
      "isEnabled": true
    },
    {
      "id": "mod-integrations",
      "name": "Third-party Integrations",
      "description": "Connect to 100+ services",
      "isEnabled": false  // Upgrade required
    }
  ]
}
```

## ❓ FAQ

### General Questions

**Q: Can a license have multiple prices?**
A: Yes, each license can have multiple pricing strategies (monthly, annual, etc.) as long as the combination of Currency + BillingModel + BillingType is unique.

**Q: How do I display licenses on my pricing page?**
A: Use `GET /api/license/landing` which returns only licenses with `ShowInLandingPage = true`. Mark popular tiers with `IsPopular = true`.

**Q: What's the difference between Description and ShortDescription?**
A: ShortDescription is a brief tagline (shown on cards), Description is the full explanation (shown on detail pages).

**Q: Can I add custom attributes?**
A: Yes, use the Attributes dictionary for any key-value metadata like "MaxUsers", "MaxStorage", etc.

### Technical Questions

**Q: How do I enforce license limits in my app?**
A: Fetch the license, check the Attributes dictionary, and enforce limits in your application code based on those values.

**Q: What happens when I add a module to a license?**
A: The module is added to the license, a `LicenseModuleAddedDomainEvent` is published, and other services can react to grant access.

**Q: Can I have per-tenant pricing?**
A: Yes, tenants are isolated. Each tenant can create their own licenses with custom pricing.

**Q: How do I version licenses?**
A: Currently not built-in. Consider using Name versioning ("Pro Plan v2") or custom attributes ("Version": "2.0").

## 🤝 Contributing

We welcome contributions!

### Development Workflow

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Run tests
5. Submit a pull request

### Code Standards

- Follow .editorconfig rules
- Aim for >80% test coverage
- Update documentation
- Use conventional commits

## 📞 Support & Resources

- **GitHub Issues**: Report bugs
- **Discussions**: Ask questions
- **Documentation**: [CodeDesignPlus Docs](https://codedesignplus.github.io/)
- **Email**: support@codedesignplus.com

## 📄 License

This project is licensed under the **GNU Lesser General Public License v3.0**.

## 🙏 Acknowledgments

Built with:
- **CodeDesignPlus SDK**
- **.NET 9**
- **MongoDB**
- **Open Source Community**

---

**Made with ❤️ by CodeDesignPlus**

*For questions, suggestions, or contributions, please open an issue or pull request.*
