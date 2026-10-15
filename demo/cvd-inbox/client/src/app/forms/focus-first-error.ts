import { FieldTree } from '@angular/forms/signals';

/** For a form's `onInvalid`: moves focus to the first control that has an error. */
export function focusFirstError(form: FieldTree<unknown>): void {
  form().errorSummary()[0]?.fieldTree().focusBoundControl();
}
