import { Routes } from '@angular/router';
import { ROUTES } from '../../core/constants/route.constants';

export const AI_ROUTES: Routes = [
  {
    path: '',
    redirectTo: 'workspace',
    pathMatch: 'full',
  },
  {
    path: 'workspace',
    loadComponent: () =>
      import('./ai-workspace/ai-workspace.component').then((m) => m.AiWorkspaceComponent),
    title: 'AI Workspace | BusinessOS',
  },
  {
    path: 'diagnostics',
    loadComponent: () =>
      import('./ai-diagnostics/ai-diagnostics.component').then((m) => m.AiDiagnosticsComponent),
    title: 'AI Diagnostics | BusinessOS',
  },
  { path: '**', redirectTo: ROUTES.ai.workspace.replace(/^\/ai\//, '') },
];
