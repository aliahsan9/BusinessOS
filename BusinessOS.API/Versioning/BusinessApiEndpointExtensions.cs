using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using BusinessOS.API.Endpoints;
using Scalar.AspNetCore;

namespace BusinessOS.API.Versioning;

/// <summary>
/// Maps the version 1.0 business API and its OpenAPI/Scalar documents.
/// </summary>
public static class BusinessApiEndpointExtensions
{
    /// <summary>
    /// Maps every business Minimal API under <c>/api/v{version}/...</c> at version 1.0.
    /// Health checks and the SignalR hub stay outside this group.
    /// Payment-provider webhooks also keep their original unversioned paths.
    /// </summary>
    public static WebApplication MapBusinessApi(this WebApplication app)
    {
        var apiVersion = new ApiVersion(1, 0);

        // Only versions that have real endpoints belong in the set.
        // Adding 2.0 here without a MapToApiVersion(2.0) endpoint would advertise a version that does not exist.
        var versionSet = app.NewApiVersionSet("BusinessOS")
            .HasApiVersion(apiVersion)
            .ReportApiVersions()
            .Build();

        var v1 = app.MapGroup(ApiVersioningPaths.RoutePrefix)
            .WithApiVersionSet(versionSet)
            .HasApiVersion(apiVersion)
            .MapToApiVersion(apiVersion);

        v1.MapAuthEndpoints();
        v1.MapAccountEndpoints();
        v1.MapCategoryEndpoints();
        v1.MapProductEndpoints();
        v1.MapCustomerEndpoints();
        v1.MapOrderEndpoints();
        v1.MapSupplierEndpoints();
        v1.MapPurchaseOrderEndpoints();
        v1.MapPaymentEndpoints();
        v1.MapInvoiceEndpoints();
        v1.MapQuotationEndpoints();
        v1.MapInventoryEndpoints();
        v1.MapDashboardEndpoints();
        v1.MapAnalyticsEndpoints();
        v1.MapReportEndpoints();
        v1.MapRoleEndpoints();
        v1.MapExpenseEndpoints();
        v1.MapFinanceEndpoints();
        v1.MapUserEndpoints();
        v1.MapAuditEndpoints();
        v1.MapNotificationEndpoints();
        v1.MapActivityEndpoints();
        v1.MapSettingsEndpoints();
        v1.MapOnboardingEndpoints();
        v1.MapAiEndpoints();
        v1.MapAgentEndpoints();
        v1.MapHelpEndpoints();
        v1.MapTeamEndpoints();
        v1.MapOrganizationEndpoints();
        v1.MapTenantEndpoints();
        v1.MapBillingEndpoints();
        v1.MapBusinessRegistrationEndpoint();
        v1.MapSystemAdminEndpoints();

        // External payment providers are configured with fixed callback URLs.
        app.MapLegacyBillingWebhooks();

        return app;
    }

    /// <summary>
    /// Exposes one OpenAPI document per API version and registers those documents with Scalar.
    /// </summary>
    public static WebApplication MapBusinessApiReference(this WebApplication app)
    {
        app.MapOpenApi().WithDocumentPerVersion();

        app.MapScalarApiReference(options =>
        {
            options.Title = "BusinessOS API";
            options.Theme = ScalarTheme.BluePlanet;
            options.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
            options.Authentication = new()
            {
                PreferredSecuritySchemes = ["Bearer", "TenantHeader"]
            };
            options.ShowSidebar = true;

            var descriptions = app.DescribeApiVersions();
            foreach (var description in descriptions)
            {
                var groupName = description.GroupName;
                var title = description.IsDeprecated
                    ? $"BusinessOS API {groupName} (deprecated)"
                    : $"BusinessOS API {groupName}";

                options.AddDocument(
                    groupName,
                    title,
                    isDefault: !description.IsDeprecated &&
                               groupName.Equals("v1", StringComparison.OrdinalIgnoreCase));
            }
        });

        return app;
    }
}
