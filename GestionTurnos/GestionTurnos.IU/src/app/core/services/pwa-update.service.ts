import { Injectable, inject, signal } from '@angular/core';
import { SwUpdate, VersionReadyEvent } from '@angular/service-worker';
import { filter } from 'rxjs';

interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>;
  readonly userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

@Injectable({ providedIn: 'root' })
export class PwaUpdateService {
  private readonly swUpdate = inject(SwUpdate, { optional: true });
  private eventoInstalacion: BeforeInstallPromptEvent | null = null;

  private readonly _actualizacionDisponible = signal(false);
  private readonly _instalable = signal(false);

  readonly actualizacionDisponible = this._actualizacionDisponible.asReadonly();
  readonly instalable = this._instalable.asReadonly();

  constructor() {
    if (this.swUpdate?.isEnabled) {
      this.swUpdate.versionUpdates
        .pipe(filter((evt): evt is VersionReadyEvent => evt.type === 'VERSION_READY'))
        .subscribe(() => this._actualizacionDisponible.set(true));
    }

    if (typeof window !== 'undefined') {
      // index.html captura el evento antes de que Angular arranque; aquí se recoge el ya disparado y los siguientes.
      const ventana = window as Window & { __eventoInstalacionPwa?: BeforeInstallPromptEvent };
      const tomar = () => {
        if (ventana.__eventoInstalacionPwa) {
          this.eventoInstalacion = ventana.__eventoInstalacionPwa;
          this._instalable.set(true);
        }
      };
      tomar();
      window.addEventListener('pwa-instalable', tomar);
      window.addEventListener('appinstalled', () => {
        this.eventoInstalacion = null;
        ventana.__eventoInstalacionPwa = undefined;
        this._instalable.set(false);
        this._instalada.set(true);
      });
    }
  }

  private readonly _instalada = signal(PwaUpdateService.detectarStandalone());

  /** La app se está ejecutando instalada (pantalla de inicio), no en una pestaña del navegador. */
  readonly instalada = this._instalada.asReadonly();

  /** iPhone/iPad: no existe el evento de instalación; se muestran instrucciones de "Agregar a inicio". */
  readonly esIos = PwaUpdateService.detectarIos();

  /** Teléfono o tableta (táctil y pantalla pequeña o agente móvil). */
  readonly esMovil = PwaUpdateService.detectarMovil();

  static detectarStandalone(): boolean {
    if (typeof window === 'undefined') return false;
    const nav = navigator as Navigator & { standalone?: boolean };
    return window.matchMedia?.('(display-mode: standalone)').matches === true || nav.standalone === true;
  }

  private static detectarIos(): boolean {
    if (typeof navigator === 'undefined') return false;
    const ua = navigator.userAgent;
    // iPadOS 13+ se presenta como Mac con pantalla táctil.
    return /iPhone|iPad|iPod/i.test(ua) || (/Macintosh/.test(ua) && navigator.maxTouchPoints > 1);
  }

  private static detectarMovil(): boolean {
    if (typeof navigator === 'undefined') return false;
    return /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent) || PwaUpdateService.detectarIos();
  }

  async aplicarActualizacion(): Promise<void> {
    if (this.swUpdate?.isEnabled) {
      await this.swUpdate.activateUpdate();
      location.reload();
    }
  }

  async instalarPwa(): Promise<void> {
    if (!this.eventoInstalacion) return;
    await this.eventoInstalacion.prompt();
    const { outcome } = await this.eventoInstalacion.userChoice;
    // El evento solo se puede usar una vez, se acepte o no.
    this.eventoInstalacion = null;
    (window as Window & { __eventoInstalacionPwa?: unknown }).__eventoInstalacionPwa = undefined;
    this._instalable.set(false);
    if (outcome === 'accepted') {
      this._instalada.set(true);
    }
  }

  posponerAvisoActualizacion(): void {
    this._actualizacionDisponible.set(false);
  }
}
