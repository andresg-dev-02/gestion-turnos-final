using FluentValidation;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Entities.Seguridad;
using GestionTurnos.Api.Domain.Errors.Seguridad;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Application.CQRS.Seguridad.FotoPerfil;

// Módulo de foto de perfil del usuario en sesión (empleado o administrador).
// El id_usuario nunca se recibe por parámetro: siempre sale del token.

public sealed record FotoPerfilDto(string? Url, DateTimeOffset? ActualizadoEn);

/// <summary>
/// Archivo recibido del cliente. El contenido se expone como método (no como propiedad) para que
/// LoggingBehavior no intente serializar el flujo de bytes en los logs.
/// </summary>
public sealed class ArchivoImagen
{
    private readonly Func<Stream> _abrir;

    public ArchivoImagen(string nombreArchivo, string tipoContenido, long tamanoBytes, Func<Stream> abrir)
    {
        NombreArchivo = nombreArchivo;
        TipoContenido = tipoContenido;
        TamanoBytes = tamanoBytes;
        _abrir = abrir;
    }

    public string NombreArchivo { get; }
    public string TipoContenido { get; }
    public long TamanoBytes { get; }

    public Stream AbrirLectura() => _abrir();
}

public static class ReglasFotoPerfil
{
    public const long TamanoMaximoBytes = 5 * 1024 * 1024;

    public static readonly string[] TiposPermitidos = ["image/jpeg", "image/png", "image/webp"];
}

// =====================================================================
// CONSULTAR MI FOTO
// =====================================================================

public sealed record ObtenerMiFotoPerfilQuery : IRequest<Result<FotoPerfilDto>>, IRequireAuthentication;

public sealed class ObtenerMiFotoPerfilQueryHandler : IRequestHandler<ObtenerMiFotoPerfilQuery, Result<FotoPerfilDto>>
{
    private readonly GestionTurnosContext _context;
    private readonly ICurrentUserService _currentUser;

    public ObtenerMiFotoPerfilQueryHandler(GestionTurnosContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<FotoPerfilDto>> Handle(ObtenerMiFotoPerfilQuery request, CancellationToken cancellationToken)
    {
        var foto = await _context.Set<UsuarioFotoPerfil>()
            .AsNoTracking()
            .Where(x => x.IdUsuario == _currentUser.UserId!.Value)
            .Select(x => new FotoPerfilDto(x.Url, x.ActualizadoEn))
            .FirstOrDefaultAsync(cancellationToken);

        return Result.Success(foto ?? new FotoPerfilDto(null, null));
    }
}

// =====================================================================
// SUBIR / REEMPLAZAR MI FOTO
// =====================================================================

public sealed record ActualizarMiFotoPerfilCommand(ArchivoImagen Archivo)
    : IRequest<Result<FotoPerfilDto>>, IRequireAuthentication;

public sealed class ActualizarMiFotoPerfilCommandValidator : AbstractValidator<ActualizarMiFotoPerfilCommand>
{
    public ActualizarMiFotoPerfilCommandValidator()
    {
        RuleFor(x => x.Archivo)
            .NotNull().WithMessage("Debe adjuntar una imagen.");

        When(x => x.Archivo is not null, () =>
        {
            RuleFor(x => x.Archivo.TamanoBytes)
                .GreaterThan(0).WithMessage("La imagen está vacía.")
                .LessThanOrEqualTo(ReglasFotoPerfil.TamanoMaximoBytes).WithMessage("La imagen no puede superar 5 MB.");

            RuleFor(x => x.Archivo.TipoContenido)
                .Must(t => ReglasFotoPerfil.TiposPermitidos.Contains(t, StringComparer.OrdinalIgnoreCase))
                .WithMessage("Formato no permitido. Use una imagen JPG, PNG o WEBP.");
        });
    }
}

public sealed class ActualizarMiFotoPerfilCommandHandler : IRequestHandler<ActualizarMiFotoPerfilCommand, Result<FotoPerfilDto>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IAlmacenImagenes _almacen;
    private readonly ILogger<ActualizarMiFotoPerfilCommandHandler> _logger;

    public ActualizarMiFotoPerfilCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IAlmacenImagenes almacen,
        ILogger<ActualizarMiFotoPerfilCommandHandler> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _almacen = almacen;
        _logger = logger;
    }

    public async Task<Result<FotoPerfilDto>> Handle(ActualizarMiFotoPerfilCommand request, CancellationToken cancellationToken)
    {
        var idUsuario = _currentUser.UserId!.Value;

        ImagenAlmacenada imagen;
        try
        {
            await using var contenido = request.Archivo.AbrirLectura();
            imagen = await _almacen.SubirFotoPerfilAsync(idUsuario, contenido, request.Archivo.NombreArchivo, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Fallo al subir la foto de perfil del usuario {IdUsuario}", idUsuario);
            return Result.Failure<FotoPerfilDto>(FotoPerfilErrors.ErrorAlmacenamiento);
        }

        var registro = await _context.Set<UsuarioFotoPerfil>()
            .FirstOrDefaultAsync(x => x.IdUsuario == idUsuario, cancellationToken);

        bool esNuevo = registro is null;
        if (registro is null)
        {
            registro = new UsuarioFotoPerfil { IdUsuario = idUsuario };
            _context.Set<UsuarioFotoPerfil>().Add(registro);
        }

        registro.Url = imagen.Url;
        registro.PublicId = imagen.PublicId;
        registro.ActualizadoEn = DateTimeOffset.UtcNow;

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Si era la primera foto, no dejar una imagen huérfana en Cloudinary.
            if (esNuevo)
            {
                await _almacen.EliminarAsync(imagen.PublicId, CancellationToken.None);
            }

            return Result.Failure<FotoPerfilDto>(DatabaseErrorTranslator.Translate(ex));
        }

        return Result.Success(new FotoPerfilDto(registro.Url, registro.ActualizadoEn));
    }
}

// =====================================================================
// QUITAR MI FOTO
// =====================================================================

public sealed record EliminarMiFotoPerfilCommand : IRequest<Result>, IRequireAuthentication;

public sealed class EliminarMiFotoPerfilCommandHandler : IRequestHandler<EliminarMiFotoPerfilCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IAlmacenImagenes _almacen;

    public EliminarMiFotoPerfilCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IAlmacenImagenes almacen)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _almacen = almacen;
    }

    public async Task<Result> Handle(EliminarMiFotoPerfilCommand request, CancellationToken cancellationToken)
    {
        var idUsuario = _currentUser.UserId!.Value;

        var registro = await _context.Set<UsuarioFotoPerfil>()
            .FirstOrDefaultAsync(x => x.IdUsuario == idUsuario, cancellationToken);

        if (registro is null)
        {
            return Result.Failure(FotoPerfilErrors.SinFoto);
        }

        var publicId = registro.PublicId;
        _context.Set<UsuarioFotoPerfil>().Remove(registro);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure(DatabaseErrorTranslator.Translate(ex));
        }

        // Después de confirmar en BD: si Cloudinary falla solo queda una imagen sin referencia.
        await _almacen.EliminarAsync(publicId, CancellationToken.None);

        return Result.Success();
    }
}
