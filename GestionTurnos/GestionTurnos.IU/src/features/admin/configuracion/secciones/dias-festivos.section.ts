import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ApiClient, NotificationService } from '../../../../app/core';
import { DiaFestivo } from '../../../../app/shared/data/modelos';
import { ParametrizacionApi } from '../../../../app/shared/data/apis';
import {
  UiButtonComponent,
  UiDialogComponent,
  UiNotchedFieldComponent,
  UiSkeletonComponent,
} from '../../../../app/shared/ui';
import { formatearFecha, hoyIso } from '../../../../app/shared/utils/formato';

/**
 * CRUD de parametrizacion.dia_festivo. Es la única entidad de parametrización que
 * la API borra físicamente (DELETE /dias-festivos/{fecha}).
 */
@Component({
  selector: 'app-dias-festivos-section',
  imports: [UiButtonComponent, UiDialogComponent, UiNotchedFieldComponent, UiSkeletonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mb-3 flex flex-wrap items-center justify-between gap-3">
      <div class="flex items-center gap-2">
        <label for="festivos-anio" class="text-sm font-semibold text-slate-700">Año</label>
        <select id="festivos-anio" class="filtro-select" [value]="anio()" (change)="cambiarAnio($any($event.target).value)">
          @for (a of anios; track a) {
            <option [value]="a" [selected]="a === anio()">{{ a }}</option>
          }
        </select>
      </div>
      <ui-button (click)="abrir(null)">Nuevo día festivo</ui-button>
    </div>

    @if (cargando()) {
      <ui-skeleton />
    } @else {
      <div class="tabla-contenedor">
        <table class="tabla">
          <thead>
            <tr>
              <th scope="col">Fecha</th>
              <th scope="col">Nombre</th>
              <th scope="col" class="text-right">Acciones</th>
            </tr>
          </thead>
          <tbody>
            @for (f of items(); track f.fecha) {
              <tr>
                <td class="whitespace-nowrap font-semibold">{{ formatearFecha(f.fecha) }}</td>
                <td>{{ f.nombre }}</td>
                <td class="text-right whitespace-nowrap">
                  <button type="button" class="accion-secundaria" (click)="abrir(f)">Editar</button>
                  <button type="button" class="accion-peligro ml-2" (click)="confirmarBorrado.set(f)">Eliminar</button>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="3" class="py-6 text-center text-slate-600">Aún no hay registros para el año {{ anio() }}.</td></tr>
            }
          </tbody>
        </table>
      </div>
    }

    <ui-dialog
      id="dlg-festivo"
      [abierto]="abierto()"
      [titulo]="edicion() ? 'Editar día festivo' : 'Nuevo día festivo'"
      (cerrar)="abierto.set(false)"
    >
      <form class="space-y-5" (submit)="guardar($event)">
        <ui-notched-field
          id="fes-fecha"
          etiqueta="Fecha"
          tipo="date"
          [valor]="fecha()"
          [deshabilitado]="edicion()"
          [obligatorio]="true"
          [error]="errores()['fecha']"
          (valorCambio)="fecha.set($event)"
        />
        <ui-notched-field
          id="fes-nombre"
          etiqueta="Nombre"
          placeholder="Día de la Independencia"
          [valor]="nombre()"
          [obligatorio]="true"
          [error]="errores()['nombre']"
          (valorCambio)="nombre.set($event)"
        />
      </form>
      <ng-container acciones>
        <ui-button variante="secundario" (click)="abierto.set(false)">Cancelar</ui-button>
        <ui-button [cargando]="guardando()" (click)="guardar($event)">Guardar</ui-button>
      </ng-container>
    </ui-dialog>

    <ui-dialog
      id="dlg-festivo-borrar"
      [abierto]="confirmarBorrado() !== null"
      titulo="Eliminar día festivo"
      (cerrar)="confirmarBorrado.set(null)"
    >
      <p class="text-sm text-slate-700">
        ¿Eliminar <strong>{{ confirmarBorrado()?.nombre }}</strong>
        ({{ formatearFecha(confirmarBorrado()?.fecha) }})? Las liquidaciones futuras dejarán de
        tratar ese día como festivo.
      </p>
      <ng-container acciones>
        <ui-button variante="secundario" (click)="confirmarBorrado.set(null)">Cancelar</ui-button>
        <ui-button variante="peligro" [cargando]="guardando()" (click)="eliminar()">Eliminar</ui-button>
      </ng-container>
    </ui-dialog>
  `,
})
export class DiasFestivosSectionComponent implements OnInit {
  private readonly api = inject(ParametrizacionApi);
  private readonly toast = inject(NotificationService);
  protected readonly formatearFecha = formatearFecha;

  private readonly anioActual = Number(hoyIso().slice(0, 4));
  protected readonly anios = [this.anioActual - 1, this.anioActual, this.anioActual + 1, this.anioActual + 2];
  protected readonly anio = signal(this.anioActual);
  protected readonly items = signal<readonly DiaFestivo[]>([]);
  protected readonly cargando = signal(true);
  protected readonly abierto = signal(false);
  protected readonly edicion = signal(false);
  protected readonly guardando = signal(false);
  protected readonly fecha = signal('');
  protected readonly nombre = signal('');
  protected readonly errores = signal<Record<string, string>>({});
  protected readonly confirmarBorrado = signal<DiaFestivo | null>(null);

  ngOnInit(): void {
    this.cargar();
  }

  cambiarAnio(valor: string): void {
    this.anio.set(Number(valor));
    this.cargar();
  }

  cargar(): void {
    this.cargando.set(true);
    this.api.listarDiasFestivos(this.anio()).subscribe({
      next: (l) => {
        this.items.set([...l].sort((a, b) => a.fecha.localeCompare(b.fecha)));
        this.cargando.set(false);
      },
      error: (err: unknown) => {
        this.cargando.set(false);
        this.toast.error(ApiClient.extraerMensajeError(err).general);
      },
    });
  }

  abrir(f: DiaFestivo | null): void {
    this.edicion.set(f !== null);
    this.fecha.set(f?.fecha ?? '');
    this.nombre.set(f?.nombre ?? '');
    this.errores.set({});
    this.abierto.set(true);
  }

  guardar(evento: Event): void {
    evento.preventDefault();
    const errores: Record<string, string> = {};
    if (!this.fecha()) errores['fecha'] = 'La fecha es obligatoria.';
    else if (!this.edicion() && this.items().some((i) => i.fecha === this.fecha()))
      errores['fecha'] = 'Ya existe un festivo en esa fecha. Edítelo en la lista.';
    if (!this.nombre().trim()) errores['nombre'] = 'El nombre es obligatorio.';
    else if (this.nombre().length > 100) errores['nombre'] = 'Máximo 100 caracteres.';
    this.errores.set(errores);
    if (Object.keys(errores).length) return;

    this.guardando.set(true);
    this.api.guardarDiaFestivo({ fecha: this.fecha(), nombre: this.nombre().trim() }).subscribe({
      next: () => {
        this.guardando.set(false);
        this.abierto.set(false);
        this.toast.exito('Día festivo guardado.');
        const anioFecha = Number(this.fecha().slice(0, 4));
        if (anioFecha !== this.anio() && this.anios.includes(anioFecha)) this.anio.set(anioFecha);
        this.cargar();
      },
      error: (err: unknown) => {
        this.guardando.set(false);
        const e = ApiClient.extraerMensajeError(err);
        this.errores.set(e.campos);
        this.toast.error(e.general);
      },
    });
  }

  eliminar(): void {
    const f = this.confirmarBorrado();
    if (!f) return;
    this.guardando.set(true);
    this.api.eliminarDiaFestivo(f.fecha).subscribe({
      next: () => {
        this.guardando.set(false);
        this.confirmarBorrado.set(null);
        this.toast.exito(`"${f.nombre}" eliminado.`);
        this.cargar();
      },
      error: (err: unknown) => {
        this.guardando.set(false);
        this.toast.error(ApiClient.extraerMensajeError(err).general);
      },
    });
  }
}
