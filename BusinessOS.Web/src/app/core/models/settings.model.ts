export interface TenantSettingsDto {
  id: string;
  tenantId: string;
  currency: string;
  language: string;
  taxRate: number;
  invoicePrefix?: string | null;
  emailFromAddress?: string | null;
  theme: string;
  logoUrl?: string | null;
  timezone: string;
  aiAssistantEnabled: boolean;
  aiShowSuggestions: boolean;
  emailNotificationsEnabled: boolean;
  systemNotificationsEnabled: boolean;
  orderNotificationsEnabled: boolean;
  inventoryAlertsEnabled: boolean;
  paymentAlertsEnabled: boolean;
  taskNotificationsEnabled: boolean;
  invoiceNotificationsEnabled: boolean;
  customerNotificationsEnabled: boolean;
  projectNotificationsEnabled: boolean;
}

export interface BusinessProfileDto {
  tenantId: string;
  name: string;
  businessType: string;
  email: string;
  phone: string;
  address: string;
  subscriptionPlan: string;
  isActive: boolean;
  website?: string | null;
  description?: string | null;
  settings: TenantSettingsDto;
}

export interface UpdateTenantSettingsRequest {
  currency: string;
  language: string;
  taxRate: number;
  invoicePrefix?: string | null;
  emailFromAddress?: string | null;
  theme: string;
  logoUrl?: string | null;
  timezone: string;
  aiAssistantEnabled: boolean;
  aiShowSuggestions: boolean;
  emailNotificationsEnabled: boolean;
  systemNotificationsEnabled: boolean;
  orderNotificationsEnabled: boolean;
  inventoryAlertsEnabled: boolean;
  paymentAlertsEnabled: boolean;
  taskNotificationsEnabled: boolean;
  invoiceNotificationsEnabled: boolean;
  customerNotificationsEnabled: boolean;
  projectNotificationsEnabled: boolean;
}

export interface UpdateBusinessProfileRequest {
  name: string;
  businessType: string;
  email: string;
  phone: string;
  address: string;
  website?: string | null;
  description?: string | null;
}
