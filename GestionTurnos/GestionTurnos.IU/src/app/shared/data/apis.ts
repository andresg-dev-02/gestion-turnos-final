import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../../core';
import {
  Area,
  BitacoraNegocio,
  Cargo,
  CatalogoItem,
  CategoriaHora,
  ConflictosSolicitudVacaciones,
  CrearEmpleadoDto,
  ActualizarEmpleadoDto,
  DiaFestivo,
  Empleado,
  EventoCalendario,
  HistorialTurno,
  HorasPorTurno,
  ItemProgramacion,
  AsignacionTurno,
  Novedad,
  PaginaApi,
  PanelAsistenciaDia,
  PanelInicial,
  Parametro,
  ParametroValor,
  PerfilEmpleado,
  PermisoSistema,
  PrevisualizacionModificacion,
  ResultadoSolicitudVacaciones,
  RevisionConflictosTurno,
  RolSistema,
  SaldoVacaciones,
  SolicitudCambioTurno,
  SolicitudRegistro,
  SolicitudVacaciones,
  SolicitudVacacionesBandeja,
  Sucursal,
  TipoNovedad,
  Turno,
  TurnoIntercambiable,
} from './modelos';

type Params = Record<string, string | number | boolean>;

/** Catálogos del esquema parametrizacion (permiso parametros.editar para escribir). */
@Injectable({ providedIn: 'root' })
export class ParametrizacionApi {
  private readonly api = inject(ApiClient);

  listarTiposDocumento(soloActivos = false): Observable<readonly CatalogoItem[]> {
    return this.api.get('/parametrizacion/tipos-documento', { soloActivos });
  }
  guardarTipoDocumento(item: CatalogoItem): Observable<void> {
    return this.api.post('/parametrizacion/tipos-documento', item);
  }

  listarTiposContrato(soloActivos = false): Observable<readonly CatalogoItem[]> {
    return this.api.get('/parametrizacion/tipos-contrato', { soloActivos });
  }
  guardarTipoContrato(item: CatalogoItem): Observable<void> {
    return this.api.post('/parametrizacion/tipos-contrato', item);
  }

  listarTiposNovedad(soloActivos = false, soloPermitidosEmpleado = false): Observable<readonly TipoNovedad[]> {
    return this.api.get('/parametrizacion/tipos-novedad', { soloActivos, soloPermitidosEmpleado });
  }
  guardarTipoNovedad(item: TipoNovedad): Observable<void> {
    return this.api.post('/parametrizacion/tipos-novedad', item);
  }

  listarCategoriasHora(soloActivos = false): Observable<readonly CategoriaHora[]> {
    return this.api.get('/parametrizacion/categorias-hora', { soloActivos });
  }
  guardarCategoriaHora(item: CategoriaHora): Observable<void> {
    return this.api.post('/parametrizacion/categorias-hora', item);
  }

  listarDiasFestivos(anio?: number): Observable<readonly DiaFestivo[]> {
    return this.api.get('/parametrizacion/dias-festivos', anio ? { anio } : undefined);
  }
  guardarDiaFestivo(item: DiaFestivo): Observable<void> {
    return this.api.post('/parametrizacion/dias-festivos', item);
  }
  eliminarDiaFestivo(fecha: string): Observable<void> {
    return this.api.delete(`/parametrizacion/dias-festivos/${fecha}`);
  }

  listarParametros(): Observable<readonly Parametro[]> {
    return this.api.get('/parametrizacion/parametros');
  }
  historialParametro(cod: string): Observable<readonly ParametroValor[]> {
    return this.api.get(`/parametrizacion/parametros/${encodeURIComponent(cod)}/historial`);
  }
  cambiarValorParametro(cod: string, valor: string, vigenteDesde: string): Observable<void> {
    return this.api.post(`/parametrizacion/parametros/${encodeURIComponent(cod)}/valores`, {
      valor,
      vigenteDesde,
    });
  }
}

@Injectable({ providedIn: 'root' })
export class OrganizacionApi {
  private readonly api = inject(ApiClient);

