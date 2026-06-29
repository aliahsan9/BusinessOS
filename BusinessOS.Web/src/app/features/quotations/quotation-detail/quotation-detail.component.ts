import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { AppCurrencyPipe } from '../../../shared/pipes/app-currency.pipe';
import { QuotationService } from '../../../core/services/quotation.service';
import { NotificationService } from '../../../core/services/notification.service';
import { QuotationDto } from '../../../core/models/quotation.model';
import { QuotationStatus } from '../../../core/enums';
import { ROUTES } from '../../../core/constants/route.constants';
import { TokenService } from '../../../core/services/token.service';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppBadgeComponent } from '../../../shared/components/app-badge/app-badge.component';
import { AppButtonComponent } from '../../../shared/components/app-button/app-button.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppEmptyStateComponent } from '../../../shared/components/app-empty-state/app-empty-state.component';
import { AppConfirmDialogComponent } from '../../../shared/components/app-confirm-dialog/app-confirm-dialog.component';
import { ButtonVariant } from '../../../core/enums';

@Component({
  selector: 'app-quotation-detail',
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
  templateUrl: './quotation-detail.component.html',
  styleUrl: './quotation-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QuotationDetailComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly quotationService = inject(QuotationService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly quotation = signal<QuotationDto | null>(null);
  readonly loading = signal(true);
  readonly actionLoading = signal(false);
  readonly confirmAction = signal<'send' | 'accept' | 'reject' | 'expire' | 'convert' | null>(null);
  readonly routes = ROUTES;
  readonly canUpdate = this.tokenService.hasPermission(PermissionCodes.quotation.update);

  ngOnInit(): void {
    this.loadQuotation();
  }

  loadQuotation(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;

    this.loading.set(true);
    this.quotationService.getById(id).subscribe({
      next: (q) => {
        this.quotation.set(q);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  headerActions() {
    const q = this.quotation();
    if (!q || !this.canUpdate) return [];

    const actions = [];
    if (this.isEditable()) {
      actions.push({
        label: 'Edit',
        route: `${ROUTES.quotations.base}/${q.id}/edit`,
        icon: '✏️',
        variant: ButtonVariant.Outline,
      });
    }
    return actions;
  }

  statusVariant(status: QuotationStatus): 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral' {
    const map: Record<QuotationStatus, 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral'> = {
      [QuotationStatus.Draft]: 'neutral',
      [QuotationStatus.Sent]: 'info',
      [QuotationStatus.Accepted]: 'success',
      [QuotationStatus.Rejected]: 'danger',
      [QuotationStatus.Expired]: 'warning',
    };
    return map[status] ?? 'neutral';
  }

  isEditable(): boolean {
    const status = this.quotation()?.status;
    return status === QuotationStatus.Draft || status === QuotationStatus.Sent;
  }

  canSend(): boolean {
    return this.canUpdate && this.quotation()?.status === QuotationStatus.Draft;
  }

  canAccept(): boolean {
    return this.canUpdate && this.quotation()?.status === QuotationStatus.Sent;
  }

  canReject(): boolean {
    const status = this.quotation()?.status;
    return this.canUpdate && (status === QuotationStatus.Draft || status === QuotationStatus.Sent);
  }

  canExpire(): boolean {
    return this.canUpdate && this.quotation()?.status === QuotationStatus.Sent;
  }

  canConvert(): boolean {
    return this.canUpdate && this.quotation()?.status === QuotationStatus.Accepted;
  }

  openConfirm(action: 'send' | 'accept' | 'reject' | 'expire' | 'convert'): void {
    this.confirmAction.set(action);
  }

  confirmVariant(): ButtonVariant {
    const action = this.confirmAction();
    if (action === 'reject' || action === 'expire') return ButtonVariant.Danger;
    return ButtonVariant.Primary;
  }

  executeAction(): void {
    const q = this.quotation();
    const action = this.confirmAction();
    if (!q || !action) return;

    this.actionLoading.set(true);

    if (action === 'convert') {
      this.quotationService.convertToOrder(q.id).subscribe({
        next: (res) => {
          this.notification.success('Quotation converted to order.');
          this.confirmAction.set(null);
          this.actionLoading.set(false);
          this.router.navigate([ROUTES.orders.base, res.id]);
        },
        error: () => this.onActionError('Failed to convert quotation to order.'),
      });
      return;
    }

    const statusMap: Record<'send' | 'accept' | 'reject' | 'expire', QuotationStatus> = {
      send: QuotationStatus.Sent,
      accept: QuotationStatus.Accepted,
      reject: QuotationStatus.Rejected,
      expire: QuotationStatus.Expired,
    };

    this.quotationService.updateStatus(q.id, statusMap[action]).subscribe({
      next: () => {
        const messages = {
          send: 'Quotation marked as sent.',
          accept: 'Quotation accepted.',
          reject: 'Quotation rejected.',
          expire: 'Quotation marked as expired.',
        };
        this.onActionSuccess(messages[action]);
      },
      error: () => this.onActionError('Failed to update quotation status.'),
    });
  }

  confirmTitle(): string {
    const action = this.confirmAction();
    if (action === 'send') return 'Send Quotation';
    if (action === 'accept') return 'Accept Quotation';
    if (action === 'reject') return 'Reject Quotation';
    if (action === 'expire') return 'Mark as Expired';
    return 'Convert to Order';
  }

  confirmMessage(): string {
    const action = this.confirmAction();
    if (action === 'send') return 'Mark this quotation as sent to the customer?';
    if (action === 'accept') return 'Mark this quotation as accepted?';
    if (action === 'reject') return 'Reject this quotation?';
    if (action === 'expire') return 'Mark this quotation as expired?';
    return 'Convert this accepted quotation into a customer order?';
  }

  confirmLabel(): string {
    const action = this.confirmAction();
    if (action === 'convert') return 'Convert to Order';
    if (action === 'reject') return 'Reject';
    if (action === 'expire') return 'Mark Expired';
    return 'Confirm';
  }

  private onActionSuccess(message: string): void {
    this.notification.success(message);
    this.confirmAction.set(null);
    this.actionLoading.set(false);
    this.loadQuotation();
  }

  private onActionError(message: string): void {
    this.notification.error(message);
    this.actionLoading.set(false);
  }
}
