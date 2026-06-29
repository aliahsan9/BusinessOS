import { ChangeDetectionStrategy, Component, inject, signal, computed, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HelpService } from '../../../core/services/help.service';
import { HelpCenterDto, HelpFaqDto } from '../../../core/models/help.model';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppPageHeaderComponent } from '../../../shared/components/app-page-header/app-page-header.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';
import { AppSkeletonComponent } from '../../../shared/components/app-skeleton/app-skeleton.component';
import { AppAlertComponent } from '../../../shared/components/app-alert/app-alert.component';
import { ROUTES } from '../../../core/constants/route.constants';

@Component({
  selector: 'app-help-center-page',
  standalone: true,
  imports: [
    RouterLink,
    AppBreadcrumbComponent,
    AppPageHeaderComponent,
    AppCardComponent,
    AppSkeletonComponent,
    AppAlertComponent,
  ],
  templateUrl: './help-center-page.component.html',
  styleUrl: './help-center-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HelpCenterPageComponent implements OnInit {
  private readonly helpService = inject(HelpService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly helpCenter = signal<HelpCenterDto | null>(null);
  readonly activeCategory = signal<string>('All');
  readonly expandedFaq = signal<string | null>(null);

  readonly breadcrumbs = [
    { label: 'Dashboard', route: ROUTES.dashboard },
    { label: 'Help Center' },
  ];

  readonly categories = computed(() => {
    const faqs = this.helpCenter()?.faqs ?? [];
    const cats = [...new Set(faqs.map((f) => f.category))];
    return ['All', ...cats];
  });

  readonly filteredFaqs = computed(() => {
    const faqs = this.helpCenter()?.faqs ?? [];
    const cat = this.activeCategory();
    return cat === 'All' ? faqs : faqs.filter((f) => f.category === cat);
  });

  ngOnInit(): void {
    this.helpService.getHelpCenter().subscribe({
      next: (data) => {
        this.helpCenter.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load help center content.');
        this.loading.set(false);
      },
    });
  }

  selectCategory(category: string): void {
    this.activeCategory.set(category);
  }

  toggleFaq(faq: HelpFaqDto): void {
    this.expandedFaq.update((current) => (current === faq.id ? null : faq.id));
  }
}
