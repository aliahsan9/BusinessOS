export interface TenantDto {
  id: string;
  name: string;
  slug: string;
  logoUrl?: string | null;
  domain?: string | null;
  timezone: string;
  currency: string;
  status: string;
  subscriptionPlan: string;
  subscriptionPlanId: string;
  businessType: string;
  email: string;
  website?: string | null;
  description?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface TenantSettingsDto {
  tenantId: string;
  name: string;
  logoUrl?: string | null;
  timezone: string;
  currency: string;
  businessType: string;
  email: string;
  phone: string;
  address: string;
  website?: string | null;
  description?: string | null;
  theme?: string | null;
}

export interface TenantUsageDto {
  userCount: number;
  maxUsers: number;
  customerCount: number;
  maxCustomers: number;
  projectCount: number;
  maxProjects: number;
  storageUsedMb: number;
  maxStorageMb: number;
  aiRequestsUsed: number;
  maxAiRequests: number;
  subscriptionPlan: string;
  lastCalculatedAt: string;
}

export interface TenantDashboardDto {
  tenant: TenantDto;
  usage: TenantUsageDto;
}

export interface UpdateTenantRequest {
  name: string;
  logoUrl?: string | null;
  timezone: string;
  currency: string;
  businessType: string;
  email: string;
  website?: string | null;
  description?: string | null;
}

export interface UpdateTenantSettingsRequest {
  name: string;
  logoUrl?: string | null;
  timezone: string;
  currency: string;
  businessType: string;
  email: string;
  phone: string;
  address: string;
  website?: string | null;
  description?: string | null;
}

export interface RegisterBusinessRequest {
  businessName: string;
  ownerFirstName: string;
  ownerLastName: string;
  email: string;
  password: string;
  timezone: string;
  currency: string;
  industry: string;
}
