export interface TeamMemberDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  fullName: string;
  avatarUrl?: string | null;
  roles: string[];
  primaryRole?: string | null;
  joinedAt: string;
  isActive: boolean;
  lastActiveAt?: string | null;
  status: string;
}

export interface TeamDashboardDto {
  totalMembers: number;
  activeMembers: number;
  pendingInvitations: number;
  roleDistribution: RoleDistributionDto[];
  recentActivity: TeamActivityDto[];
  assignedTasks: AssignedTaskSummaryDto[];
}

export interface RoleDistributionDto {
  roleName: string;
  count: number;
}

export interface TeamActivityDto {
  id: string;
  userId: string;
  userName: string;
  action: string;
  entityType: string;
  entityName: string;
  createdAt: string;
}

export interface AssignedTaskSummaryDto {
  id: string;
  title: string;
  projectName: string;
  assignedUserName?: string | null;
  status: string;
  dueDate?: string | null;
}

export interface TeamInvitationDto {
  id: string;
  email: string;
  roleName: string;
  status: string;
  expiresAt: string;
  createdAt: string;
}

export interface InviteTeamMemberRequest {
  email: string;
  roleId: string;
  firstName?: string;
  lastName?: string;
}

export interface UpdateTeamMemberRequest {
  firstName: string;
  lastName: string;
  avatarUrl?: string | null;
  roleId?: string | null;
  isActive: boolean;
}

export interface OrganizationDto {
  id: string;
  name: string;
  businessType: string;
  email: string;
  phone: string;
  address: string;
  website?: string | null;
  description?: string | null;
  logoUrl?: string | null;
  currency: string;
  timezone: string;
  subscriptionPlan: string;
  isActive: boolean;
}

export interface UpdateOrganizationRequest {
  name: string;
  businessType: string;
  email: string;
  phone: string;
  address: string;
  website?: string | null;
  description?: string | null;
  logoUrl?: string | null;
  currency: string;
  timezone: string;
}
