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

export interface AiChatResponse {
  reply: string;
  suggestions: AiSuggestionDto[];
  quickActions: AiQuickActionDto[];
  searchResults: AiSearchResultDto[];
  sources: AiRetrievedSources;
  actionResult?: AiActionResult | null;
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
  actionResult?: AiActionResult | null;
}
