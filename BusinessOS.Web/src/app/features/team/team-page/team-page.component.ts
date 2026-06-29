import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TeamService } from '../../../core/services/team.service';
import { RoleService } from '../../../core/services/role.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TokenService } from '../../../core/services/token.service';
import {
  AssignedTaskSummaryDto,
  RoleDistributionDto,
  TeamActivityDto,
  TeamDashboardDto,
  TeamInvitationDto,
  TeamMemberDto,
} from '../../../core/models/team.model';
import { RoleDto } from '../../../core/models/role.model';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { ButtonVariant } from '../../../core/enums';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppSearchBarComponent } from '../../../shared/components/app-search-bar/app-search-bar.component';
import { AppPaginationComponent } from '../../../shared/components/app-pagination/app-pagination.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppBadgeComponent } from '../../../shared/components/app-badge/app-badge.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { AppConfirmDialogComponent } from '../../../shared/components/app-confirm-dialog/app-confirm-dialog.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppInputComponent } from '../../../shared/components/app-input/app-input.component';
import { ROUTES } from '../../../core/constants/route.constants';

@Component({
  selector: 'app-team-page',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppSearchBarComponent,
    AppPaginationComponent,
    AppButtonComponent,
    AppBadgeComponent,
    AppSkeletonComponent,
    AppEmptyStateComponent,
    AppAlertComponent,
    AppConfirmDialogComponent,
    AppCardComponent,
    AppInputComponent,
  ],
  templateUrl: './team-page.component.html',
  styleUrl: './team-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TeamPageComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly teamService = inject(TeamService);
  private readonly roleService = inject(RoleService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly dashboard = signal<TeamDashboardDto | null>(null);
  readonly members = signal<TeamMemberDto[]>([]);
  readonly invitations = signal<TeamInvitationDto[]>([]);
  readonly roles = signal<RoleDto[]>([]);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly loading = signal(false);
  readonly dashboardLoading = signal(false);
  readonly error = signal<string | null>(null);
  readonly searchTerm = signal('');
  readonly statusFilter = signal('');
  readonly roleFilter = signal('');

  readonly showInviteModal = signal(false);
  readonly showEditModal = signal(false);
  readonly showRemoveConfirm = signal(false);
  readonly saving = signal(false);
  readonly selectedMember = signal<TeamMemberDto | null>(null);

  inviteEmail = '';
  inviteRoleId = '';
  editFirstName = '';
  editLastName = '';
  editAvatarUrl = '';
  editRoleId = '';
  editIsActive = true;

  readonly canInvite = this.tokenService.hasPermission(PermissionCodes.team.invite);
  readonly canManage = this.tokenService.hasPermission(PermissionCodes.team.manage);
  readonly breadcrumbs = [{ label: 'Team', route: ROUTES.team.list }, { label: 'Management' }];
  readonly headerActions = this.canInvite
    ? [{ label: 'Invite Member', icon: '✉️', action: () => this.openInviteModal() }]
    : [];

  ngOnInit(): void {
    this.loadDashboard();
    this.loadMembers();
    this.loadInvitations();
    if (this.canInvite || this.canManage) {
      this.roleService.getAll().subscribe({
        next: (roles) => this.roles.set(roles.filter((r) => r.name !== 'Owner')),
      });
    }
  }

  loadDashboard(): void {
    this.dashboardLoading.set(true);
    this.teamService.getDashboard().subscribe({
      next: (data) => {
        this.dashboard.set(data);
        this.dashboardLoading.set(false);
      },
      error: () => this.dashboardLoading.set(false),
    });
  }

  loadMembers(page = this.page()): void {
    this.loading.set(true);
    this.error.set(null);
    this.teamService
      .getMembers({
        page,
        pageSize: this.pageSize(),
        search: this.searchTerm() || undefined,
        status: this.statusFilter() || undefined,
        role: this.roleFilter() || undefined,
      })
      .subscribe({
        next: (result) => {
          this.members.set(result.items);
          this.page.set(result.page);
          this.totalCount.set(result.totalCount);
          this.totalPages.set(result.totalPages);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Failed to load team members.');
          this.loading.set(false);
        },
      });
  }

  loadInvitations(): void {
    if (!this.canInvite) return;
    this.teamService.getPendingInvitations().subscribe({
      next: (items) => this.invitations.set(items),
    });
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.loadMembers(1);
  }

  onFilterChange(): void {
    this.loadMembers(1);
  }

  onPageChange(page: number): void {
    this.loadMembers(page);
  }

  openInviteModal(): void {
    this.inviteEmail = '';
    this.inviteRoleId = this.roles()[0]?.id ?? '';
    this.showInviteModal.set(true);
  }

  submitInvite(): void {
    if (!this.inviteEmail.trim() || !this.inviteRoleId) return;
    this.saving.set(true);
    this.teamService
      .invite({ email: this.inviteEmail.trim(), roleId: this.inviteRoleId })
      .subscribe({
        next: () => {
          this.notification.success('Invitation sent successfully.');
          this.showInviteModal.set(false);
          this.saving.set(false);
          this.loadInvitations();
          this.loadDashboard();
        },
        error: (err) => {
          this.notification.error(err?.message ?? 'Failed to send invitation.');
          this.saving.set(false);
        },
      });
  }

  openEditModal(member: TeamMemberDto): void {
    this.selectedMember.set(member);
    this.editFirstName = member.firstName;
    this.editLastName = member.lastName;
    this.editAvatarUrl = member.avatarUrl ?? '';
    this.editRoleId = this.roles().find((r) => member.roles.includes(r.name))?.id ?? '';
    this.editIsActive = member.isActive;
    this.showEditModal.set(true);
  }

  submitEdit(): void {
    const member = this.selectedMember();
    if (!member) return;
    this.saving.set(true);
    this.teamService
      .updateMember(member.id, {
        firstName: this.editFirstName.trim(),
        lastName: this.editLastName.trim(),
        avatarUrl: this.editAvatarUrl.trim() || null,
        roleId: this.editRoleId || null,
        isActive: this.editIsActive,
      })
      .subscribe({
        next: () => {
          this.notification.success('Team member updated.');
          this.showEditModal.set(false);
          this.saving.set(false);
          this.loadMembers();
          this.loadDashboard();
        },
        error: (err) => {
          this.notification.error(err?.message ?? 'Failed to update member.');
          this.saving.set(false);
        },
      });
  }

  confirmRemove(member: TeamMemberDto): void {
    this.selectedMember.set(member);
    this.showRemoveConfirm.set(true);
  }

  removeMember(): void {
    const member = this.selectedMember();
    if (!member) return;
    this.teamService.removeMember(member.id).subscribe({
      next: () => {
        this.notification.success('Team member removed.');
        this.showRemoveConfirm.set(false);
        this.loadMembers();
        this.loadDashboard();
      },
      error: (err) => this.notification.error(err?.message ?? 'Failed to remove member.'),
    });
  }

  roleBadgeVariant(role: string): 'primary' | 'success' | 'warning' | 'neutral' | 'danger' {
    const map: Record<string, 'primary' | 'success' | 'warning' | 'neutral' | 'danger'> = {
      Owner: 'primary',
      Admin: 'primary',
      Manager: 'success',
      Employee: 'neutral',
      Accountant: 'warning',
      Viewer: 'neutral',
    };
    return map[role] ?? 'neutral';
  }

  initials(name: string): string {
    return name
      .split(' ')
      .map((p) => p[0])
      .join('')
      .slice(0, 2)
      .toUpperCase();
  }

  trackById(_: number, item: { id: string }): string {
    return item.id;
  }
}
