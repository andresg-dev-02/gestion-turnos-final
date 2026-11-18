import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { ApiClient, NotificationService } from '../../../../app/core';
import { Parametro, ParametroValor } from '../../../../app/shared/data/modelos';
import { ParametrizacionApi } from '../../../../app/shared/data/apis';
import {
  OpcionSelect,
  UiButtonComponent,
  UiDialogComponent,
  UiNotchedFieldComponent,
  UiNotchedSelectComponent,
  UiSkeletonComponent,
} from '../../../../app/shared/ui';
import { formatearFecha, hoyIso } from '../../../../app/shared/utils/formato';

const AYUDA_TIPO: Record<Parametro['tipoDato'], string> = {
  ENTERO: 'Número entero, por ejemplo 5.',
  DECIMAL: 'Número con punto decimal, por ejemplo 10.5.',
  TEXTO: 'Texto libre.',
  BOOLEANO: 'Sí o No.',
  HORA: 'Hora en formato 24 h, por ejemplo 19:00.',
};

/**
 * Parámetros de negocio con vigencia (parametrizacion.parametro / parametro_valor).
 * No se editan ni borran valores: se registra una nueva vigencia y la API cierra la anterior
 * en la misma transacción (RG-10), conservando el historial.
 */
@Component({
  selector: 'app-parametros-section',
  imports: [
    UiButtonComponent,
    UiDialogComponent,
    UiNotchedFieldComponent,
    UiNotchedSelectComponent,
    UiSkeletonComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="mb-3 text-sm text-slate-600">
      Cambiar un valor crea una nueva vigencia desde la fecha indicada; el valor anterior se conserva
      en el historial para los cálculos de fechas pasadas.
    </p>

    @if (cargando()) {
      <ui-skeleton />
    } @else {
      <div class="tabla-contenedor">
        <table class="tabla">
          <thead>
            <tr>
              <th scope="col">Parámetro</th>
              <th scope="col">Tipo</th>
              <th scope="col">Valor vigente</th>
              <th scope="col">Desde</th>
              <th scope="col" class="text-right">Acciones</th>
            </tr>
          </thead>
          <tbody>
            @for (p of items(); track p.codParametro) {
              <tr>
                <td>
                  <span class="font-semibold text-slate-900">{{ p.nombre }}</span>
                  <span class="block font-mono text-xs text-slate-500">{{ p.codParametro }}</span>
                </td>
                <td class="text-xs font-semibold text-slate-600">{{ p.tipoDato }}</td>
                <td class="font-mono font-bold text-marca-700">{{ mostrarValor(p) }}</td>
                <td class="whitespace-nowrap">{{ formatearFecha(p.vigenteDesde) }}</td>
                <td class="text-right whitespace-nowrap">
                  <button type="button" class="accion-secundaria" (click)="verHistorial(p)">Historial</button>
                  @if (p.editable) {
                    <button type="button" class="accion-primaria ml-2" (click)="abrir(p)">Cambiar valor</button>
                  } @else {
                    <span class="ml-2 text-xs text-slate-500">No editable</span>
                  }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="5" class="py-6 text-center text-slate-600">Aún no hay registros con este filtro.</td></tr>
            }
          </tbody>
        </table>
      </div>
    }

    <ui-dialog
      id="dlg-parametro"
      [abierto]="seleccionado() !== null"
      [titulo]="'Cambiar ' + (seleccionado()?.nombre ?? '')"
      [subtitulo]="'Valor actual: ' + (seleccionado() ? mostrarValor(seleccionado()!) : '')"
      (cerrar)="seleccionado.set(null)"
    >
      <form class="space-y-5" (submit)="guardar($event)">
        @if (seleccionado()?.tipoDato === 'BOOLEANO') {
          <ui-notched-select
            id="par-valor"
            etiqueta="Nuevo valor"
            [opciones]="opcionesBooleano"
            [valor]="valor()"
            (valorCambio)="valor.set($event)"
          />
        } @else {
          <ui-notched-field
            id="par-valor"
            etiqueta="Nuevo valor"
            [tipo]="tipoInput()"
            [paso]="seleccionado()?.tipoDato === 'DECIMAL' ? '0.01' : null"
            [valor]="valor()"
            [obligatorio]="true"
            [ayuda]="ayuda()"
            [error]="errores()['valor']"
            (valorCambio)="valor.set($event)"
          />
        }
        <ui-notched-field
          id="par-desde"
          etiqueta="Vigente desde"
          tipo="date"
          [valor]="vigenteDesde()"
          [obligatorio]="true"
          [error]="errores()['vigenteDesde']"
          (valorCambio)="vigenteDesde.set($event)"
        />
      </form>
      <ng-container acciones>
        <ui-button variante="secundario" (click)="seleccionado.set(null)">Cancelar</ui-button>
        <ui-button [cargando]="guardando()" (click)="guardar($event)">Registrar nueva vigencia</ui-button>
      </ng-container>
    </ui-dialog>

    <ui-dialog
      id="dlg-parametro-historial"
      ancho="lg"
      [abierto]="historialDe() !== null"
      [titulo]="'Historial: ' + (historialDe()?.nombre ?? '')"
      (cerrar)="historialDe.set(null)"
    >
      @if (cargandoHistorial()) {
        <ui-skeleton [filas]="2" />
      } @else {
        <div class="tabla-contenedor">
          <table class="tabla">
            <thead>
              <tr><th scope="col">Valor</th><th scope="col">Desde</th><th scope="col">Hasta</th></tr>
            </thead>
            <tbody>
              @for (h of historial(); track h.idParametroValor) {
                <tr>
                  <td class="font-mono font-semibold">{{ h.valor }}</td>
                  <td>{{ formatearFecha(h.vigenteDesde) }}</td>
                  <td>{{ h.vigenteHasta ? formatearFecha(h.vigenteHasta) : 'Vigente' }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
      <ng-container acciones>
        <ui-button variante="secundario" (click)="historialDe.set(null)">Cerrar</ui-button>
      </ng-container>
    </ui-dialog>
  `,
})
export class ParametrosSectionComponent implements OnInit {
  private readonly api = inject(ParametrizacionApi);
  private readonly toast = inject(NotificationService);
  protected readonly formatearFecha = formatearFecha;

  protected readonly opcionesBooleano: readonly OpcionSelect[] = [
    { valor: 'true', etiqueta: 'Sí' },
    { valor: 'false', etiqueta: 'No' },
  ];

  protected readonly items = signal<readonly Parametro[]>([]);
  protected readonly cargando = signal(true);
  protected readonly seleccionado = signal<Parametro | null>(null);
  protected readonly valor = signal('');
  protected readonly vigenteDesde = signal(hoyIso());
  protected readonly guardando = signal(false);
  protected readonly errores = signal<Record<string, string>>({});
  protected readonly historialDe = signal<Parametro | null>(null);
  protected readonly historial = signal<readonly ParametroValor[]>([]);
  protected readonly cargandoHistorial = signal(false);

  protected readonly tipoInput = computed(() => {
    const t = this.seleccionado()?.tipoDato;
    return t === 'ENTERO' || t === 'DECIMAL' ? 'number' : t === 'HORA' ? 'time' : 'text';
  });
  protected readonly ayuda = computed(() => {
    const t = this.seleccionado()?.tipoDato;
    return t ? AYUDA_TIPO[t] : null;
  });

  ngOnInit(): void {
    this.cargar();
  }

  mostrarValor(p: Parametro): string {
    if (p.valorVigente === null) return 'Sin valor';
    if (p.tipoDato === 'BOOLEANO') return p.valorVigente === 'true' ? 'Sí' : 'No';
    return p.valorVigente;
  }

  cargar(): void {
    this.cargando.set(true);
    this.api.listarParametros().subscribe({
      next: (l) => {
        this.items.set(l);
        this.cargando.set(false);
      },
      error: (err: unknown) => {
        this.cargando.set(false);
        this.toast.error(ApiClient.extraerMensajeError(err).general);
      },
    });
  }

  abrir(p: Parametro): void {
    this.valor.set(p.valorVigente ?? (p.tipoDato === 'BOOLEANO' ? 'true' : ''));
    this.vigenteDesde.set(hoyIso());
    this.errores.set({});
    this.seleccionado.set(p);
  }

  guardar(evento: Event): void {
    evento.preventDefault();
    const p = this.seleccionado();
    if (!p) return;
    const valor = this.valor().trim();
    const errores: Record<string, string> = {};
    if (!valor) errores['valor'] = 'El valor es obligatorio.';
    else if (p.tipoDato === 'ENTERO' && !/^-?\d+$/.test(valor)) errores['valor'] = 'Debe ser un número entero.';
    else if (p.tipoDato === 'DECIMAL' && !/^-?\d+(\.\d+)?$/.test(valor)) errores['valor'] = 'Debe ser un número decimal (use punto).';
    else if (p.tipoDato === 'HORA' && !/^\d{2}:\d{2}$/.test(valor.slice(0, 5))) errores['valor'] = 'Formato HH:mm.';
    if (!this.vigenteDesde()) errores['vigenteDesde'] = 'La fecha es obligatoria.';
    else if (p.vigenteDesde && this.vigenteDesde() <= p.vigenteDesde)
      errores['vigenteDesde'] = `Debe ser posterior a la vigencia actual (${formatearFecha(p.vigenteDesde)}).`;
    this.errores.set(errores);
    if (Object.keys(errores).length) return;

    const valorFinal = p.tipoDato === 'HORA' ? valor.slice(0, 5) : valor;
    this.guardando.set(true);
    this.api.cambiarValorParametro(p.codParametro, valorFinal, this.vigenteDesde()).subscribe({
      next: () => {
        this.guardando.set(false);
        this.seleccionado.set(null);
        this.toast.exito(`Nueva vigencia registrada para "${p.nombre}".`);
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

  verHistorial(p: Parametro): void {
    this.historialDe.set(p);
    this.cargandoHistorial.set(true);
    this.api.historialParametro(p.codParametro).subscribe({
      next: (h) => {
        this.historial.set([...h].sort((a, b) => b.vigenteDesde.localeCompare(a.vigenteDesde)));
        this.cargandoHistorial.set(false);
      },
      error: (err: unknown) => {
        this.cargandoHistorial.set(false);
        this.toast.error(ApiClient.extraerMensajeError(err).general);
      },
    });
  }
}
