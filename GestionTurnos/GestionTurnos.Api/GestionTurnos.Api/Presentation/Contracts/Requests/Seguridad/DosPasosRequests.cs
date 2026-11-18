namespace GestionTurnos.Api.Presentation.Contracts.Requests.Seguridad;

/// <summary>Segundo paso del inicio de sesión: correo usado en el login y código recibido.</summary>
public sealed record VerificarDosPasosRequest(string Correo, string ClaveVerificacion, bool Recordar = false);

/// <summary>Solicita un código nuevo para el inicio de sesión pendiente.</summary>
public sealed record ReenviarCodigoDosPasosRequest(string Correo);

/// <summary>Re-autenticación con la contraseña actual (activar o desactivar).</summary>
public sealed record ContrasenaActualRequest(string Contrasena);

/// <summary>Código recibido para confirmar la activación.</summary>
public sealed record ConfirmarDosPasosRequest(string ClaveVerificacion);
