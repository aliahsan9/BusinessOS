import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ExpenseCategoryService } from '../../../core/services/expense-category.service';
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
  selector: 'app-category-form',
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
  templateUrl: './category-form.component.html',
  styleUrl: './category-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CategoryFormComponent implements OnInit {
  readonly ButtonVariant = ButtonVariant;
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly categoryService = inject(ExpenseCategoryService);
  private readonly notification = inject(NotificationService);

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly isEdit = signal(false);
  readonly categoryId = signal<string | null>(null);
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
    if (id) this.categoryId.set(id);

    if (this.isEdit() && id) {
      this.loading.set(true);
      this.categoryService.getById(id).subscribe({
        next: (category) => {
          this.form.patchValue({
            name: category.name,
            description: category.description ?? '',
            isActive: category.isActive,
          });
          this.loading.set(false);
        },
        error: () => {
          this.notification.error('Failed to load category.');
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

    if (this.isEdit() && this.categoryId()) {
      this.categoryService.update(this.categoryId()!, value).subscribe({
        next: () => {
          this.notification.success('Category updated.');
          this.router.navigate([ROUTES.expenseCategories.list]);
        },
        error: () => {
          this.notification.error('Failed to update category.');
          this.saving.set(false);
        },
      });
    } else {
      const { isActive: _, ...payload } = value;
      this.categoryService.create(payload).subscribe({
        next: () => {
          this.notification.success('Category created.');
          this.router.navigate([ROUTES.expenseCategories.list]);
        },
        error: () => {
          this.notification.error('Failed to create category.');
          this.saving.set(false);
        },
      });
    }
  }
}
