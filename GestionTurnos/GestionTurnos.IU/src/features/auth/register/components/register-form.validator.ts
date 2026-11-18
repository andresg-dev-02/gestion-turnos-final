import { SolicitarRegistroRequest } from '../../../../app/core';

export function validarSolicitudRegistro(
  datos: SolicitarRegistroRequest
): Record<string, string> {
  const errores: Record<string, string> = {};

  if (!datos.codTipoDocumento.trim()) {
    errores['codTipoDocumento'] = 'Selecciona el tipo de documento.';
  }
  if (!datos.numeroDocumento.trim()) {
    errores['numeroDocumento'] = 'Ingresa el número de documento.';
  } else if (datos.numeroDocumento.trim().length > 30) {
    errores['numeroDocumento'] = 'Máximo 30 caracteres.';
  }
  if (!datos.nombres.trim()) {
    errores['nombres'] = 'Ingresa tus nombres.';
  } else if (datos.nombres.trim().length > 80) {
    errores['nombres'] = 'Máximo 80 caracteres.';
  }
  if (!datos.apellidos.trim()) {
    errores['apellidos'] = 'Ingresa tus apellidos.';
  } else if (datos.apellidos.trim().length > 80) {
    errores['apellidos'] = 'Máximo 80 caracteres.';
  }
  if (!datos.correo.trim()) {
    errores['correo'] = 'Ingresa tu correo electrónico.';
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(datos.correo.trim())) {
    errores['correo'] = 'El formato del correo no es válido.';
  }
  if (datos.contrasena.length < 8) {
    errores['contrasena'] = 'La contraseña debe tener al menos 8 caracteres.';
  }

  return errores;
}
