import { ROUTES } from '../../core/constants/route.constants';

/** @deprecated Use ROUTES from core/constants/route.constants.ts */
export const APP_ROUTE_PATHS = {
  dashboard: ROUTES.dashboard,
  users: ROUTES.users.base,
  roles: ROUTES.roles.base,
  permissions: ROUTES.permissions.base,
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
  expenses: ROUTES.expenses.base,
  expenseCategories: ROUTES.expenseCategories.base,
  finance: ROUTES.finance.base,
  audit: ROUTES.audit.base,
  notifications: ROUTES.notifications.base,
  settings: ROUTES.settings.base,
  admin: ROUTES.admin.base,
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
  group?: 'Finance' | 'Administration';
}

/** Compact quick links shown in the top navbar (sidebar holds full navigation). */
export const TOP_NAV_ITEMS: NavItem[] = [
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
    permissions: ['Report.View'],
  },
  {
    label: 'Financial Dashboard',
    icon: '💹',
    route: ROUTES.finance.dashboard,
    description: 'Financial KPIs, revenue and expense trends.',
    permissions: ['Finance.View'],
  },
];

export const NAV_ITEMS: NavItem[] = [
  {
    label: 'Dashboard',
    icon: '📊',
    route: ROUTES.dashboard,
    description: 'Overview of sales, orders, inventory, and key metrics.',
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
    label: 'Customers',
    icon: '🤝',
    route: ROUTES.customers.base,
    description: 'Manage customer records and relationships.',
    permissions: ['Customer.View'],
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
    permissions: ['Report.View'],
  },
  {
    label: 'Business Analytics',
    icon: '📉',
    route: ROUTES.analytics.base,
    description: 'Advanced analytics across customers, projects, revenue, and expenses.',
    permissions: ['Report.View'],
  },
  {
    label: 'Financial Dashboard',
    icon: '💹',
    route: ROUTES.finance.dashboard,
    description: 'Financial KPIs, revenue and expense trends.',
    permissions: ['Finance.View'],
    group: 'Finance',
  },
  {
    label: 'Expenses',
    icon: '💸',
    route: ROUTES.expenses.base,
    description: 'Track and manage business expenses.',
    permissions: ['Expense.View'],
    group: 'Finance',
  },
  {
    label: 'Expense Categories',
    icon: '🏷️',
    route: ROUTES.expenseCategories.base,
    description: 'Organize expenses into categories.',
    permissions: ['ExpenseCategory.View'],
    group: 'Finance',
  },
  {
    label: 'Profit & Loss',
    icon: '📉',
    route: ROUTES.finance.profitLoss,
    description: 'Analyze revenue, expenses, and profitability.',
    permissions: ['Finance.View'],
    group: 'Finance',
  },
  {
    label: 'Users',
    icon: '👥',
    route: ROUTES.users.base,
    description: 'Manage user accounts and access.',
    permissions: ['User.View'],
    group: 'Administration',
  },
  {
    label: 'Roles',
    icon: '🛡️',
    route: ROUTES.roles.base,
    description: 'Configure roles and assign permissions.',
    permissions: ['Role.View'],
    group: 'Administration',
  },
  {
    label: 'Permissions',
    icon: '🔐',
    route: ROUTES.permissions.base,
    description: 'Review permission definitions across the system.',
    permissions: ['Role.View'],
    group: 'Administration',
  },
  {
    label: 'Audit Logs',
    icon: '📋',
    route: ROUTES.audit.base,
    description: 'Review system audit trail and entity changes.',
    permissions: ['Audit.View'],
    group: 'Administration',
  },
  {
    label: 'Notifications',
    icon: '🔔',
    route: ROUTES.notifications.base,
    description: 'View and manage notifications.',
    permissions: ['Notification.View'],
    group: 'Administration',
  },
  {
    label: 'Settings',
    icon: '⚙️',
    route: ROUTES.settings.base,
    description: 'Configure application and tenant settings.',
    permissions: ['Settings.View'],
    group: 'Administration',
  },
  {
    label: 'System Admin',
    icon: '🖥️',
    route: ROUTES.admin.base,
    description: 'Monitor system health, stats, and environment.',
    permissions: ['SystemAdmin.View'],
    group: 'Administration',
  },
];
