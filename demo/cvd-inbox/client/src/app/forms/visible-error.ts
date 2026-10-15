interface FieldStatus {
  touched(): boolean;
  invalid(): boolean;
  errors(): readonly { message?: string }[];
}

/** The first error of a field the user has already visited, or '' when there is nothing to show yet. */
export function visibleError(field: FieldStatus): string {
  return field.touched() && field.invalid() ? (field.errors()[0]?.message ?? '') : '';
}
