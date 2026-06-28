export interface PermissionDto {
  id: string;
  name: string;
  code: string;
  description?: string | null;
  category: string;
}

export interface RoleDto {
  id: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  createdAt: string;
  permissions: string[];
}

export interface CreateRoleRequest {
  name: string;
  description?: string | null;
  isActive?: boolean;
}

export interface UpdateRoleRequest {
  name: string;
  description?: string | null;
  isActive: boolean;
}

export interface AssignPermissionRequest {
  permissionId: string;
}
