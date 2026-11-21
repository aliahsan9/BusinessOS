using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Domain.Common;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Billing;
using BusinessOS.Infrastructure.Data.Configurations;
using BusinessOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Data;

public partial class BusinessOSDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, string>, IApplicationDbContext
{
    private readonly ITenantProvider _tenantProvider;

    public BusinessOSDbContext(
        DbContextOptions<BusinessOSDbContext> options,
        ITenantProvider tenantProvider)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    /// <summary>
    /// Resolved at query/save time so auth can set tenant after the scoped DbContext is created
    /// (e.g. login loads Identity before CreateSessionForUserAsync calls SetTenantId).
    /// </summary>
    private Guid _tenantId =>
        _tenantProvider.HasTenant() ? _tenantProvider.TenantId : Guid.Empty;

    // DbSets
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<TenantUsage> TenantUsages => Set<TenantUsage>();
    public DbSet<TenantAuditLog> TenantAuditLogs => Set<TenantAuditLog>();
    public DbSet<BillingInvoice> BillingInvoices => Set<BillingInvoice>();
    public DbSet<BillingTransaction> BillingTransactions => Set<BillingTransaction>();
    public DbSet<PaymentProvider> PaymentProviders => Set<PaymentProvider>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<ProcurementAgentSettings> ProcurementAgentSettings => Set<ProcurementAgentSettings>();
    public DbSet<ProcurementRecommendation> ProcurementRecommendations => Set<ProcurementRecommendation>();
    public DbSet<PurchaseReceivingEvent> PurchaseReceivingEvents => Set<PurchaseReceivingEvent>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationItem> QuotationItems => Set<QuotationItem>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
    public DbSet<Role> RbacRoles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> RbacUserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RbacAuditLog> RbacAuditLogs => Set<RbacAuditLog>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<EntityAuditLog> EntityAuditLogs => Set<EntityAuditLog>();
    public DbSet<AIConversation> AIConversations => Set<AIConversation>();
    public DbSet<AiConversationSession> AiConversationSessions => Set<AiConversationSession>();
    public DbSet<AiMessage> AiMessages => Set<AiMessage>();
    public DbSet<AiDocument> AiDocuments => Set<AiDocument>();
    public DbSet<AiDocumentChunk> AiDocumentChunks => Set<AiDocumentChunk>();
    public DbSet<AiCopilotAuditLog> AiCopilotAuditLogs => Set<AiCopilotAuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<GeneratedReport> GeneratedReports => Set<GeneratedReport>();
    public DbSet<UserOnboardingProgress> UserOnboardingProgress => Set<UserOnboardingProgress>();
    public DbSet<TeamInvitation> TeamInvitations => Set<TeamInvitation>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<VectorSyncOutboxMessage> VectorSyncOutboxMessages => Set<VectorSyncOutboxMessage>();
    public DbSet<AgentProfile> AgentProfiles => Set<AgentProfile>();
    public DbSet<VoicePreference> VoicePreferences => Set<VoicePreference>();
    public DbSet<AgentWorkflowRun> AgentWorkflowRuns => Set<AgentWorkflowRun>();
    public DbSet<AgentWorkflowStep> AgentWorkflowSteps => Set<AgentWorkflowStep>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<ProcessedWebhookEvent> ProcessedWebhookEvents => Set<ProcessedWebhookEvent>();
    public DbSet<RegionalPrice> RegionalPrices => Set<RegionalPrice>();
    public DbSet<SubscriptionHistory> SubscriptionHistories => Set<SubscriptionHistory>();
    public DbSet<BillingPaymentMethod> BillingPaymentMethods => Set<BillingPaymentMethod>();
    public DbSet<AiUsageLedger> AiUsageLedgers => Set<AiUsageLedger>();
    public DbSet<PlanFeatureGrant> PlanFeatureGrants => Set<PlanFeatureGrant>();
    public DbSet<CompanyFeatureOverride> CompanyFeatureOverrides => Set<CompanyFeatureOverride>();
    public DbSet<AiModelPrice> AiModelPrices => Set<AiModelPrice>();
    public DbSet<UsageAlert> UsageAlerts => Set<UsageAlert>();
    public DbSet<CompanyMembership> CompanyMemberships => Set<CompanyMembership>();
    public DbSet<BusinessTypeTemplate> BusinessTypeTemplates => Set<BusinessTypeTemplate>();
    public DbSet<PlatformAnnouncement> PlatformAnnouncements => Set<PlatformAnnouncement>();
    public DbSet<PlatformAnnouncementTarget> PlatformAnnouncementTargets => Set<PlatformAnnouncementTarget>();
    public DbSet<PlatformSetting> PlatformSettings => Set<PlatformSetting>();
    public DbSet<PlatformAuditLog> PlatformAuditLogs => Set<PlatformAuditLog>();
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();

