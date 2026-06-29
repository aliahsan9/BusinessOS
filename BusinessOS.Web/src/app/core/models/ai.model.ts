export interface AiChatRequest {
  message: string;
  currentPage?: string | null;
  searchQuery?: string | null;
}

export interface AiChatResponse {
  reply: string;
  suggestions: AiSuggestionDto[];
  quickActions: AiQuickActionDto[];
  searchResults: AiSearchResultDto[];
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
}
