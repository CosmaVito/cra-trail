import { FieldTree } from '@angular/forms/signals';
import { fieldProblems } from '../api/problem';

/**
 * Maps a 400 validation problem onto the form's fields as submission errors, so the usual
 * visible-error path shows them, and focuses the first field that has one. Returns undefined
 * when the failure is not a field problem or names no known field.
 */
export function serverFieldErrors(error: unknown, fields: Record<string, FieldTree<unknown>>) {
  const problems = fieldProblems(error);
  if (!problems) return undefined;
  const mapped = Object.entries(fields)
    .filter(([name]) => name in problems)
    .map(([name, fieldTree]) => ({ fieldTree, kind: 'server', message: problems[name] }));
  if (!mapped.length) return undefined;
  mapped[0].fieldTree().focusBoundControl();
  return mapped;
}