  listarSucursales(soloActivos = true): Observable<readonly Sucursal[]> {
    return this.api.get('/organizacion/sucursales', { soloActivos });
  }
  listarAreas(soloActivos = true): Observable<readonly Area[]> {
    return this.api.get('/organizacion/areas', { soloActivos });
  }
  listarCargos(soloActivos = true): Observable<readonly Cargo[]> {
    return this.api.get('/organizacion/cargos', { soloActivos });
  }
  crearCargo(nombre: string, activo = true): Observable<{ idCargo: number }> {
    return this.api.post('/organizacion/cargos', { nombre, activo });
  }
  listarEmpleados(params: Params = {}): Observable<PaginaApi<Empleado>> {
    return this.api.get('/empleados', { estado: 'ACTIVO', pageSize: 100, ...params });
  }
  obtenerEmpleado(id: number): Observable<Empleado> {
    return this.api.get(`/empleados/${id}`);
  }
  crearEmpleado(datos: CrearEmpleadoDto): Observable<{ idEmpleado: number }> {
    return this.api.post('/empleados', datos);
  }
  actualizarEmpleado(id: number, datos: ActualizarEmpleadoDto): Observable<void> {
    return this.api.put(`/empleados/${id}`, datos);
  }
  desactivarEmpleado(id: number, fechaRetiro: string): Observable<void> {
    return this.api.post(`/empleados/${id}/desactivar`, { fechaRetiro });
  }
  reactivarEmpleado(id: number): Observable<void> {
    return this.api.post(`/empleados/${id}/reactivar`, {});
  }
}

@Injectable({ providedIn: 'root' })
export class SolicitudesAdminApi {
  private readonly api = inject(ApiClient);

  panel(): Observable<PanelInicial> {
    return this.api.get('/panel');
  }

  // Registro
  listarRegistros(params: Params): Observable<PaginaApi<SolicitudRegistro>> {
    return this.api.get('/solicitudes-registro', params);
  }
  aprobarRegistro(
    id: number,
    cuerpo: { idArea: number; idCargo: number; fechaIngreso: string; codTipoContrato: string | null }
  ): Observable<unknown> {
    return this.api.post(`/solicitudes-registro/${id}/aprobar`, cuerpo);
  }
  rechazarRegistro(id: number, motivoRechazo: string): Observable<void> {
    return this.api.post(`/solicitudes-registro/${id}/rechazar`, { motivoRechazo });
  }

  // Vacaciones
  listarVacaciones(params: Params): Observable<PaginaApi<SolicitudVacacionesBandeja>> {
    return this.api.get('/vacaciones/solicitudes', params);
  }
  conflictosVacaciones(id: number): Observable<ConflictosSolicitudVacaciones> {
    return this.api.get(`/vacaciones/solicitudes/${id}/turnos-en-conflicto`);
  }
  resolverVacaciones(
    id: number,
    cuerpo: { aprobar: boolean; motivoRechazo?: string | null; confirmarAnulacionTurnos?: boolean }
  ): Observable<void> {
    return this.api.post(`/vacaciones/solicitudes/${id}/resolver`, cuerpo);
  }

  // Novedades / permisos
  listarNovedades(params: Params): Observable<PaginaApi<Novedad>> {
    return this.api.get('/novedades', params);
  }
  resolverNovedad(
    id: number,
    cuerpo: { aprobar: boolean; motivoRechazo?: string | null; confirmarAnulacionTurnos?: boolean }
  ): Observable<void> {
    return this.api.post(`/novedades/${id}/resolver`, cuerpo);
  }

  // Cambios de turno
  listarCambios(params: Params): Observable<PaginaApi<SolicitudCambioTurno>> {
    return this.api.get('/turnos/cambios', params);
  }
  resolverCambio(id: number, aprobar: boolean, motivoRechazo: string | null): Observable<void> {
    return this.api.post(`/turnos/cambios/${id}/resolver`, { aprobar, motivoRechazo });
  }
}

@Injectable({ providedIn: 'root' })
export class ProgramacionApi {
  private readonly api = inject(ApiClient);

