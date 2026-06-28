import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RoleService } from '../../../core/services/role.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ROUTES } from '../../../core/constants/route.constants';
import { ButtonVariant } from '../../../core/enums';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppInputComponent } from '../../../shared/components/app-input/app-input.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { getFieldError } from '../../../shared/validators/form.validators';

@Component({
  selector: 'app-role-form',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppInputComponent,
    AppButtonComponent,
    AppCardComponent,
    AppSkeletonComponent,
  ],
  templateUrl: './role-form.component.html',
  styleUrl: './role-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoleFormComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly roleService = inject(RoleService);
  private readonly notification = inject(NotificationService);

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly isEdit = signal(false);
  readonly roleId = signal<string | null>(null);
  readonly routes = ROUTES;

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', [Validators.maxLength(500)]],
    isActive: [true],
  });

  ngOnInit(): void {
    const mode = this.route.snapshot.data['mode'] as string;
    const id = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(mode === 'edit' && !!id);
    if (id) this.roleId.set(id);

    if (this.isEdit() && id) {
      this.loading.set(true);
      this.roleService.getById(id).subscribe({
        next: (role) => {
          this.form.patchValue({
            name: role.name,
            description: role.description ?? '',
            isActive: role.isActive,
          });
          this.loading.set(false);
        },
        error: () => {
          this.notification.error('Failed to load role.');
          this.loading.set(false);
        },
      });
    }
  }

  fieldError(field: string): string | null {
    return getFieldError(this.form.get(field), field);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const value = this.form.getRawValue();

    if (this.isEdit() && this.roleId()) {
      this.roleService.update(this.roleId()!, value).subscribe({
        next: () => {
          this.notification.success('Role updated.');
          this.router.navigate([ROUTES.roles.base, this.roleId()]);
        },
        error: () => {
          this.notification.error('Failed to update role.');
          this.saving.set(false);
        },
      });
    } else {
      this.roleService.create({ name: value.name, description: value.description || null, isActive: value.isActive }).subscribe({
        next: (role) => {
          this.notification.success('Role created.');
          this.router.navigate([ROUTES.roles.base, role.id]);
        },
        error: () => {
          this.notification.error('Failed to create role.');
          this.saving.set(false);
        },
      });
    }
  }
}
