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
