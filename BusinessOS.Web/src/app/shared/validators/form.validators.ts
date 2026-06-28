import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export function passwordMatchValidator(passwordField = 'password', confirmField = 'confirmPassword'): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const password = control.get(passwordField)?.value;
    const confirm = control.get(confirmField)?.value;
    if (password !== confirm) {
      control.get(confirmField)?.setErrors({ passwordMismatch: true });
      return { passwordMismatch: true };
    }
    return null;
  };
}

export function strongPasswordValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value as string;
    if (!value) return null;
    const hasMinLength = value.length >= 8;
    const hasUpper = /[A-Z]/.test(value);
    const hasLower = /[a-z]/.test(value);
    const hasNumber = /\d/.test(value);
    if (!hasMinLength || !hasUpper || !hasLower || !hasNumber) {
      return { weakPassword: true };
    }
    return null;
  };
}

export function getFieldError(control: AbstractControl | null, fieldName: string): string | null {
  if (!control?.errors || !control.touched) return null;
  if (control.errors['required']) return `${fieldName} is required`;
  if (control.errors['email']) return 'Enter a valid email address';
  if (control.errors['minlength']) return `${fieldName} must be at least ${control.errors['minlength'].requiredLength} characters`;
  if (control.errors['maxlength']) return `${fieldName} must not exceed ${control.errors['maxlength'].requiredLength} characters`;
  if (control.errors['passwordMismatch']) return 'Passwords do not match';
  if (control.errors['weakPassword']) return 'Password must be 8+ chars with upper, lower, and number';
  return 'Invalid value';
}