    /// <summary>
    /// Allows an authorized company switch to change the active company on the user account.
    /// Business records still cannot move between companies.
    /// </summary>
    public bool AllowUserTenantReassignment { get; set; }
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<CompanyTeam> CompanyTeams => Set<CompanyTeam>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<AnnouncementRead> AnnouncementReads => Set<AnnouncementRead>();
    public DbSet<PerformanceGoal> PerformanceGoals => Set<PerformanceGoal>();
    public DbSet<PerformanceReview> PerformanceReviews => Set<PerformanceReview>();
    public DbSet<PipelineStage> PipelineStages => Set<PipelineStage>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<StageTransition> StageTransitions => Set<StageTransition>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<CustomerSegment> CustomerSegments => Set<CustomerSegment>();
    public DbSet<CustomerInteraction> CustomerInteractions => Set<CustomerInteraction>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<WarehouseStock> WarehouseStocks => Set<WarehouseStock>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<StockTransferLine> StockTransferLines => Set<StockTransferLine>();
    public DbSet<OrderFulfillment> OrderFulfillments => Set<OrderFulfillment>();
    public DbSet<FulfillmentLine> FulfillmentLines => Set<FulfillmentLine>();
    public DbSet<SalesReturn> SalesReturns => Set<SalesReturn>();
    public DbSet<SalesReturnLine> SalesReturnLines => Set<SalesReturnLine>();
    public DbSet<IncomeRecord> IncomeRecords => Set<IncomeRecord>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<TaxRate> TaxRates => Set<TaxRate>();
    public DbSet<OperationalSubscription> OperationalSubscriptions => Set<OperationalSubscription>();
    public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();
    public DbSet<ProjectMilestone> ProjectMilestones => Set<ProjectMilestone>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<WorkTaskComment> WorkTaskComments => Set<WorkTaskComment>();
    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<ProjectFile> ProjectFiles => Set<ProjectFile>();
    public DbSet<ClientPortalAccess> ClientPortalAccesses => Set<ClientPortalAccess>();
    public DbSet<NalvaCompanyAgent> NalvaCompanyAgents => Set<NalvaCompanyAgent>();
    public DbSet<NalvaAgentRevision> NalvaAgentRevisions => Set<NalvaAgentRevision>();
    public DbSet<NalvaTask> NalvaTasks => Set<NalvaTask>();
    public DbSet<NalvaExecution> NalvaExecutions => Set<NalvaExecution>();
    public DbSet<NalvaApproval> NalvaApprovals => Set<NalvaApproval>();
    public DbSet<NalvaMemoryRecord> NalvaMemoryRecords => Set<NalvaMemoryRecord>();
    public DbSet<NalvaKnowledgeSource> NalvaKnowledgeSources => Set<NalvaKnowledgeSource>();
    public DbSet<NalvaWorkflowDefinition> NalvaWorkflowDefinitions => Set<NalvaWorkflowDefinition>();
    public DbSet<NalvaWorkflowRun> NalvaWorkflowRuns => Set<NalvaWorkflowRun>();
    public DbSet<NalvaCompanySettings> NalvaCompanySettings => Set<NalvaCompanySettings>();
    public DbSet<NalvaWorkItem> NalvaWorkItems => Set<NalvaWorkItem>();
    public DbSet<NalvaBusinessEvent> NalvaBusinessEvents => Set<NalvaBusinessEvent>();
    public DbSet<CompanyIntegrationConnection> CompanyIntegrationConnections => Set<CompanyIntegrationConnection>();
    public DbSet<IntegrationOAuthState> IntegrationOAuthStates => Set<IntegrationOAuthState>();
    public DbSet<IntegrationActivityLog> IntegrationActivityLogs => Set<IntegrationActivityLog>();
    public DbSet<CompanyStorefront> CompanyStorefronts => Set<CompanyStorefront>();
    public DbSet<StorefrontSlugRedirect> StorefrontSlugRedirects => Set<StorefrontSlugRedirect>();
    public DbSet<StorefrontOffering> StorefrontOfferings => Set<StorefrontOffering>();
    public DbSet<StorefrontPackage> StorefrontPackages => Set<StorefrontPackage>();
    public DbSet<StorefrontPortfolioItem> StorefrontPortfolioItems => Set<StorefrontPortfolioItem>();
    public DbSet<StorefrontReview> StorefrontReviews => Set<StorefrontReview>();
    public DbSet<StorefrontInquiry> StorefrontInquiries => Set<StorefrontInquiry>();
    public DbSet<DashboardPreference> DashboardPreferences => Set<DashboardPreference>();
    public DbSet<SupplierPortalAccess> SupplierPortalAccesses => Set<SupplierPortalAccess>();
    public DbSet<AgencyWorkspace> AgencyWorkspaces => Set<AgencyWorkspace>();
    public DbSet<EarlyAccessInvitation> EarlyAccessInvitations => Set<EarlyAccessInvitation>();
    public DbSet<LaunchFeedback> LaunchFeedback => Set<LaunchFeedback>();
    public DbSet<ProductUsageEvent> ProductUsageEvents => Set<ProductUsageEvent>();
    public DbSet<ProductBacklogItem> ProductBacklogItems => Set<ProductBacklogItem>();
    public DbSet<CompanyDataRequest> CompanyDataRequests => Set<CompanyDataRequest>();
    public DbSet<MarketingBrandProfile> MarketingBrandProfiles => Set<MarketingBrandProfile>();
    public DbSet<MarketingCampaign> MarketingCampaigns => Set<MarketingCampaign>();
    public DbSet<MarketingContentItem> MarketingContentItems => Set<MarketingContentItem>();
    public DbSet<MarketingContentAsset> MarketingContentAssets => Set<MarketingContentAsset>();
    public DbSet<MarketingResearchReport> MarketingResearchReports => Set<MarketingResearchReport>();
    public DbSet<MarketingPublishJob> MarketingPublishJobs => Set<MarketingPublishJob>();
    public DbSet<MarketingAnalyticsSnapshot> MarketingAnalyticsSnapshots => Set<MarketingAnalyticsSnapshot>();
    public DbSet<MarketingAgentPolicy> MarketingAgentPolicies => Set<MarketingAgentPolicy>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportMessage> SupportMessages => Set<SupportMessage>();
    public DbSet<SupportFollowUpJob> SupportFollowUpJobs => Set<SupportFollowUpJob>();
    public DbSet<SupportChannelAccount> SupportChannelAccounts => Set<SupportChannelAccount>();
    public DbSet<SalesAgentSettings> SalesAgentSettings => Set<SalesAgentSettings>();
    public DbSet<SalesAgentObjective> SalesAgentObjectives => Set<SalesAgentObjective>();
    public DbSet<SalesProspectResearch> SalesProspectResearch => Set<SalesProspectResearch>();
    public DbSet<SalesOutreachCampaign> SalesOutreachCampaigns => Set<SalesOutreachCampaign>();
    public DbSet<SalesOutreachMessage> SalesOutreachMessages => Set<SalesOutreachMessage>();
    public DbSet<SalesEmailSuppression> SalesEmailSuppressions => Set<SalesEmailSuppression>();
    public DbSet<SalesOutboundEmailLog> SalesOutboundEmailLogs => Set<SalesOutboundEmailLog>();

