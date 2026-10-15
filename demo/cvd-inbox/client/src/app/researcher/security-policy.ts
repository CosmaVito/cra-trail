import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { InboxApi } from '../api/inbox-api';
import { Disclosure } from '../api/models';
import { describeFailure } from '../api/problem';

@Component({
  selector: 'app-security-policy',
  imports: [DatePipe, RouterLink],
  templateUrl: './security-policy.html',
})
export class SecurityPolicy {
  private readonly api = inject(InboxApi);

  protected readonly policy = signal<Disclosure | null>(null);
  protected readonly failure = signal('');

  constructor() {
    this.api.disclosure().then(
      (policy) => this.policy.set(policy),
      (error: unknown) => this.failure.set(describeFailure(error)),
    );
  }
}
