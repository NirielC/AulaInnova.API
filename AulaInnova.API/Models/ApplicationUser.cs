using Microsoft.AspNetCore.Identity;

namespace AulaInnova.API.Models;

public class ApplicationUser : IdentityUser<Guid>
{
    public long? PersonaId { get; set; }

    public Persona? Persona { get; set; }
}
