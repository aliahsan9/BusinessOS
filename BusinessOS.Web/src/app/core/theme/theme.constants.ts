import {
  BorderRadiusId,
  BuiltInThemeId,
  FontFamilyId,
  FontSizeId,
  ThemeDefinition,
  ThemePreferences,
} from './theme.model';

export const THEME_STORAGE_KEY = 'businessos.ui.themePreferences';
export const THEME_LEGACY_KEY = 'businessos.ui.theme';

export const FONT_FAMILIES: Record<
  FontFamilyId,
  { label: string; stack: string; googleUrl: string }
> = {
  inter: {
    label: 'Inter',
    stack: "'Inter', sans-serif",
    googleUrl: 'https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&display=swap',
  },
  poppins: {
    label: 'Poppins',
    stack: "'Poppins', sans-serif",
    googleUrl: 'https://fonts.googleapis.com/css2?family=Poppins:wght@300;400;500;600;700&display=swap',
  },
  roboto: {
    label: 'Roboto',
    stack: "'Roboto', sans-serif",
    googleUrl: 'https://fonts.googleapis.com/css2?family=Roboto:wght@300;400;500;700&display=swap',
  },
  'open-sans': {
    label: 'Open Sans',
    stack: "'Open Sans', sans-serif",
    googleUrl: 'https://fonts.googleapis.com/css2?family=Open+Sans:wght@300;400;500;600;700&display=swap',
  },
  nunito: {
    label: 'Nunito',
    stack: "'Nunito', sans-serif",
    googleUrl: 'https://fonts.googleapis.com/css2?family=Nunito:wght@300;400;500;600;700&display=swap',
  },
};

export const FONT_SIZE_SCALE: Record<FontSizeId, string> = {
  sm: '0.875rem',
  md: '0.9375rem',
  lg: '1rem',
  xl: '1.125rem',
};

export const BORDER_RADIUS_SCALE: Record<BorderRadiusId, { sm: string; md: string; lg: string }> = {
  none: { sm: '0', md: '0', lg: '0' },
  sm: { sm: '4px', md: '6px', lg: '8px' },
  md: { sm: '6px', md: '10px', lg: '14px' },
  lg: { sm: '8px', md: '14px', lg: '20px' },
  full: { sm: '999px', md: '999px', lg: '999px' },
};

export const BUILT_IN_THEMES: Record<BuiltInThemeId, ThemeDefinition> = {
  light: {
    id: 'light',
    name: 'Light',
    description: 'Clean, bright interface ideal for daytime work and maximum readability.',
    category: 'light',
    preview: { primary: '#2563eb', secondary: '#64748b', background: '#f9fafb', surface: '#ffffff' },
    isDark: false,
    isBuiltIn: true,
  },
  dark: {
    id: 'dark',
    name: 'Dark',
    description: 'Reduced eye strain for low-light environments with deep slate tones.',
    category: 'dark',
    preview: { primary: '#3b82f6', secondary: '#94a3b8', background: '#0f172a', surface: '#1e293b' },
    isDark: true,
    isBuiltIn: true,
  },
  'blue-professional': {
    id: 'blue-professional',
    name: 'Blue Professional',
    description: 'Corporate blue palette with crisp whites for a polished business look.',
    category: 'light',
    preview: { primary: '#1e40af', secondary: '#475569', background: '#f0f4ff', surface: '#ffffff' },
    isDark: false,
    isBuiltIn: true,
  },
  'green-business': {
    id: 'green-business',
    name: 'Green Business',
    description: 'Fresh green accents conveying growth, sustainability, and trust.',
    category: 'light',
    preview: { primary: '#059669', secondary: '#6b7280', background: '#f0fdf4', surface: '#ffffff' },
    isDark: false,
    isBuiltIn: true,
  },
  'purple-executive': {
    id: 'purple-executive',
    name: 'Purple Executive',
    description: 'Sophisticated purple tones for leadership dashboards and executive views.',
    category: 'light',
    preview: { primary: '#7c3aed', secondary: '#64748b', background: '#faf5ff', surface: '#ffffff' },
    isDark: false,
    isBuiltIn: true,
  },
  'orange-startup': {
    id: 'orange-startup',
    name: 'Orange Startup',
    description: 'Energetic orange highlights for dynamic, fast-paced teams.',
    category: 'light',
    preview: { primary: '#ea580c', secondary: '#78716c', background: '#fff7ed', surface: '#ffffff' },
    isDark: false,
    isBuiltIn: true,
  },
  'corporate-gray': {
    id: 'corporate-gray',
    name: 'Corporate Gray',
    description: 'Neutral gray palette for understated, enterprise-grade aesthetics.',
    category: 'light',
    preview: { primary: '#374151', secondary: '#6b7280', background: '#f3f4f6', surface: '#ffffff' },
    isDark: false,
    isBuiltIn: true,
  },
  'high-contrast': {
    id: 'high-contrast',
    name: 'High Contrast',
    description: 'WCAG AAA-friendly contrast for maximum accessibility compliance.',
    category: 'accessibility',
    preview: { primary: '#0000ee', secondary: '#000000', background: '#ffffff', surface: '#ffffff' },
    isDark: false,
    isBuiltIn: true,
  },
};

export const BUILT_IN_THEME_LIST: ThemeDefinition[] = Object.values(BUILT_IN_THEMES);

export const MAX_RECENT_THEMES = 5;

/** Maps legacy ThemeMode storage values to new preferences */
export function migrateLegacyThemeMode(legacy: string): Partial<ThemePreferences> {
  switch (legacy) {
    case 'dark':
      return { themeId: 'dark', colorScheme: 'dark' };
    case 'light':
      return { themeId: 'light', colorScheme: 'light' };
    case 'system':
    default:
      return { colorScheme: 'system' };
  }
}
