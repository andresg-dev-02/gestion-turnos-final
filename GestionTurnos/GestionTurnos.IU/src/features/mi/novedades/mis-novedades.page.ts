import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { ApiClient, NotificationService } from '../../../app/core';
import { Novedad, TipoNovedad } from '../../../app/shared/data/modelos';
import { MiApi, ParametrizacionApi } from '../../../app/shared/data/apis';
import {
  OpcionSelect,
  UiBadgeComponent,
  UiButtonComponent,
  UiNotchedFieldComponent,
  UiNotchedSelectComponent,
  UiNotchedTextareaComponent,
  UiSkeletonComponent,
} from '../../../app/shared/ui';
import { formatearFechaHora, localBogotaAIso } from '../../../app/shared/utils/formato';

/**
 * Mis novedades (/mi/novedades): permisos personales, calamidades e incapacidades.
 * Solo se ofrecen los tipos permitidos al empleado (soloPermitidosEmpleado=true).
 */
@Component({
  selector: 'app-mis-novedades-page',
  imports: [
    UiBadgeComponent,
    UiButtonComponent,
    UiNotchedFieldComponent,
    UiNotchedSelectComponent,
    UiNotchedTextareaComponent,
    UiSkeletonComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="titulo-mis-novedades" class="space-y-6">
      <header>
        <h1 id="titulo-mis-novedades" class="text-2xl font-extrabold tracking-tight text-slate-900">Mis permisos y novedades</h1>
        <p class="mt-1 text-sm text-slate-600">Solicita permisos o reporta incapacidades y calamidades.</p>
      </header>

      @if (cargando()) {
        <ui-skeleton [filas]="3" />
      } @else if (errorCarga()) {
        <p class="aviso-alerta">{{ errorCarga() }}</p>
      } @else {
        <div class="grid grid-cols-1 gap-6 lg:grid-cols-5">
          <article class="tarjeta lg:col-span-2" aria-labelledby="titulo-nueva-nov">
            <h2 id="titulo-nueva-nov" class="text-base font-bold text-slate-900">Nueva solicitud</h2>
            <form class="mt-5 space-y-5" (submit)="solicitar($event)" novalidate>
              <ui-notched-select id="nov-tipo" etiqueta="Tipo" [opciones]="opcionesTipo()" [valor]="codTipo()" [obligatorio]="true"
                [error]="errores()['codTipoNovedad']" (valorCambio)="codTipo.set($event)" />
              @if (tipoSeleccionado(); as t) {
                <p class="text-xs text-slate-600">
                  {{ t.remuneradoDefecto ? 'Remunerado' : 'No remunerado' }} por defecto
                  · {{ t.requiereAprobacion ? 'requiere aprobación' : 'no requiere aprobación' }}
                  @if (t.requiereSoporte) { · <strong>requiere soporte</strong> }
                </p>
              }
              <ui-notched-field id="nov-inicio" etiqueta="Inicio" tipo="datetime-local" [valor]="inicio()" [obligatorio]="true"
                [error]="errores()['inicio']" (valorCambio)="inicio.set($event)" />
              <ui-notched-field id="nov-fin" etiqueta="Fin" tipo="datetime-local" [valor]="fin()" [obligatorio]="true"
                [error]="errores()['fin']" (valorCambio)="fin.set($event)" />
              <ui-notched-field id="nov-soporte" etiqueta="Enlace al soporte" placeholder="https://..."
                [obligatorio]="tipoSeleccionado()?.requiereSoporte ?? false"
                ayuda="Enlace al documento (incapacidad, constancia...) en tu almacenamiento."
                [valor]="urlSoporte()" [error]="errores()['urlSoporte']" (valorCambio)="urlSoporte.set($event)" />
              <ui-notched-textarea id="nov-obs" etiqueta="Observaciones" [valor]="observaciones()" (valorCambio)="observaciones.set($event)" />
              <ui-button tipo="submit" [anchoCompleto]="true" [cargando]="enviando()">Enviar solicitud</ui-button>
            </form>
          </article>

          <article class="tarjeta lg:col-span-3" aria-labelledby="titulo-hist-nov">
            <h2 id="titulo-hist-nov" class="mb-3 text-base font-bold text-slate-900">Mis solicitudes</h2>
            <ul class="divide-y divide-slate-100">
              @for (n of novedades(); track n.idNovedad) {
                <li class="flex flex-wrap items-start justify-between gap-3 py-3">
                  <div>
                    <p class="font-semibold text-slate-900">{{ n.nombreTipoNovedad }}</p>
                    <p class="text-xs text-slate-600">{{ formatearFechaHora(n.inicio) }} → {{ formatearFechaHora(n.fin) }}</p>
                    @if (n.observaciones) { <p class="mt-1 text-xs text-slate-700">{{ n.observaciones }}</p> }
                    @if (n.motivoRechazo) { <p class="mt-1 text-xs text-peligro">Motivo: {{ n.motivoRechazo }}</p> }
                  </div>
                  <div class="flex items-center gap-2">
                    <ui-badge [estado]="n.codEstadoSolicitud" />
                    @if (n.codEstadoSolicitud === 'PENDIENTE') {
                      <button type="button" class="accion-secundaria" [disabled]="cancelando() === n.idNovedad" (click)="cancelar(n)">Cancelar</button>
                    }
                  </div>
                </li>
              } @empty {
                <li class="py-6 text-center text-sm text-slate-600">No tienes novedades registradas.</li>
              }
            </ul>
          </article>
        </div>
      }
    </section>
  `,
})
export class MisNovedadesPageComponent implements OnInit {
  private readonly api = inject(MiApi);
  private readonly param = inject(ParametrizacionApi);
  private readonly toast = inject(NotificationService);
  protected readonly formatearFechaHora = formatearFechaHora;

  private readonly tipos = signal<readonly TipoNovedad[]>([]);
  protected readonly novedades = signal<readonly Novedad[]>([]);
  protected readonly cargando = signal(true);
  protected readonly errorCarga = signal<string | null>(null);
  protected readonly codTipo = signal('');
  protected readonly inicio = signal('');
  protected readonly fin = signal('');
  protected readonly urlSoporte = signal('');
  protected readonly observaciones = signal('');
  protected readonly errores = signal<Record<string, string>>({});
  protected readonly enviando = signal(false);
  protected readonly cancelando = signal<number | null>(null);

  protected readonly opcionesTipo = computed<readonly OpcionSelect[]>(() => [
    { valor: '', etiqueta: 'Seleccione el tipo' },
    ...this.tipos().map((t) => ({ valor: t.codTipoNovedad, etiqueta: t.nombre })),
  ]);
  protected readonly tipoSeleccionado = computed(() => this.tipos().find((t) => t.codTipoNovedad === this.codTipo()) ?? null);

  ngOnInit(): void {
    forkJoin({ tipos: this.param.listarTiposNovedad(true, true), novedades: this.api.misNovedades() }).subscribe({
      next: ({ tipos, novedades }) => {
        this.tipos.set(tipos);
        this.ordenar(novedades);
        this.cargando.set(false);
      },
      error: (err: unknown) => {
        this.cargando.set(false);
        this.errorCarga.set(ApiClient.extraerMensajeError(err).general);
      },
    });
  }

  private ordenar(lista: readonly Novedad[]): void {
    this.novedades.set([...lista].sort((a, b) => b.registradoEn.localeCompare(a.registradoEn)));
  }

  private recargar(): void {
    this.api.misNovedades().subscribe((l) => this.ordenar(l));
  }

  solicitar(evento: Event): void {
    evento.preventDefault();
    const url = this.urlSoporte().trim();
    const errores: Record<string, string> = {};
    if (!this.codTipo()) errores['codTipoNovedad'] = 'Selecciona el tipo.';
    if (!this.inicio()) errores['inicio'] = 'Indica el inicio.';
    if (!this.fin()) errores['fin'] = 'Indica el fin.';
    else if (this.inicio() && this.fin() <= this.inicio()) errores['fin'] = 'Debe ser posterior al inicio.';
    if (this.tipoSeleccionado()?.requiereSoporte && !url) errores['urlSoporte'] = 'Este tipo requiere soporte.';
    else if (url && !/^https?:\/\//i.test(url)) errores['urlSoporte'] = 'Debe ser un enlace http(s).';
    this.errores.set(errores);
    if (Object.keys(errores).length) return;

    this.enviando.set(true);
    this.api
      .solicitarNovedad({
        codTipoNovedad: this.codTipo(),
        inicio: localBogotaAIso(this.inicio()),
        fin: localBogotaAIso(this.fin()),
        observaciones: this.observaciones().trim() || null,
        urlSoporte: url || null,
      })
      .subscribe({
        next: () => {
          this.enviando.set(false);
          this.toast.exito('Solicitud enviada.');
          this.codTipo.set('');
          this.inicio.set('');
          this.fin.set('');
          this.urlSoporte.set('');
          this.observaciones.set('');
          this.recargar();
        },
        error: (err: unknown) => {
          this.enviando.set(false);
          const e = ApiClient.extraerMensajeError(err);
          this.errores.set(e.campos);
          this.toast.error(e.general);
        },
      });
  }

  cancelar(n: Novedad): void {
    this.cancelando.set(n.idNovedad);
    this.api.cancelarNovedad(n.idNovedad).subscribe({
      next: () => {
        this.cancelando.set(null);
        this.toast.exito('Solicitud cancelada.');
        this.recargar();
      },
      error: (err: unknown) => {
        this.cancelando.set(null);
        this.toast.error(ApiClient.extraerMensajeError(err).general);
      },
    });
  }
}
