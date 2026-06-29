import { PaginationParams } from './pagination.model';

export type ActivityEntityType = 'Customer' | 'Project' | 'Task' | 'Invoice' | 'Expense' | 'Settings';

export interface ActivityDto {
  id: string;
  userId: string;
  userName: string;
  action: string;
  entityType: ActivityEntityType;
  entityId: string;
  entityName: string;
  metadata?: string | null;
  createdAt: string;
  description: string;
}

export interface ActivityQueryParams extends PaginationParams {
  search?: string;
  entityType?: ActivityEntityType | '';
  dateFrom?: string;
  dateTo?: string;
}

export type ActivityDatePreset = 'today' | 'last7' | 'last30' | 'custom';

export const ACTIVITY_ENTITY_TYPES: { value: ActivityEntityType | ''; label: string }[] = [
  { value: '', label: 'All types' },
  { value: 'Customer', label: 'Customer' },
  { value: 'Project', label: 'Project' },
  { value: 'Task', label: 'Task' },
  { value: 'Invoice', label: 'Invoice' },
  { value: 'Expense', label: 'Expense' },
  { value: 'Settings', label: 'Settings' },
];

export const ACTIVITY_DATE_PRESETS: { value: ActivityDatePreset; label: string }[] = [
  { value: 'today', label: 'Today' },
  { value: 'last7', label: 'Last 7 Days' },
  { value: 'last30', label: 'Last 30 Days' },
  { value: 'custom', label: 'Custom Range' },
];
