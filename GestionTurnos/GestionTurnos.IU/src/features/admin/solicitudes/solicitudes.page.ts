import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthStore, CodigoPermiso, PERMISOS } from '../../../app/core';
import { SolicitudesAdminApi } from '../../../app/shared/data/apis';
import { PestanaItem, UiEmptyStateComponent, UiTabsComponent } from '../../../app/shared/ui';
import { CambiosTurnoBandejaComponent } from './bandejas/cambios-turno.bandeja';
import { NovedadesBandejaComponent } from './bandejas/novedades.bandeja';
import { RegistroBandejaComponent } from './bandejas/registro.bandeja';
import { VacacionesBandejaComponent } from './bandejas/vacaciones.bandeja';

interface DefPestana {
  readonly id: 'registro' | 'vacaciones' | 'novedades' | 'cambios';
  readonly etiqueta: string;
  readonly permiso: CodigoPermiso;
}

const DEFINICION: readonly DefPestana[] = [
  { id: 'registro', etiqueta: 'Registro', permiso: PERMISOS.registrosAprobar },
  { id: 'vacaciones', etiqueta: 'Vacaciones', permiso: PERMISOS.vacacionesAprobar },
  { id: 'novedades', etiqueta: 'Permisos y novedades', permiso: PERMISOS.novedadesAprobar },
  { id: 'cambios', etiqueta: 'Cambios de turno', permiso: PERMISOS.turnosProgramar },
];

/**
 * Centro de aprobación de solicitudes (/admin/solicitudes): una bandeja por tipo,
 * visible solo si el usuario tiene el permiso correspondiente.
 */
@Component({
  selector: 'app-solicitudes-page',
  imports: [
    UiTabsComponent,
    UiEmptyStateComponent,
    RegistroBandejaComponent,
    VacacionesBandejaComponent,
    NovedadesBandejaComponent,
    CambiosTurnoBandejaComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="titulo-solicitudes" class="space-y-5">
      <header>
        <h1 id="titulo-solicitudes" class="text-2xl font-extrabold tracking-tight text-slate-900">
          Solicitudes por aprobar
        </h1>
        <p class="mt-1 text-sm text-slate-600">
          Registro de nuevos usuarios, vacaciones, permisos y cambios de turno entre compañeros.
        </p>
      </header>

      @if (pestanas().length === 0) {
        <ui-empty-state titulo="Sin bandejas disponibles" descripcion="Tu rol no tiene permisos para aprobar solicitudes." />
      } @else {
        <ui-tabs etiqueta="Tipos de solicitud" [pestanas]="pestanas()" [activa]="activa()" (cambio)="cambiar($event)" />

        <div role="tabpanel" [attr.aria-labelledby]="'tab-' + activa()" class="tarjeta">
          @switch (activa()) {
            @case ('registro') { <app-registro-bandeja (cambioPendientes)="contar()" /> }
            @case ('vacaciones') { <app-vacaciones-bandeja (cambioPendientes)="contar()" /> }
            @case ('novedades') { <app-novedades-bandeja (cambioPendientes)="contar()" /> }
            @case ('cambios') { <app-cambios-turno-bandeja (cambioPendientes)="contar()" /> }
          }
        </div>
      }
    </section>
  `,
})
export class SolicitudesPageComponent implements OnInit {
  private readonly store = inject(AuthStore);
  private readonly api = inject(SolicitudesAdminApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private readonly disponibles = DEFINICION.filter((d) => this.store.puede(d.permiso));
  private readonly contadores = signal<Record<string, number>>({});

  protected readonly pestanas = computed<readonly PestanaItem[]>(() =>
    this.disponibles.map((d) => ({ id: d.id, etiqueta: d.etiqueta, contador: this.contadores()[d.id] ?? null }))
  );
  protected readonly activa = signal<string>(this.pestanaInicial());

  ngOnInit(): void {
    this.contar();
  }

  private pestanaInicial(): string {
    const t = this.route.snapshot.queryParamMap.get('tipo');
    return this.disponibles.some((d) => d.id === t) ? (t as string) : (this.disponibles[0]?.id ?? '');
  }

  cambiar(id: string): void {
    this.activa.set(id);
    void this.router.navigate([], { queryParams: { tipo: id }, replaceUrl: true });
  }

  /** Recalcula los contadores de pendientes de las pestañas. */
  contar(): void {
    this.api.panel().subscribe({
      next: (p) =>
        this.contadores.update((c) => ({
          ...c,
          registro: p.registrosPendientes,
          vacaciones: p.vacacionesPendientes,
          novedades: p.novedadesPendientes,
        })),
      error: () => undefined,
    });
    if (this.store.puede(PERMISOS.turnosProgramar)) {
      this.api.listarCambios({ estado: 'PENDIENTE', pageNumber: 1, pageSize: 1 }).subscribe({
        next: (r) => this.contadores.update((c) => ({ ...c, cambios: r.totalCount })),
        error: () => undefined,
      });
    }
  }
}
