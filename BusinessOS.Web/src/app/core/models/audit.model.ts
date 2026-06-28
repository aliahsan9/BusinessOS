import { PaginationParams } from './pagination.model';

export interface AuditLogDto {
  id: string;
  actorUserId: string;
  action: string;
  entityType: string;
  entityId: string;
  oldValue?: string | null;
  newValue?: string | null;
  createdAt: string;
}

export interface AuditLogQueryParams extends PaginationParams {
  action?: string;
  entityType?: string;
  userId?: string;
  dateFrom?: string;
  dateTo?: string;
}
