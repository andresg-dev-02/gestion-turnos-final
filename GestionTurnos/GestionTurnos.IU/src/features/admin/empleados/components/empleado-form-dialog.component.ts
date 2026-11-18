import {
  ChangeDetectionStrategy,
  Component,
  effect,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ActualizarEmpleadoDto,
  Area,
  Cargo,
  CatalogoItem,
  CrearEmpleadoDto,
  Empleado,
} from '../../../../app/shared/data';
import {
  OpcionSelect,
  UiButtonComponent,
  UiDialogComponent,
  UiNotchedFieldComponent,
  UiNotchedSelectComponent,
} from '../../../../app/shared/ui';

@Component({
  selector: 'app-empleado-form-dialog',
  imports: [
    FormsModule,
    UiDialogComponent,
    UiNotchedFieldComponent,
    UiNotchedSelectComponent,
    UiButtonComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-dialog
      id="dlg-empleado-form"
      [abierto]="abierto()"
      [titulo]="esEdicion() ? 'Editar Empleado' : 'Nuevo Empleado'"
      [subtitulo]="esEdicion() ? (empleado()?.nombres + ' ' + empleado()?.apellidos) : 'Ingresa la información básica y laboral del empleado.'"
      (cerrar)="cerrar.emit()"
    >
      <form (submit)="enviar($event)" class="space-y-4">
        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <ui-notched-select
            id="emp-tipo-doc"
            etiqueta="Tipo de documento"
            [opciones]="opcionesTipoDoc()"
            [valor]="formTipoDoc()"
            [obligatorio]="true"
            (valorCambio)="formTipoDoc.set($event)"
          />
          <ui-notched-field
            id="emp-num-doc"
            etiqueta="Número de documento"
            [valor]="formNumeroDoc()"
            [obligatorio]="true"
            (valorCambio)="formNumeroDoc.set($event)"
          />
        </div>

        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <ui-notched-field
            id="emp-nombres"
            etiqueta="Nombres"
            [valor]="formNombres()"
            [obligatorio]="true"
            (valorCambio)="formNombres.set($event)"
          />
          <ui-notched-field
            id="emp-apellidos"
            etiqueta="Apellidos"
            [valor]="formApellidos()"
            [obligatorio]="true"
            (valorCambio)="formApellidos.set($event)"
          />
        </div>

        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <ui-notched-field
            id="emp-correo"
            tipo="email"
            etiqueta="Correo de contacto"
            [valor]="formCorreo()"
            (valorCambio)="formCorreo.set($event)"
          />
          <ui-notched-field
            id="emp-telefono"
            etiqueta="Teléfono"
            [valor]="formTelefono()"
            (valorCambio)="formTelefono.set($event)"
          />
        </div>

        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <ui-notched-select
            id="emp-area"
            etiqueta="Área"
            [opciones]="opcionesArea()"
            [valor]="formArea()"
            [obligatorio]="true"
            (valorCambio)="formArea.set($event)"
          />
          <ui-notched-select
            id="emp-cargo"
            etiqueta="Cargo"
            [opciones]="opcionesCargo()"
            [valor]="formCargo()"
            [obligatorio]="true"
            (valorCambio)="formCargo.set($event)"
          />
        </div>

        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <ui-notched-select
            id="emp-contrato"
            etiqueta="Tipo de contrato"
            [opciones]="opcionesContrato()"
            [valor]="formContrato()"
            (valorCambio)="formContrato.set($event)"
          />
          <ui-notched-field
            id="emp-fecha-ingreso"
            tipo="date"
            etiqueta="Fecha de ingreso"
            [valor]="formFechaIngreso()"
            [obligatorio]="true"
            (valorCambio)="formFechaIngreso.set($event)"
          />
        </div>

        <div class="mt-6 flex justify-end gap-3 pt-3">
          <button
            type="button"
            class="accion-secundaria"
            (click)="cerrar.emit()"
          >
            Cancelar
          </button>
          <ui-button
            tipo="submit"
            [deshabilitado]="guardando() || !formularioValido()"
          >
            {{ guardando() ? 'Guardando...' : 'Guardar empleado' }}
          </ui-button>
        </div>
      </form>
    </ui-dialog>
  `,
})
export class EmpleadoFormDialogComponent {
  readonly abierto = input.required<boolean>();
  readonly empleado = input<Empleado | null>(null);
  readonly areas = input<readonly Area[]>([]);
  readonly cargos = input<readonly Cargo[]>([]);
  readonly tiposDocumento = input<readonly CatalogoItem[]>([]);
  readonly tiposContrato = input<readonly CatalogoItem[]>([]);
  readonly guardando = input<boolean>(false);

  readonly guardar = output<CrearEmpleadoDto | ActualizarEmpleadoDto>();
  readonly cerrar = output<void>();

  protected readonly formTipoDoc = signal('CC');
  protected readonly formNumeroDoc = signal('');
  protected readonly formNombres = signal('');
  protected readonly formApellidos = signal('');
  protected readonly formCorreo = signal('');
  protected readonly formTelefono = signal('');
  protected readonly formArea = signal('');
  protected readonly formCargo = signal('');
  protected readonly formContrato = signal('INDEF');
  protected readonly formFechaIngreso = signal(new Date().toISOString().substring(0, 10));

  constructor() {
    effect(() => {
      const emp = this.empleado();
      if (emp) {
        this.formTipoDoc.set(emp.codTipoDocumento);
        this.formNumeroDoc.set(emp.numeroDocumento);
        this.formNombres.set(emp.nombres);
        this.formApellidos.set(emp.apellidos);
        this.formCorreo.set(emp.correoContacto || emp.correoUsuario || '');
        this.formTelefono.set(emp.telefono || '');
        this.formArea.set(emp.idArea ? String(emp.idArea) : '');
        this.formCargo.set(emp.idCargo ? String(emp.idCargo) : '');
        this.formContrato.set(emp.codTipoContrato || 'INDEF');
        this.formFechaIngreso.set(emp.fechaIngreso || new Date().toISOString().substring(0, 10));
      } else {
        this.formTipoDoc.set('CC');
        this.formNumeroDoc.set('');
        this.formNombres.set('');
        this.formApellidos.set('');
        this.formCorreo.set('');
        this.formTelefono.set('');
        this.formArea.set('');
        this.formCargo.set('');
        this.formContrato.set('INDEF');
        this.formFechaIngreso.set(new Date().toISOString().substring(0, 10));
      }
    });
  }

  protected esEdicion(): boolean {
    return this.empleado() !== null;
  }

  protected opcionesTipoDoc(): readonly OpcionSelect[] {
    const items = this.tiposDocumento();
    return items.length > 0
      ? items.map((t) => ({ valor: t.codigo, etiqueta: `${t.codigo} - ${t.nombre}` }))
      : [
          { valor: 'CC', etiqueta: 'CC - Cédula de Ciudadanía' },
          { valor: 'CE', etiqueta: 'CE - Cédula de Extranjería' },
          { valor: 'TI', etiqueta: 'TI - Tarjeta de Identidad' },
          { valor: 'PAS', etiqueta: 'PAS - Pasaporte' },
        ];
  }

  protected opcionesContrato(): readonly OpcionSelect[] {
    const items = this.tiposContrato();
    return items.length > 0
      ? items.map((t) => ({ valor: t.codigo, etiqueta: t.nombre }))
      : [
          { valor: 'INDEF', etiqueta: 'Término Indefinido' },
          { valor: 'FIJO', etiqueta: 'Término Fijo' },
          { valor: 'OBRA', etiqueta: 'Obra o Labor' },
          { valor: 'APREN', etiqueta: 'Aprendizaje' },
        ];
  }

  protected opcionesArea(): readonly OpcionSelect[] {
    return this.areas().map((a) => ({ valor: String(a.idArea), etiqueta: a.nombre }));
  }

  protected opcionesCargo(): readonly OpcionSelect[] {
    return this.cargos().map((c) => ({ valor: String(c.idCargo), etiqueta: c.nombre }));
  }

  protected formularioValido(): boolean {
    return (
      this.formTipoDoc().trim().length > 0 &&
      this.formNumeroDoc().trim().length > 0 &&
      this.formNombres().trim().length > 0 &&
      this.formApellidos().trim().length > 0 &&
      this.formArea().trim().length > 0 &&
      this.formCargo().trim().length > 0 &&
      this.formFechaIngreso().trim().length > 0
    );
  }

  protected enviar(event: Event): void {
    event.preventDefault();
    if (!this.formularioValido()) return;

    const payload: CrearEmpleadoDto = {
      codTipoDocumento: this.formTipoDoc().trim(),
      numeroDocumento: this.formNumeroDoc().trim(),
      nombres: this.formNombres().trim(),
      apellidos: this.formApellidos().trim(),
      correoContacto: this.formCorreo().trim() || null,
      telefono: this.formTelefono().trim() || null,
      direccion: null,
      idArea: Number(this.formArea()),
      idCargo: Number(this.formCargo()),
      codTipoContrato: this.formContrato().trim() || null,
      fechaIngreso: this.formFechaIngreso(),
    };

    this.guardar.emit(payload);
  }
}
