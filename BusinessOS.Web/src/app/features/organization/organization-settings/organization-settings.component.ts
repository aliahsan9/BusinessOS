import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { OrganizationService } from '../../../core/services/organization.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TokenService } from '../../../core/services/token.service';
import { OrganizationDto } from '../../../core/models/team.model';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { ButtonVariant } from '../../../core/enums';
import { ROUTES } from '../../../core/constants/route.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppInputComponent } from '../../../shared/components/app-input/app-input.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';

@Component({
  selector: 'app-organization-settings',
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
  templateUrl: './organization-settings.component.html',
  styleUrl: './organization-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrganizationSettingsComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly fb = inject(FormBuilder);
  private readonly organizationService = inject(OrganizationService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly organization = signal<OrganizationDto | null>(null);
  readonly canManage = this.tokenService.hasPermission(PermissionCodes.organization.manage);
  readonly breadcrumbs = [{ label: 'Organization', route: ROUTES.organization.base }, { label: 'Settings' }];

  readonly form = this.fb.nonNullable.group({
    name: ['', Validators.required],
    businessType: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    phone: ['', Validators.required],
    address: ['', Validators.required],
    website: [''],
    description: [''],
    logoUrl: [''],
    currency: ['USD', Validators.required],
    timezone: ['UTC', Validators.required],
  });

  ngOnInit(): void {
    this.load();
    if (!this.canManage) {
      this.form.disable();
    }
  }

  load(): void {
    this.loading.set(true);
    this.organizationService.getOrganization().subscribe({
      next: (org) => {
        this.organization.set(org);
        this.form.patchValue({
          name: org.name,
          businessType: org.businessType,
          email: org.email,
          phone: org.phone,
          address: org.address,
          website: org.website ?? '',
          description: org.description ?? '',
          logoUrl: org.logoUrl ?? '',
          currency: org.currency,
          timezone: org.timezone,
        });
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load organization settings.');
        this.loading.set(false);
      },
    });
  }

  save(): void {
    if (this.form.invalid || !this.canManage) return;
    this.saving.set(true);
    const v = this.form.getRawValue();
    this.organizationService
      .update({
        name: v.name,
        businessType: v.businessType,
        email: v.email,
        phone: v.phone,
        address: v.address,
        website: v.website || null,
        description: v.description || null,
        logoUrl: v.logoUrl || null,
        currency: v.currency,
        timezone: v.timezone,
      })
      .subscribe({
        next: (org) => {
          this.organization.set(org);
          this.notification.success('Organization settings saved.');
          this.saving.set(false);
        },
        error: (err) => {
          this.notification.error(err?.message ?? 'Failed to save settings.');
          this.saving.set(false);
        },
      });
  }
}
