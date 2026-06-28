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
  emailNotificationsEnabled: boolean;
  systemNotificationsEnabled: boolean;
  orderNotificationsEnabled: boolean;
  inventoryAlertsEnabled: boolean;
  paymentAlertsEnabled: boolean;
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
  emailNotificationsEnabled: boolean;
  systemNotificationsEnabled: boolean;
  orderNotificationsEnabled: boolean;
  inventoryAlertsEnabled: boolean;
  paymentAlertsEnabled: boolean;
}

export interface UpdateBusinessProfileRequest {
  name: string;
  businessType: string;
  email: string;
  phone: string;
  address: string;
}
