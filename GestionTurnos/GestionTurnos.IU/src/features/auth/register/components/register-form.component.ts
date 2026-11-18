import { ChangeDetectionStrategy, Component, OnInit, inject, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiClient, AuthService, SolicitarRegistroRequest } from '../../../../app/core';
import {
  OpcionSelect,
  UiButtonComponent,
  UiNotchedFieldComponent,
  UiNotchedSelectComponent,
} from '../../../../app/shared/ui';
import { validarSolicitudRegistro } from './register-form.validator';

@Component({
  selector: 'app-register-form',
  imports: [RouterLink, UiNotchedFieldComponent, UiNotchedSelectComponent, UiButtonComponent],
  templateUrl: './register-form.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterFormComponent implements OnInit {
  private readonly auth = inject(AuthService);

  readonly solicitudCreada = output<number>();
  readonly opcionesTipoDoc = signal<readonly OpcionSelect[]>([
    { valor: 'CC', etiqueta: 'CC - Cédula de Ciudadanía' },
    { valor: 'CE', etiqueta: 'CE - Cédula de Extranjería' },
  ]);

  readonly codTipoDocumento = signal('CC');
  readonly numeroDocumento = signal('');
  readonly nombres = signal('');
  readonly apellidos = signal('');
  readonly correo = signal('');
  readonly telefono = signal('');
  readonly contrasena = signal('');
  readonly enviando = signal(false);
  readonly errorGeneral = signal<string | null>(null);
  readonly errores = signal<Record<string, string>>({});

  ngOnInit(): void {
    this.auth.listarTiposDocumento().subscribe((lista) => {
      if (lista.length > 0) {
        this.opcionesTipoDoc.set(
          lista.map((t) => ({
            valor: t.codTipoDocumento,
            etiqueta: `${t.codTipoDocumento} - ${t.nombre}`,
          }))
        );
      }
    });
  }

  enviar(evento: Event): void {
    evento.preventDefault();
    this.errorGeneral.set(null);

    const payload: SolicitarRegistroRequest = {
      codTipoDocumento: this.codTipoDocumento(),
      numeroDocumento: this.numeroDocumento().trim(),
      nombres: this.nombres().trim(),
      apellidos: this.apellidos().trim(),
      correo: this.correo().trim(),
      telefono: this.telefono().trim() || null,
      contrasena: this.contrasena(),
    };

    const erroresValidacion = validarSolicitudRegistro(payload);
    if (Object.keys(erroresValidacion).length > 0) {
      this.errores.set(erroresValidacion);
      return;
    }

    this.errores.set({});
    this.enviando.set(true);
    this.auth.solicitarRegistro(payload).subscribe({
      next: (id) => {
        this.enviando.set(false);
        this.solicitudCreada.emit(id || 1);
      },
      error: (err: unknown) => {
        this.enviando.set(false);
        const extraido = ApiClient.extraerMensajeError(err);
        this.errorGeneral.set(extraido.general);
        this.errores.set(extraido.campos);
      },
    });
  }
}
