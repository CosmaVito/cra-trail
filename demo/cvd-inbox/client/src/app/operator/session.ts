import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { InboxApi } from '../api/inbox-api';

@Injectable({ providedIn: 'root' })
export class Session {
  private readonly api = inject(InboxApi);
  private readonly current = signal<string | null>(null);
  private loading: Promise<void> | null = null;

  readonly operator = this.current.asReadonly();

  /** Loads the session once. The call also gives the browser its request token (the XSRF-TOKEN cookie). */
  ensureLoaded(): Promise<void> {
    this.loading ??= this.refresh().catch(() => this.current.set(null));
    return this.loading;
  }

  async signIn(name: string, password: string): Promise<void> {
    await this.api.signIn(name, password);
    // The request token is bound to the user, so a new one is needed now.
    await this.refresh();
  }

  /** A 401 means the session cookie had expired already: the operator is signed out either way. */
  async signOut(): Promise<void> {
    try {
      await this.api.signOut();
    } catch (error) {
      if (!(error instanceof HttpErrorResponse && error.status === 401)) throw error;
    } finally {
      this.current.set(null);
      // The request token is bound to the user, so the signed-out browser needs a new one.
      await this.refresh();
    }
  }

  /** Forgets the operator without asking the server, for example after a 401. */
  expire(): void {
    this.current.set(null);
    this.loading = null;
  }

  private async refresh(): Promise<void> {
    const session = await this.api.session();
    this.current.set(session.operator);
  }
}