  listarTurnos(soloActivos = true): Observable<readonly Turno[]> {
    return this.api.get('/turnos/catalogo', { soloActivos });
  }
  listarAsignaciones(params: Params): Observable<PaginaApi<AsignacionTurno>> {
    return this.api.get('/turnos/asignaciones', params);
  }
  revisarConflictos(idEmpleado: number, idTurno: number, fecha: string): Observable<RevisionConflictosTurno> {
    return this.api.get('/turnos/asignaciones/revisar-conflictos', { idEmpleado, idTurno, fecha });
  }
  previsualizarModificacion(
    idAsignacion: number,
    idTurno: number,
    fecha: string
  ): Observable<PrevisualizacionModificacion> {
    return this.api.get(`/turnos/asignaciones/${idAsignacion}/previsualizar-modificacion`, {
      idTurno,
      fecha,
    });
  }
  programar(asignaciones: readonly ItemProgramacion[]): Observable<readonly number[]> {
    return this.api.post('/turnos/asignaciones', { asignaciones });
  }
  modificar(idAsignacion: number, idTurno: number, fecha: string, observacion: string | null): Observable<void> {
    return this.api.put(`/turnos/asignaciones/${idAsignacion}`, { idTurno, fecha, observacion });
  }
  anular(idAsignacion: number, observacion: string | null): Observable<void> {
    return this.api.post(`/turnos/asignaciones/${idAsignacion}/anular`, { observacion });
  }
  publicar(fechaDesde: string, fechaHasta: string, idArea: number | null): Observable<unknown> {
    return this.api.post('/turnos/asignaciones/publicar', { fechaDesde, fechaHasta, idArea });
  }
}

/** Endpoints del empleado en sesión ("mi-..."). */
@Injectable({ providedIn: 'root' })
export class MiApi {
  private readonly api = inject(ApiClient);

  perfil(): Observable<PerfilEmpleado> {
    return this.api.get('/mi-perfil');
  }
  editarPerfil(cuerpo: {
    correoContacto: string | null;
    telefono: string | null;
    direccion: string | null;
  }): Observable<void> {
    return this.api.put('/mi-perfil', cuerpo);
  }
  cambiarContrasena(contrasenaActual: string, nuevaContrasena: string): Observable<void> {
    return this.api.post('/mi-perfil/cambiar-contrasena', { contrasenaActual, nuevaContrasena });
  }

  calendario(desde: string, hasta: string): Observable<readonly EventoCalendario[]> {
    return this.api.get('/mi-calendario', { desde, hasta });
  }
  historial(pageNumber = 1): Observable<PaginaApi<HistorialTurno>> {
    return this.api.get('/mi-calendario/historial-turnos', { pageNumber, pageSize: 20 });
  }
  proximosTurnos(dias = 60): Observable<readonly TurnoIntercambiable[]> {
    return this.api.get('/mi-calendario/proximos-turnos', { dias });
  }
  turnosCompaneros(desde: string, hasta: string): Observable<readonly TurnoIntercambiable[]> {
    return this.api.get('/mi-calendario/turnos-companeros', { desde, hasta });
  }
  misCambios(): Observable<readonly SolicitudCambioTurno[]> {
    return this.api.get('/mi-calendario/cambios-turno');
  }
  solicitarCambio(
    idAsignacionSolicitante: number,
    idAsignacionCompanero: number,
    motivo: string | null
  ): Observable<number> {
    return this.api.post('/mi-calendario/cambios-turno', {
      idAsignacionSolicitante,
      idAsignacionCompanero,
      motivo,
    });
  }
  cancelarCambio(id: number): Observable<void> {
    return this.api.post(`/mi-calendario/cambios-turno/${id}/cancelar`, {});
  }

  saldoVacaciones(): Observable<SaldoVacaciones> {
    return this.api.get('/vacaciones/mi-saldo');
  }
  misVacaciones(): Observable<readonly SolicitudVacaciones[]> {
    return this.api.get('/vacaciones/mis-solicitudes');
  }
  solicitarVacaciones(
    fechaInicio: string,
    fechaFin: string,
    observacion: string | null
  ): Observable<ResultadoSolicitudVacaciones> {
    return this.api.post('/vacaciones/mis-solicitudes', { fechaInicio, fechaFin, observacion });
  }
  cancelarVacaciones(id: number): Observable<void> {
    return this.api.post(`/vacaciones/mis-solicitudes/${id}/cancelar`, {});
  }

  misNovedades(): Observable<readonly Novedad[]> {
    return this.api.get('/novedades/mis-solicitudes');
  }
  solicitarNovedad(cuerpo: {
    codTipoNovedad: string;
    inicio: string;
    fin: string;
    observaciones: string | null;
    urlSoporte: string | null;
  }): Observable<number> {
    return this.api.post('/novedades/mis-solicitudes', cuerpo);
  }
  cancelarNovedad(id: number): Observable<void> {
    return this.api.post(`/novedades/mis-solicitudes/${id}/cancelar`, {});
  }

