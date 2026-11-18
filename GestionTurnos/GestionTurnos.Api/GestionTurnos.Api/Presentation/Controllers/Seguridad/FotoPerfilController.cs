using Asp.Versioning;
using GestionTurnos.Api.Application.CQRS.Seguridad.FotoPerfil;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GestionTurnos.Api.Presentation.Controllers.Seguridad;

/// <summary>
/// Foto de perfil del usuario en sesión (empleado o administrador). Las imágenes se guardan en Cloudinary.
/// El usuario siempre se resuelve desde el token: nadie puede cambiar la foto de otro.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/mi-perfil/foto")]
public sealed class FotoPerfilController : BaseApiController
{
    // Margen sobre el máximo de 5 MB de la imagen para el resto del formulario multipart.
    private const long LimiteSolicitudBytes = ReglasFotoPerfil.TamanoMaximoBytes + 512 * 1024;

    public FotoPerfilController(ISender sender) : base(sender)
    {
    }

    /// <summary>URL de la foto de perfil actual (<c>url = null</c> si no tiene).</summary>
    [HttpGet]
    public async Task<IActionResult> Obtener(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ObtenerMiFotoPerfilQuery(), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Sube o reemplaza la foto de perfil. multipart/form-data con el campo <c>archivo</c>
    /// (JPG, PNG o WEBP, máximo 5 MB). Se recorta a un cuadrado centrado en el rostro.
    /// </summary>
    [HttpPut]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(LimiteSolicitudBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = LimiteSolicitudBytes)]
    public async Task<IActionResult> Actualizar(IFormFile? archivo, CancellationToken cancellationToken)
    {
        var imagen = archivo is null
            ? null
            : new ArchivoImagen(archivo.FileName, archivo.ContentType, archivo.Length, archivo.OpenReadStream);

        var result = await Sender.Send(new ActualizarMiFotoPerfilCommand(imagen!), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Quita la foto de perfil actual.</summary>
    [HttpDelete]
    public async Task<IActionResult> Eliminar(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new EliminarMiFotoPerfilCommand(), cancellationToken);
        return HandleResult(result);
    }
}
