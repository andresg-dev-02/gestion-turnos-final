import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ApiClient, AuthService, AuthStore, NotificationService } from '../../../app/core';
import { PerfilEmpleado } from '../../../app/shared/data/modelos';
import { MiApi } from '../../../app/shared/data/apis';
import { UiButtonComponent, UiNotchedFieldComponent, UiSkeletonComponent } from '../../../app/shared/ui';
import { formatearFecha } from '../../../app/shared/utils/formato';

const CORREO = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/**
 * Mi perfil (/mi/perfil). Los datos laborales son de solo lectura; el empleado solo
 * edita su contacto (lista blanca de la API: correo de contacto, teléfono, dirección)
 * y su contraseña.
 */
@Component({
  selector: 'app-mi-perfil-page',
  imports: [UiButtonComponent, UiNotchedFieldComponent, UiSkeletonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="titulo-perfil" class="space-y-6">
      <header>
        <h1 id="titulo-perfil" class="text-2xl font-extrabold tracking-tight text-slate-900">Mi perfil</h1>
        <p class="mt-1 text-sm text-slate-600">Consulta tus datos laborales y actualiza tu información de contacto.</p>
      </header>

      @if (cargando()) {
        <ui-skeleton [filas]="4" />
      } @else if (errorCarga()) {
        <p class="aviso-alerta">{{ errorCarga() }}</p>
      } @else if (perfil(); as p) {
        <div class="grid grid-cols-1 gap-6 lg:grid-cols-2">
          <article class="tarjeta" aria-labelledby="titulo-datos">
            <div class="flex items-center gap-4">
              <span class="flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-marca-600 text-xl font-extrabold text-white" aria-hidden="true">
                {{ p.nombres.charAt(0) }}{{ p.apellidos.charAt(0) }}
              </span>
              <div>
                <h2 id="titulo-datos" class="text-lg font-bold text-slate-900">{{ p.nombres }} {{ p.apellidos }}</h2>
                <p class="text-sm text-slate-600">{{ p.cargo }} · {{ p.area }}</p>
              </div>
            </div>
            <dl class="mt-5 grid grid-cols-1 gap-x-6 gap-y-3 text-sm sm:grid-cols-2">
              <div><dt class="text-xs font-semibold text-slate-600">Documento</dt><dd class="font-medium">{{ p.codTipoDocumento }} {{ p.numeroDocumento }}</dd></div>
              <div><dt class="text-xs font-semibold text-slate-600">Estado</dt><dd class="font-medium">{{ p.estado }}</dd></div>
              <div><dt class="text-xs font-semibold text-slate-600">Sucursal</dt><dd class="font-medium">{{ p.sucursal }}</dd></div>
              <div><dt class="text-xs font-semibold text-slate-600">Fecha de ingreso</dt><dd class="font-medium">{{ formatearFecha(p.fechaIngreso) }}</dd></div>
              <div><dt class="text-xs font-semibold text-slate-600">Tipo de contrato</dt><dd class="font-medium">{{ p.tipoContrato || 'Sin especificar' }}</dd></div>
            </dl>
            <p class="mt-4 text-xs text-slate-500">Para corregir tus datos laborales o de identificación, comunícate con el administrador.</p>
          </article>

          <article class="tarjeta" aria-labelledby="titulo-contacto">
            <h2 id="titulo-contacto" class="text-base font-bold text-slate-900">Información de contacto</h2>
            <form class="mt-5 space-y-5" (submit)="guardarContacto($event)" novalidate>
              <ui-notched-field id="pf-correo" etiqueta="Correo de contacto" tipo="email" autocomplete="email"
                [valor]="correo()" [error]="erroresContacto()['correoContacto']" (valorCambio)="correo.set($event)" />
              <ui-notched-field id="pf-telefono" etiqueta="Teléfono" tipo="tel" autocomplete="tel"
                [valor]="telefono()" [error]="erroresContacto()['telefono']" (valorCambio)="telefono.set($event)" />
              <ui-notched-field id="pf-direccion" etiqueta="Dirección" autocomplete="street-address"
                [valor]="direccion()" [error]="erroresContacto()['direccion']" (valorCambio)="direccion.set($event)" />
              <div class="flex justify-end">
                <ui-button tipo="submit" [cargando]="guardandoContacto()">Guardar cambios</ui-button>
              </div>
            </form>
          </article>

          <article class="tarjeta lg:col-span-2" aria-labelledby="titulo-contrasena">
            <h2 id="titulo-contrasena" class="text-base font-bold text-slate-900">Cambiar contraseña</h2>
            <form class="mt-5 grid grid-cols-1 gap-5 md:grid-cols-3" (submit)="cambiarContrasena($event)" novalidate>
              <ui-notched-field id="pf-actual" etiqueta="Contraseña actual" tipo="password" autocomplete="current-password"
                [valor]="actual()" [obligatorio]="true" [error]="erroresClave()['contrasenaActual']" (valorCambio)="actual.set($event)" />
              <ui-notched-field id="pf-nueva" etiqueta="Nueva contraseña" tipo="password" autocomplete="new-password"
                ayuda="Mínimo 8 caracteres." [valor]="nueva()" [obligatorio]="true" [error]="erroresClave()['nuevaContrasena']" (valorCambio)="nueva.set($event)" />
              <ui-notched-field id="pf-confirmar" etiqueta="Confirmar nueva contraseña" tipo="password" autocomplete="new-password"
                [valor]="confirmar()" [obligatorio]="true" [error]="erroresClave()['confirmar']" (valorCambio)="confirmar.set($event)" />
              <div class="flex justify-end md:col-span-3">
                <ui-button tipo="submit" [cargando]="guardandoClave()">Actualizar contraseña</ui-button>
              </div>
            </form>
          </article>
        </div>
      }
    </section>
  `,
})
export class MiPerfilPageComponent implements OnInit {
  private readonly api = inject(MiApi);
  private readonly toast = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly authStore = inject(AuthStore);
  protected readonly formatearFecha = formatearFecha;

  protected readonly perfil = signal<PerfilEmpleado | null>(null);
  protected readonly cargando = signal(true);
  protected readonly errorCarga = signal<string | null>(null);

  protected readonly correo = signal('');
  protected readonly telefono = signal('');
  protected readonly direccion = signal('');
  protected readonly erroresContacto = signal<Record<string, string>>({});
  protected readonly guardandoContacto = signal(false);

  protected readonly actual = signal('');
  protected readonly nueva = signal('');
  protected readonly confirmar = signal('');
  protected readonly erroresClave = signal<Record<string, string>>({});
  protected readonly guardandoClave = signal(false);

  ngOnInit(): void {
    this.cargar();
  }

  private cargar(): void {
    this.api.perfil().subscribe({
      next: (p) => {
        this.perfil.set(p);
        this.correo.set(p.correoContacto ?? '');
        this.telefono.set(p.telefono ?? '');
        this.direccion.set(p.direccion ?? '');
        this.cargando.set(false);
      },
      error: (err: unknown) => {
        this.cargando.set(false);
        this.errorCarga.set(ApiClient.extraerMensajeError(err).general);
      },
    });
  }

  guardarContacto(evento: Event): void {
    evento.preventDefault();
    const correo = this.correo().trim();
    const telefono = this.telefono().trim();
    const direccion = this.direccion().trim();
    const errores: Record<string, string> = {};
    if (correo && !CORREO.test(correo)) errores['correoContacto'] = 'El formato del correo no es válido.';
    if (telefono.length > 30) errores['telefono'] = 'Máximo 30 caracteres.';
    if (direccion.length > 200) errores['direccion'] = 'Máximo 200 caracteres.';
    this.erroresContacto.set(errores);
    if (Object.keys(errores).length) return;

    this.guardandoContacto.set(true);
    this.api
      .editarPerfil({ correoContacto: correo || null, telefono: telefono || null, direccion: direccion || null })
      .subscribe({
        next: () => {
          this.guardandoContacto.set(false);
          this.toast.exito('Tu información de contacto se actualizó.');
        },
        error: (err: unknown) => {
          this.guardandoContacto.set(false);
          const e = ApiClient.extraerMensajeError(err);
          this.erroresContacto.set(e.campos);
          this.toast.error(e.general);
        },
      });
  }

  cambiarContrasena(evento: Event): void {
    evento.preventDefault();
    const errores: Record<string, string> = {};
    if (!this.actual()) errores['contrasenaActual'] = 'Ingresa tu contraseña actual.';
    if (this.nueva().length < 8) errores['nuevaContrasena'] = 'Mínimo 8 caracteres.';
    else if (this.nueva().length > 72) errores['nuevaContrasena'] = 'Máximo 72 caracteres.';
    else if (this.nueva() === this.actual()) errores['nuevaContrasena'] = 'Debe ser diferente a la actual.';
    if (this.confirmar() !== this.nueva()) errores['confirmar'] = 'Las contraseñas no coinciden.';
    this.erroresClave.set(errores);
    if (Object.keys(errores).length) return;

    this.guardandoClave.set(true);
    const nueva = this.nueva();
    this.api.cambiarContrasena(this.actual(), nueva).subscribe({
      next: () => {
        this.actual.set('');
        this.nueva.set('');
        this.confirmar.set('');
        // El cambio cierra la sesión en todos los dispositivos: este vuelve a ingresar con la contraseña nueva.
        const correo = this.authStore.usuario()?.correo;
        if (!correo) {
          this.guardandoClave.set(false);
          this.auth.finalizarSesionLocal();
          return;
        }
        this.auth.iniciarSesion({ correo, contrasena: nueva, recordar: this.authStore.recordar }).subscribe({
          next: () => {
            this.guardandoClave.set(false);
            this.toast.exito('Tu contraseña se actualizó. Se cerró la sesión en tus otros dispositivos.');
          },
          error: () => {
            this.guardandoClave.set(false);
            this.toast.alerta('Tu contraseña se actualizó. Ingresa nuevamente con la contraseña nueva.');
            this.auth.finalizarSesionLocal();
          },
        });
      },
      error: (err: unknown) => {
        this.guardandoClave.set(false);
        const e = ApiClient.extraerMensajeError(err);
        this.erroresClave.set(e.campos);
        this.toast.error(e.general);
      },
    });
  }
}
