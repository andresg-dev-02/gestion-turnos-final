import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'ui-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col items-center justify-center rounded-2xl border border-dashed border-slate-300 bg-white p-8 text-center">
      <div
        class="mb-3 flex h-12 w-12 items-center justify-center rounded-full bg-marca-50 text-marca-700"
        aria-hidden="true"
      >
        <span class="text-lg font-bold">i</span>
      </div>
      <h3 class="text-base font-bold text-slate-900">{{ titulo() }}</h3>
      <p class="mt-1 max-w-md text-sm text-slate-600">{{ descripcion() }}</p>
      <div class="mt-4">
        <ng-content />
      </div>
    </div>
  `,
})
export class UiEmptyStateComponent {
  readonly titulo = input.required<string>();
  readonly descripcion = input.required<string>();
}
