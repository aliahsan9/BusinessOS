import { ROUTES } from '../../core/constants/route.constants';

/** @deprecated Use ROUTES from core/constants/route.constants.ts */
export const APP_ROUTE_PATHS = {
  dashboard: ROUTES.dashboard,
  users: ROUTES.users,
  roles: ROUTES.roles,
  permissions: ROUTES.permissions,
  customers: ROUTES.customers.base,
  products: ROUTES.products.base,
  inventory: ROUTES.inventory.base,
  suppliers: ROUTES.suppliers.base,
  purchaseOrders: ROUTES.purchaseOrders.base,
  orders: ROUTES.orders.base,
  quotations: ROUTES.quotations.base,
  invoices: ROUTES.invoices.base,
  payments: ROUTES.payments.base,
  sales: ROUTES.sales.base,
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
    route: ROUTES.customers.base,
    description: 'Manage customer records and relationships.',
    permissions: ['Customer.View'],
  },
  {
    label: 'Products',
    icon: '📦',
    route: ROUTES.products.base,
    description: 'Manage product catalog and pricing.',
    permissions: ['Product.View'],
  },
  {
    label: 'Inventory',
    icon: '🏭',
    route: ROUTES.inventory.base,
    description: 'Track stock levels and inventory movements.',
    permissions: ['Inventory.View'],
  },
  {
    label: 'Suppliers',
    icon: '🚚',
    route: ROUTES.suppliers.base,
    description: 'Manage supplier contacts and purchase relationships.',
    permissions: ['Supplier.View'],
  },
  {
    label: 'Purchase Orders',
    icon: '📋',
    route: ROUTES.purchaseOrders.base,
    description: 'Create and track purchase orders from suppliers.',
    permissions: ['PurchaseOrder.View'],
  },
  {
    label: 'Orders',
    icon: '🛒',
    route: ROUTES.orders.base,
    description: 'View and manage customer orders.',
    permissions: ['Order.View'],
  },
  {
    label: 'Quotations',
    icon: '📝',
    route: ROUTES.quotations.base,
    description: 'Create and track customer quotations.',
    permissions: ['Quotation.View'],
  },
  {
    label: 'Invoices',
    icon: '🧾',
    route: ROUTES.invoices.base,
    description: 'Review billing and invoice history.',
    permissions: ['Invoice.View'],
  },
  {
    label: 'Payments',
    icon: '💳',
    route: ROUTES.payments.base,
    description: 'Record and track customer payments.',
    permissions: ['Payment.View'],
  },
  {
    label: 'Sales Dashboard',
    icon: '💰',
    route: ROUTES.sales.dashboard,
    description: 'Sales KPIs, revenue trends, and top products.',
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
