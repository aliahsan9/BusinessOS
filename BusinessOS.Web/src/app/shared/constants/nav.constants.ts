/** Route paths aligned with app.routes.ts */
export const APP_ROUTE_PATHS = {
  dashboard: '/dashboard',
  users: '/users',
  roles: '/roles',
  permissions: '/permissions',
  customers: '/customers',
  products: '/products',
  inventory: '/inventory',
  orders: '/orders',
  invoices: '/invoices',
  reports: '/reports',
  settings: '/settings',
  profile: '/profile',
} as const;

export interface NavItem {
  label: string;
  icon: string;
  route: string;
  permissions?: string[];
}

export const NAV_ITEMS: NavItem[] = [
  { label: 'Dashboard', icon: '📊', route: APP_ROUTE_PATHS.dashboard },
  { label: 'Users', icon: '👥', route: APP_ROUTE_PATHS.users, permissions: ['User.View'] },
  { label: 'Roles', icon: '🛡️', route: APP_ROUTE_PATHS.roles, permissions: ['Role.View'] },
  { label: 'Permissions', icon: '🔐', route: APP_ROUTE_PATHS.permissions, permissions: ['Role.View'] },
  { label: 'Customers', icon: '🤝', route: APP_ROUTE_PATHS.customers, permissions: ['Customer.View'] },
  { label: 'Products', icon: '📦', route: APP_ROUTE_PATHS.products, permissions: ['Product.View'] },
  { label: 'Inventory', icon: '🏭', route: APP_ROUTE_PATHS.inventory, permissions: ['Inventory.View'] },
  { label: 'Orders', icon: '🛒', route: APP_ROUTE_PATHS.orders, permissions: ['Order.View'] },
  { label: 'Invoices', icon: '🧾', route: APP_ROUTE_PATHS.invoices, permissions: ['Order.View'] },
  { label: 'Reports', icon: '📈', route: APP_ROUTE_PATHS.reports, permissions: ['Order.View'] },
  { label: 'Settings', icon: '⚙️', route: APP_ROUTE_PATHS.settings },
];
