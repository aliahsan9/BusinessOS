# API Versioning

BusinessOS business APIs are versioned with [ASP.NET API Versioning](https://github.com/dotnet/aspnet-api-versioning) (`Asp.Versioning.Http` 10.2.3) and URL segments. Minimal APIs stay Minimal APIs. There is no custom version parser.

## Current contract

| Item | Policy |
| :--- | :--- |
| Current version | **1.0** |
| URL | `/api/v1/{resource}` and `/api/v1.0/{resource}` |
| Default version | 1.0, recorded on `ApiVersioningOptions.DefaultApiVersion` |
| Missing version | **400** `API_VERSION_REQUIRED`. The version is not assumed. |
| Unknown version | **400**. Response includes `api-supported-versions`. |
| Deprecated version | None today. `ReportApiVersions` is on, so a deprecated version would also send `api-deprecated-versions`. |
| OpenAPI | One document per version. Version 1.0 is `/openapi/v1.json`. Scalar is `/scalar`. |
| Health | `/api/health` and `/api/health/ready` stay unversioned. They are operational probes, not a business contract. |
| SignalR | `/hubs/notifications` stays unversioned. |
| Payment webhooks | Version 1.0 paths are canonical. The original `/api/billing/webhook/{provider}` paths remain as compatibility aliases and are omitted from OpenAPI. |

Version 1.0 is the existing public contract: the same handlers, validation, authorization, tenant checks, and response bodies as the previous unversioned API. The Angular client calls it by setting `environment.apiUrl` to `/api/v1`.

## What belongs in a version

- Backward-compatible additions stay on the current version: new optional fields, new endpoints, and looser validation.
- A breaking contract change requires a new version: removing or renaming a field, changing a status code or auth requirement, or changing the meaning of an existing field.
- Register every version explicitly with `HasApiVersion` / `MapToApiVersion`. Do not add a version to the set unless at least one endpoint implements it.
- Keep an old version available until its support window ends. Deprecate it with `HasDeprecatedApiVersion` before removal, and record the sunset date in this document.
- A version number is not an authorization check. JWT policies, permissions, and `X-Tenant-ID` still apply inside every version.

## Adding version 2.0 later

Do not register 2.0 until a real endpoint needs it. Endpoints that only map to 1.0 must not advertise 2.0.

```csharp
var versionSet = app.NewApiVersionSet("BusinessOS")
    .HasApiVersion(new ApiVersion(1, 0))
    .HasApiVersion(new ApiVersion(2, 0))
    .ReportApiVersions()
    .Build();

var api = app.MapGroup("/api/v{version:apiVersion}")
    .WithApiVersionSet(versionSet);

var v1 = api.MapGroup(string.Empty)
    .HasApiVersion(new ApiVersion(1, 0))
    .MapToApiVersion(new ApiVersion(1, 0));

v1.MapCustomerEndpoints(); // unchanged 1.0 contract

api.MapGroup("/customers")
    .HasApiVersion(new ApiVersion(2, 0))
    .MapToApiVersion(new ApiVersion(2, 0))
    .MapGet("/export", ExportCustomersV2);
```

`GET /api/v1/customers` keeps the 1.0 handler. `GET /api/v2/customers` stays unsupported until that route is mapped to 2.0. `GET /api/v2/customers/export` is the new contract. OpenAPI then publishes `/openapi/v1.json` and `/openapi/v2.json`, and Scalar lists both because documents are built from `DescribeApiVersions()`.

When 1.0 is retired, map it with `HasDeprecatedApiVersion(new ApiVersion(1, 0))` instead of deleting it. Responses then include `api-deprecated-versions: 1.0`, and the version 1.0 OpenAPI document is marked deprecated. Remove the version only after clients have moved and this document records the removal date.

## Example requests

```http
GET /api/v1/billing/plans
GET /api/v1/customers
Authorization: Bearer {token}
X-Tenant-ID: {tenantId}
```

```http
GET /api/customers
```

```http
HTTP/1.1 400 Bad Request
api-supported-versions: 1.0

{ "title": "API version is required", "code": "API_VERSION_REQUIRED" }
```

```http
GET /api/v2/customers
```

```http
HTTP/1.1 400 Bad Request
api-supported-versions: 1.0

{ "title": "Unsupported API version", "code": "API_VERSION_UNSUPPORTED" }
```

OpenAPI and Scalar (Development and the test host):

```text
http://localhost:5162/openapi/v1.json
http://localhost:5162/scalar
```
