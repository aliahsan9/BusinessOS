export interface HealthCheckItem {
  name: string;
  status: string;
  message?: string | null;
}

export interface SystemHealth {
  status: string;
  databaseConnected: boolean;
  checkedAt: string;
  checks: HealthCheckItem[];
}

export interface SystemStats {
  totalTenants: number;
  totalUsers: number;
  totalProducts: number;
  totalCustomers: number;
  totalOrders: number;
  totalInvoices: number;
  totalPayments: number;
  totalExpenses: number;
  totalNotifications: number;
  totalAuditLogs: number;
  generatedAt: string;
}

export interface EnvironmentInfo {
  environment: string;
  applicationName: string;
  version: string;
  framework: string;
  machineName: string;
  osVersion: string;
  serverTimeUtc: string;
}
