import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { ExpenseService } from '../../../core/services/expense.service';
import { ExpenseDto } from '../../../core/models/expense.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { TokenService } from '../../../core/services/token.service';
import { PermissionCodes } from '../../../core/constants/permission.constants';
import { ButtonVariant } from '../../../core/enums';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppBadgeComponent } from '../../../shared/components/app-badge/app-badge.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';

@Component({
  selector: 'app-expense-detail',
  standalone: true,
  imports: [RouterLink, CurrencyPipe, DatePipe, AppBreadcrumbComponent, AppPageHeaderComponent, AppCardComponent, AppBadgeComponent, AppSkeletonComponent],
  templateUrl: './expense-detail.component.html',
  styleUrl: './expense-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExpenseDetailComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly route = inject(ActivatedRoute);
  private readonly expenseService = inject(ExpenseService);
  private readonly tokenService = inject(TokenService);

  readonly expense = signal<ExpenseDto | null>(null);
  readonly loading = signal(true);
  readonly routes = ROUTES;
  readonly canEdit = this.tokenService.hasPermission(PermissionCodes.expense.update);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;
    this.expenseService.getById(id).subscribe({
      next: (e) => { this.expense.set(e); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  headerActions() {
    if (!this.canEdit || !this.expense()) return [];
    return [{ label: 'Edit', route: `${ROUTES.expenses.base}/${this.expense()!.id}/edit`, icon: '✏️', variant: ButtonVariant.Outline }];
  }

  statusVariant(status: string): 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral' {
    const map: Record<string, 'primary' | 'success' | 'danger' | 'warning' | 'info' | 'neutral'> = {
      Pending: 'warning', Approved: 'info', Rejected: 'danger', Paid: 'success',
    };
    return map[status] ?? 'neutral';
  }
}
