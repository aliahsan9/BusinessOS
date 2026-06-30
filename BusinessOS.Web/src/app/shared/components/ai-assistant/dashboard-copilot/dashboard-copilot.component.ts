import { ChangeDetectionStrategy, Component, inject, signal, OnInit, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AiChatService } from '../../../../core/services/ai-chat.service';
import { AiDashboardCopilot, AiProactiveInsight } from '../../../../core/models/ai.model';
import { ROUTES } from '../../../../core/constants/route.constants';
import { AppCardComponent } from '../../app-card/app-card.component';

@Component({
  selector: 'app-dashboard-copilot',
  standalone: true,
  imports: [RouterLink, AppCardComponent],
  templateUrl: './dashboard-copilot.component.html',
  styleUrl: './dashboard-copilot.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardCopilotComponent implements OnInit {
  private readonly aiChat = inject(AiChatService);

  readonly askQuestion = output<string>();
  readonly routes = ROUTES;
  readonly copilot = signal<AiDashboardCopilot | null>(null);
  readonly insights = signal<AiProactiveInsight[]>([]);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.aiChat.getDashboardCopilot().subscribe({
      next: (data) => {
        this.copilot.set(data);
        this.insights.set(data.insights);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  onFocus(message: string): void {
    this.askQuestion.emit(message);
  }
}