    public override int SaveChanges() =>
        SaveChangesAsync(CancellationToken.None).GetAwaiter().GetResult();

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        ApplyTenantAndAuditRules();
        if (QuotaEnforcer.IsReentrant)
            return await base.SaveChangesAsync(cancellationToken);

        var hold = await QuotaEnforcer.AcquireForSaveAsync(this, cancellationToken);
        try
        {
            var result = await base.SaveChangesAsync(cancellationToken);
            hold.MarkSuccess();
            return result;
        }
        finally
        {
            await hold.DisposeAsync();
        }
    }

    private void AssignInMemoryRowVersions()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
                continue;

            var rowVersion = entry.Properties.FirstOrDefault(property => property.Metadata.Name == "RowVersion");
            if (rowVersion?.CurrentValue is byte[] { Length: > 0 })
                continue;

            if (rowVersion is not null)
            {
                rowVersion.CurrentValue = Guid.NewGuid().ToByteArray()[..8];
                rowVersion.IsTemporary = false;
            }
        }
    }

    private void ApplyTenantAndAuditRules()
    {
        if (!Database.IsRelational())
            AssignInMemoryRowVersions();

        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is TenantAuditLog or EntityAuditLog or RbacAuditLog or AiCopilotAuditLog or PlatformAuditLog
                && entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException("Audit records cannot be modified or deleted.");
            }

            // Assign TenantId automatically
            if (entry.State == EntityState.Added)
            {
                // UserRole.TenantId is owned by application code (company switch / invitations).
                if (entry.Entity is UserRole)
                    continue;

                var tenantIdProp = entry.Properties
                    .FirstOrDefault(p => p.Metadata.Name == "TenantId");

                if (tenantIdProp is not null && _tenantId != Guid.Empty)
                {
                    // Guid? TenantId may be intentionally null (system-scoped rows such as AgentProfile / system Roles).
                    if (tenantIdProp.Metadata.ClrType == typeof(Guid?))
                    {
                        var nullable = (Guid?)tenantIdProp.CurrentValue;
                        if (nullable == Guid.Empty || (nullable.HasValue && nullable.Value != _tenantId))
                            tenantIdProp.CurrentValue = _tenantId;
                    }
                    else if (tenantIdProp.Metadata.ClrType == typeof(Guid))
                    {
                        tenantIdProp.CurrentValue = _tenantId;
                    }
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                var tenantIdProp = entry.Properties
                    .FirstOrDefault(p => p.Metadata.Name == "TenantId");

                if (tenantIdProp is not null && tenantIdProp.IsModified
                    && !(AllowUserTenantReassignment && entry.Entity is Identity.ApplicationUser))
                    tenantIdProp.CurrentValue = tenantIdProp.OriginalValue;
            }

            // Soft delete handling (if entity has IsDeleted)
            if (entry.State == EntityState.Deleted)
            {
                var isDeletedProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "IsDeleted");

                if (isDeletedProp != null)
                {
                    entry.State = EntityState.Modified;
                    isDeletedProp.CurrentValue = true;
                }
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(
            typeof(CustomerConfiguration).Assembly);

        ConfigureGlobalQueryFilters(builder);
    }

    private void ConfigureGlobalQueryFilters(ModelBuilder builder)
    {
        // Tenant + Soft Delete safe filter pattern

        builder.Entity<Product>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Category>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Customer>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Supplier>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Order>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<OrderItem>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Expense>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<ExpenseCategory>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Notification>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Activity>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<TenantSettings>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Employee>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Inventory>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<StockTransaction>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Purchase>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<PurchaseItem>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Payment>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Invoice>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Quotation>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<QuotationItem>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Tenant>()
            .HasQueryFilter(x => x.Id == _tenantId && !x.IsDeleted);

        builder.Entity<GeneratedReport>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<TeamInvitation>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Project>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<TenantUsage>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<SubscriptionHistory>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<BillingPaymentMethod>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<AiUsageLedger>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<TenantSubscription>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<TenantAuditLog>()
            .HasQueryFilter(x => x.TenantId == _tenantId);

        builder.Entity<EntityAuditLog>()
            .HasQueryFilter(x => x.TenantId == _tenantId);

        builder.Entity<RbacAuditLog>()
            .HasQueryFilter(x => x.TenantId == _tenantId);

        builder.Entity<BillingInvoice>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<BillingTransaction>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<IdempotencyRecord>()
            .HasQueryFilter(x => x.TenantId == _tenantId);

        builder.Entity<WorkTask>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<AIConversation>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<AiConversationSession>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<AiMessage>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<AiDocument>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<AiDocumentChunk>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<AiCopilotAuditLog>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        // Agent profiles: system defaults (TenantId null) + current tenant overrides.
        builder.Entity<AgentProfile>()
            .HasQueryFilter(x => !x.IsDeleted && (x.TenantId == null || x.TenantId == _tenantId));

        // Shared immutable system roles (TenantId null + IsSystem) plus this company's custom roles.
        builder.Entity<Role>()
            .HasQueryFilter(x => (x.IsSystem && x.TenantId == null) || x.TenantId == _tenantId);

        // System role permissions (TenantId null) plus this company's custom role permissions.
        builder.Entity<RolePermission>()
            .HasQueryFilter(x => x.TenantId == null || x.TenantId == _tenantId);

        // UserRole is filtered in repositories by the user's active company so company switch
        // can read/write assignments after TenantId changes on the same scoped DbContext.

        builder.Entity<VoicePreference>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<AgentWorkflowRun>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        // Steps inherit tenant isolation via parent workflow run.
        builder.Entity<AgentWorkflowStep>()
            .HasQueryFilter(x => !x.IsDeleted && x.WorkflowRun.TenantId == _tenantId);

        builder.Entity<StoredFile>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        // Company memberships are read by user id across companies. Callers must filter to the authenticated user.
        // Outbox is cross-tenant for background workers; filter by Status, not TenantId.

        FilterTenant<Department>(builder);
        FilterTenant<CompanyTeam>(builder);
        FilterTenant<AttendanceRecord>(builder);
        FilterTenant<LeaveType>(builder);
        FilterTenant<LeaveBalance>(builder);
        FilterTenant<LeaveRequest>(builder);
        FilterTenant<Announcement>(builder);
        FilterTenant<AnnouncementRead>(builder);
        FilterTenant<PerformanceGoal>(builder);
        FilterTenant<PerformanceReview>(builder);
        FilterTenant<PipelineStage>(builder);
        FilterTenant<Lead>(builder);
        FilterTenant<Deal>(builder);
        FilterTenant<StageTransition>(builder);
        FilterTenant<FollowUp>(builder);
        FilterTenant<CustomerSegment>(builder);
        FilterTenant<CustomerInteraction>(builder);
        FilterTenant<Warehouse>(builder);
        FilterTenant<WarehouseStock>(builder);
        FilterTenant<StockTransfer>(builder);
        FilterTenant<StockTransferLine>(builder);
        FilterTenant<OrderFulfillment>(builder);
        FilterTenant<FulfillmentLine>(builder);
        FilterTenant<SalesReturn>(builder);
        FilterTenant<SalesReturnLine>(builder);
        FilterTenant<IncomeRecord>(builder);
        FilterTenant<PurchaseInvoice>(builder);
        FilterTenant<TaxRate>(builder);
        FilterTenant<OperationalSubscription>(builder);
        FilterTenant<PriceHistory>(builder);
        FilterTenant<ProjectMilestone>(builder);
        FilterTenant<ProjectMember>(builder);
        FilterTenant<TimeEntry>(builder);
        FilterTenant<WorkTaskComment>(builder);
        FilterTenant<Sprint>(builder);
        FilterTenant<ProjectFile>(builder);
        FilterTenant<ClientPortalAccess>(builder);
        FilterTenant<NalvaCompanyAgent>(builder);
        FilterTenant<NalvaAgentRevision>(builder);
        FilterTenant<NalvaTask>(builder);
        FilterTenant<NalvaExecution>(builder);
        FilterTenant<NalvaApproval>(builder);
        FilterTenant<NalvaMemoryRecord>(builder);
        FilterTenant<NalvaKnowledgeSource>(builder);
        FilterTenant<NalvaWorkflowDefinition>(builder);
        FilterTenant<NalvaWorkflowRun>(builder);
        FilterTenant<NalvaCompanySettings>(builder);
        FilterTenant<NalvaWorkItem>(builder);
        FilterTenant<NalvaBusinessEvent>(builder);
        FilterTenant<CompanyIntegrationConnection>(builder);
        FilterTenant<IntegrationOAuthState>(builder);
        FilterTenant<IntegrationActivityLog>(builder);
        FilterTenant<CompanyFeatureOverride>(builder);
        FilterTenant<UsageAlert>(builder);
        FilterTenant<CompanyStorefront>(builder);
        FilterTenant<StorefrontSlugRedirect>(builder);
        FilterTenant<StorefrontOffering>(builder);
        FilterTenant<StorefrontPackage>(builder);
        FilterTenant<StorefrontPortfolioItem>(builder);
        FilterTenant<StorefrontReview>(builder);
        FilterTenant<StorefrontInquiry>(builder);
        FilterTenant<DashboardPreference>(builder);
        FilterTenant<SupplierPortalAccess>(builder);
        FilterTenant<ProcurementAgentSettings>(builder);
        FilterTenant<ProcurementRecommendation>(builder);
        FilterTenant<PurchaseReceivingEvent>(builder);
        FilterTenant<AgencyWorkspace>(builder);
        FilterTenant<LaunchFeedback>(builder);
        FilterTenant<ProductUsageEvent>(builder);
        FilterTenant<CompanyDataRequest>(builder);
        FilterTenant<SupportTicket>(builder);
        FilterTenant<SupportMessage>(builder);
        FilterTenant<SupportFollowUpJob>(builder);
        FilterTenant<SupportChannelAccount>(builder);
        FilterTenant<MarketingBrandProfile>(builder);
        FilterTenant<MarketingCampaign>(builder);
        FilterTenant<MarketingContentItem>(builder);
        FilterTenant<MarketingContentAsset>(builder);
        FilterTenant<MarketingResearchReport>(builder);
        FilterTenant<MarketingPublishJob>(builder);
        FilterTenant<MarketingAnalyticsSnapshot>(builder);
        FilterTenant<MarketingAgentPolicy>(builder);
        FilterTenant<SalesAgentSettings>(builder);
        FilterTenant<SalesAgentObjective>(builder);
        FilterTenant<SalesProspectResearch>(builder);
        FilterTenant<SalesOutreachCampaign>(builder);
        FilterTenant<SalesOutreachMessage>(builder);
        FilterTenant<SalesEmailSuppression>(builder);
        FilterTenant<SalesOutboundEmailLog>(builder);
    }

    private void FilterTenant<TEntity>(ModelBuilder builder)
        where TEntity : AuditableEntity
    {
        builder.Entity<TEntity>().HasQueryFilter(x =>
            EF.Property<Guid>(x, "TenantId") == _tenantId && !x.IsDeleted);
    }
}
