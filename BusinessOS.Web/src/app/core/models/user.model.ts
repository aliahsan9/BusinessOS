import { PaginationParams } from './pagination.model';

export interface UserSummaryDto {
  id: string;
  email: string;
  fullName: string;
  isActive: boolean;
  roles: string[];
}

export interface UserDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  fullName: string;
  tenantId: string;
  isActive: boolean;
  roles: string[];
  createdAt?: string | null;
}

export interface CreateUserRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  tenantId: string;
}

export interface UpdateUserRequest {
  email: string;
  firstName: string;
  lastName: string;
  isActive: boolean;
}

export interface ResetPasswordRequest {
  newPassword: string;
}

export interface AssignUserRoleRequest {
  roleId: string;
}

export interface UserQueryParams extends PaginationParams {}
