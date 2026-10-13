using AulaInnova.API.Data;
using AulaInnova.API.DTOs.Admin.Nfc;
using AulaInnova.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AulaInnova.API.Services;

public class AdminNfcService
{
    private readonly AulaInnovaDbContext _db;
    private readonly AuditService _auditService;

    public AdminNfcService(
        AulaInnovaDbContext db,
        AuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<NfcCredentialResponse> CreateAsync(
        CreateNfcCredentialRequest request,
        ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.Referencia))
            throw new ArgumentException(
                "La referencia NFC es obligatoria.");

        if (request.DesdeUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException(
                "DesdeUtc debe estar expresado en UTC.");

        var persona = await _db.Personas
            .FirstOrDefaultAsync(p => p.Id == request.PersonaId);

        if (persona is null || !persona.Habilitada)
            throw new ArgumentException(
                "La persona no existe o está deshabilitada.");

        // CK_credencial_nfc_referencia: solo hexadecimal en MAYÚSCULAS.
        // Se aceptan guiones o dos puntos (UUID v4, UID "04:A1:...") y se
        // quitan, igual que hace la estación al leer la tarjeta.
        var referencia = EstacionReglas.NormalizarReferenciaAdmin(
            request.Referencia);

        var referenceExists = await _db.CredencialesNfc
            .AnyAsync(c => c.Referencia == referencia);

        if (referenceExists)
            throw new InvalidOperationException(
                "La referencia NFC ya existe.");

        var activeCredential = await _db.CredencialesNfc
            .AnyAsync(c =>
                c.PersonaId == request.PersonaId &&
                c.Activa);

        if (activeCredential)
            throw new InvalidOperationException(
                "La persona ya tiene una credencial NFC activa.");

        var credential = new CredencialNfc
        {
            PersonaId = request.PersonaId,
            Referencia = referencia,
            DesdeUtc = request.DesdeUtc,
            HastaUtc = null,
            Activa = true,
            MotivoBaja = null,
            CreadoUtc = DateTime.UtcNow
        };

        _db.CredencialesNfc.Add(credential);

        _auditService.Registrar(
            user,
            "CredencialNfc",
            "NUEVA",
            "CREAR",
            "Alta de credencial NFC.",
            antes: null,
            despues: new
            {
                credential.PersonaId,
                credential.DesdeUtc,
                credential.HastaUtc,
                credential.Activa,
                credential.MotivoBaja
            });

        await _db.SaveChangesAsync();

        return ToResponse(credential);
    }

    public async Task<List<NfcCredentialResponse>> GetAllAsync()
    {
        return await _db.CredencialesNfc
            .AsNoTracking()
            .OrderByDescending(c => c.CreadoUtc)
            .Select(c => new NfcCredentialResponse
            {
                Id = c.Id,
                PersonaId = c.PersonaId,
                Referencia = c.Referencia,
                DesdeUtc = c.DesdeUtc,
                HastaUtc = c.HastaUtc,
                Activa = c.Activa,
                MotivoBaja = c.MotivoBaja
            })
            .ToListAsync();
    }

    public async Task<NfcCredentialResponse?> GetByIdAsync(long id)
    {
        var credential = await _db.CredencialesNfc
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        return credential is null
            ? null
            : ToResponse(credential);
    }

    public async Task RevokeAsync(
        long id,
        RevokeNfcCredentialRequest request,
        ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.MotivoBaja))
            throw new ArgumentException(
                "El motivo de baja es obligatorio.");

        var credential = await _db.CredencialesNfc
            .FirstOrDefaultAsync(c => c.Id == id);

        if (credential is null)
            throw new KeyNotFoundException(
                "Credencial NFC no encontrada.");

        if (!credential.Activa)
            throw new InvalidOperationException(
                "La credencial ya está inactiva.");

        var antes = new
        {
            credential.Id,
            credential.PersonaId,
            credential.DesdeUtc,
            credential.HastaUtc,
            credential.Activa,
            credential.MotivoBaja
        };

        credential.Activa = false;
        credential.HastaUtc = DateTime.UtcNow;
        credential.MotivoBaja = request.MotivoBaja.Trim();

        _auditService.Registrar(
            user,
            "CredencialNfc",
            credential.Id.ToString(),
            "REVOCAR",
            request.MotivoBaja.Trim(),
            antes: antes,
            despues: new
            {
                credential.Id,
                credential.PersonaId,
                credential.DesdeUtc,
                credential.HastaUtc,
                credential.Activa,
                credential.MotivoBaja
            });

        await _db.SaveChangesAsync();
    }

    private static NfcCredentialResponse ToResponse(
        CredencialNfc credential)
    {
        return new NfcCredentialResponse
        {
            Id = credential.Id,
            PersonaId = credential.PersonaId,
            Referencia = credential.Referencia,
            DesdeUtc = credential.DesdeUtc,
            HastaUtc = credential.HastaUtc,
            Activa = credential.Activa,
            MotivoBaja = credential.MotivoBaja
        };
    }
}