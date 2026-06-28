import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { UserService } from '../../../core/services/user.service';
import { RoleService } from '../../../core/services/role.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TokenService } from '../../../core/services/token.service';
import { UserDto } from '../../../core/models/user.model';
import { RoleDto } from '../../../core/models/role.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { ButtonVariant } from '../../../core/enums';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppBadgeComponent } from '../../../shared/components/app-badge/app-badge.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppInputComponent } from '../../../shared/components/app-input/app-input.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppConfirmDialogComponent } from '../../../shared/components/app-confirm-dialog/app-confirm-dialog.component';

@Component({
  selector: 'app-user-detail',
  standalone: true,
  imports: [
    RouterLink,
    DatePipe,
    ReactiveFormsModule,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppCardComponent,
    AppBadgeComponent,
    AppButtonComponent,
    AppInputComponent,
    AppSkeletonComponent,
    AppConfirmDialogComponent,
  ],
  templateUrl: './user-detail.component.html',
  styleUrl: './user-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserDetailComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);
  private readonly userService = inject(UserService);
  private readonly roleService = inject(RoleService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly user = signal<UserDto | null>(null);
  readonly roles = signal<RoleDto[]>([]);
  readonly loading = signal(true);
  readonly actionLoading = signal(false);
  readonly showResetPassword = signal(false);
  readonly showDeactivateConfirm = signal(false);
  readonly selectedRoleId = signal('');
  readonly routes = ROUTES;
  readonly canUpdate = this.tokenService.hasPermission(PermissionCodes.user.update);

  readonly resetForm = this.fb.nonNullable.group({
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;

    this.roleService.getAll().subscribe({ next: (r) => this.roles.set(r) });

    this.userService.getById(id).subscribe({
      next: (u) => {
        this.user.set(u);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  headerActions() {
    const actions = [];
    if (this.canUpdate) {
      actions.push({
        label: 'Edit',
        route: `${ROUTES.users.base}/${this.user()?.id}/edit`,
        icon: '✏️',
        variant: ButtonVariant.Outline,
      });
    }
    return actions;
  }

  assignRole(): void {
    const user = this.user();
    const roleId = this.selectedRoleId();
    if (!user || !roleId) return;

    this.actionLoading.set(true);
    this.userService.assignRole(user.id, { roleId }).subscribe({
      next: () => {
        this.notification.success('Role assigned.');
        this.refreshUser(user.id);
        this.selectedRoleId.set('');
        this.actionLoading.set(false);
      },
      error: () => {
        this.notification.error('Failed to assign role.');
        this.actionLoading.set(false);
      },
    });
  }

  removeRole(roleName: string): void {
    const user = this.user();
    const role = this.roles().find((r) => r.name === roleName);
    if (!user || !role) return;

    this.actionLoading.set(true);
    this.userService.removeRole(user.id, role.id).subscribe({
      next: () => {
        this.notification.success('Role removed.');
        this.refreshUser(user.id);
        this.actionLoading.set(false);
      },
      error: () => {
        this.notification.error('Failed to remove role.');
        this.actionLoading.set(false);
      },
    });
  }

  toggleActive(): void {
    const user = this.user();
    if (!user) return;

    if (user.isActive) {
      this.showDeactivateConfirm.set(true);
      return;
    }

    this.actionLoading.set(true);
    this.userService.activate(user.id).subscribe({
      next: () => {
        this.notification.success('User activated.');
        this.refreshUser(user.id);
        this.actionLoading.set(false);
      },
      error: () => {
        this.notification.error('Failed to activate user.');
        this.actionLoading.set(false);
      },
    });
  }

  deactivateUser(): void {
    const user = this.user();
    if (!user) return;

    this.actionLoading.set(true);
    this.userService.deactivate(user.id).subscribe({
      next: () => {
        this.notification.success('User deactivated.');
        this.showDeactivateConfirm.set(false);
        this.refreshUser(user.id);
        this.actionLoading.set(false);
      },
      error: () => {
        this.notification.error('Failed to deactivate user.');
        this.actionLoading.set(false);
      },
    });
  }

  submitResetPassword(): void {
    if (this.resetForm.invalid) {
      this.resetForm.markAllAsTouched();
      return;
    }

    const user = this.user();
    if (!user) return;

    this.actionLoading.set(true);
    this.userService.resetPassword(user.id, { newPassword: this.resetForm.controls.newPassword.value }).subscribe({
      next: () => {
        this.notification.success('Password reset successfully.');
        this.showResetPassword.set(false);
        this.resetForm.reset();
        this.actionLoading.set(false);
      },
      error: () => {
        this.notification.error('Failed to reset password.');
        this.actionLoading.set(false);
      },
    });
  }

  availableRoles(): RoleDto[] {
    const assigned = this.user()?.roles ?? [];
    return this.roles().filter((r) => !assigned.includes(r.name));
  }

  private refreshUser(id: string): void {
    this.userService.getById(id).subscribe({ next: (u) => this.user.set(u) });
  }
}
