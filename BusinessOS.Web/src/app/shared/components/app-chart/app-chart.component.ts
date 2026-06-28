import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  effect,
  inject,
  input,
  viewChild,
} from '@angular/core';
import { Chart, ChartConfiguration, registerables } from 'chart.js';
import { ChartDataResponse } from '../../../core/models/dashboard.model';
import { ThemeService } from '../../../core/theme/theme.service';

Chart.register(...registerables);

@Component({
  selector: 'app-chart',
  standalone: true,
  templateUrl: './app-chart.component.html',
  styleUrl: './app-chart.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppChartComponent implements AfterViewInit, OnDestroy {
  readonly data = input<ChartDataResponse | null>(null);
  readonly height = input(280);

  private readonly themeService = inject(ThemeService);
  private readonly canvasRef = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  private chart: Chart | null = null;

  constructor() {
    effect(() => {
      const chartData = this.data();
      if (this.chart && chartData) {
        this.updateChart(chartData);
      }
    });

    effect(() => {
      this.themeService.themeId();
      this.themeService.resolvedAppearance();
      const chartData = this.data();
      if (this.chart && chartData) {
        this.updateChart(chartData);
      }
    });
  }

  ngAfterViewInit(): void {
    const chartData = this.data();
    if (chartData) {
      this.createChart(chartData);
    }
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
  }

  private createChart(chartData: ChartDataResponse): void {
    const canvas = this.canvasRef().nativeElement;
    const colors = this.getChartColors();
    const config: ChartConfiguration = {
      type: this.mapChartType(chartData.chartType),
      data: {
        labels: chartData.labels,
        datasets: chartData.datasets.map((ds) => ({
          label: ds.label,
          data: ds.data,
          borderColor: colors.primary,
          backgroundColor: ds.chartStyle === 'line' ? colors.primaryFaded : colors.primarySolid,
          tension: 0.35,
          fill: ds.chartStyle === 'line',
          borderRadius: 6,
        })),
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { display: chartData.datasets.length > 1, position: 'bottom' },
        },
        scales: {
          y: { beginAtZero: true, grid: { color: colors.grid } },
          x: { grid: { display: false } },
        },
      },
    };
    this.chart = new Chart(canvas, config);
  }

  private updateChart(chartData: ChartDataResponse): void {
    if (!this.chart) {
      this.createChart(chartData);
      return;
    }
    const colors = this.getChartColors();
    this.chart.data.labels = chartData.labels;
    this.chart.data.datasets = chartData.datasets.map((ds) => ({
      label: ds.label,
      data: ds.data,
      borderColor: colors.primary,
      backgroundColor: ds.chartStyle === 'line' ? colors.primaryFaded : colors.primarySolid,
      tension: 0.35,
      fill: ds.chartStyle === 'line',
      borderRadius: 6,
    }));
    this.chart.update();
  }

  private getChartColors(): {
    primary: string;
    primaryFaded: string;
    primarySolid: string;
    grid: string;
  } {
    const root = document.documentElement;
    const primary =
      getComputedStyle(root).getPropertyValue('--chart-primary-color').trim() ||
      getComputedStyle(root).getPropertyValue('--primary-color').trim() ||
      '#2563eb';
    const isDark = this.themeService.resolvedAppearance() === 'dark';
    return {
      primary,
      primaryFaded: this.withAlpha(primary, 0.1),
      primarySolid: this.withAlpha(primary, 0.7),
      grid: isDark ? 'rgba(255,255,255,0.08)' : 'rgba(0,0,0,0.05)',
    };
  }

  private withAlpha(hex: string, alpha: number): string {
    if (hex.startsWith('#') && hex.length >= 7) {
      const r = parseInt(hex.slice(1, 3), 16);
      const g = parseInt(hex.slice(3, 5), 16);
      const b = parseInt(hex.slice(5, 7), 16);
      return `rgba(${r}, ${g}, ${b}, ${alpha})`;
    }
    return hex;
  }

  private mapChartType(type: string): 'line' | 'bar' | 'doughnut' {
    const normalized = type.toLowerCase();
    if (normalized.includes('bar')) return 'bar';
    if (normalized.includes('doughnut') || normalized.includes('pie')) return 'doughnut';
    return 'line';
  }
}
