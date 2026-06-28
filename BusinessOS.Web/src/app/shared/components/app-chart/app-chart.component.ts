import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  effect,
  input,
  viewChild,
} from '@angular/core';
import { Chart, ChartConfiguration, registerables } from 'chart.js';
import { ChartDataResponse } from '../../../core/models/dashboard.model';

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

  private readonly canvasRef = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  private chart: Chart | null = null;

  constructor() {
    effect(() => {
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
    const config: ChartConfiguration = {
      type: this.mapChartType(chartData.chartType),
      data: {
        labels: chartData.labels,
        datasets: chartData.datasets.map((ds) => ({
          label: ds.label,
          data: ds.data,
          borderColor: '#2563EB',
          backgroundColor: ds.chartStyle === 'line' ? 'rgba(37, 99, 235, 0.1)' : 'rgba(37, 99, 235, 0.7)',
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
          y: { beginAtZero: true, grid: { color: 'rgba(0,0,0,0.05)' } },
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
    this.chart.data.labels = chartData.labels;
    this.chart.data.datasets = chartData.datasets.map((ds) => ({
      label: ds.label,
      data: ds.data,
      borderColor: '#2563EB',
      backgroundColor: ds.chartStyle === 'line' ? 'rgba(37, 99, 235, 0.1)' : 'rgba(37, 99, 235, 0.7)',
      tension: 0.35,
      fill: ds.chartStyle === 'line',
      borderRadius: 6,
    }));
    this.chart.update();
  }

  private mapChartType(type: string): 'line' | 'bar' | 'doughnut' {
    const normalized = type.toLowerCase();
    if (normalized.includes('bar')) return 'bar';
    if (normalized.includes('doughnut') || normalized.includes('pie')) return 'doughnut';
    return 'line';
  }
}
