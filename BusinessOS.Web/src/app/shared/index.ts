/** Shared module public API */
export { APP_ROUTE_PATHS, NAV_ITEMS, ROUTES } from './constants/nav.constants';
export type { NavItem } from './constants/nav.constants';

export { passwordMatchValidator, strongPasswordValidator, getFieldError } from './validators/form.validators';
