using BusinessOS.Application.Common.Concurrency;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Category> Categories { get; }
    DbSet<Product> Products { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Employee> Employees { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<Purchase> Purchases { get; }
    DbSet<PurchaseItem> PurchaseItems { get; }
    DbSet<ProcurementAgentSettings> ProcurementAgentSettings { get; }
    DbSet<ProcurementRecommendation> ProcurementRecommendations { get; }
    DbSet<PurchaseReceivingEvent> PurchaseReceivingEvents { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<Quotation> Quotations { get; }
    DbSet<QuotationItem> QuotationItems { get; }
    DbSet<Inventory> Inventories { get; }
    DbSet<StockTransaction> StockTransactions { get; }
    DbSet<Role> RbacRoles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<UserRole> RbacUserRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<RbacAuditLog> RbacAuditLogs { get; }
    DbSet<EntityAuditLog> EntityAuditLogs { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<ExpenseCategory> ExpenseCategories { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<Activity> Activities { get; }
    DbSet<Tenant> Tenants { get; }
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }
    DbSet<TenantSubscription> TenantSubscriptions { get; }
    DbSet<TenantUsage> TenantUsages { get; }
    DbSet<TenantAuditLog> TenantAuditLogs { get; }
    DbSet<BillingInvoice> BillingInvoices { get; }
    DbSet<BillingTransaction> BillingTransactions { get; }
    DbSet<PaymentProvider> PaymentProviders { get; }
    DbSet<TenantSettings> TenantSettings { get; }
    DbSet<GeneratedReport> GeneratedReports { get; }
    DbSet<UserOnboardingProgress> UserOnboardingProgress { get; }
    DbSet<AIConversation> AIConversations { get; }
    DbSet<AiConversationSession> AiConversationSessions { get; }
    DbSet<AiMessage> AiMessages { get; }
    DbSet<AiDocument> AiDocuments { get; }
    DbSet<AiDocumentChunk> AiDocumentChunks { get; }
    DbSet<AiCopilotAuditLog> AiCopilotAuditLogs { get; }
    DbSet<TeamInvitation> TeamInvitations { get; }
    DbSet<Project> Projects { get; }
    DbSet<WorkTask> WorkTasks { get; }
    DbSet<VectorSyncOutboxMessage> VectorSyncOutboxMessages { get; }
    DbSet<AgentProfile> AgentProfiles { get; }
    DbSet<VoicePreference> VoicePreferences { get; }
    DbSet<AgentWorkflowRun> AgentWorkflowRuns { get; }
    DbSet<AgentWorkflowStep> AgentWorkflowSteps { get; }
    DbSet<IdempotencyRecord> IdempotencyRecords { get; }
    DbSet<ProcessedWebhookEvent> ProcessedWebhookEvents { get; }
    DbSet<RegionalPrice> RegionalPrices { get; }
    DbSet<SubscriptionHistory> SubscriptionHistories { get; }
    DbSet<BillingPaymentMethod> BillingPaymentMethods { get; }
    DbSet<AiUsageLedger> AiUsageLedgers { get; }
    DbSet<PlanFeatureGrant> PlanFeatureGrants { get; }
    DbSet<CompanyFeatureOverride> CompanyFeatureOverrides { get; }
    DbSet<AiModelPrice> AiModelPrices { get; }
    DbSet<UsageAlert> UsageAlerts { get; }
    DbSet<CompanyMembership> CompanyMemberships { get; }
    DbSet<BusinessTypeTemplate> BusinessTypeTemplates { get; }
    DbSet<PlatformAnnouncement> PlatformAnnouncements { get; }
    DbSet<PlatformAnnouncementTarget> PlatformAnnouncementTargets { get; }
    DbSet<PlatformSetting> PlatformSettings { get; }
    DbSet<PlatformAuditLog> PlatformAuditLogs { get; }
    DbSet<StoredFile> StoredFiles { get; }
    DbSet<Department> Departments { get; }
    DbSet<CompanyTeam> CompanyTeams { get; }
    DbSet<AttendanceRecord> AttendanceRecords { get; }
    DbSet<LeaveType> LeaveTypes { get; }
    DbSet<LeaveBalance> LeaveBalances { get; }
    DbSet<LeaveRequest> LeaveRequests { get; }
    DbSet<Announcement> Announcements { get; }
    DbSet<AnnouncementRead> AnnouncementReads { get; }
    DbSet<PerformanceGoal> PerformanceGoals { get; }
    DbSet<PerformanceReview> PerformanceReviews { get; }
    DbSet<PipelineStage> PipelineStages { get; }
    DbSet<Lead> Leads { get; }
    DbSet<Deal> Deals { get; }
    DbSet<StageTransition> StageTransitions { get; }
    DbSet<FollowUp> FollowUps { get; }
    DbSet<CustomerSegment> CustomerSegments { get; }
    DbSet<CustomerInteraction> CustomerInteractions { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<WarehouseStock> WarehouseStocks { get; }
    DbSet<StockTransfer> StockTransfers { get; }
    DbSet<StockTransferLine> StockTransferLines { get; }
    DbSet<OrderFulfillment> OrderFulfillments { get; }
    DbSet<FulfillmentLine> FulfillmentLines { get; }
    DbSet<SalesReturn> SalesReturns { get; }
    DbSet<SalesReturnLine> SalesReturnLines { get; }
    DbSet<IncomeRecord> IncomeRecords { get; }
    DbSet<PurchaseInvoice> PurchaseInvoices { get; }
    DbSet<TaxRate> TaxRates { get; }
    DbSet<OperationalSubscription> OperationalSubscriptions { get; }
    DbSet<PriceHistory> PriceHistories { get; }
    DbSet<ProjectMilestone> ProjectMilestones { get; }
    DbSet<ProjectMember> ProjectMembers { get; }
    DbSet<TimeEntry> TimeEntries { get; }
    DbSet<WorkTaskComment> WorkTaskComments { get; }
    DbSet<Sprint> Sprints { get; }
    DbSet<ProjectFile> ProjectFiles { get; }
    DbSet<ClientPortalAccess> ClientPortalAccesses { get; }
    DbSet<NalvaCompanyAgent> NalvaCompanyAgents { get; }
    DbSet<NalvaAgentRevision> NalvaAgentRevisions { get; }
    DbSet<NalvaTask> NalvaTasks { get; }
    DbSet<NalvaExecution> NalvaExecutions { get; }
    DbSet<NalvaApproval> NalvaApprovals { get; }
    DbSet<NalvaMemoryRecord> NalvaMemoryRecords { get; }
    DbSet<NalvaKnowledgeSource> NalvaKnowledgeSources { get; }
    DbSet<NalvaWorkflowDefinition> NalvaWorkflowDefinitions { get; }
    DbSet<NalvaWorkflowRun> NalvaWorkflowRuns { get; }
    DbSet<NalvaCompanySettings> NalvaCompanySettings { get; }
    DbSet<NalvaWorkItem> NalvaWorkItems { get; }
    DbSet<NalvaBusinessEvent> NalvaBusinessEvents { get; }
    DbSet<CompanyIntegrationConnection> CompanyIntegrationConnections { get; }
    DbSet<IntegrationOAuthState> IntegrationOAuthStates { get; }
    DbSet<IntegrationActivityLog> IntegrationActivityLogs { get; }
    DbSet<CompanyStorefront> CompanyStorefronts { get; }
    DbSet<StorefrontSlugRedirect> StorefrontSlugRedirects { get; }
    DbSet<StorefrontOffering> StorefrontOfferings { get; }
    DbSet<StorefrontPackage> StorefrontPackages { get; }
    DbSet<StorefrontPortfolioItem> StorefrontPortfolioItems { get; }
    DbSet<StorefrontReview> StorefrontReviews { get; }
    DbSet<StorefrontInquiry> StorefrontInquiries { get; }
    DbSet<DashboardPreference> DashboardPreferences { get; }
    DbSet<SupplierPortalAccess> SupplierPortalAccesses { get; }
    DbSet<AuthSession> AuthSessions { get; }
    DbSet<AgencyWorkspace> AgencyWorkspaces { get; }
    DbSet<EarlyAccessInvitation> EarlyAccessInvitations { get; }
    DbSet<LaunchFeedback> LaunchFeedback { get; }
    DbSet<ProductUsageEvent> ProductUsageEvents { get; }
    DbSet<ProductBacklogItem> ProductBacklogItems { get; }
    DbSet<CompanyDataRequest> CompanyDataRequests { get; }
    DbSet<SupportTicket> SupportTickets { get; }
    DbSet<SupportMessage> SupportMessages { get; }
    DbSet<SupportFollowUpJob> SupportFollowUpJobs { get; }
    DbSet<SupportChannelAccount> SupportChannelAccounts { get; }
    DbSet<MarketingBrandProfile> MarketingBrandProfiles { get; }
    DbSet<MarketingCampaign> MarketingCampaigns { get; }
    DbSet<MarketingContentItem> MarketingContentItems { get; }
    DbSet<MarketingContentAsset> MarketingContentAssets { get; }
    DbSet<MarketingResearchReport> MarketingResearchReports { get; }
    DbSet<MarketingPublishJob> MarketingPublishJobs { get; }
    DbSet<MarketingAnalyticsSnapshot> MarketingAnalyticsSnapshots { get; }
    DbSet<MarketingAgentPolicy> MarketingAgentPolicies { get; }
    DbSet<SalesAgentSettings> SalesAgentSettings { get; }
    DbSet<SalesAgentObjective> SalesAgentObjectives { get; }
    DbSet<SalesProspectResearch> SalesProspectResearch { get; }
    DbSet<SalesOutreachCampaign> SalesOutreachCampaigns { get; }
    DbSet<SalesOutreachMessage> SalesOutreachMessages { get; }
    DbSet<SalesEmailSuppression> SalesEmailSuppressions { get; }
    DbSet<SalesOutboundEmailLog> SalesOutboundEmailLogs { get; }

    bool HasActiveTransaction { get; }

    Task<IApplicationTransaction?> BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task<StockAdjustmentResult> TryAdjustStockAsync(
        Guid productId,
        decimal quantityDelta,
        CancellationToken cancellationToken = default);

    Task<bool> TryTransitionOrderStatusAsync(
        Guid orderId,
        string expectedStatus,
        string newStatus,
        CancellationToken cancellationToken = default);

    Task<bool> TryTransitionPurchaseStatusAsync(
        Guid purchaseId,
        string expectedStatus,
        string newStatus,
        CancellationToken cancellationToken = default);

    Task<int> TryRefundPaymentAsync(
        Guid paymentId,
        decimal amount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts a payment only when the order balance still covers it.
    /// On SQL Server the order row is locked for the caller's transaction.
    /// </summary>
    Task<bool> TryAcceptOrderPaymentAsync(
        Guid orderId,
        decimal amount,
        CancellationToken cancellationToken = default);

    void Detach<TEntity>(TEntity entity) where TEntity : class;

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
