import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { UserService } from '../../../core/services/user.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TokenService } from '../../../core/services/token.service';
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
  selector: 'app-user-form',
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
  templateUrl: './user-form.component.html',
  styleUrl: './user-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserFormComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly userService = inject(UserService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly isEdit = signal(false);
  readonly userId = signal<string | null>(null);
  readonly routes = ROUTES;

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(200)]],
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    lastName: ['', [Validators.required, Validators.maxLength(100)]],
    password: [''],
    isActive: [true],
  });

  ngOnInit(): void {
    const mode = this.route.snapshot.data['mode'] as string;
    const id = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(mode === 'edit' && !!id);
    if (id) this.userId.set(id);

    if (this.isEdit()) {
      this.form.controls.password.clearValidators();
    } else {
      this.form.controls.password.setValidators([Validators.required, Validators.minLength(8)]);
    }
    this.form.controls.password.updateValueAndValidity();

    if (this.isEdit() && id) {
      this.loading.set(true);
      this.userService.getById(id).subscribe({
        next: (user) => {
          this.form.patchValue({
            email: user.email,
            firstName: user.firstName,
            lastName: user.lastName,
            isActive: user.isActive,
          });
          this.loading.set(false);
        },
        error: () => {
          this.notification.error('Failed to load user.');
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

    if (this.isEdit() && this.userId()) {
      this.userService
        .update(this.userId()!, {
          email: value.email,
          firstName: value.firstName,
          lastName: value.lastName,
          isActive: value.isActive,
        })
        .subscribe({
          next: () => {
            this.notification.success('User updated.');
            this.router.navigate([ROUTES.users.base, this.userId()]);
          },
          error: () => {
            this.notification.error('Failed to update user.');
            this.saving.set(false);
          },
        });
    } else {
      const tenantId = this.tokenService.tenantId();
      if (!tenantId) {
        this.notification.error('Tenant context is missing.');
        this.saving.set(false);
        return;
      }
      this.userService
        .create({
          email: value.email,
          password: value.password,
          firstName: value.firstName,
          lastName: value.lastName,
          tenantId,
        })
        .subscribe({
          next: (user) => {
            this.notification.success('User created.');
            this.router.navigate([ROUTES.users.base, user.id]);
          },
          error: () => {
            this.notification.error('Failed to create user.');
            this.saving.set(false);
          },
        });
    }
  }
}
