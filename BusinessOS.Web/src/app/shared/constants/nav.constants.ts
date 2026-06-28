export interface NavItem {
  label: string;
  icon: string;
  route: string;
  permissions?: string[];
}

export const NAV_ITEMS: NavItem[] = [
  { label: 'Dashboard', icon: '📊', route: '/dashboard' },
  { label: 'Users', icon: '👥', route: '/users', permissions: ['User.View'] },
  { label: 'Roles', icon: '🛡️', route: '/roles', permissions: ['Role.View'] },
  { label: 'Permissions', icon: '🔐', route: '/permissions', permissions: ['Role.View'] },
  { label: 'Customers', icon: '🤝', route: '/customers', permissions: ['Customer.View'] },
  { label: 'Products', icon: '📦', route: '/products', permissions: ['Product.View'] },
  { label: 'Inventory', icon: '🏭', route: '/inventory', permissions: ['Inventory.View'] },
  { label: 'Orders', icon: '🛒', route: '/orders', permissions: ['Order.View'] },
  { label: 'Invoices', icon: '🧾', route: '/invoices', permissions: ['Order.View'] },
  { label: 'Reports', icon: '📈', route: '/reports', permissions: ['Order.View'] },
  { label: 'Settings', icon: '⚙️', route: '/settings' },
];
