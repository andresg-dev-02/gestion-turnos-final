import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AuthStore } from '../../../app/core';
import { MarcajeRapidoCardComponent } from '../components/marcaje-rapido-card.component';
import { ResumenVacacionesCardComponent } from '../components/resumen-vacaciones-card.component';
import { TurnoHoyCardComponent } from '../components/turno-hoy-card.component';

/**
 * Página recopilatoria de Inicio del Empleado (/mi).
 * Únicamente ensambla los componentes especializados de turno del día, marcaje rápido y vacaciones.
 */
@Component({
  selector: 'app-mi-inicio-page',
  imports: [TurnoHoyCardComponent, MarcajeRapidoCardComponent, ResumenVacacionesCardComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="encabezado-mi-inicio" class="space-y-6">
      <header>
        <h1 id="encabezado-mi-inicio" class="text-2xl font-extrabold tracking-tight text-slate-900">
          Hola, {{ store.usuario()?.nombreCompleto || ' Colaborador' }}
        </h1>
        <p class="mt-1 text-sm text-slate-600">
          Resumen de tu jornada, marcaje de asistencia y solicitudes activas.
        </p>
      </header>

      <div class="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <app-turno-hoy-card #turnoHoy />
        <div class="space-y-6">
          <app-marcaje-rapido-card (marcado)="turnoHoy.cargar()" />
          <app-resumen-vacaciones-card />
        </div>
      </div>
    </section>
  `,
})
export class MiInicioPageComponent {
  protected readonly store = inject(AuthStore);
}
