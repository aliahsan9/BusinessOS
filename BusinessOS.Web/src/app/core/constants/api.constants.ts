export const API_ENDPOINTS = {
  auth: {
    login: '/auth/login',
    register: '/auth/register',
    forgotPassword: '/auth/forgot-password',
    resetPassword: '/auth/reset-password',
  },
  dashboard: {
    overview: '/dashboard/overview',
    sales: '/dashboard/sales',
    customers: '/dashboard/customers',
    products: '/dashboard/products',
    inventory: '/dashboard/inventory',
    orders: '/dashboard/orders',
    charts: {
      revenue: '/dashboard/charts/revenue',
      orders: '/dashboard/charts/orders',
      customers: '/dashboard/charts/customers',
      products: '/dashboard/charts/products',
      inventory: '/dashboard/charts/inventory',
    },
  },
  users: '/users',
  roles: '/roles',
  permissions: '/permissions',
  customers: '/customers',
  products: '/products',
  categories: '/categories',
  inventory: '/inventory',
  orders: '/orders',
} as const;

export const HTTP_HEADERS = {
  tenantId: 'X-Tenant-ID',
  authorization: 'Authorization',
  contentType: 'Content-Type',
} as const;

export const RETRY_CONFIG = {
  maxRetries: 2,
  delayMs: 1000,
  retryableStatuses: [408, 429, 500, 502, 503, 504],
} as const;
