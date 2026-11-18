import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-sin-permiso-page',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section
      aria-labelledby="titulo-sin-permiso"
      class="mx-auto flex max-w-lg flex-col items-center justify-center rounded-2xl border border-slate-200 bg-white p-8 text-center shadow-xs"
    >
      <span
        class="mb-3 flex h-12 w-12 items-center justify-center rounded-full bg-red-100 text-lg font-extrabold text-red-900"
        aria-hidden="true"
      >
        !
      </span>
      <h1 id="titulo-sin-permiso" class="text-xl font-extrabold text-slate-900">
        No tienes permiso para acceder a esta sección
      </h1>
      <p class="mt-2 text-sm text-slate-600">
        Esta pantalla requiere un código de permiso específico en tu rol. Si necesitas acceso,
        solicítalo al administrador del sistema.
      </p>
      <div class="mt-6">
        <a
          routerLink="/mi"
          class="inline-flex min-h-11 items-center justify-center rounded-lg bg-marca-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-marca-700"
        >
          Ir a mi inicio
        </a>
      </div>
    </section>
  `,
})
export class SinPermisoPageComponent {}
