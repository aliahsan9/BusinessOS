import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { AppCurrencyPipe } from '../../../shared/pipes/app-currency.pipe';
import { InvoiceService } from '../../../core/services/invoice.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TokenService } from '../../../core/services/token.service';
import { InvoiceDto } from '../../../core/models/invoice.model';
import { ButtonVariant, InvoiceStatus } from '../../../core/enums';
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

const INVOICE_TRANSITIONS: Record<InvoiceStatus, InvoiceStatus[]> = {
  [InvoiceStatus.Draft]: [InvoiceStatus.Sent, InvoiceStatus.Cancelled],
  [InvoiceStatus.Sent]: [
    InvoiceStatus.Paid,
    InvoiceStatus.PartiallyPaid,
    InvoiceStatus.Overdue,
    InvoiceStatus.Cancelled,
  ],
  [InvoiceStatus.PartiallyPaid]: [InvoiceStatus.Paid, InvoiceStatus.Overdue],
  [InvoiceStatus.Overdue]: [InvoiceStatus.Paid, InvoiceStatus.PartiallyPaid, InvoiceStatus.Cancelled],
  [InvoiceStatus.Paid]: [],
  [InvoiceStatus.Cancelled]: [],
};

@Component({
  selector: 'app-invoice-detail',
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
  templateUrl: './invoice-detail.component.html',
  styleUrl: './invoice-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InvoiceDetailComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly route = inject(ActivatedRoute);
  private readonly invoiceService = inject(InvoiceService);
  private readonly notification = inject(NotificationService);
  private readonly tokenService = inject(TokenService);

  readonly invoice = signal<InvoiceDto | null>(null);
  readonly loading = signal(true);
  readonly actionLoading = signal(false);
  readonly printing = signal(false);
  readonly pendingStatus = signal<InvoiceStatus | null>(null);
  readonly routes = ROUTES;
  readonly canUpdate = this.tokenService.hasPermission(PermissionCodes.invoice.update);

  readonly allowedTransitions = computed(() => {
    const inv = this.invoice();
    if (!inv || !this.canUpdate) return [];
    return INVOICE_TRANSITIONS[inv.status] ?? [];
  });

  readonly canRecordPayment = computed(() => {
    const inv = this.invoice();
    return (
      !!inv &&
      inv.outstandingAmount > 0 &&
      inv.status !== InvoiceStatus.Cancelled &&
      inv.status !== InvoiceStatus.Draft
    );
  });

  ngOnInit(): void {
    this.loadInvoice();
  }

  loadInvoice(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;

    this.loading.set(true);
    this.invoiceService.getById(id).subscribe({
      next: (inv) => {
        this.invoice.set(inv);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  statusVariant(status: InvoiceStatus): 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral' {
    const map: Record<InvoiceStatus, 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral'> = {
      [InvoiceStatus.Draft]: 'neutral',
      [InvoiceStatus.Sent]: 'info',
      [InvoiceStatus.Paid]: 'success',
      [InvoiceStatus.PartiallyPaid]: 'warning',
      [InvoiceStatus.Overdue]: 'danger',
      [InvoiceStatus.Cancelled]: 'neutral',
    };
    return map[status] ?? 'neutral';
  }

  transitionLabel(status: InvoiceStatus): string {
    const labels: Partial<Record<InvoiceStatus, string>> = {
      [InvoiceStatus.Sent]: 'Mark Sent',
      [InvoiceStatus.Paid]: 'Mark Paid',
      [InvoiceStatus.PartiallyPaid]: 'Mark Partially Paid',
      [InvoiceStatus.Overdue]: 'Mark Overdue',
      [InvoiceStatus.Cancelled]: 'Cancel',
    };
    return labels[status] ?? status;
  }

  transitionVariant(status: InvoiceStatus): ButtonVariant {
    if (status === InvoiceStatus.Cancelled) return ButtonVariant.Danger;
    if (status === InvoiceStatus.Paid) return ButtonVariant.Success;
    return ButtonVariant.Primary;
  }

  openStatusConfirm(status: InvoiceStatus): void {
    this.pendingStatus.set(status);
  }

  confirmStatusUpdate(): void {
    const inv = this.invoice();
    const status = this.pendingStatus();
    if (!inv || !status) return;

    this.actionLoading.set(true);
    this.invoiceService.updateStatus(inv.id, status).subscribe({
      next: () => {
        this.notification.success(`Invoice status updated to ${status}.`);
        this.pendingStatus.set(null);
        this.actionLoading.set(false);
        this.loadInvoice();
      },
      error: () => {
        this.notification.error('Failed to update invoice status.');
        this.actionLoading.set(false);
      },
    });
  }

  printInvoice(): void {
    const inv = this.invoice();
    if (!inv) return;

    this.printing.set(true);
    this.invoiceService.getPdfHtml(inv.id).subscribe({
      next: (html) => {
        const blob = new Blob([html], { type: 'text/html' });
        const url = URL.createObjectURL(blob);
        window.open(url, '_blank');
        setTimeout(() => URL.revokeObjectURL(url), 60000);
        this.printing.set(false);
      },
      error: () => {
        this.notification.error('Failed to load invoice for printing.');
        this.printing.set(false);
      },
    });
  }

  confirmTitle(): string {
    const status = this.pendingStatus();
    return status ? `Update Status to ${status}` : 'Update Status';
  }

  confirmMessage(): string {
    const status = this.pendingStatus();
    return status ? `Change this invoice status to "${status}"?` : '';
  }

  confirmVariant(): ButtonVariant {
    return this.pendingStatus() === InvoiceStatus.Cancelled ? ButtonVariant.Danger : ButtonVariant.Primary;
  }
}
