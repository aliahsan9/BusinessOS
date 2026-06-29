import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { AppCurrencyPipe } from '../../../shared/pipes/app-currency.pipe';
import { PaymentService } from '../../../core/services/payment.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TokenService } from '../../../core/services/token.service';
import { PaymentDto } from '../../../core/models/payment.model';
import { ButtonVariant } from '../../../core/enums';
import { ROUTES } from '../../../core/constants/route.constants';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppBadgeComponent } from '../../../shared/components/app-badge/app-badge.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppConfirmDialogComponent } from '../../../shared/components/app-confirm-dialog/app-confirm-dialog.component';

@Component({
  selector: 'app-payment-detail',
  standalone: true,
  imports: [
    RouterLink,
    AppCurrencyPipe,
    DatePipe,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppCardComponent,
    AppBadgeComponent,
    AppButtonComponent,
    AppSkeletonComponent,
    AppEmptyStateComponent,
    AppConfirmDialogComponent,
  ],
  templateUrl: './payment-detail.component.html',
  styleUrl: './payment-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PaymentDetailComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly paymentService = inject(PaymentService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly payment = signal<PaymentDto | null>(null);
  readonly loading = signal(true);
  readonly deleting = signal(false);
  readonly confirmDeleteOpen = signal(false);
  readonly routes = ROUTES;
  readonly canUpdate = this.tokenService.hasPermission(PermissionCodes.payment.update);
  readonly canDelete = this.tokenService.hasPermission(PermissionCodes.payment.delete);

  ngOnInit(): void {
    this.loadPayment();
  }

  loadPayment(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;

    this.loading.set(true);
    this.paymentService.getById(id).subscribe({
      next: (p) => {
        this.payment.set(p);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  headerActions() {
    const p = this.payment();
    if (!p) return [];
    const actions = [];
    if (this.canUpdate) {
      actions.push({
        label: 'Edit',
        route: `${ROUTES.payments.base}/${p.id}/edit`,
        icon: '✏️',
        variant: ButtonVariant.Outline,
      });
    }
    return actions;
  }

  deletePayment(): void {
    const p = this.payment();
    if (!p) return;

    this.deleting.set(true);
    this.paymentService.remove(p.id).subscribe({
      next: () => {
        this.notification.success('Payment deleted.');
        this.router.navigate([ROUTES.payments.list]);
      },
      error: () => {
        this.notification.error('Failed to delete payment.');
        this.deleting.set(false);
        this.confirmDeleteOpen.set(false);
      },
    });
  }
}
