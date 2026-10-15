import { ComponentFixture } from '@angular/core/testing';

/** Lets pending promise callbacks and timers run once. */
export function tick(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve));
}

/** Waits for promise callbacks, then for Angular to render. Call it only when no HTTP request is open. */
export async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  await tick();
  await fixture.whenStable();
}

export function element(fixture: ComponentFixture<unknown>): HTMLElement {
  return fixture.nativeElement as HTMLElement;
}

export function typeInto(fixture: ComponentFixture<unknown>, id: string, value: string): void {
  const field = element(fixture).querySelector<HTMLInputElement | HTMLTextAreaElement>(`#${id}`);
  if (!field) throw new Error(`No field #${id}`);
  field.value = value;
  field.dispatchEvent(new Event('input'));
}

export function clickButton(fixture: ComponentFixture<unknown>, label: string): void {
  const button = Array.from(element(fixture).querySelectorAll('button')).find((b) => b.textContent?.trim() === label);
  if (!button) throw new Error(`No button "${label}"`);
  button.click();
}

export function textOf(fixture: ComponentFixture<unknown>, selector: string): string {
  return element(fixture).querySelector(selector)?.textContent?.trim() ?? '';
}
