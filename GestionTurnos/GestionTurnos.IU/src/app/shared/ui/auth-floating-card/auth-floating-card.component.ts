import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'ui-auth-floating-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="relative mx-auto w-full max-w-md">
      <!-- Cuadrícula decorativa superior izquierda (puntos verde menta #31BBA1) -->
      <div
        aria-hidden="true"
        class="patron-puntos-menta pointer-events-none absolute -top-7 -left-7 h-28 w-28 opacity-90"
      ></div>

      <!-- Cuadrícula decorativa inferior derecha (puntos azul marino #113459) -->
      <div
        aria-hidden="true"
        class="patron-puntos-marino pointer-events-none absolute -right-7 -bottom-7 h-28 w-28 opacity-90"
      ></div>

      <!-- Tarjeta flotante principal -->
      <section
        [attr.aria-labelledby]="idTitulo()"
        class="relative z-10 rounded-2xl border border-slate-200/80 bg-white px-7 py-8 shadow-[0_20px_50px_rgba(17,52,89,0.12)] sm:px-9 sm:py-10"
      >
        <header class="mb-6 text-center">
          <img
            src="/Logo_APP.png"
            alt="Logo Gestión de Turnos"
            width="60"
            height="60"
            class="mx-auto mb-3 h-15 w-15 object-contain"
          />
          <h1
            [id]="idTitulo()"
            class="text-2xl font-extrabold tracking-tight text-[#113459] sm:text-3xl"
          >
            {{ titulo() }}
          </h1>
          @if (subtitulo()) {
            <p class="mt-2 text-sm text-slate-600">{{ subtitulo() }}</p>
          }
        </header>

        <ng-content />
      </section>
    </div>
  `,
})
export class AuthFloatingCardComponent {
  readonly titulo = input.required<string>();
  readonly subtitulo = input<string>('');
  readonly idTitulo = input('auth-card-heading');
}
