import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { Session } from './session';

/** A 401 from the inbox API means the session cookie expired: forget the operator and go to the sign-in page. */
export const sessionExpired: HttpInterceptorFn = (request, next) => {
  const session = inject(Session);
  const router = inject(Router);
  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && request.url.startsWith('/api/inbox')) {
        session.expire();
        void router.navigateByUrl('/operator/sign-in');
      }
      return throwError(() => error);
    }),
  );
};
