import { SchemaPath, validate } from '@angular/forms/signals';

/** Checks a text field the way the server does: on the trimmed value. */
export function trimmedLength(path: SchemaPath<string>, rules: { required?: string; min?: [number, string]; max: [number, string] }): void {
  validate(path, ({ value }) => {
    const length = value().trim().length;
    if (length === 0) return rules.required === undefined ? undefined : { kind: 'required', message: rules.required };
    if (rules.min && length < rules.min[0]) return { kind: 'minLength', message: rules.min[1] };
    if (length > rules.max[0]) return { kind: 'maxLength', message: rules.max[1] };
    return undefined;
  });
}
