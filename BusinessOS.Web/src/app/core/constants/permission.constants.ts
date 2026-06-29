export const PermissionCodes = {
  category: {
    create: 'Category.Create',
    view: 'Category.View',
    update: 'Category.Update',
    delete: 'Category.Delete',
  },
  product: {
    create: 'Product.Create',
    view: 'Product.View',
    update: 'Product.Update',
    delete: 'Product.Delete',
  },
  customer: {
    create: 'Customer.Create',
    view: 'Customer.View',
    update: 'Customer.Update',
    delete: 'Customer.Delete',
  },
  order: {
    create: 'Order.Create',
    view: 'Order.View',
    update: 'Order.Update',
    delete: 'Order.Delete',
  },
  inventory: {
    view: 'Inventory.View',
    update: 'Inventory.Update',
    adjust: 'Inventory.Adjust',
  },
  supplier: {
    create: 'Supplier.Create',
    view: 'Supplier.View',
    update: 'Supplier.Update',
    delete: 'Supplier.Delete',
  },
  purchaseOrder: {
    create: 'PurchaseOrder.Create',
    view: 'PurchaseOrder.View',
    update: 'PurchaseOrder.Update',
    delete: 'PurchaseOrder.Delete',
  },
  payment: {
    create: 'Payment.Create',
    view: 'Payment.View',
    update: 'Payment.Update',
    delete: 'Payment.Delete',
  },
  invoice: {
    create: 'Invoice.Create',
    view: 'Invoice.View',
    update: 'Invoice.Update',
    delete: 'Invoice.Delete',
  },
  quotation: {
    create: 'Quotation.Create',
    view: 'Quotation.View',
    update: 'Quotation.Update',
    delete: 'Quotation.Delete',
  },
  user: {
    create: 'User.Create',
    view: 'User.View',
    update: 'User.Update',
    delete: 'User.Delete',
  },
  role: {
    create: 'Role.Create',
    view: 'Role.View',
    update: 'Role.Update',
    delete: 'Role.Delete',
  },
  expense: {
    create: 'Expense.Create',
    view: 'Expense.View',
    update: 'Expense.Update',
    delete: 'Expense.Delete',
  },
  expenseCategory: {
    create: 'ExpenseCategory.Create',
    view: 'ExpenseCategory.View',
    update: 'ExpenseCategory.Update',
    delete: 'ExpenseCategory.Delete',
  },
  finance: {
    view: 'Finance.View',
  },
  audit: {
    view: 'Audit.View',
  },
  notification: {
    view: 'Notification.View',
    update: 'Notification.Update',
  },
  activity: {
    view: 'Activity.View',
  },
  report: {
    view: 'Report.View',
  },
  settings: {
    view: 'Settings.View',
    update: 'Settings.Update',
  },
  systemAdmin: {
    view: 'SystemAdmin.View',
  },
  project: {
    create: 'Projects.Create',
    view: 'Projects.View',
    update: 'Projects.Update',
    delete: 'Projects.Delete',
  },
  task: {
    create: 'Tasks.Create',
    view: 'Tasks.View',
    update: 'Tasks.Update',
    delete: 'Tasks.Delete',
  },
  analytics: {
    view: 'Analytics.View',
  },
  team: {
    view: 'Team.View',
    manage: 'Team.Manage',
    invite: 'Team.Invite',
  },
  organization: {
    view: 'Organization.View',
    manage: 'Organization.Manage',
  },
  tenant: {
    view: 'Tenant.View',
    manage: 'Tenant.Manage',
  },
  subscription: {
    view: 'Subscription.View',
    manage: 'Subscription.Manage',
  },
} as const;

export const RoleNames = {
  owner: 'Owner',
  admin: 'Admin',
  manager: 'Manager',
  employee: 'Employee',
  accountant: 'Accountant',
  sales: 'Sales',
  inventoryManager: 'InventoryManager',
  viewer: 'Viewer',
} as const;

export type PermissionCode =
  (typeof PermissionCodes)[keyof typeof PermissionCodes][keyof (typeof PermissionCodes)[keyof typeof PermissionCodes]];
