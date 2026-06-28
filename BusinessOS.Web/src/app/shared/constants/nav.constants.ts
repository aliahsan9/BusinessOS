import { ROUTES } from '../../core/constants/route.constants';

/** @deprecated Use ROUTES from core/constants/route.constants.ts */
export const APP_ROUTE_PATHS = {
  dashboard: ROUTES.dashboard,
  users: ROUTES.users,
  roles: ROUTES.roles,
  permissions: ROUTES.permissions,
  customers: ROUTES.customers,
  products: ROUTES.products,
  inventory: ROUTES.inventory,
  orders: ROUTES.orders,
  invoices: ROUTES.invoices,
  reports: ROUTES.reports,
  settings: ROUTES.settings,
  profile: ROUTES.profile,
  onboarding: ROUTES.onboarding.base,
  forbidden: ROUTES.forbidden,
  notFound: ROUTES.notFound,
} as const;

export { ROUTES };

export interface NavItem {
  label: string;
  icon: string;
  route: string;
  description: string;
  permissions?: string[];
}

export const NAV_ITEMS: NavItem[] = [
  {
    label: 'Dashboard',
    icon: '📊',
    route: ROUTES.dashboard,
    description: 'Overview of sales, orders, inventory, and key metrics.',
  },
  {
    label: 'Users',
    icon: '👥',
    route: ROUTES.users,
    description: 'Manage user accounts and access.',
    permissions: ['User.View'],
  },
  {
    label: 'Roles',
    icon: '🛡️',
    route: ROUTES.roles,
    description: 'Configure roles and assign permissions.',
    permissions: ['Role.View'],
  },
  {
    label: 'Permissions',
    icon: '🔐',
    route: ROUTES.permissions,
    description: 'Review permission definitions across the system.',
    permissions: ['Role.View'],
  },
  {
    label: 'Customers',
    icon: '🤝',
    route: ROUTES.customers,
    description: 'Manage customer records and relationships.',
    permissions: ['Customer.View'],
  },
  {
    label: 'Products',
    icon: '📦',
    route: ROUTES.products,
    description: 'Manage product catalog and pricing.',
    permissions: ['Product.View'],
  },
  {
    label: 'Inventory',
    icon: '🏭',
    route: ROUTES.inventory,
    description: 'Track stock levels and inventory movements.',
    permissions: ['Inventory.View'],
  },
  {
    label: 'Orders',
    icon: '🛒',
    route: ROUTES.orders,
    description: 'View and manage customer orders.',
    permissions: ['Order.View'],
  },
  {
    label: 'Invoices',
    icon: '🧾',
    route: ROUTES.invoices,
    description: 'Review billing and invoice history.',
    permissions: ['Order.View'],
  },
  {
    label: 'Reports',
    icon: '📈',
    route: ROUTES.reports,
    description: 'Analyze business performance and trends.',
    permissions: ['Order.View'],
  },
  {
    label: 'Settings',
    icon: '⚙️',
    route: ROUTES.settings,
    description: 'Configure application and tenant settings.',
  },
];
