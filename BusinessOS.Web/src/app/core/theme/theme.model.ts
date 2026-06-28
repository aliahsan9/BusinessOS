/** Built-in theme palette identifiers */
export type BuiltInThemeId =
  | 'light'
  | 'dark'
  | 'blue-professional'
  | 'green-business'
  | 'purple-executive'
  | 'orange-startup'
  | 'corporate-gray'
  | 'high-contrast';

export type ThemeId = BuiltInThemeId | `custom-${string}`;

export type ColorScheme = 'light' | 'dark' | 'system';

export type FontFamilyId = 'inter' | 'poppins' | 'roboto' | 'open-sans' | 'nunito';

export type FontSizeId = 'sm' | 'md' | 'lg' | 'xl';

export type BorderRadiusId = 'none' | 'sm' | 'md' | 'lg' | 'full';

export type SidebarStyle = 'expanded' | 'collapsed' | 'mini' | 'floating';

export type NavbarStyle = 'fixed' | 'static' | 'compact';

export type FooterVisibility = 'visible' | 'hidden';

export type CardStyle = 'flat' | 'elevated' | 'glassmorphism' | 'modern';

export type TableDensity = 'comfortable' | 'compact' | 'spacious';

/** Semantic CSS variable map for dynamic custom themes */
export type ThemeColorTokens = Record<string, string>;

export interface ThemeDefinition {
  id: ThemeId;
  name: string;
  description: string;
  category: 'light' | 'dark' | 'accessibility' | 'custom';
  preview: {
    primary: string;
    secondary: string;
    background: string;
    surface: string;
  };
  isDark: boolean;
  isBuiltIn: boolean;
  tokens?: ThemeColorTokens;
}

export interface ThemePreferences {
  version: 1;
  themeId: ThemeId;
  colorScheme: ColorScheme;
  fontFamily: FontFamilyId;
  fontSize: FontSizeId;
  borderRadius: BorderRadiusId;
  compactMode: boolean;
  animationsEnabled: boolean;
  sidebarStyle: SidebarStyle;
  navbarStyle: NavbarStyle;
  footerVisibility: FooterVisibility;
  cardStyle: CardStyle;
  tableDensity: TableDensity;
  largeFontMode: boolean;
  reducedMotion: boolean;
  favoriteThemeIds: ThemeId[];
  recentThemeIds: ThemeId[];
  customThemes: ThemeDefinition[];
}

export interface ThemeExportPayload {
  exportedAt: string;
  preferences: ThemePreferences;
  customThemes: ThemeDefinition[];
}

export const DEFAULT_THEME_PREFERENCES: ThemePreferences = {
  version: 1,
  themeId: 'light',
  colorScheme: 'system',
  fontFamily: 'inter',
  fontSize: 'md',
  borderRadius: 'md',
  compactMode: false,
  animationsEnabled: true,
  sidebarStyle: 'expanded',
  navbarStyle: 'fixed',
  footerVisibility: 'visible',
  cardStyle: 'elevated',
  tableDensity: 'comfortable',
  largeFontMode: false,
  reducedMotion: false,
  favoriteThemeIds: [],
  recentThemeIds: [],
  customThemes: [],
};
