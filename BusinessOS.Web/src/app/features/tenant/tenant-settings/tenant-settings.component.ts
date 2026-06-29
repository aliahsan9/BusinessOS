import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TenantService } from '../../../core/services/tenant.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TokenService } from '../../../core/services/token.service';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { ROUTES } from '../../../core/constants/route.constants';
import { ButtonVariant } from '../../../core/enums';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppInputComponent } from '../../../shared/components/app-input/app-input.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';

@Component({
  selector: 'app-tenant-settings',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppButtonComponent,
    AppInputComponent,
    AppCardComponent,
    AppSkeletonComponent,
    AppAlertComponent,
  ],
  templateUrl: './tenant-settings.component.html',
  styleUrl: './tenant-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TenantSettingsComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly fb = inject(FormBuilder);
  private readonly tenantService = inject(TenantService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly canManage = this.tokenService.hasPermission(PermissionCodes.tenant.manage);
  readonly breadcrumbs = [{ label: 'Tenant', route: ROUTES.tenant.dashboard }, { label: 'Settings' }];

  readonly form = this.fb.nonNullable.group({
    name: ['', Validators.required],
    logoUrl: [''],
    timezone: ['UTC', Validators.required],
    currency: ['USD', Validators.required],
    businessType: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    phone: ['', Validators.required],
    address: ['', Validators.required],
    website: [''],
    description: [''],
  });

  ngOnInit(): void {
    this.load();
    if (!this.canManage) {
      this.form.disable();
    }
  }

  load(): void {
    this.loading.set(true);
    this.tenantService.getSettings().subscribe({
      next: (settings) => {
        this.form.patchValue({
          name: settings.name,
          logoUrl: settings.logoUrl ?? '',
          timezone: settings.timezone,
          currency: settings.currency,
          businessType: settings.businessType,
          email: settings.email,
          phone: settings.phone,
          address: settings.address,
          website: settings.website ?? '',
          description: settings.description ?? '',
        });
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load tenant settings.');
        this.loading.set(false);
      },
    });
  }

  save(): void {
    if (this.form.invalid || !this.canManage) return;
    this.saving.set(true);
    const v = this.form.getRawValue();
    this.tenantService
      .updateSettings({
        name: v.name,
        logoUrl: v.logoUrl || null,
        timezone: v.timezone,
        currency: v.currency,
        businessType: v.businessType,
        email: v.email,
        phone: v.phone,
        address: v.address,
        website: v.website || null,
        description: v.description || null,
      })
      .subscribe({
        next: () => {
          this.notification.success('Tenant settings saved.');
          this.saving.set(false);
        },
        error: (err) => {
          this.notification.error(err?.message ?? 'Failed to save tenant settings.');
          this.saving.set(false);
        },
      });
  }
}
