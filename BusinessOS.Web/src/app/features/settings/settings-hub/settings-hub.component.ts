import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { SettingsService } from '../../../core/services/settings.service';
import { TenantSettingsStoreService } from '../../../core/services/tenant-settings-store.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TokenService } from '../../../core/services/token.service';
import { BusinessProfileDto, TenantSettingsDto } from '../../../core/models/settings.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { DEFAULT_CURRENCY_CODE } from '../../../core/constants/currency.constants';
import { ButtonVariant } from '../../../core/enums';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppInputComponent } from '../../../shared/components/app-input/app-input.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { ThemeSelectorComponent } from '../../../shared/components/theme-selector/theme-selector.component';
import { getFieldError } from '../../../shared/validators/form.validators';

type SettingsTab =
  | 'general'
  | 'business'
  | 'currency'
  | 'invoice'
  | 'email'
  | 'notifications'
  | 'appearance'
  | 'security'
  | 'branding';

@Component({
  selector: 'app-settings-hub',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppCardComponent,
    AppInputComponent,
    AppButtonComponent,
    AppSkeletonComponent,
    AppAlertComponent,
    ThemeSelectorComponent,
  ],
  templateUrl: './settings-hub.component.html',
  styleUrl: './settings-hub.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SettingsHubComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly fb = inject(FormBuilder);
  private readonly settingsService = inject(SettingsService);
  private readonly tenantSettingsStore = inject(TenantSettingsStoreService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly activeTab = signal<SettingsTab>('general');
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly settings = signal<TenantSettingsDto | null>(null);
  readonly profile = signal<BusinessProfileDto | null>(null);
  readonly routes = ROUTES;
  readonly canUpdate = this.tokenService.hasPermission(PermissionCodes.settings.update);
  readonly breadcrumbs = [{ label: 'Settings', route: ROUTES.settings.hub }];

  readonly tabs: { id: SettingsTab; label: string }[] = [
    { id: 'general', label: 'General' },
    { id: 'business', label: 'Business Profile' },
    { id: 'currency', label: 'Currency & Tax' },
    { id: 'invoice', label: 'Invoice' },
    { id: 'email', label: 'Email' },
    { id: 'notifications', label: 'Notifications' },
    { id: 'appearance', label: 'Appearance' },
    { id: 'security', label: 'Security' },
    { id: 'branding', label: 'Branding' },
  ];

  readonly settingsForm = this.fb.nonNullable.group({
    currency: [DEFAULT_CURRENCY_CODE, Validators.required],
    language: ['en', Validators.required],
    taxRate: [0, [Validators.required, Validators.min(0)]],
    invoicePrefix: [''],
    emailFromAddress: ['', Validators.email],
    theme: ['light', Validators.required],
    logoUrl: [''],
    emailNotificationsEnabled: [true],
    systemNotificationsEnabled: [true],
    orderNotificationsEnabled: [true],
    inventoryAlertsEnabled: [true],
    paymentAlertsEnabled: [true],
  });

  readonly profileForm = this.fb.nonNullable.group({
    name: ['', Validators.required],
    businessType: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    phone: ['', Validators.required],
    address: ['', Validators.required],
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.settingsService.getSettings().subscribe({
      next: (s) => {
        this.settings.set(s);
        this.tenantSettingsStore.applySettings(s);
        this.settingsForm.patchValue({
          currency: s.currency,
          language: s.language,
          taxRate: s.taxRate,
          invoicePrefix: s.invoicePrefix ?? '',
          emailFromAddress: s.emailFromAddress ?? '',
          theme: s.theme,
          logoUrl: s.logoUrl ?? '',
          emailNotificationsEnabled: s.emailNotificationsEnabled,
          systemNotificationsEnabled: s.systemNotificationsEnabled,
          orderNotificationsEnabled: s.orderNotificationsEnabled,
          inventoryAlertsEnabled: s.inventoryAlertsEnabled,
          paymentAlertsEnabled: s.paymentAlertsEnabled,
        });
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load settings.');
        this.loading.set(false);
      },
    });

    this.settingsService.getBusinessProfile().subscribe({
      next: (p) => {
        this.profile.set(p);
        this.profileForm.patchValue({
          name: p.name,
          businessType: p.businessType,
          email: p.email,
          phone: p.phone,
          address: p.address,
        });
      },
    });
  }

  selectTab(tab: SettingsTab): void {
    this.activeTab.set(tab);
  }

  settingsFieldError(field: string): string | null {
    return getFieldError(this.settingsForm.get(field), field);
  }

  profileFieldError(field: string): string | null {
    return getFieldError(this.profileForm.get(field), field);
  }

  saveSettings(): void {
    if (this.settingsForm.invalid || !this.canUpdate) {
      this.settingsForm.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const value = this.settingsForm.getRawValue();
    this.settingsService
      .updateSettings({
        ...value,
        invoicePrefix: value.invoicePrefix || null,
        emailFromAddress: value.emailFromAddress || null,
        logoUrl: value.logoUrl || null,
      })
      .subscribe({
        next: (s) => {
          this.settings.set(s);
          this.tenantSettingsStore.applySettings(s);
          this.notification.success('Settings saved.');
          this.saving.set(false);
        },
        error: () => {
          this.notification.error('Failed to save settings.');
          this.saving.set(false);
        },
      });
  }

  saveProfile(): void {
    if (this.profileForm.invalid || !this.canUpdate) {
      this.profileForm.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.settingsService.updateBusinessProfile(this.profileForm.getRawValue()).subscribe({
      next: (p) => {
        this.profile.set(p);
        this.notification.success('Business profile saved.');
        this.saving.set(false);
      },
      error: () => {
        this.notification.error('Failed to save business profile.');
        this.saving.set(false);
      },
    });
  }
}
