export const DEFAULT_CURRENCY_CODE = 'USD';

export const SUPPORTED_CURRENCY_CODES = [
  'USD',
  'PKR',
  'EUR',
  'GBP',
  'AED',
  'SAR',
  'INR',
  'CAD',
  'AUD',
] as const;

export type SupportedCurrencyCode = (typeof SUPPORTED_CURRENCY_CODES)[number];
