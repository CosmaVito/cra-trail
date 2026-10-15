import { HttpErrorResponse } from '@angular/common/http';
import { describeFailure } from './problem';

describe('describeFailure', () => {
  it('explains network errors, rate limits and refusals from the server', () => {
    expect(describeFailure(new HttpErrorResponse({ status: 0 }))).toContain('cannot be reached');
    expect(describeFailure(new HttpErrorResponse({ status: 429 }))).toContain('Too many attempts');
    expect(describeFailure(new HttpErrorResponse({ status: 400, error: { title: 'The page expired. Reload it and try again.' } })))
      .toBe('The page expired. Reload it and try again.');
  });

  it('keeps server faults and anything else generic', () => {
    expect(describeFailure(new HttpErrorResponse({ status: 500, error: { title: 'NullReferenceException' } })))
      .toBe('Something went wrong on the server. Try again later.');
    expect(describeFailure(new Error('boom'))).toBe('Something went wrong on the server. Try again later.');
  });
});
