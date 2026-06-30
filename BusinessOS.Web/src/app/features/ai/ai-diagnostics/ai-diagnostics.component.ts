import { ChangeDetectionStrategy, Component, inject, signal, OnInit } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AiChatService } from '../../../core/services/ai-chat.service';
import { AiDiagnosticsSummary } from '../../../core/models/ai.model';
import { ROUTES } from '../../../core/constants/route.constants';
import { AppBreadcrumbComponent } from '../../../shared/components/app-breadcrumb/app-breadcrumb.component';
import { AppCardComponent } from '../../../shared/components/app-card/app-card.component';

@Component({
  selector: 'app-ai-diagnostics',
  standalone: true,
  imports: [RouterLink, DatePipe, DecimalPipe, AppBreadcrumbComponent, AppCardComponent],
  templateUrl: './ai-diagnostics.component.html',
  styleUrl: './ai-diagnostics.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AiDiagnosticsComponent implements OnInit {
  private readonly aiChat = inject(AiChatService);

  readonly routes = ROUTES;
  readonly diagnostics = signal<AiDiagnosticsSummary | null>(null);
  readonly loading = signal(true);
  readonly breadcrumbs = [
    { label: 'AI Copilot', route: ROUTES.ai.workspace },
    { label: 'Diagnostics' },
  ];

  ngOnInit(): void {
    this.aiChat.getDiagnostics().subscribe({
      next: (data) => {
        this.diagnostics.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.diagnostics.set(null);
        this.loading.set(false);
      },
    });
  }
}
