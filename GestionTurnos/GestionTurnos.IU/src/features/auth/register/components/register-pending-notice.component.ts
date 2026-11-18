import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { UiBadgeComponent } from '../../../../app/shared/ui';

@Component({
  selector: 'app-register-pending-notice',
  imports: [RouterLink, UiBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div role="status" aria-live="polite" class="space-y-4 text-center">
      <div class="flex justify-center">
        <ui-badge estado="PENDIENTE" />
      </div>
      <h2 class="text-lg font-bold text-[#113459]">
        Solicitud #{{ idSolicitud() }} enviada correctamente
      </h2>
      <p class="text-sm leading-relaxed text-slate-700">
        Tu solicitud de registro quedó en estado <strong>PENDIENTE</strong>. Un administrador
        revisará tus datos y te asignará área y cargo para activar tu cuenta.
      </p>
      <div class="pt-3">
        <a
          routerLink="/ingresar"
          class="inline-flex min-h-11 w-full items-center justify-center rounded-lg bg-[#1D768A] px-5 py-2.5 text-sm font-semibold text-white hover:bg-[#165e6e]"
        >
          Volver al inicio de sesión
        </a>
      </div>
    </div>
  `,
})
export class RegisterPendingNoticeComponent {
  readonly idSolicitud = input.required<number>();
}
