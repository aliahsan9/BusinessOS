export class StorageHelper {
  static get<T>(key: string): T | null {
    try {
      const raw = localStorage.getItem(key);
      if (!raw) {
        return null;
      }
      return JSON.parse(raw) as T;
    } catch {
      return null;
    }
  }

  static set<T>(key: string, value: T): void {
    localStorage.setItem(key, JSON.stringify(value));
  }

  static remove(key: string): void {
    localStorage.removeItem(key);
  }

  static getString(key: string): string | null {
    return localStorage.getItem(key);
  }

  static setString(key: string, value: string): void {
    localStorage.setItem(key, value);
  }

  static clear(keys: string[]): void {
    keys.forEach((key) => localStorage.removeItem(key));
  }
}
