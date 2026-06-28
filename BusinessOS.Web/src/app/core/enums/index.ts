export enum OrderStatus {
  Pending = 'Pending',
  Confirmed = 'Confirmed',
  Processing = 'Processing',
  Completed = 'Completed',
  Cancelled = 'Cancelled',
}

export enum DashboardPeriod {
  Today = 'today',
  Week = 'week',
  Month = 'month',
  Year = 'year',
  All = 'all',
  Custom = 'custom',
}

export enum StockTransactionType {
  Purchase = 'Purchase',
  Sale = 'Sale',
  Adjustment = 'Adjustment',
  Return = 'Return',
  Damage = 'Damage',
  Transfer = 'Transfer',
}

export enum PurchaseOrderStatus {
  Draft = 'Draft',
  Pending = 'Pending',
  Approved = 'Approved',
  Received = 'Received',
  Cancelled = 'Cancelled',
}

export enum ProductStatus {
  Active = 'Active',
  Inactive = 'Inactive',
}

export enum ToastType {
  Success = 'success',
  Error = 'error',
  Warning = 'warning',
  Info = 'info',
}

export enum ButtonVariant {
  Primary = 'primary',
  Secondary = 'secondary',
  Success = 'success',
  Danger = 'danger',
  Warning = 'warning',
  Outline = 'outline',
  Ghost = 'ghost',
}

export enum ButtonSize {
  Sm = 'sm',
  Md = 'md',
  Lg = 'lg',
}

export enum ThemeMode {
  Light = 'light',
  Dark = 'dark',
  System = 'system',
}
