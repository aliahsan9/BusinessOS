import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { NotificationCenterService } from '../../../core/services/notification-center.service';
import { NotificationService } from '../../../core/services/notification.service';
import { NotificationPreferences } from '../../../core/models/notification.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { ButtonVariant } from '../../../core/enums';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';

@Component({
  selector: 'app-notification-settings',
  standalone: true,
  imports: [
    RouterLink,
    ReactiveFormsModule,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppCardComponent,
    AppButtonComponent,
    AppSkeletonComponent,
    AppAlertComponent,
  ],
  templateUrl: './notification-settings.component.html',
  styleUrl: './notification-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationSettingsComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly fb = inject(FormBuilder);
  private readonly notificationCenter = inject(NotificationCenterService);
  private readonly notification = inject(NotificationService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly routes = ROUTES;
  readonly breadcrumbs = [
    { label: 'Notifications', route: ROUTES.notifications.list },
    { label: 'Settings' },
  ];

  readonly form = this.fb.nonNullable.group({
    emailNotificationsEnabled: [true],
    systemNotificationsEnabled: [true],
    orderNotificationsEnabled: [true],
    inventoryAlertsEnabled: [true],
    paymentAlertsEnabled: [true],
    taskNotificationsEnabled: [true],
    invoiceNotificationsEnabled: [true],
    customerNotificationsEnabled: [true],
    projectNotificationsEnabled: [true],
  });

  ngOnInit(): void {
    this.notificationCenter.getPreferences().subscribe({
      next: (prefs) => {
        this.form.patchValue(prefs);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load notification preferences.');
        this.loading.set(false);
      },
    });
  }

  submit(): void {
    this.saving.set(true);
    this.notificationCenter.updatePreferences(this.form.getRawValue()).subscribe({
      next: (prefs: NotificationPreferences) => {
        this.form.patchValue(prefs);
        this.notification.success('Notification preferences saved.');
        this.saving.set(false);
      },
      error: () => {
        this.notification.error('Failed to save preferences.');
        this.saving.set(false);
      },
    });
  }
}
