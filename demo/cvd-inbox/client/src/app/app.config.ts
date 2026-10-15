import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { Session } from './operator/session';
import { sessionExpired } from './operator/session-expired';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([sessionExpired])),
    // Loads the session and, with it, the request token before any form can be sent.
    provideAppInitializer(() => inject(Session).ensureLoaded()),
  ],
};
