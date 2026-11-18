import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { UiEmptyStateComponent } from '../../../app/shared/ui';
import { MarcajeRapidoCardComponent } from '../components/marcaje-rapido-card.component';

/**
 * Página recopilatoria para las subrutas del empleado (/mi/calendario, /mi/asistencia, etc.).
 */
@Component({
  selector: 'app-mi-seccion-page',
  imports: [UiEmptyStateComponent, MarcajeRapidoCardComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="titulo-seccion-empleado" class="space-y-6">
      <header>
        <h1 id="titulo-seccion-empleado" class="text-2xl font-extrabold tracking-tight text-slate-900">
          {{ titulo }}
        </h1>
        <p class="mt-1 text-sm text-slate-600">{{ descripcion }}</p>
      </header>

      @if (esAsistencia) {
        <app-marcaje-rapido-card />
      }

      <ui-empty-state [titulo]="titulo" [descripcion]="descripcion">
        <p class="text-sm font-medium text-slate-500">Aún no hay registros en esta sección.</p>
      </ui-empty-state>
    </section>
  `,
})
export class MiSeccionPageComponent {
  private readonly route = inject(ActivatedRoute);
  protected readonly titulo = (this.route.snapshot.data['titulo'] as string) || 'Mi sección';
  protected readonly descripcion =
    (this.route.snapshot.data['descripcion'] as string) ||
    'Módulo preparado para consulta conectada al endpoint del empleado.';
  protected readonly esAsistencia = Boolean(this.route.snapshot.data['esAsistencia']);
}
