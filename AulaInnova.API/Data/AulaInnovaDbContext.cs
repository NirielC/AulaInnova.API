using AulaInnova.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AulaInnova.API.Data;

public class AulaInnovaDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AulaInnovaDbContext(
        DbContextOptions<AulaInnovaDbContext> options)
        : base(options)
    {
    }

    // Bloque A
    public DbSet<Persona> Personas => Set<Persona>();
    public DbSet<Estudiante> Estudiantes => Set<Estudiante>();
    public DbSet<Personal> Personal => Set<Personal>();
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<Concesion> Concesiones => Set<Concesion>();
    public DbSet<Aula> Aulas => Set<Aula>();
    public DbSet<Grupo> Grupos => Set<Grupo>();
    public DbSet<Matricula> Matriculas => Set<Matricula>();
    public DbSet<Asignatura> Asignaturas => Set<Asignatura>();
    public DbSet<HorarioClase> HorariosClase => Set<HorarioClase>();
    public DbSet<Jornada> Jornadas => Set<Jornada>();
    public DbSet<CredencialNfc> CredencialesNfc => Set<CredencialNfc>();
    public DbSet<Estacion> Estaciones => Set<Estacion>();
    public DbSet<EstacionConfiguracion> EstacionesConfiguracion => Set<EstacionConfiguracion>();
    public DbSet<EstacionSecreto> EstacionesSecretos => Set<EstacionSecreto>();
    public DbSet<EstacionOperacion> EstacionesOperaciones => Set<EstacionOperacion>();
    public DbSet<MotivoSalida> MotivosSalida => Set<MotivoSalida>();
    public DbSet<Evento> Eventos => Set<Evento>();
    public DbSet<Asistencia> Asistencias => Set<Asistencia>();
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();

    // Bloque B
    public DbSet<SalidaTemporal> SalidasTemporales => Set<SalidaTemporal>();
    public DbSet<CategoriaIncidencia> CategoriasIncidencia => Set<CategoriaIncidencia>();
    public DbSet<TipoIncidencia> TiposIncidencia => Set<TipoIncidencia>();
    public DbSet<Incidencia> Incidencias => Set<Incidencia>();
    public DbSet<ObservacionIncidencia> ObservacionesIncidencia => Set<ObservacionIncidencia>();

    // Bloque C
    public DbSet<FallaEstacion> FallasEstacion => Set<FallaEstacion>();
    public DbSet<Asignacion> Asignaciones => Set<Asignacion>();
    public DbSet<NecesidadCobertura> NecesidadesCobertura => Set<NecesidadCobertura>();
    public DbSet<VerificacionCobertura> VerificacionesCobertura => Set<VerificacionCobertura>();

    // Bloque D
    public DbSet<ContextoClase> ContextosClase => Set<ContextoClase>();
    public DbSet<ZonaVisual> ZonasVisuales => Set<ZonaVisual>();
    public DbSet<SesionVisual> SesionesVisuales => Set<SesionVisual>();
    public DbSet<IndicadorVisual> IndicadoresVisuales => Set<IndicadorVisual>();
    public DbSet<MedicionVisual> MedicionesVisuales => Set<MedicionVisual>();

    // Model configuration
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Bloque A

        // Persona
        builder.Entity<Persona>(entity =>
        {
            entity.ToTable(
                "persona",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(p => p.Id);

            entity.Property(p => p.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(p => p.TipoPersona)
                .HasColumnName("tipo_persona")
                .HasMaxLength(10)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(p => p.Nombres)
                .HasColumnName("nombres")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(p => p.Apellidos)
                .HasColumnName("apellidos")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(p => p.Habilitada)
                .HasColumnName("habilitada")
                .IsRequired();

            entity.Property(p => p.CreadoUtc)
                .HasColumnName("creado_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(p => p.Version)
                .HasColumnName("version")
                .IsRowVersion();

            entity.HasIndex(p => new { p.Id, p.TipoPersona })
                .IsUnique()
                .HasDatabaseName("UQ_persona_id_tipo");
        });

        // Estudiante
        builder.Entity<Estudiante>(entity =>
        {
            entity.ToTable(
                "estudiante",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(e => e.PersonaId);

            entity.Property(e => e.PersonaId)
                .HasColumnName("persona_id");

            entity.Property(e => e.TipoPersona)
                .HasColumnName("tipo_persona")
                .HasMaxLength(10)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(e => e.Codigo)
                .HasColumnName("codigo")
                .HasMaxLength(30)
                .IsUnicode(false)
                .IsRequired();

            entity.HasIndex(e => e.Codigo)
                .IsUnique()
                .HasDatabaseName("UQ_estudiante_codigo");

            entity.HasOne(e => e.Persona)
                .WithOne(p => p.Estudiante)
                .HasForeignKey<Estudiante>(e => new
                {
                    e.PersonaId,
                    e.TipoPersona
                })
                .HasPrincipalKey<Persona>(p => new
                {
                    p.Id,
                    p.TipoPersona
                })
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Personal
        builder.Entity<Personal>(entity =>
        {
            entity.ToTable(
                "personal",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(p => p.PersonaId);

            entity.Property(p => p.PersonaId)
                .HasColumnName("persona_id");

            entity.Property(p => p.TipoPersona)
                .HasColumnName("tipo_persona")
                .HasMaxLength(10)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(p => p.Codigo)
                .HasColumnName("codigo")
                .HasMaxLength(30)
                .IsUnicode(false)
                .IsRequired();

            entity.HasIndex(p => p.Codigo)
                .IsUnique()
                .HasDatabaseName("UQ_personal_codigo");

            entity.HasOne(p => p.Persona)
                .WithOne(x => x.Personal)
                .HasForeignKey<Personal>(p => new
                {
                    p.PersonaId,
                    p.TipoPersona
                })
                .HasPrincipalKey<Persona>(p => new
                {
                    p.Id,
                    p.TipoPersona
                })
                .OnDelete(DeleteBehavior.Restrict);
        });

        //Aula
        builder.Entity<Aula>(entity =>
        {
            entity.ToTable(
                "aula",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(a => a.Id);

            entity.Property(a => a.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(a => a.Codigo)
                .HasColumnName("codigo")
                .HasMaxLength(30)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(a => a.Nombre)
                .HasColumnName("nombre")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(a => a.Habilitada)
                .HasColumnName("habilitada")
                .IsRequired();

            entity.Property(a => a.Version)
                .HasColumnName("version")
                .IsRowVersion();

            entity.HasIndex(a => a.Codigo)
                .IsUnique()
                .HasDatabaseName("UQ_aula_codigo");
        });

        // Grupo
        builder.Entity<Grupo>(entity =>
        {
            entity.ToTable(
                "grupo",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(g => g.Id);

            entity.Property(g => g.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(g => g.Ciclo)
                .HasColumnName("ciclo")
                .IsRequired();

            entity.Property(g => g.Grado)
                .HasColumnName("grado")
                .HasMaxLength(20)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(g => g.Seccion)
                .HasColumnName("seccion")
                .HasMaxLength(10)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(g => g.Desde)
                .HasColumnName("desde")
                .HasColumnType("date")
                .IsRequired();

            entity.Property(g => g.Hasta)
                .HasColumnName("hasta")
                .HasColumnType("date")
                .IsRequired();

            entity.Property(g => g.Habilitado)
                .HasColumnName("habilitado")
                .IsRequired();

            entity.Property(g => g.Version)
                .HasColumnName("version")
                .IsRowVersion();
        });

        // Asignatura
        builder.Entity<Asignatura>(entity =>
        {
            entity.ToTable(
                "asignatura",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(a => a.Id);

            entity.Property(a => a.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(a => a.Codigo)
                .HasColumnName("codigo")
                .HasMaxLength(30)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(a => a.Nombre)
                .HasColumnName("nombre")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(a => a.Habilitada)
                .HasColumnName("habilitada")
                .IsRequired();

            entity.Property(a => a.Version)
                .HasColumnName("version")
                .IsRowVersion();

            entity.HasIndex(a => a.Codigo)
                .IsUnique()
                .HasDatabaseName("UQ_asignatura_codigo");
        });

        // Matricula
        builder.Entity<Matricula>(entity =>
        {
            entity.ToTable(
                "matricula",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(m => m.Id);

            entity.Property(m => m.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(m => m.EstudianteId)
                .HasColumnName("estudiante_id")
                .IsRequired();

            entity.Property(m => m.GrupoId)
                .HasColumnName("grupo_id")
                .IsRequired();

            entity.Property(m => m.Desde)
                .HasColumnName("desde")
                .HasColumnType("date")
                .IsRequired();

            entity.Property(m => m.Hasta)
                .HasColumnName("hasta")
                .HasColumnType("date");

            entity.Property(m => m.Anulada)
                .HasColumnName("anulada")
                .IsRequired();

            entity.Property(m => m.CreadoUtc)
                .HasColumnName("creado_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(m => m.Version)
                .HasColumnName("version")
                .IsRowVersion();

            entity.HasOne(m => m.Estudiante)
                .WithMany()
                .HasForeignKey(m => m.EstudianteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.Grupo)
                .WithMany()
                .HasForeignKey(m => m.GrupoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // HorarioClase
        builder.Entity<HorarioClase>(entity =>
        {
            entity.ToTable(
                "horario_clase",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(h => h.Id);

            entity.HasAlternateKey(h => new
            {
                h.Id,
                h.GrupoId,
                h.DocenteId,
                h.AulaId
            })
            .HasName("AK_horario_clase_contexto");

            entity.Property(h => h.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(h => h.AsignaturaId)
                .HasColumnName("asignatura_id")
                .IsRequired();

            entity.Property(h => h.GrupoId)
                .HasColumnName("grupo_id")
                .IsRequired();

            entity.Property(h => h.DocenteId)
                .HasColumnName("docente_id")
                .IsRequired();

            entity.Property(h => h.AulaId)
                .HasColumnName("aula_id")
                .IsRequired();

            entity.Property(h => h.DiaSemana)
                .HasColumnName("dia_semana")
                .IsRequired();

            entity.Property(h => h.HoraInicio)
                .HasColumnName("hora_inicio")
                .HasColumnType("time(0)")
                .IsRequired();

            entity.Property(h => h.HoraFin)
                .HasColumnName("hora_fin")
                .HasColumnType("time(0)")
                .IsRequired();

            entity.Property(h => h.VigenteDesde)
                .HasColumnName("vigente_desde")
                .HasColumnType("date")
                .IsRequired();

            entity.Property(h => h.VigenteHasta)
                .HasColumnName("vigente_hasta")
                .HasColumnType("date");

            entity.Property(h => h.Anulado)
                .HasColumnName("anulado")
                .IsRequired();

            entity.Property(h => h.CreadoUtc)
                .HasColumnName("creado_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(h => h.Version)
                .HasColumnName("version")
                .IsRowVersion();

            entity.HasOne(h => h.Asignatura)
                .WithMany()
                .HasForeignKey(h => h.AsignaturaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(h => h.Grupo)
                .WithMany()
                .HasForeignKey(h => h.GrupoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(h => h.Docente)
                .WithMany()
                .HasForeignKey(h => h.DocenteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(h => h.Aula)
                .WithMany()
                .HasForeignKey(h => h.AulaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Jornada
        builder.Entity<Jornada>(entity =>
        {
            entity.ToTable(
                "jornada",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(j => j.Id);

            entity.Property(j => j.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(j => j.FechaLocal)
                .HasColumnName("fecha_local")
                .HasColumnType("date")
                .IsRequired();

            entity.Property(j => j.ZonaHoraria)
                .HasColumnName("zona_horaria")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(j => j.InicioUtc)
                .HasColumnName("inicio_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(j => j.FinUtc)
                .HasColumnName("fin_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(j => j.LimitePuntualUtc)
                .HasColumnName("limite_puntual_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(j => j.Estado)
                .HasColumnName("estado")
                .HasMaxLength(20)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(j => j.MotivoExcepcion)
                .HasColumnName("motivo_excepcion")
                .HasMaxLength(500);

            entity.Property(j => j.Version)
                .HasColumnName("version")
                .IsRowVersion();
        });

        // CredencialNfc
        builder.Entity<CredencialNfc>(entity =>
        {
            entity.ToTable(
                "credencial_nfc",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(c => c.Id);

            entity.Property(c => c.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(c => c.Referencia)
                .HasColumnName("referencia")
                .HasMaxLength(64)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(c => c.PersonaId)
                .HasColumnName("persona_id")
                .IsRequired();

            entity.Property(c => c.DesdeUtc)
                .HasColumnName("desde_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(c => c.HastaUtc)
                .HasColumnName("hasta_utc")
                .HasColumnType("datetime2(3)");

            entity.Property(c => c.Activa)
                .HasColumnName("activa")
                .IsRequired();

            entity.Property(c => c.MotivoBaja)
                .HasColumnName("motivo_baja")
                .HasMaxLength(200);

            entity.Property(c => c.CreadoUtc)
                .HasColumnName("creado_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(c => c.Version)
                .HasColumnName("version")
                .IsRowVersion();

            entity.HasOne(c => c.Persona)
                .WithMany()
                .HasForeignKey(c => c.PersonaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(c => c.Referencia)
                .IsUnique()
                .HasFilter("[activa] = 1")
                .HasDatabaseName("UX_credencial_nfc_referencia_activa");
        });

        // Estacion
        builder.Entity<Estacion>(entity =>
        {
            entity.ToTable(
                "estacion",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.CodigoLogico)
                .HasColumnName("codigo_logico")
                .HasMaxLength(40)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(e => e.Habilitada)
                .HasColumnName("habilitada")
                .IsRequired();

            entity.Property(e => e.MacInventario)
                .HasColumnName("mac_inventario")
                .HasMaxLength(17)
                .IsUnicode(false);

            entity.Property(e => e.UltimaComunicacionUtc)
                .HasColumnName("ultima_comunicacion_utc")
                .HasColumnType("datetime2(3)");

            entity.Property(e => e.ModoReportado)
                .HasColumnName("modo_reportado")
                .HasMaxLength(15)
                .IsUnicode(false);

            entity.Property(e => e.ModoReportadoUtc)
                .HasColumnName("modo_reportado_utc")
                .HasColumnType("datetime2(3)");

            entity.Property(e => e.CreadoUtc)
                .HasColumnName("creado_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(e => e.Version)
                .HasColumnName("version")
                .IsRowVersion();

            entity.HasIndex(e => e.CodigoLogico)
                .IsUnique()
                .HasDatabaseName("UQ_estacion_codigo_logico");

            entity.HasIndex(e => e.MacInventario)
                .IsUnique()
                .HasFilter("[mac_inventario] IS NOT NULL")
                .HasDatabaseName("UX_estacion_mac_inventario");
        });

        // EstacionConfiguracion
        builder.Entity<EstacionConfiguracion>(entity =>
        {
            entity.ToTable(
                "estacion_configuracion",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.EstacionId)
                .HasColumnName("estacion_id")
                .IsRequired();

            entity.Property(e => e.TipoPunto)
                .HasColumnName("tipo_punto")
                .HasMaxLength(12)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(e => e.AulaId)
                .HasColumnName("aula_id");

            entity.Property(e => e.EtiquetaPunto)
                .HasColumnName("etiqueta_punto")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.ExpiracionSelectorS)
                .HasColumnName("expiracion_selector_s")
                .IsRequired();

            entity.Property(e => e.VigenteDesdeUtc)
                .HasColumnName("vigente_desde_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(e => e.VigenteHastaUtc)
                .HasColumnName("vigente_hasta_utc")
                .HasColumnType("datetime2(3)");

            entity.Property(e => e.ConfiguradaPor)
                .HasColumnName("configurada_por")
                .IsRequired();

            entity.Property(e => e.MotivoCambio)
                .HasColumnName("motivo_cambio")
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.CreadoUtc)
                .HasColumnName("creado_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(e => e.Version)
                .HasColumnName("version")
                .IsRowVersion();

            entity.HasOne(e => e.Estacion)
                .WithMany(e => e.Configuraciones)
                .HasForeignKey(e => e.EstacionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Aula)
                .WithMany()
                .HasForeignKey(e => e.AulaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ConfiguradaPorUsuario)
                .WithMany()
                .HasForeignKey(e => e.ConfiguradaPor)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.EstacionId)
                .IsUnique()
                .HasFilter("[vigente_hasta_utc] IS NULL")
                .HasDatabaseName("UX_estacion_configuracion_vigente");
        });

        // EstacionSecreto
        builder.Entity<EstacionSecreto>(entity =>
        {
            entity.ToTable(
                "estacion_secreto",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.EstacionId)
                .HasColumnName("estacion_id")
                .IsRequired();

            entity.Property(e => e.IdentificadorClave)
                .HasColumnName("identificador_clave")
                .HasMaxLength(50)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(e => e.HashSecreto)
                .HasColumnName("hash_secreto")
                .HasColumnType("varbinary(32)")
                .IsRequired();

            entity.Property(e => e.DesdeUtc)
                .HasColumnName("desde_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(e => e.RevocadaUtc)
                .HasColumnName("revocada_utc")
                .HasColumnType("datetime2(3)");

            entity.HasOne(e => e.Estacion)
                .WithMany(e => e.Secretos)
                .HasForeignKey(e => e.EstacionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.IdentificadorClave)
                .IsUnique()
                .HasDatabaseName("UQ_estacion_secreto_identificador");
        });

        // EstacionOperacion
        builder.Entity<EstacionOperacion>(entity =>
        {
            entity.ToTable(
                "estacion_operacion",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(e => new
            {
                e.EstacionConfiguracionId,
                e.Accion
            });

            entity.Property(e => e.EstacionConfiguracionId)
                .HasColumnName("estacion_configuracion_id");

            entity.Property(e => e.Accion)
                .HasColumnName("accion")
                .HasMaxLength(35)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(e => e.ModoInterfaz)
                .HasColumnName("modo_interfaz")
                .HasMaxLength(15)
                .IsUnicode(false)
                .IsRequired();

            entity.HasOne(e => e.EstacionConfiguracion)
                .WithMany(e => e.Operaciones)
                .HasForeignKey(e => e.EstacionConfiguracionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // MotivoSalida
        builder.Entity<MotivoSalida>(entity =>
        {
            entity.ToTable(
                "motivo_salida",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(m => m.Codigo);

            entity.Property(m => m.Codigo)
                .HasColumnName("codigo")
                .HasMaxLength(25)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(m => m.Nombre)
                .HasColumnName("nombre")
                .HasMaxLength(80)
                .IsRequired();

            entity.Property(m => m.Habilitado)
                .HasColumnName("habilitado")
                .IsRequired();
        });

        // Evento
        builder.Entity<Evento>(entity =>
        {
            entity.ToTable(
                "evento",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedNever();

            entity.Property(e => e.HashSolicitud)
                .HasColumnName("hash_solicitud")
                .HasColumnType("varbinary(32)")
                .IsRequired();

            entity.Property(e => e.Origen)
                .HasColumnName("origen")
                .HasMaxLength(15)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(e => e.Accion)
                .HasColumnName("accion")
                .HasMaxLength(35)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(e => e.PersonaId)
                .HasColumnName("persona_id");

            entity.Property(e => e.CredencialId)
                .HasColumnName("credencial_id");

            entity.Property(e => e.EstacionId)
                .HasColumnName("estacion_id");

            entity.Property(e => e.EstacionConfiguracionId)
                .HasColumnName("estacion_configuracion_id");

            entity.Property(e => e.OperadorId)
                .HasColumnName("operador_id");

            entity.Property(e => e.AulaId)
                .HasColumnName("aula_id");

            entity.Property(e => e.JornadaId)
                .HasColumnName("jornada_id");

            entity.Property(e => e.MatriculaId)
                .HasColumnName("matricula_id");

            entity.Property(e => e.AsignacionId)
                .HasColumnName("asignacion_id");

            entity.Property(e => e.PuntoManual)
                .HasColumnName("punto_manual")
                .HasMaxLength(100);

            entity.Property(e => e.MotivoCodigo)
                .HasColumnName("motivo_codigo")
                .HasMaxLength(25)
                .IsUnicode(false);

            entity.Property(e => e.MotivoManual)
                .HasColumnName("motivo_manual")
                .HasMaxLength(500);

            entity.Property(e => e.OcurridoUtc)
                .HasColumnName("ocurrido_utc")
                .HasColumnType("datetime2(3)");

            entity.Property(e => e.RecibidoUtc)
                .HasColumnName("recibido_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(e => e.CalidadHora)
                .HasColumnName("calidad_hora")
                .HasMaxLength(20)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(e => e.Estado)
                .HasColumnName("estado")
                .HasMaxLength(15)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(e => e.CodigoResultado)
                .HasColumnName("codigo_resultado")
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.Property(e => e.EventoCanonicoId)
                .HasColumnName("evento_canonico_id");

            entity.Property(e => e.SeleccionId)
                .HasColumnName("seleccion_id");

            entity.Property(e => e.SeleccionEdadMs)
                .HasColumnName("seleccion_edad_ms");

            entity.HasIndex(e => e.HashSolicitud)
                .HasDatabaseName("IX_evento_hash_solicitud");

            entity.HasIndex(e => new
            {
                e.PersonaId,
                e.OcurridoUtc
            })
                .HasDatabaseName("IX_evento_persona_ocurrido");

            entity.HasIndex(e => new
            {
                e.AsignacionId,
                e.RecibidoUtc,
                e.Id
            })
                .HasDatabaseName("IX_evento_asignacion_recibido")
                .IncludeProperties(e => new
                {
                    e.Accion,
                    e.PersonaId,
                    e.EstacionId,
                    e.OcurridoUtc,
                    e.CalidadHora,
                    e.Estado,
                    e.CodigoResultado
                })
                .HasFilter("[asignacion_id] IS NOT NULL");

            entity.HasOne(e => e.Persona)
                .WithMany()
                .HasForeignKey(e => e.PersonaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Credencial)
                .WithMany()
                .HasForeignKey(e => e.CredencialId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Estacion)
                .WithMany()
                .HasForeignKey(e => e.EstacionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.EstacionConfiguracion)
                .WithMany()
                .HasForeignKey(e => e.EstacionConfiguracionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Operador)
                .WithMany()
                .HasForeignKey(e => e.OperadorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Aula)
                .WithMany()
                .HasForeignKey(e => e.AulaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Jornada)
                .WithMany()
                .HasForeignKey(e => e.JornadaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Matricula)
                .WithMany()
                .HasForeignKey(e => e.MatriculaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.EventoCanonico)
                .WithMany()
                .HasForeignKey(e => e.EventoCanonicoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Asignacion)
                .WithMany()
                .HasForeignKey(e => e.AsignacionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Asistencia
        builder.Entity<Asistencia>(entity =>
        {
            entity.ToTable(
                "asistencia",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(a => a.Id);

            entity.Property(a => a.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(a => a.EstudianteId)
                .HasColumnName("estudiante_id")
                .IsRequired();

            entity.Property(a => a.JornadaId)
                .HasColumnName("jornada_id")
                .IsRequired();

            entity.Property(a => a.MatriculaId)
                .HasColumnName("matricula_id")
                .IsRequired();

            entity.Property(a => a.EventoLlegadaId)
                .HasColumnName("evento_llegada_id");

            entity.Property(a => a.LlegadaUtc)
                .HasColumnName("llegada_utc")
                .HasColumnType("datetime2(3)");

            entity.Property(a => a.CalidadHora)
                .HasColumnName("calidad_hora")
                .HasMaxLength(20)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(a => a.Estado)
                .HasColumnName("estado")
                .HasMaxLength(25)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(a => a.Puntualidad)
                .HasColumnName("puntualidad")
                .HasMaxLength(20)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(a => a.Justificacion)
                .HasColumnName("justificacion")
                .HasMaxLength(500);

            entity.Property(a => a.RevisadoPor)
                .HasColumnName("revisado_por");

            entity.Property(a => a.RevisadoUtc)
                .HasColumnName("revisado_utc")
                .HasColumnType("datetime2(3)");

            entity.Property(a => a.CreadoUtc)
                .HasColumnName("creado_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(a => a.Version)
                .HasColumnName("version")
                .IsRowVersion();

            entity.HasIndex(a => new
            {
                a.EstudianteId,
                a.JornadaId
            })
            .IsUnique()
            .HasDatabaseName("UQ_asistencia_estudiante_jornada");

            entity.HasOne(a => a.Estudiante)
                .WithMany()
                .HasForeignKey(a => a.EstudianteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Jornada)
                .WithMany()
                .HasForeignKey(a => a.JornadaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Matricula)
                .WithMany()
                .HasForeignKey(a => a.MatriculaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.EventoLlegada)
                .WithMany()
                .HasForeignKey(a => a.EventoLlegadaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Revisor)
                .WithMany()
                .HasForeignKey(a => a.RevisadoPor)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Auditoria
        builder.Entity<Auditoria>(entity =>
        {
            entity.ToTable(
                "auditoria",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(a => a.Id);

            entity.Property(a => a.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(a => a.OperadorId)
                .HasColumnName("operador_id")
                .IsRequired();

            entity.Property(a => a.Entidad)
                .HasColumnName("entidad")
                .HasMaxLength(100)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(a => a.RegistroId)
                .HasColumnName("registro_id")
                .HasMaxLength(100)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(a => a.Operacion)
                .HasColumnName("operacion")
                .HasMaxLength(20)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(a => a.Motivo)
                .HasColumnName("motivo")
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(a => a.AntesJson)
                .HasColumnName("antes_json")
                .HasColumnType("nvarchar(max)");

            entity.Property(a => a.DespuesJson)
                .HasColumnName("despues_json")
                .HasColumnType("nvarchar(max)");

            entity.Property(a => a.EventoId)
                .HasColumnName("evento_id");

            entity.Property(a => a.CreadoUtc)
                .HasColumnName("creado_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(a => a.SolicitudId)
                .HasColumnName("solicitud_id")
                .IsRequired();

            entity.Property(a => a.HashSolicitud)
                .HasColumnName("hash_solicitud")
                .HasColumnType("varbinary(32)")
                .IsRequired();

            entity.HasIndex(a => new
            {
                a.SolicitudId,
                a.Entidad,
                a.RegistroId
            })
            .IsUnique()
            .HasDatabaseName("UQ_auditoria_solicitud_entidad_registro");

            entity.HasOne(a => a.Operador)
                .WithMany()
                .HasForeignKey(a => a.OperadorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Evento)
                .WithMany()
                .HasForeignKey(a => a.EventoId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        // Permiso
        builder.Entity<Permiso>(entity =>
        {
            entity.ToTable(
                "permiso",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(e => e.Codigo);

            entity.Property(e => e.Codigo)
                .HasColumnName("codigo")
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.Property(e => e.Descripcion)
                .HasColumnName("descripcion")
                .HasMaxLength(200)
                .IsUnicode(false);

            entity.Property(e => e.Habilitado)
                .HasColumnName("habilitado");
        });

        // Concesion
        builder.Entity<Concesion>(entity =>
        {
            entity.ToTable(
                "concesion",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.UsuarioId)
                .HasColumnName("usuario_id")
                .IsRequired();

            entity.Property(e => e.PermisoCodigo)
                .HasColumnName("permiso_codigo")
                .HasMaxLength(60)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(e => e.Ambito)
                .HasColumnName("ambito")
                .HasMaxLength(15)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(e => e.GrupoId)
                .HasColumnName("grupo_id");

            entity.Property(e => e.DesdeUtc)
                .HasColumnName("desde_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(e => e.HastaUtc)
                .HasColumnName("hasta_utc")
                .HasColumnType("datetime2(3)");

            entity.Property(e => e.Habilitada)
                .HasColumnName("habilitada")
                .IsRequired();

            entity.Property(e => e.Version)
                .HasColumnName("version")
                .IsRowVersion();

            entity.HasOne(e => e.Usuario)
                .WithMany()
                .HasForeignKey(e => e.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Permiso)
                .WithMany()
                .HasForeignKey(e => e.PermisoCodigo)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Grupo)
                .WithMany()
                .HasForeignKey(e => e.GrupoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Bloque B

        // Salida Temporal
        // SalidaTemporal
        builder.Entity<SalidaTemporal>(entity =>
        {
            entity.ToTable(
                "salida_temporal",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(s => s.Id);

            entity.Property(s => s.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(s => s.EstudianteId)
                .HasColumnName("estudiante_id")
                .IsRequired();

            entity.Property(s => s.JornadaId)
                .HasColumnName("jornada_id")
                .IsRequired();

            entity.Property(s => s.MatriculaId)
                .HasColumnName("matricula_id")
                .IsRequired();

            entity.Property(s => s.AulaId)
                .HasColumnName("aula_id")
                .IsRequired();

            entity.Property(s => s.MotivoCodigo)
                .HasColumnName("motivo_codigo")
                .HasMaxLength(25)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(s => s.EventoSalidaId)
                .HasColumnName("evento_salida_id")
                .IsRequired();

            entity.Property(s => s.EventoRegresoId)
                .HasColumnName("evento_regreso_id");

            entity.Property(s => s.SalidaUtc)
                .HasColumnName("salida_utc")
                .HasColumnType("datetime2(3)");

            entity.Property(s => s.RegresoUtc)
                .HasColumnName("regreso_utc")
                .HasColumnType("datetime2(3)");

            entity.Property(s => s.ExpiraUtc)
                .HasColumnName("expira_utc")
                .HasColumnType("datetime2(3)");

            entity.Property(s => s.HorasFiables)
                .HasColumnName("horas_fiables")
                .IsRequired();

            entity.Property(s => s.Estado)
                .HasColumnName("estado")
                .HasMaxLength(15)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(s => s.CreadoUtc)
                .HasColumnName("creado_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(s => s.Version)
                .HasColumnName("version")
                .IsRowVersion();

            entity.HasOne(s => s.Estudiante)
                .WithMany()
                .HasForeignKey(s => s.EstudianteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Jornada)
                .WithMany()
                .HasForeignKey(s => s.JornadaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Matricula)
                .WithMany()
                .HasForeignKey(s => s.MatriculaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Aula)
                .WithMany()
                .HasForeignKey(s => s.AulaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Motivo)
                .WithMany()
                .HasForeignKey(s => s.MotivoCodigo)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.EventoSalida)
                .WithMany()
                .HasForeignKey(s => s.EventoSalidaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.EventoRegreso)
                .WithMany()
                .HasForeignKey(s => s.EventoRegresoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(s => new
            {
                s.EstudianteId,
                s.JornadaId,
                s.Estado
            })
            .HasDatabaseName("IX_salida_temporal_estudiante_jornada_estado");
        });

        // CategoriaIncidencia
        builder.Entity<CategoriaIncidencia>(entity =>
        {
            entity.ToTable(
                "categoria_incidencia",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(c => c.Id);

            entity.Property(c => c.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(c => c.Codigo)
                .HasColumnName("codigo")
                .HasMaxLength(40)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(c => c.Nombre)
                .HasColumnName("nombre")
                .HasMaxLength(100)
                .IsUnicode()
                .IsRequired();

            entity.Property(c => c.Descripcion)
                .HasColumnName("descripcion")
                .HasMaxLength(400)
                .IsUnicode()
                .IsRequired();

            entity.Property(c => c.Habilitada)
                .HasColumnName("habilitada")
                .IsRequired();

            entity.Property(c => c.Version)
                .HasColumnName("version")
                .IsRowVersion();

            entity.HasIndex(c => c.Codigo)
                .IsUnique()
                .HasDatabaseName("UQ_categoria_incidencia_codigo");

            entity.HasIndex(c => c.Nombre)
                .IsUnique()
                .HasDatabaseName("UQ_categoria_incidencia_nombre");
        });

        // TipoIncidencia
        builder.Entity<TipoIncidencia>(entity =>
        {
            entity.ToTable(
                "tipo_incidencia",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(t => t.Id);

            entity.Property(t => t.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(t => t.CategoriaId)
                .HasColumnName("categoria_id")
                .IsRequired();

            entity.Property(t => t.Codigo)
                .HasColumnName("codigo")
                .HasMaxLength(50)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(t => t.Nombre)
                .HasColumnName("nombre")
                .HasMaxLength(150)
                .IsUnicode()
                .IsRequired();

            entity.Property(t => t.Descripcion)
                .HasColumnName("descripcion")
                .HasMaxLength(500)
                .IsUnicode()
                .IsRequired();

            entity.Property(t => t.Habilitado)
                .HasColumnName("habilitado")
                .IsRequired();

            entity.Property(t => t.Version)
                .HasColumnName("version")
                .IsRowVersion();

            entity.HasIndex(t => t.Codigo)
                .IsUnique()
                .HasDatabaseName("UQ_tipo_incidencia_codigo");

            entity.HasIndex(t => t.Nombre)
                .IsUnique()
                .HasDatabaseName("UQ_tipo_incidencia_nombre");

            entity.HasIndex(t => new
            {
                t.CategoriaId,
                t.Habilitado
            })
            .HasDatabaseName("IX_tipo_incidencia_categoria_habilitado");

            entity.HasOne(t => t.Categoria)
                .WithMany(c => c.TiposIncidencia)
                .HasForeignKey(t => t.CategoriaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Incidencia
        builder.Entity<Incidencia>(entity =>
        {
            entity.ToTable(
                "incidencia",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(i => i.Id);

            entity.Property(i => i.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(i => i.EstudianteId)
                .HasColumnName("estudiante_id")
                .IsRequired();

            entity.Property(i => i.JornadaId)
                .HasColumnName("jornada_id")
                .IsRequired();

            entity.Property(i => i.TipoIncidenciaId)
                .HasColumnName("tipo_incidencia_id")
                .IsRequired();

            entity.Property(i => i.EventoOrigenId)
                .HasColumnName("evento_origen_id")
                .IsRequired();

            entity.Property(i => i.CreadaPor)
                .HasColumnName("creada_por")
                .IsRequired();

            entity.Property(i => i.Vigente)
                .HasColumnName("vigente")
                .IsRequired();

            entity.Property(i => i.CreadoUtc)
                .HasColumnName("creado_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(i => i.Version)
                .HasColumnName("version")
                .IsRowVersion();

            // Una incidencia solo puede tener un evento de origen.
            entity.HasIndex(i => i.EventoOrigenId)
                .IsUnique()
                .HasDatabaseName("UQ_incidencia_evento_origen");

            // Consultas por estudiante, jornada y tipo de incidencia.
            entity.HasIndex(i => new
            {
                i.EstudianteId,
                i.JornadaId,
                i.TipoIncidenciaId
            })
            .HasDatabaseName("IX_incidencia_estudiante_jornada_tipo")
            .IncludeProperties(i => new
            {
                i.Vigente,
                i.EventoOrigenId,
                i.CreadaPor,
                i.CreadoUtc
            });

            // Solo puede existir una incidencia vigente
            // del mismo tipo para un estudiante en una jornada.
            entity.HasIndex(i => new
            {
                i.EstudianteId,
                i.JornadaId,
                i.TipoIncidenciaId
            })
            .IsUnique()
            .HasFilter("[vigente] = 1")
            .HasDatabaseName("UX_incidencia_estudiante_jornada_tipo_vigente");

            // Consultas de incidencias de una jornada.
            entity.HasIndex(i => new
            {
                i.JornadaId,
                i.Vigente,
                i.CreadoUtc
            })
            .HasDatabaseName("IX_incidencia_jornada_vigente")
            .IncludeProperties(i => new
            {
                i.EstudianteId,
                i.TipoIncidenciaId,
                i.CreadaPor
            });

            // Consultas por tipo y fecha.
            entity.HasIndex(i => new
            {
                i.TipoIncidenciaId,
                i.CreadoUtc
            })
            .HasDatabaseName("IX_incidencia_tipo_fecha")
            .IncludeProperties(i => new
            {
                i.EstudianteId,
                i.JornadaId,
                i.Vigente
            });

            // Consultas por usuario que creó la incidencia.
            entity.HasIndex(i => new
            {
                i.CreadaPor,
                i.CreadoUtc
            })
            .HasDatabaseName("IX_incidencia_creada_por_fecha")
            .IncludeProperties(i => new
            {
                i.EstudianteId,
                i.JornadaId,
                i.TipoIncidenciaId,
                i.Vigente
            });

            // Estudiante
            entity.HasOne(i => i.Estudiante)
                .WithMany()
                .HasForeignKey(i => i.EstudianteId)
                .OnDelete(DeleteBehavior.Restrict);

            // Jornada
            entity.HasOne(i => i.Jornada)
                .WithMany()
                .HasForeignKey(i => i.JornadaId)
                .OnDelete(DeleteBehavior.Restrict);

            // Tipo de incidencia
            entity.HasOne(i => i.TipoIncidencia)
                .WithMany(t => t.Incidencias)
                .HasForeignKey(i => i.TipoIncidenciaId)
                .OnDelete(DeleteBehavior.Restrict);

            // Evento que originó la incidencia
            entity.HasOne(i => i.EventoOrigen)
                .WithMany()
                .HasForeignKey(i => i.EventoOrigenId)
                .OnDelete(DeleteBehavior.Restrict);

            // Usuario de ASP.NET Identity que creó la incidencia
            entity.HasOne(i => i.Creador)
                .WithMany()
                .HasForeignKey(i => i.CreadaPor)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ObservacionIncidencia
        builder.Entity<ObservacionIncidencia>(entity =>
        {
            entity.ToTable(
                "observacion_incidencia",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(o => o.Id);

            entity.Property(o => o.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(o => o.IncidenciaId)
                .HasColumnName("incidencia_id")
                .IsRequired();

            entity.Property(o => o.EventoId)
                .HasColumnName("evento_id")
                .IsRequired();

            entity.Property(o => o.AutorId)
                .HasColumnName("autor_id")
                .IsRequired();

            entity.Property(o => o.Texto)
                .HasColumnName("texto")
                .HasMaxLength(2000)
                .IsUnicode()
                .IsRequired();

            entity.Property(o => o.HechoUtc)
                .HasColumnName("hecho_utc")
                .HasColumnType("datetime2(3)");

            entity.Property(o => o.Comunicable)
                .HasColumnName("comunicable")
                .IsRequired();

            entity.Property(o => o.Vigente)
                .HasColumnName("vigente")
                .IsRequired();

            entity.Property(o => o.CreadoUtc)
                .HasColumnName("creado_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(o => o.Version)
                .HasColumnName("version")
                .IsRowVersion();

            entity.HasIndex(o => o.EventoId)
                .IsUnique()
                .HasDatabaseName("UQ_observacion_incidencia_evento");

            entity.HasIndex(o => new
            {
                o.IncidenciaId,
                o.Vigente,
                o.CreadoUtc
            })
            .HasDatabaseName("IX_observacion_incidencia_incidencia_fecha")
            .IncludeProperties(o => new
            {
                o.HechoUtc,
                o.Comunicable,
                o.EventoId
            });

            entity.HasIndex(o => new
            {
                o.IncidenciaId,
                o.CreadoUtc
            })
            .HasDatabaseName("IX_observacion_incidencia_comunicable")
            .IncludeProperties(o => new
            {
                o.Texto,
                o.HechoUtc
            })
            .HasFilter("[vigente] = 1 AND [comunicable] = 1");

            entity.HasIndex(o => new
            {
                o.AutorId,
                o.CreadoUtc
            })
            .HasDatabaseName("IX_observacion_incidencia_autor_fecha")
            .IncludeProperties(o => new
            {
                o.IncidenciaId,
                o.Comunicable,
                o.Vigente
            });

            entity.HasOne(o => o.Incidencia)
                .WithMany(i => i.Observaciones)
                .HasForeignKey(o => o.IncidenciaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.Evento)
                .WithMany()
                .HasForeignKey(o => o.EventoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.Autor)
                .WithMany()
                .HasForeignKey(o => o.AutorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Bloque C

        // FallaEstacion
        builder.Entity<FallaEstacion>(entity =>
        {
            entity.ToTable(
                "falla_estacion",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(f => f.Id);

            entity.Property(f => f.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(f => f.EstacionId)
                .HasColumnName("estacion_id")
                .IsRequired();

            entity.Property(f => f.ReportadoPor)
                .HasColumnName("reportado_por")
                .IsRequired();

            entity.Property(f => f.Descripcion)
                .HasColumnName("descripcion")
                .HasMaxLength(1000)
                .IsUnicode()
                .IsRequired();

            entity.Property(f => f.Estado)
                .HasColumnName("estado")
                .HasMaxLength(12)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(f => f.ResueltaUtc)
                .HasColumnName("resuelta_utc")
                .HasColumnType("datetime2(3)");

            entity.Property(f => f.CreadoUtc)
                .HasColumnName("creado_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(f => f.Version)
                .HasColumnName("version")
                .IsRowVersion();

            entity.HasIndex(f => new
            {
                f.EstacionId,
                f.Estado,
                f.CreadoUtc
            })
            .HasDatabaseName("IX_falla_estacion_estacion_estado_fecha");

            entity.HasIndex(f => new
            {
                f.ReportadoPor,
                f.CreadoUtc
            })
            .HasDatabaseName("IX_falla_estacion_reportado_por_fecha");

            entity.HasOne(f => f.Estacion)
                .WithMany()
                .HasForeignKey(f => f.EstacionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(f => f.Reportador)
                .WithMany()
                .HasForeignKey(f => f.ReportadoPor)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Asignacion
        builder.Entity<Asignacion>(entity =>
        {
            entity.ToTable(
                "asignacion",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(a => a.Id);

            entity.Property(a => a.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(a => a.JornadaId)
                .HasColumnName("jornada_id")
                .IsRequired();

            entity.Property(a => a.GrupoId)
                .HasColumnName("grupo_id")
                .IsRequired();

            entity.Property(a => a.HorarioClaseId)
                .HasColumnName("horario_clase_id");

            entity.Property(a => a.PersonalId)
                .HasColumnName("personal_id")
                .IsRequired();

            entity.Property(a => a.AulaId)
                .HasColumnName("aula_id")
                .IsRequired();

            entity.Property(a => a.Tipo)
                .HasColumnName("tipo")
                .HasMaxLength(12)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(a => a.InicioUtc)
                .HasColumnName("inicio_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(a => a.FinUtc)
                .HasColumnName("fin_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(a => a.Estado)
                .HasColumnName("estado")
                .HasMaxLength(12)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(a => a.SustituyeId)
                .HasColumnName("sustituye_id");

            entity.Property(a => a.Actividad)
                .HasColumnName("actividad")
                .HasMaxLength(150)
                .IsUnicode();

            entity.Property(a => a.CreadoUtc)
                .HasColumnName("creado_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(a => a.Version)
                .HasColumnName("version")
                .IsRowVersion();

            // Índice único: no puede existir dos veces
            // la misma asignación ACTIVA exacta.
            entity.HasIndex(a => new
            {
                a.JornadaId,
                a.GrupoId,
                a.PersonalId,
                a.AulaId,
                a.Tipo,
                a.InicioUtc,
                a.FinUtc
            })
            .IsUnique()
            .HasFilter("[estado] = 'ACTIVA'")
            .HasDatabaseName("UX_asignacion_activa_exacta");

            // Una sustitución activa no puede repetir
            // el mismo segmento temporal de la asignación sustituida.
            entity.HasIndex(a => new
            {
                a.SustituyeId,
                a.InicioUtc,
                a.FinUtc
            })
            .IsUnique()
            .HasFilter("[estado] = 'ACTIVA' AND [sustituye_id] IS NOT NULL")
            .HasDatabaseName("UX_asignacion_sustitucion_segmento");

            entity.HasIndex(a => new
            {
                a.PersonalId,
                a.Estado,
                a.InicioUtc,
                a.FinUtc
            })
            .HasDatabaseName("IX_asignacion_personal_intervalo")
            .IncludeProperties(a => new
            {
                a.JornadaId,
                a.GrupoId,
                a.AulaId,
                a.Tipo,
                a.SustituyeId
            });

            entity.HasIndex(a => new
            {
                a.AulaId,
                a.Estado,
                a.InicioUtc,
                a.FinUtc
            })
            .HasDatabaseName("IX_asignacion_aula_intervalo")
            .IncludeProperties(a => new
            {
                a.JornadaId,
                a.GrupoId,
                a.PersonalId,
                a.Tipo
            });

            entity.HasIndex(a => new
            {
                a.GrupoId,
                a.JornadaId,
                a.Estado,
                a.InicioUtc,
                a.FinUtc
            })
            .HasDatabaseName("IX_asignacion_grupo_jornada_intervalo")
            .IncludeProperties(a => new
            {
                a.PersonalId,
                a.AulaId,
                a.Tipo,
                a.HorarioClaseId
            });

            entity.HasIndex(a => new
            {
                a.HorarioClaseId,
                a.JornadaId
            })
            .HasDatabaseName("IX_asignacion_horario")
            .IncludeProperties(a => new
            {
                a.PersonalId,
                a.GrupoId,
                a.AulaId,
                a.Tipo,
                a.Estado
            })
            .HasFilter("[horario_clase_id] IS NOT NULL");

            // Jornada
            entity.HasOne(a => a.Jornada)
                .WithMany()
                .HasForeignKey(a => a.JornadaId)
                .OnDelete(DeleteBehavior.Restrict);

            // Grupo
            entity.HasOne(a => a.Grupo)
                .WithMany()
                .HasForeignKey(a => a.GrupoId)
                .OnDelete(DeleteBehavior.Restrict);

            // FK COMPUESTA hacia HorarioClase.
            entity.HasOne(a => a.HorarioClase)
                .WithMany()
                .HasForeignKey(
                    a => new
                    {
                        a.HorarioClaseId,
                        a.GrupoId,
                        a.PersonalId,
                        a.AulaId
                    })
                .HasPrincipalKey(
                    h => new
                    {
                        h.Id,
                        h.GrupoId,
                        h.DocenteId,
                        h.AulaId
                    })
                .OnDelete(DeleteBehavior.Restrict);

            // Personal
            entity.HasOne(a => a.Personal)
                .WithMany()
                .HasForeignKey(a => a.PersonalId)
                .OnDelete(DeleteBehavior.Restrict);

            // Aula
            entity.HasOne(a => a.Aula)
                .WithMany()
                .HasForeignKey(a => a.AulaId)
                .OnDelete(DeleteBehavior.Restrict);

            // Autorreferencia de sustitución.
            entity.HasOne(a => a.Sustituye)
                .WithMany(a => a.Sustituciones)
                .HasForeignKey(a => a.SustituyeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // NecesidadCobertura
        builder.Entity<NecesidadCobertura>(entity =>
        {
            entity.ToTable(
                "necesidad_cobertura",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(n => n.Id);

            entity.Property(n => n.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(n => n.AsignacionOrigenId)
                .HasColumnName("asignacion_origen_id")
                .IsRequired();

            entity.Property(n => n.AsignacionCoberturaId)
                .HasColumnName("asignacion_cobertura_id");

            entity.Property(n => n.Estado)
                .HasColumnName("estado")
                .HasMaxLength(12)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(n => n.Motivo)
                .HasColumnName("motivo")
                .HasMaxLength(500)
                .IsUnicode()
                .IsRequired();

            entity.Property(n => n.CreadaPor)
                .HasColumnName("creada_por")
                .IsRequired();

            entity.Property(n => n.CreadoUtc)
                .HasColumnName("creado_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(n => n.CerradoPor)
                .HasColumnName("cerrado_por");

            entity.Property(n => n.CerradoUtc)
                .HasColumnName("cerrado_utc")
                .HasColumnType("datetime2(3)");

            entity.Property(n => n.Version)
                .HasColumnName("version")
                .IsRowVersion();

            // Una necesidad de cobertura solo puede pertenecer
            // una vez a una asignación de origen.
            entity.HasIndex(n => n.AsignacionOrigenId)
                .IsUnique()
                .HasDatabaseName("UQ_necesidad_cobertura_origen");

            // Una asignación de cobertura solo puede estar asociada
            // a una necesidad cuando no sea NULL, y no puede repetirse.
            entity.HasIndex(n => n.AsignacionCoberturaId)
                .IsUnique()
                .HasFilter("[asignacion_cobertura_id] IS NOT NULL")
                .HasDatabaseName("UX_necesidad_cobertura_asignacion");

            entity.HasIndex(n => new
            {
                n.Estado,
                n.CreadoUtc
            })
            .HasDatabaseName("IX_necesidad_cobertura_estado_fecha")
            .IncludeProperties(n => new
            {
                n.AsignacionOrigenId,
                n.AsignacionCoberturaId,
                n.Motivo
            });

            entity.HasIndex(n => new
            {
                n.CreadaPor,
                n.CreadoUtc
            })
            .HasDatabaseName("IX_necesidad_cobertura_creada_por_fecha")
            .IncludeProperties(n => new
            {
                n.Estado,
                n.AsignacionOrigenId,
                n.AsignacionCoberturaId
            });

            entity.HasIndex(n => new
            {
                n.CerradoPor,
                n.CerradoUtc
            })
            .HasDatabaseName("IX_necesidad_cobertura_cerrado_por_fecha")
            .IncludeProperties(n => new
            {
                n.Estado,
                n.AsignacionOrigenId
            })
            .HasFilter("[cerrado_por] IS NOT NULL");

            // Asignación que originó la necesidad.
            entity.HasOne(n => n.AsignacionOrigen)
                .WithMany()
                .HasForeignKey(n => n.AsignacionOrigenId)
                .OnDelete(DeleteBehavior.Restrict);

            // Asignación que cubre la necesidad.
            entity.HasOne(n => n.AsignacionCobertura)
                .WithMany()
                .HasForeignKey(n => n.AsignacionCoberturaId)
                .OnDelete(DeleteBehavior.Restrict);

            // Usuario que creó la necesidad.
            entity.HasOne(n => n.Creador)
                .WithMany()
                .HasForeignKey(n => n.CreadaPor)
                .OnDelete(DeleteBehavior.Restrict);

            // Usuario que cerró la necesidad.
            entity.HasOne(n => n.Cerrador)
                .WithMany()
                .HasForeignKey(n => n.CerradoPor)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // VerificacionCobertura
        builder.Entity<VerificacionCobertura>(entity =>
        {
            entity.ToTable(
                "verificacion_cobertura",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(v => v.Id);

            entity.Property(v => v.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(v => v.AsignacionId)
                .HasColumnName("asignacion_id")
                .IsRequired();

            entity.Property(v => v.VerificadoPor)
                .HasColumnName("verificado_por")
                .IsRequired();

            entity.Property(v => v.Estado)
                .HasColumnName("estado")
                .HasMaxLength(20)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(v => v.Observacion)
                .HasColumnName("observacion")
                .HasMaxLength(500)
                .IsUnicode()
                .IsRequired();

            entity.Property(v => v.OcurridoUtc)
                .HasColumnName("ocurrido_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(v => v.CreadoUtc)
                .HasColumnName("creado_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.HasIndex(v => new
            {
                v.AsignacionId,
                v.OcurridoUtc,
                v.CreadoUtc,
                v.Id
            })
            .HasDatabaseName("IX_verificacion_cobertura_asignacion_fecha")
            .IncludeProperties(v => new
            {
                v.Estado,
                v.Observacion,
                v.VerificadoPor
            });

            entity.HasIndex(v => new
            {
                v.VerificadoPor,
                v.CreadoUtc
            })
            .HasDatabaseName("IX_verificacion_cobertura_verificador_fecha")
            .IncludeProperties(v => new
            {
                v.AsignacionId,
                v.Estado,
                v.OcurridoUtc
            });

            entity.HasOne(v => v.Asignacion)
                .WithMany()
                .HasForeignKey(v => v.AsignacionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(v => v.Verificador)
                .WithMany()
                .HasForeignKey(v => v.VerificadoPor)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Bloque D

        // ContextoClase
        builder.Entity<ContextoClase>(entity =>
        {
            entity.ToTable(
                "contexto_clase",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.AsignacionId)
                .HasColumnName("asignacion_id")
                .IsRequired();

            entity.Property(e => e.AutorId)
                .HasColumnName("autor_id")
                .IsRequired();

            entity.Property(e => e.InicioUtc)
                .HasColumnName("inicio_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(e => e.FinUtc)
                .HasColumnName("fin_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(e => e.Descripcion)
                .HasColumnName("descripcion")
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(e => e.CreadoUtc)
                .HasColumnName("creado_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(e => e.Version)
                .HasColumnName("version")
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasIndex(e => new
            {
                e.AsignacionId,
                e.InicioUtc,
                e.FinUtc
            })
            .HasDatabaseName("IX_contexto_clase_asignacion_intervalo")
            .IncludeProperties(e => new
            {
                e.AutorId,
                e.Descripcion,
                e.CreadoUtc
            });

            entity.HasIndex(e => new
            {
                e.AutorId,
                e.CreadoUtc
            })
            .HasDatabaseName("IX_contexto_clase_autor_fecha")
            .IncludeProperties(e => new
            {
                e.AsignacionId,
                e.InicioUtc,
                e.FinUtc
            });

            entity.HasOne(e => e.Asignacion)
                .WithMany()
                .HasForeignKey(e => e.AsignacionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Autor)
                .WithMany()
                .HasForeignKey(e => e.AutorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ZonaVisual
        builder.Entity<ZonaVisual>(entity =>
        {
            entity.ToTable(
                "zona_visual",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.AulaId)
                .HasColumnName("aula_id")
                .IsRequired();

            entity.Property(e => e.Codigo)
                .HasColumnName("codigo")
                .HasMaxLength(30)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(e => e.Revision)
                .HasColumnName("revision")
                .IsRequired();

            entity.Property(e => e.GeometriaJson)
                .HasColumnName("geometria_json")
                .HasMaxLength(2000)
                .IsRequired();

            entity.Property(e => e.Habilitada)
                .HasColumnName("habilitada")
                .IsRequired();

            entity.HasIndex(e => new
            {
                e.AulaId,
                e.Codigo,
                e.Revision
            })
            .IsUnique()
            .HasDatabaseName("UQ_zona_visual_aula_codigo_revision");

            entity.HasIndex(e => new
            {
                e.AulaId,
                e.Codigo
            })
            .IsUnique()
            .HasDatabaseName("UX_zona_visual_habilitada")
            .IncludeProperties(e => new
            {
                e.Id,
                e.Revision,
                e.GeometriaJson
            })
            .HasFilter("[habilitada] = 1");

            entity.HasOne(e => e.Aula)
                .WithMany()
                .HasForeignKey(e => e.AulaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // SesionVisual
        builder.Entity<SesionVisual>(entity =>
        {
            entity.ToTable(
                "sesion_visual",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.AsignacionId)
                .HasColumnName("asignacion_id")
                .IsRequired();

            entity.Property(e => e.AulaId)
                .HasColumnName("aula_id")
                .IsRequired();

            entity.Property(e => e.AutorizadaPor)
                .HasColumnName("autorizada_por")
                .IsRequired();

            entity.Property(e => e.ReferenciaAutorizacion)
                .HasColumnName("referencia_autorizacion")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.InicioUtc)
                .HasColumnName("inicio_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(e => e.FinUtc)
                .HasColumnName("fin_utc")
                .HasColumnType("datetime2(3)");

            entity.Property(e => e.VersionProceso)
                .HasColumnName("version_proceso")
                .HasMaxLength(50)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(e => e.Estado)
                .HasColumnName("estado")
                .HasMaxLength(12)
                .IsUnicode(false)
                .IsRequired();

            entity.HasIndex(e => e.AulaId)
                .IsUnique()
                .HasDatabaseName("UX_sesion_visual_aula_activa")
                .HasFilter("[estado] = 'ACTIVA'");

            entity.HasIndex(e => new
            {
                e.AsignacionId,
                e.InicioUtc
            })
            .HasDatabaseName("IX_sesion_visual_asignacion_fecha")
            .IncludeProperties(e => new
            {
                e.AulaId,
                e.FinUtc,
                e.Estado,
                e.VersionProceso
            });

            entity.HasIndex(e => new
            {
                e.AulaId,
                e.InicioUtc
            })
            .HasDatabaseName("IX_sesion_visual_aula_fecha")
            .IncludeProperties(e => new
            {
                e.AsignacionId,
                e.FinUtc,
                e.Estado,
                e.VersionProceso
            });

            entity.HasIndex(e => new
            {
                e.AutorizadaPor,
                e.InicioUtc
            })
            .HasDatabaseName("IX_sesion_visual_autorizada_por_fecha")
            .IncludeProperties(e => new
            {
                e.AsignacionId,
                e.AulaId,
                e.FinUtc,
                e.Estado
            });

            entity.HasOne(e => e.Asignacion)
                .WithMany()
                .HasForeignKey(e => e.AsignacionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Aula)
                .WithMany()
                .HasForeignKey(e => e.AulaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Autorizador)
                .WithMany()
                .HasForeignKey(e => e.AutorizadaPor)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // IndicadorVisual
        builder.Entity<IndicadorVisual>(entity =>
        {
            entity.ToTable(
                "indicador_visual",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Codigo)
                .HasColumnName("codigo")
                .HasMaxLength(40)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(e => e.Revision)
                .HasColumnName("revision")
                .IsRequired();

            entity.Property(e => e.Unidad)
                .HasColumnName("unidad")
                .HasMaxLength(60)
                .IsRequired();

            entity.Property(e => e.Metodo)
                .HasColumnName("metodo")
                .HasMaxLength(1500)
                .IsRequired();

            entity.Property(e => e.ConfiguracionJson)
                .HasColumnName("configuracion_json")
                .HasMaxLength(2000)
                .IsRequired();

            entity.Property(e => e.ReferenciaValidacion)
                .HasColumnName("referencia_validacion")
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(e => e.Habilitado)
                .HasColumnName("habilitado")
                .IsRequired();

            entity.HasIndex(e => new
            {
                e.Codigo,
                e.Revision
            })
            .IsUnique()
            .HasDatabaseName("UQ_indicador_visual_codigo_revision");

            entity.HasIndex(e => e.Codigo)
                .IsUnique()
                .HasDatabaseName("UX_indicador_visual_habilitado")
                .IncludeProperties(e => new
                {
                    e.Id,
                    e.Revision,
                    e.Unidad
                })
                .HasFilter("[habilitado] = 1");
        });

        // MedicionVisual
        builder.Entity<MedicionVisual>(entity =>
        {
            entity.ToTable(
                "medicion_visual",
                "ai",
                table => table.ExcludeFromMigrations());

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.SesionId)
                .HasColumnName("sesion_id")
                .IsRequired();

            entity.Property(e => e.ZonaId)
                .HasColumnName("zona_id")
                .IsRequired();

            entity.Property(e => e.IndicadorId)
                .HasColumnName("indicador_id")
                .IsRequired();

            entity.Property(e => e.InicioUtc)
                .HasColumnName("inicio_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(e => e.FinUtc)
                .HasColumnName("fin_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.Property(e => e.Valor)
                .HasColumnName("valor")
                .HasColumnType("decimal(18,4)");

            entity.Property(e => e.Valida)
                .HasColumnName("valida")
                .IsRequired();

            entity.Property(e => e.MotivoInvalidez)
                .HasColumnName("motivo_invalidez")
                .HasMaxLength(200);

            entity.Property(e => e.MuestrasValidas)
                .HasColumnName("muestras_validas")
                .IsRequired();

            entity.Property(e => e.MuestrasTotales)
                .HasColumnName("muestras_totales")
                .IsRequired();

            entity.Property(e => e.CreadoUtc)
                .HasColumnName("creado_utc")
                .HasColumnType("datetime2(3)")
                .IsRequired();

            entity.HasIndex(e => new
            {
                e.SesionId,
                e.ZonaId,
                e.IndicadorId,
                e.InicioUtc,
                e.FinUtc
            })
            .IsUnique()
            .HasDatabaseName("UQ_medicion_visual_ventana");

            entity.HasIndex(e => new
            {
                e.SesionId,
                e.InicioUtc,
                e.FinUtc
            })
            .HasDatabaseName("IX_medicion_visual_sesion_inicio")
            .IncludeProperties(e => new
            {
                e.ZonaId,
                e.IndicadorId,
                e.Valor,
                e.Valida,
                e.MuestrasValidas,
                e.MuestrasTotales
            });

            entity.HasIndex(e => new
            {
                e.IndicadorId,
                e.InicioUtc,
                e.FinUtc
            })
            .HasDatabaseName("IX_medicion_visual_indicador_inicio")
            .IncludeProperties(e => new
            {
                e.SesionId,
                e.ZonaId,
                e.Valor,
                e.Valida,
                e.MuestrasValidas,
                e.MuestrasTotales
            });

            entity.HasIndex(e => new
            {
                e.ZonaId,
                e.InicioUtc,
                e.FinUtc
            })
            .HasDatabaseName("IX_medicion_visual_zona_inicio")
            .IncludeProperties(e => new
            {
                e.SesionId,
                e.IndicadorId,
                e.Valor,
                e.Valida,
                e.MuestrasValidas,
                e.MuestrasTotales
            });

            entity.HasOne(e => e.Sesion)
                .WithMany()
                .HasForeignKey(e => e.SesionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Zona)
                .WithMany()
                .HasForeignKey(e => e.ZonaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Indicador)
                .WithMany()
                .HasForeignKey(e => e.IndicadorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
