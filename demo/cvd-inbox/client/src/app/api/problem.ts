import { HttpErrorResponse } from '@angular/common/http';

/** Turns a failed API call into a sentence for the page. Server faults stay generic. */
export function describeFailure(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) return 'The server cannot be reached. Check your connection and try again.';
    if (error.status === 429) return 'Too many attempts from your network. Wait a while and try again.';
    const title: unknown = error.error?.title;
    if (error.status < 500 && typeof title === 'string') return title;
  }
  return 'Something went wrong on the server. Try again later.';
}

/** The per-field messages of a 400 validation problem, or null when the failure is anything else. */
export function fieldProblems(error: unknown): Record<string, string> | null {
  if (!(error instanceof HttpErrorResponse) || error.status !== 400) return null;
  const errors: unknown = error.error?.errors;
  if (typeof errors !== 'object' || errors === null) return null;
  const found: Record<string, string> = {};
  for (const [key, messages] of Object.entries(errors)) {
    if (Array.isArray(messages) && typeof messages[0] === 'string') found[key.toLowerCase()] = messages[0];
  }
  return Object.keys(found).length ? found : null;
}
