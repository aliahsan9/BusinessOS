export interface AiPageContext {
  url: string;
  module: string;
  customerId?: string | null;
  orderId?: string | null;
  invoiceId?: string | null;
  projectId?: string | null;
}

export interface AiChatRequest {
  message: string;
  currentPage?: string | null;
  searchQuery?: string | null;
  customerId?: string | null;
  orderId?: string | null;
  invoiceId?: string | null;
  projectId?: string | null;
  sessionId?: string | null;
}

export interface AiCopilotChatRequest extends AiChatRequest {
  stream?: boolean;
}

export interface AiRetrievedSources {
  customers: number;
  orders: number;
  invoices: number;
  projects: number;
}

export interface AiActionResult {
  action: string;
  success: boolean;
  message: string;
  entityType?: string | null;
  entityId?: string | null;
  route?: string | null;
}

export interface AiCitation {
  title: string;
  documentType: string;
  sourceId?: string | null;
  excerpt?: string | null;
  score: number;
}

export interface AiCopilotDiagnostics {
  intent: string;
  toolsUsed: string[];
  executionTimeMs: number;
  tokenUsage?: number | null;
  retrievedDocuments: number;
  usedLlm: boolean;
}

export interface AiChatResponse {
  reply: string;
  sessionId?: string | null;
  intent?: string | null;
  toolsUsed?: string[];
  citations?: AiCitation[];
  suggestions: AiSuggestionDto[];
  quickActions: AiQuickActionDto[];
  searchResults: AiSearchResultDto[];
  sources: AiRetrievedSources;
  actionResult?: AiActionResult | null;
  diagnostics?: AiCopilotDiagnostics | null;
  permissionDenied?: boolean;
}

export interface AiSuggestionDto {
  label: string;
  message: string;
}

export interface AiQuickActionDto {
  label: string;
  route: string;
  icon: string;
}

export interface AiSearchResultDto {
  type: string;
  id: string;
  title: string;
  subtitle?: string | null;
  route: string;
}

export interface AiChatMessage {
  role: 'user' | 'assistant';
  content: string;
  timestamp: Date;
  sources?: AiRetrievedSources | null;
  citations?: AiCitation[] | null;
  toolsUsed?: string[] | null;
  actionResult?: AiActionResult | null;
  streaming?: boolean;
}

export interface AiConversationSession {
  id: string;
  title: string;
  lastActivityAt: string;
  messageCount: number;
}

export interface AiConversationMessage {
  id: string;
  prompt: string;
  response: string;
  intent?: string | null;
  toolsUsed: string[];
  citations: AiCitation[];
  createdAt: string;
}

export interface AiProactiveInsight {
  type: string;
  severity: string;
  title: string;
  message: string;
  actionRoute?: string | null;
  actionLabel?: string | null;
}

export interface AiDashboardCopilot {
  summary: string;
  insights: AiProactiveInsight[];
  focusAreas: AiSuggestionDto[];
}

export interface AiDiagnosticsSummary {
  totalRequests: number;
  successfulRequests: number;
  failedRequests: number;
  averageExecutionTimeMs: number;
  totalTokenUsage: number;
  intentBreakdown: { intent: string; count: number }[];
  toolBreakdown: { tool: string; count: number }[];
  recentLogs: AiCopilotAuditEntry[];
}

export interface AiCopilotAuditEntry {
  id: string;
  intent: string;
  userMessage?: string | null;
  toolsUsed: string[];
  executionTimeMs: number;
  tokenUsage?: number | null;
  success: boolean;
  errorMessage?: string | null;
  createdAt: string;
}

export interface AiStreamChunk {
  type: string;
  content?: string | null;
  finalResponse?: AiChatResponse | null;
}
