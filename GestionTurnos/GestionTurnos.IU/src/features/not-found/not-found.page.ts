import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../app/core';

@Component({
  selector: 'app-not-found-page',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main
      id="contenido-principal"
      tabindex="-1"
      class="flex min-h-dvh flex-col items-center justify-center bg-white px-4 py-12 text-center sm:px-6 lg:px-8 focus:outline-none"
    >
      <div class="mx-auto flex w-full max-w-2xl flex-col items-center">
        <!-- Ilustración oficial 404 del sistema con fondo blanco original -->
        <div class="relative w-full max-w-lg overflow-hidden">
          <img
            src="/404.webp"
            alt="Página no encontrada - Error 404"
            width="688"
            height="384"
            priority
            class="h-auto w-full object-contain"
          />
        </div>

        <header class="mt-6 space-y-2">
          <p class="text-xs font-bold tracking-widest text-[#1D768A] uppercase">
            Error 404 · Turno Fuera de Rango
          </p>
          <h1 class="text-2xl font-extrabold tracking-tight text-[#113459] sm:text-3xl">
            Página no encontrada
          </h1>
          <p class="mx-auto max-w-md text-sm leading-relaxed text-slate-600 sm:text-base">
            La ruta a la que intentas acceder no existe, fue reubicada o el turno programado ha
            finalizado.
          </p>
        </header>

        <div class="mt-8 flex flex-wrap items-center justify-center gap-3">
          <a
            [routerLink]="rutaDestino()"
            class="inline-flex min-h-11 items-center justify-center gap-2 rounded-lg bg-[#1D768A] px-6 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:bg-[#165e6e] focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-[#1D768A]/25"
          >
            Volver al inicio
          </a>

          @if (!store.autenticado()) {
            <a
              routerLink="/ingresar"
              class="inline-flex min-h-11 items-center justify-center rounded-lg border border-slate-300 bg-white px-5 py-2.5 text-sm font-semibold text-[#113459] transition hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-[#1D768A]/25"
            >
              Iniciar sesión
            </a>
          }
        </div>
      </div>
    </main>
  `,
})
export class NotFoundPageComponent {
  protected readonly store = inject(AuthStore);

  rutaDestino(): string {
    return this.store.autenticado() ? '/mi' : '/ingresar';
  }
}