  horasPorTurno(desde: string, hasta: string): Observable<readonly HorasPorTurno[]> {
    return this.api.get('/asistencia/mis-horas/turnos', { desde, hasta });
  }
}

/** Consulta de auditoría y bitácora de negocio (permiso auditoria.consultar). */
@Injectable({ providedIn: 'root' })
export class AuditoriaApi {
  private readonly api = inject(ApiClient);

  consultarBitacora(params: {
    idEmpleado?: number;
    idUsuario?: number;
    codTipoAccion?: string;
    esquema?: string;
    tabla?: string;
    fechaDesde?: string;
    fechaHasta?: string;
    pageNumber?: number;
    pageSize?: number;
  } = {}): Observable<PaginaApi<BitacoraNegocio>> {
    const query: Record<string, string | number> = {};
    if (params.idEmpleado) query['idEmpleado'] = params.idEmpleado;
    if (params.idUsuario) query['idUsuario'] = params.idUsuario;
    if (params.codTipoAccion) query['codTipoAccion'] = params.codTipoAccion;
    if (params.esquema) query['esquema'] = params.esquema;
    if (params.tabla) query['tabla'] = params.tabla;
    if (params.fechaDesde) query['fechaDesde'] = params.fechaDesde;
    if (params.fechaHasta) query['fechaHasta'] = params.fechaHasta;
    query['pageNumber'] = params.pageNumber ?? 1;
    query['pageSize'] = params.pageSize ?? 25;

    return this.api.get('/auditoria/bitacora', query);
  }

  obtenerHistorialRegistro(
    esquema: string,
    tabla: string,
    idRegistro: string,
    pageNumber = 1,
    pageSize = 25
  ): Observable<PaginaApi<BitacoraNegocio>> {
    return this.api.get(`/auditoria/bitacora/${esquema}/${tabla}/${idRegistro}`, { pageNumber, pageSize });
  }
}

/** Panel y supervisión de asistencia administrativa (permiso asistencia.consultar). */
@Injectable({ providedIn: 'root' })
export class AsistenciaAdminApi {
  private readonly api = inject(ApiClient);

  obtenerPanelDia(fecha?: string, idArea?: number): Observable<PanelAsistenciaDia> {
    const params: Record<string, string | number> = {};
    if (fecha) params['fecha'] = fecha;
    if (idArea) params['idArea'] = idArea;
    return this.api.get('/asistencia/panel-dia', params);
  }

  registrarManual(cuerpo: {
    idEmpleado: number;
    idAsignacion?: number | null;
    horaEntrada: string;
    horaSalida?: string | null;
    observacion: string;
  }): Observable<{ idRegistro: number }> {
    return this.api.post('/asistencia/manual', cuerpo);
  }

  corregirManual(
    idRegistro: number,
    cuerpo: {
      idEmpleado: number;
      idAsignacion?: number | null;
      horaEntrada: string;
      horaSalida?: string | null;
      observacion: string;
    }
  ): Observable<{ idRegistro: number }> {
    return this.api.put(`/asistencia/manual/${idRegistro}`, cuerpo);
  }
}

/** Administración de Roles y Matriz de Permisos (permiso usuarios.gestionar). */
@Injectable({ providedIn: 'root' })
export class RolesAdminApi {
  private readonly api = inject(ApiClient);

  listarRoles(soloActivos = false): Observable<readonly RolSistema[]> {
    return this.api.get('/usuarios/roles', { soloActivos });
  }

  listarPermisos(): Observable<readonly PermisoSistema[]> {
    return this.api.get('/usuarios/permisos');
  }

  guardarRol(cuerpo: {
    idRol?: number | null;
    nombre: string;
    descripcion?: string | null;
    activo: boolean;
    codigosPermisos: readonly string[];
  }): Observable<number> {
    if (cuerpo.idRol) {
      return this.api.put(`/usuarios/roles/${cuerpo.idRol}`, cuerpo);
    }
    return this.api.post('/usuarios/roles', cuerpo);
  }

  desactivarRol(idRol: number): Observable<void> {
    return this.api.post(`/usuarios/roles/${idRol}/desactivar`, {});
  }
}

