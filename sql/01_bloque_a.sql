/*
    Aula Innova - INSTALADOR COMPLETO DEL BLOQUE A
    Motor objetivo: SQL Server 2022 (16.x) o superior, no Express.

    Uso:
      1. Abra una consulta nueva en SQL Server Management Studio.
      2. Pegue o abra este archivo completo.
      3. Ejecute todo el contenido una sola vez.

    El instalador crea la base AulaInnova solo cuando todavía no existe y,
    después, instala el esquema ai y todos los objetos del Bloque A.

    No crea ni altera dbo.AspNetUsers. Las cinco FK hacia ASP.NET Core Identity
    se agregan posteriormente con 003_bloque_a_integracion_identity.sql.
*/

USE [master];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF TRY_CONVERT(int, SERVERPROPERTY(N'ProductMajorVersion')) < 16
    THROW 51000, N'Aula Innova requiere SQL Server 2022 (16.x) o superior.', 1;

IF DB_ID(N'AulaInnova') IS NULL
BEGIN
    PRINT N'Creando base AulaInnova...';

    CREATE DATABASE [AulaInnova]
        COLLATE Modern_Spanish_100_CI_AI_SC_UTF8;

    ALTER DATABASE [AulaInnova] SET RECOVERY SIMPLE WITH NO_WAIT;
    ALTER DATABASE [AulaInnova] SET READ_COMMITTED_SNAPSHOT ON WITH NO_WAIT;

    PRINT N'Base AulaInnova creada correctamente.';
END
ELSE
BEGIN
    PRINT N'La base AulaInnova ya existe; se conservará su configuración actual.';
END;
GO

SELECT
    d.name AS base_datos,
    d.collation_name AS intercalacion,
    d.recovery_model_desc AS modelo_recuperacion,
    d.is_read_committed_snapshot_on AS read_committed_snapshot
FROM sys.databases AS d
WHERE d.name = N'AulaInnova';
GO

/*
    Aula Innova - Bloque A
    SQL Server 2022

    Incluye:
      - identidad escolar y perfiles excluyentes;
      - permisos y concesiones;
      - aulas, grupos, matrículas, asignaturas, horarios y jornadas;
      - credenciales NFC y estaciones con configuración versionada;
      - los dos modos exactos de estación: ASISTENCIA y MOVIMIENTOS;
      - sobre inmutable de eventos, asistencia diaria y auditoría;
      - catálogos iniciales, índices, vista operativa y trigger de auditoría.

    Despliegue por expansión:
      - Las columnas que apuntan a dbo.AspNetUsers se crean ahora, pero sus FK
        se agregan con 003_bloque_a_integracion_identity.sql después de que el
        responsable de ASP.NET Core Identity genere esa tabla con Id GUID.
      - evento.asignacion_id queda preparado; su FK se agregará en el Bloque C,
        cuando exista ai.asignacion.

    Seguridad:
      - No borra ni reemplaza objetos existentes.
      - Todo el bloque es transaccional y falla si detecta una instalación previa.
*/

USE [AulaInnova];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF TRY_CONVERT(int, SERVERPROPERTY(N'ProductMajorVersion')) < 16
    THROW 51001, N'El Bloque A requiere SQL Server 2022 (16.x) o superior.', 1;

IF OBJECT_ID(N'ai.persona', N'U') IS NOT NULL
    THROW 51002, N'El Bloque A ya parece instalado: existe ai.persona. No se hicieron cambios.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF SCHEMA_ID(N'ai') IS NULL
        EXEC(N'CREATE SCHEMA ai AUTHORIZATION dbo;');

    /* ================================================================
       1. Identidad escolar
       ================================================================ */

    CREATE TABLE ai.persona
    (
        id              bigint IDENTITY(1,1) NOT NULL,
        tipo_persona    varchar(10) NOT NULL,
        nombres         nvarchar(100) NOT NULL,
        apellidos       nvarchar(100) NOT NULL,
        habilitada      bit NOT NULL
            CONSTRAINT DF_persona_habilitada DEFAULT (1),
        creado_utc      datetime2(3) NOT NULL
            CONSTRAINT DF_persona_creado_utc DEFAULT (SYSUTCDATETIME()),
        version         rowversion NOT NULL,

        CONSTRAINT PK_persona PRIMARY KEY CLUSTERED (id),
        CONSTRAINT UQ_persona_id_tipo UNIQUE (id, tipo_persona),
        CONSTRAINT CK_persona_tipo
            CHECK (tipo_persona IN ('ESTUDIANTE', 'PERSONAL')),
        CONSTRAINT CK_persona_nombres
            CHECK (LEN(LTRIM(RTRIM(nombres))) > 0),
        CONSTRAINT CK_persona_apellidos
            CHECK (LEN(LTRIM(RTRIM(apellidos))) > 0)
    );

    CREATE TABLE ai.estudiante
    (
        persona_id      bigint NOT NULL,
        tipo_persona    varchar(10) NOT NULL
            CONSTRAINT DF_estudiante_tipo DEFAULT ('ESTUDIANTE'),
        codigo          varchar(30) NOT NULL,

        CONSTRAINT PK_estudiante PRIMARY KEY CLUSTERED (persona_id),
        CONSTRAINT UQ_estudiante_codigo UNIQUE (codigo),
        CONSTRAINT CK_estudiante_tipo
            CHECK (tipo_persona = 'ESTUDIANTE'),
        CONSTRAINT CK_estudiante_codigo
            CHECK (LEN(LTRIM(RTRIM(codigo))) > 0),
        CONSTRAINT FK_estudiante_persona_tipo
            FOREIGN KEY (persona_id, tipo_persona)
            REFERENCES ai.persona (id, tipo_persona)
    );

    CREATE TABLE ai.personal
    (
        persona_id      bigint NOT NULL,
        tipo_persona    varchar(10) NOT NULL
            CONSTRAINT DF_personal_tipo DEFAULT ('PERSONAL'),
        codigo          varchar(30) NOT NULL,

        CONSTRAINT PK_personal PRIMARY KEY CLUSTERED (persona_id),
        CONSTRAINT UQ_personal_codigo UNIQUE (codigo),
        CONSTRAINT CK_personal_tipo
            CHECK (tipo_persona = 'PERSONAL'),
        CONSTRAINT CK_personal_codigo
            CHECK (LEN(LTRIM(RTRIM(codigo))) > 0),
        CONSTRAINT FK_personal_persona_tipo
            FOREIGN KEY (persona_id, tipo_persona)
            REFERENCES ai.persona (id, tipo_persona)
    );

    /* ================================================================
       2. Catálogo de permisos y organización escolar
       ================================================================ */

    CREATE TABLE ai.permiso
    (
        codigo          varchar(60) NOT NULL,
        descripcion     nvarchar(200) NOT NULL,
        habilitado      bit NOT NULL
            CONSTRAINT DF_permiso_habilitado DEFAULT (1),

        CONSTRAINT PK_permiso PRIMARY KEY CLUSTERED (codigo),
        CONSTRAINT CK_permiso_codigo
            CHECK (LEN(LTRIM(RTRIM(codigo))) > 0),
        CONSTRAINT CK_permiso_descripcion
            CHECK (LEN(LTRIM(RTRIM(descripcion))) > 0)
    );

    CREATE TABLE ai.aula
    (
        id              bigint IDENTITY(1,1) NOT NULL,
        codigo          varchar(20) NOT NULL,
        nombre          nvarchar(100) NOT NULL,
        habilitada      bit NOT NULL
            CONSTRAINT DF_aula_habilitada DEFAULT (1),
        version         rowversion NOT NULL,

        CONSTRAINT PK_aula PRIMARY KEY CLUSTERED (id),
        CONSTRAINT UQ_aula_codigo UNIQUE (codigo),
        CONSTRAINT CK_aula_codigo
            CHECK (LEN(LTRIM(RTRIM(codigo))) > 0),
        CONSTRAINT CK_aula_nombre
            CHECK (LEN(LTRIM(RTRIM(nombre))) > 0)
    );

    CREATE TABLE ai.grupo
    (
        id              bigint IDENTITY(1,1) NOT NULL,
        ciclo           smallint NOT NULL,
        grado           nvarchar(60) NOT NULL,
        seccion         varchar(20) NOT NULL,
        desde           date NOT NULL,
        hasta           date NOT NULL,
        habilitado      bit NOT NULL
            CONSTRAINT DF_grupo_habilitado DEFAULT (1),
        version         rowversion NOT NULL,

        CONSTRAINT PK_grupo PRIMARY KEY CLUSTERED (id),
        CONSTRAINT UQ_grupo_ciclo_grado_seccion
            UNIQUE (ciclo, grado, seccion),
        CONSTRAINT CK_grupo_grado
            CHECK (LEN(LTRIM(RTRIM(grado))) > 0),
        CONSTRAINT CK_grupo_seccion
            CHECK (LEN(LTRIM(RTRIM(seccion))) > 0),
        CONSTRAINT CK_grupo_vigencia
            CHECK (hasta > desde)
    );

    CREATE TABLE ai.matricula
    (
        id              bigint IDENTITY(1,1) NOT NULL,
        estudiante_id   bigint NOT NULL,
        grupo_id        bigint NOT NULL,
        desde           date NOT NULL,
        hasta           date NULL,
        anulada         bit NOT NULL
            CONSTRAINT DF_matricula_anulada DEFAULT (0),
        creado_utc      datetime2(3) NOT NULL
            CONSTRAINT DF_matricula_creado_utc DEFAULT (SYSUTCDATETIME()),
        version         rowversion NOT NULL,

        CONSTRAINT PK_matricula PRIMARY KEY CLUSTERED (id),
        CONSTRAINT UQ_matricula_id_estudiante UNIQUE (id, estudiante_id),
        CONSTRAINT UQ_matricula_contexto UNIQUE (id, estudiante_id, grupo_id),
        CONSTRAINT CK_matricula_vigencia
            CHECK (hasta IS NULL OR hasta > desde),
        CONSTRAINT FK_matricula_estudiante
            FOREIGN KEY (estudiante_id) REFERENCES ai.estudiante (persona_id),
        CONSTRAINT FK_matricula_grupo
            FOREIGN KEY (grupo_id) REFERENCES ai.grupo (id)
    );

    CREATE UNIQUE INDEX UX_matricula_estudiante_abierta
        ON ai.matricula (estudiante_id)
        WHERE hasta IS NULL AND anulada = 0;

    CREATE INDEX IX_matricula_grupo_vigencia
        ON ai.matricula (grupo_id, desde, hasta)
        INCLUDE (estudiante_id, anulada);

    CREATE TABLE ai.asignatura
    (
        id              bigint IDENTITY(1,1) NOT NULL,
        codigo          varchar(30) NOT NULL,
        nombre          nvarchar(120) NOT NULL,
        habilitada      bit NOT NULL
            CONSTRAINT DF_asignatura_habilitada DEFAULT (1),
        version         rowversion NOT NULL,

        CONSTRAINT PK_asignatura PRIMARY KEY CLUSTERED (id),
        CONSTRAINT UQ_asignatura_codigo UNIQUE (codigo),
        CONSTRAINT CK_asignatura_codigo
            CHECK (LEN(LTRIM(RTRIM(codigo))) > 0),
        CONSTRAINT CK_asignatura_nombre
            CHECK (LEN(LTRIM(RTRIM(nombre))) > 0)
    );

    CREATE TABLE ai.horario_clase
    (
        id              bigint IDENTITY(1,1) NOT NULL,
        asignatura_id   bigint NOT NULL,
        grupo_id        bigint NOT NULL,
        docente_id      bigint NOT NULL,
        aula_id         bigint NOT NULL,
        dia_semana      tinyint NOT NULL,
        hora_inicio     time(0) NOT NULL,
        hora_fin        time(0) NOT NULL,
        vigente_desde   date NOT NULL,
        vigente_hasta   date NULL,
        anulado         bit NOT NULL
            CONSTRAINT DF_horario_clase_anulado DEFAULT (0),
        creado_utc      datetime2(3) NOT NULL
            CONSTRAINT DF_horario_clase_creado_utc DEFAULT (SYSUTCDATETIME()),
        version         rowversion NOT NULL,

        CONSTRAINT PK_horario_clase PRIMARY KEY CLUSTERED (id),
        CONSTRAINT CK_horario_clase_dia
            CHECK (dia_semana BETWEEN 1 AND 7),
        CONSTRAINT CK_horario_clase_horas
            CHECK (hora_fin > hora_inicio),
        CONSTRAINT CK_horario_clase_vigencia
            CHECK (vigente_hasta IS NULL OR vigente_hasta > vigente_desde),
        CONSTRAINT FK_horario_clase_asignatura
            FOREIGN KEY (asignatura_id) REFERENCES ai.asignatura (id),
        CONSTRAINT FK_horario_clase_grupo
            FOREIGN KEY (grupo_id) REFERENCES ai.grupo (id),
        CONSTRAINT FK_horario_clase_docente
            FOREIGN KEY (docente_id) REFERENCES ai.personal (persona_id),
        CONSTRAINT FK_horario_clase_aula
            FOREIGN KEY (aula_id) REFERENCES ai.aula (id)
    );

    CREATE INDEX IX_horario_clase_grupo_dia_vigencia
        ON ai.horario_clase (grupo_id, dia_semana, vigente_desde, vigente_hasta)
        INCLUDE (hora_inicio, hora_fin, asignatura_id, docente_id, aula_id, anulado);

    CREATE INDEX IX_horario_clase_docente_dia_vigencia
        ON ai.horario_clase (docente_id, dia_semana, vigente_desde, vigente_hasta)
        INCLUDE (hora_inicio, hora_fin, grupo_id, aula_id, anulado);

    CREATE INDEX IX_horario_clase_aula_dia_vigencia
        ON ai.horario_clase (aula_id, dia_semana, vigente_desde, vigente_hasta)
        INCLUDE (hora_inicio, hora_fin, grupo_id, docente_id, anulado);

    CREATE TABLE ai.jornada
    (
        id                  bigint IDENTITY(1,1) NOT NULL,
        fecha_local         date NOT NULL,
        zona_horaria        nvarchar(64) NOT NULL
            CONSTRAINT DF_jornada_zona_horaria DEFAULT (N'America/El_Salvador'),
        inicio_utc          datetime2(3) NOT NULL,
        fin_utc             datetime2(3) NOT NULL,
        limite_puntual_utc  datetime2(3) NOT NULL,
        estado              varchar(15) NOT NULL,
        motivo_excepcion    nvarchar(300) NULL,
        version             rowversion NOT NULL,

        CONSTRAINT PK_jornada PRIMARY KEY CLUSTERED (id),
        CONSTRAINT UQ_jornada_fecha_local UNIQUE (fecha_local),
        CONSTRAINT CK_jornada_zona_horaria
            CHECK (LEN(LTRIM(RTRIM(zona_horaria))) > 0),
        CONSTRAINT CK_jornada_intervalo
            CHECK (fin_utc > inicio_utc),
        CONSTRAINT CK_jornada_limite_puntual
            CHECK (limite_puntual_utc >= inicio_utc
               AND limite_puntual_utc <= fin_utc),
        CONSTRAINT CK_jornada_estado
            CHECK (estado IN ('PLANIFICADA', 'ABIERTA', 'CERRADA', 'CANCELADA')),
        CONSTRAINT CK_jornada_motivo_excepcion
            CHECK (motivo_excepcion IS NULL
                OR LEN(LTRIM(RTRIM(motivo_excepcion))) > 0)
    );

    CREATE TABLE ai.concesion
    (
        id                  bigint IDENTITY(1,1) NOT NULL,
        usuario_id          uniqueidentifier NOT NULL,
        permiso_codigo      varchar(60) NOT NULL,
        ambito              varchar(15) NOT NULL,
        grupo_id            bigint NULL,
        desde_utc           datetime2(3) NOT NULL,
        hasta_utc           datetime2(3) NULL,
        habilitada          bit NOT NULL
            CONSTRAINT DF_concesion_habilitada DEFAULT (1),
        version             rowversion NOT NULL,

        CONSTRAINT PK_concesion PRIMARY KEY CLUSTERED (id),
        CONSTRAINT CK_concesion_ambito
            CHECK (ambito IN ('PROPIO', 'GRUPO', 'INSTITUCION')),
        CONSTRAINT CK_concesion_grupo
            CHECK ((ambito = 'GRUPO' AND grupo_id IS NOT NULL)
                OR (ambito IN ('PROPIO', 'INSTITUCION') AND grupo_id IS NULL)),
        CONSTRAINT CK_concesion_vigencia
            CHECK (hasta_utc IS NULL OR hasta_utc > desde_utc),
        CONSTRAINT FK_concesion_permiso
            FOREIGN KEY (permiso_codigo) REFERENCES ai.permiso (codigo),
        CONSTRAINT FK_concesion_grupo
            FOREIGN KEY (grupo_id) REFERENCES ai.grupo (id)
        /* FK usuario_id -> dbo.AspNetUsers.Id: archivo 003. */
    );

    CREATE UNIQUE INDEX UX_concesion_activa_sin_grupo
        ON ai.concesion (usuario_id, permiso_codigo, ambito)
        WHERE habilitada = 1 AND grupo_id IS NULL;

    CREATE UNIQUE INDEX UX_concesion_activa_grupo
        ON ai.concesion (usuario_id, permiso_codigo, ambito, grupo_id)
        WHERE habilitada = 1 AND grupo_id IS NOT NULL;

    CREATE INDEX IX_concesion_grupo_vigencia
        ON ai.concesion (grupo_id, desde_utc, hasta_utc)
        INCLUDE (usuario_id, permiso_codigo, ambito, habilitada)
        WHERE grupo_id IS NOT NULL;

    /* ================================================================
       3. Captura NFC y configuración de estaciones
       ================================================================ */

    CREATE TABLE ai.credencial_nfc
    (
        id              bigint IDENTITY(1,1) NOT NULL,
        referencia      varchar(64) NOT NULL,
        persona_id      bigint NOT NULL,
        desde_utc       datetime2(3) NOT NULL,
        hasta_utc       datetime2(3) NULL,
        activa          bit NOT NULL
            CONSTRAINT DF_credencial_nfc_activa DEFAULT (1),
        motivo_baja     nvarchar(200) NULL,
        creado_utc      datetime2(3) NOT NULL
            CONSTRAINT DF_credencial_nfc_creado_utc DEFAULT (SYSUTCDATETIME()),
        version         rowversion NOT NULL,

        CONSTRAINT PK_credencial_nfc PRIMARY KEY CLUSTERED (id),
        CONSTRAINT UQ_credencial_nfc_id_persona UNIQUE (id, persona_id),
        CONSTRAINT CK_credencial_nfc_referencia
            CHECK (LEN(LTRIM(RTRIM(referencia))) > 0
               AND referencia COLLATE Latin1_General_100_BIN2
                   = UPPER(referencia) COLLATE Latin1_General_100_BIN2
               AND referencia COLLATE Latin1_General_100_BIN2
                   NOT LIKE '%[^0-9A-F]%'),
        CONSTRAINT CK_credencial_nfc_estado
            CHECK ((activa = 1 AND hasta_utc IS NULL AND motivo_baja IS NULL)
                OR (activa = 0 AND hasta_utc IS NOT NULL
                    AND hasta_utc >= desde_utc
                    AND motivo_baja IS NOT NULL
                    AND LEN(LTRIM(RTRIM(motivo_baja))) > 0)),
        CONSTRAINT FK_credencial_nfc_persona
            FOREIGN KEY (persona_id) REFERENCES ai.persona (id)
    );

    CREATE UNIQUE INDEX UX_credencial_nfc_referencia_activa
        ON ai.credencial_nfc (referencia)
        WHERE activa = 1;

    CREATE INDEX IX_credencial_nfc_persona_vigencia
        ON ai.credencial_nfc (persona_id, desde_utc, hasta_utc)
        INCLUDE (referencia, activa);

    CREATE TABLE ai.estacion
    (
        id                      bigint IDENTITY(1,1) NOT NULL,
        codigo_logico           varchar(40) NOT NULL,
        habilitada              bit NOT NULL
            CONSTRAINT DF_estacion_habilitada DEFAULT (1),
        mac_inventario          varchar(17) NULL,
        ultima_comunicacion_utc datetime2(3) NULL,
        modo_reportado          varchar(15) NULL,
        modo_reportado_utc      datetime2(3) NULL,
        creado_utc              datetime2(3) NOT NULL
            CONSTRAINT DF_estacion_creado_utc DEFAULT (SYSUTCDATETIME()),
        version                 rowversion NOT NULL,

        CONSTRAINT PK_estacion PRIMARY KEY CLUSTERED (id),
        CONSTRAINT UQ_estacion_codigo_logico UNIQUE (codigo_logico),
        CONSTRAINT CK_estacion_codigo_logico
            CHECK (LEN(LTRIM(RTRIM(codigo_logico))) > 0),
        CONSTRAINT CK_estacion_modo_reportado
            CHECK (modo_reportado IS NULL
                OR modo_reportado IN ('ASISTENCIA', 'MOVIMIENTOS')),
        CONSTRAINT CK_estacion_modo_hora
            CHECK ((modo_reportado IS NULL AND modo_reportado_utc IS NULL)
                OR (modo_reportado IS NOT NULL AND modo_reportado_utc IS NOT NULL))
    );

    CREATE UNIQUE INDEX UX_estacion_mac_inventario
        ON ai.estacion (mac_inventario)
        WHERE mac_inventario IS NOT NULL;

    CREATE INDEX IX_estacion_ultima_comunicacion
        ON ai.estacion (ultima_comunicacion_utc)
        INCLUDE (codigo_logico, habilitada, modo_reportado, modo_reportado_utc);

    CREATE TABLE ai.estacion_configuracion
    (
        id                      bigint IDENTITY(1,1) NOT NULL,
        estacion_id             bigint NOT NULL,
        tipo_punto              varchar(12) NOT NULL,
        aula_id                 bigint NULL,
        etiqueta_punto          nvarchar(100) NOT NULL,
        expiracion_selector_s   smallint NOT NULL
            CONSTRAINT DF_estacion_config_expiracion DEFAULT (15),
        vigente_desde_utc       datetime2(3) NOT NULL,
        vigente_hasta_utc       datetime2(3) NULL,
        configurada_por         uniqueidentifier NOT NULL,
        motivo_cambio           nvarchar(500) NOT NULL,
        creado_utc              datetime2(3) NOT NULL
            CONSTRAINT DF_estacion_config_creado_utc DEFAULT (SYSUTCDATETIME()),
        version                 rowversion NOT NULL,

        CONSTRAINT PK_estacion_configuracion PRIMARY KEY CLUSTERED (id),
        CONSTRAINT UQ_estacion_configuracion_id_estacion
            UNIQUE (id, estacion_id),
        CONSTRAINT CK_estacion_configuracion_tipo_punto
            CHECK (tipo_punto IN ('AULA', 'ACCESO')),
        CONSTRAINT CK_estacion_configuracion_aula
            CHECK ((tipo_punto = 'AULA' AND aula_id IS NOT NULL)
                OR (tipo_punto = 'ACCESO' AND aula_id IS NULL)),
        CONSTRAINT CK_estacion_configuracion_etiqueta
            CHECK (LEN(LTRIM(RTRIM(etiqueta_punto))) > 0),
        CONSTRAINT CK_estacion_configuracion_expiracion
            CHECK (expiracion_selector_s BETWEEN 3 AND 60),
        CONSTRAINT CK_estacion_configuracion_vigencia
            CHECK (vigente_hasta_utc IS NULL
                OR vigente_hasta_utc > vigente_desde_utc),
        CONSTRAINT CK_estacion_configuracion_motivo
            CHECK (LEN(LTRIM(RTRIM(motivo_cambio))) > 0),
        CONSTRAINT FK_estacion_configuracion_estacion
            FOREIGN KEY (estacion_id) REFERENCES ai.estacion (id),
        CONSTRAINT FK_estacion_configuracion_aula
            FOREIGN KEY (aula_id) REFERENCES ai.aula (id)
        /* FK configurada_por -> dbo.AspNetUsers.Id: archivo 003. */
    );

    CREATE UNIQUE INDEX UX_estacion_configuracion_vigente
        ON ai.estacion_configuracion (estacion_id)
        WHERE vigente_hasta_utc IS NULL;

    CREATE INDEX IX_estacion_configuracion_vigencia
        ON ai.estacion_configuracion
            (estacion_id, vigente_desde_utc, vigente_hasta_utc)
        INCLUDE (tipo_punto, aula_id, etiqueta_punto, expiracion_selector_s);

    CREATE TABLE ai.estacion_secreto
    (
        id                      bigint IDENTITY(1,1) NOT NULL,
        estacion_id             bigint NOT NULL,
        identificador_clave     varchar(50) NOT NULL,
        hash_secreto            varbinary(32) NOT NULL,
        desde_utc               datetime2(3) NOT NULL,
        revocada_utc            datetime2(3) NULL,

        CONSTRAINT PK_estacion_secreto PRIMARY KEY CLUSTERED (id),
        CONSTRAINT UQ_estacion_secreto_identificador UNIQUE (identificador_clave),
        CONSTRAINT CK_estacion_secreto_identificador
            CHECK (LEN(LTRIM(RTRIM(identificador_clave))) > 0),
        CONSTRAINT CK_estacion_secreto_vigencia
            CHECK (revocada_utc IS NULL OR revocada_utc >= desde_utc),
        CONSTRAINT FK_estacion_secreto_estacion
            FOREIGN KEY (estacion_id) REFERENCES ai.estacion (id)
    );

    CREATE INDEX IX_estacion_secreto_estacion_vigencia
        ON ai.estacion_secreto (estacion_id, revocada_utc, desde_utc)
        INCLUDE (identificador_clave);

    CREATE TABLE ai.estacion_operacion
    (
        estacion_configuracion_id bigint NOT NULL,
        accion                    varchar(35) NOT NULL,
        modo_interfaz             varchar(15) NOT NULL,

        CONSTRAINT PK_estacion_operacion
            PRIMARY KEY CLUSTERED (estacion_configuracion_id, accion),
        CONSTRAINT CK_estacion_operacion_accion
            CHECK (accion IN
                ('ASISTENCIA', 'SALIDA_AULA', 'REGRESO_AULA',
                 'LLEGADA_PERSONAL', 'SALIDA_PERSONAL',
                 'INGRESO_CAMPUS', 'SALIDA_CAMPUS')),
        CONSTRAINT CK_estacion_operacion_modo
            CHECK (modo_interfaz IN ('ASISTENCIA', 'MOVIMIENTOS')),
        CONSTRAINT CK_estacion_operacion_coherencia
            CHECK ((accion = 'ASISTENCIA' AND modo_interfaz = 'ASISTENCIA')
                OR (accion <> 'ASISTENCIA' AND modo_interfaz = 'MOVIMIENTOS')),
        CONSTRAINT FK_estacion_operacion_configuracion
            FOREIGN KEY (estacion_configuracion_id)
            REFERENCES ai.estacion_configuracion (id)
    );

    CREATE TABLE ai.motivo_salida
    (
        codigo          varchar(25) NOT NULL,
        nombre          nvarchar(80) NOT NULL,
        habilitado      bit NOT NULL
            CONSTRAINT DF_motivo_salida_habilitado DEFAULT (1),

        CONSTRAINT PK_motivo_salida PRIMARY KEY CLUSTERED (codigo),
        CONSTRAINT CK_motivo_salida_codigo
            CHECK (LEN(LTRIM(RTRIM(codigo))) > 0),
        CONSTRAINT CK_motivo_salida_nombre
            CHECK (LEN(LTRIM(RTRIM(nombre))) > 0)
    );

    /* ================================================================
       4. Eventos, asistencia y auditoría
       ================================================================ */

    CREATE TABLE ai.evento
    (
        id                          uniqueidentifier NOT NULL,
        hash_solicitud              varbinary(32) NOT NULL,
        origen                      varchar(15) NOT NULL,
        accion                      varchar(35) NOT NULL,
        persona_id                  bigint NULL,
        credencial_id               bigint NULL,
        estacion_id                 bigint NULL,
        estacion_configuracion_id   bigint NULL,
        operador_id                 uniqueidentifier NULL,
        aula_id                     bigint NULL,
        jornada_id                  bigint NULL,
        matricula_id                bigint NULL,
        asignacion_id               bigint NULL,
        punto_manual                nvarchar(100) NULL,
        motivo_codigo               varchar(25) NULL,
        motivo_manual               nvarchar(300) NULL,
        ocurrido_utc                datetime2(3) NULL,
        recibido_utc                datetime2(3) NOT NULL
            CONSTRAINT DF_evento_recibido_utc DEFAULT (SYSUTCDATETIME()),
        calidad_hora                varchar(15) NOT NULL,
        estado                      varchar(15) NOT NULL,
        codigo_resultado            varchar(50) NOT NULL,
        evento_canonico_id          uniqueidentifier NULL,
        seleccion_id                uniqueidentifier NULL,
        seleccion_edad_ms           int NULL,

        CONSTRAINT PK_evento PRIMARY KEY NONCLUSTERED (id),
        CONSTRAINT CK_evento_origen
            CHECK (origen IN ('ESTACION', 'NFC_MOVIL', 'MANUAL')),
        CONSTRAINT CK_evento_accion
            CHECK (accion IN
                ('ASISTENCIA', 'SALIDA_AULA', 'REGRESO_AULA',
                 'LLEGADA_PERSONAL', 'SALIDA_PERSONAL',
                 'INGRESO_CAMPUS', 'SALIDA_CAMPUS',
                 'REGISTRAR_INCIDENCIA', 'AMPLIAR_OBSERVACION')),
        CONSTRAINT CK_evento_calidad_hora
            CHECK (calidad_hora IN ('FIABLE', 'DECLARADA', 'NO_FIABLE')),
        CONSTRAINT CK_evento_ocurrido
            CHECK (calidad_hora = 'NO_FIABLE' OR ocurrido_utc IS NOT NULL),
        CONSTRAINT CK_evento_estado
            CHECK (estado IN ('ACEPTADO', 'REPETIDO', 'RECHAZADO', 'POR_REVISAR')),
        CONSTRAINT CK_evento_codigo_resultado
            CHECK (LEN(LTRIM(RTRIM(codigo_resultado))) > 0),
        CONSTRAINT CK_evento_evento_canonico
            CHECK ((estado = 'REPETIDO' AND evento_canonico_id IS NOT NULL
                    AND evento_canonico_id <> id)
                OR (estado <> 'REPETIDO' AND evento_canonico_id IS NULL)),
        CONSTRAINT CK_evento_origen_contexto
            CHECK ((origen = 'ESTACION'
                    AND estacion_id IS NOT NULL
                    AND estacion_configuracion_id IS NOT NULL
                    AND operador_id IS NULL
                    AND motivo_manual IS NULL
                    AND punto_manual IS NULL)
                OR (origen = 'NFC_MOVIL'
                    AND estacion_id IS NULL
                    AND estacion_configuracion_id IS NULL
                    AND operador_id IS NOT NULL
                    AND motivo_manual IS NULL
                    AND punto_manual IS NULL)
                OR (origen = 'MANUAL'
                    AND estacion_id IS NULL
                    AND estacion_configuracion_id IS NULL
                    AND operador_id IS NOT NULL
                    AND credencial_id IS NULL
                    AND motivo_manual IS NOT NULL
                    AND LEN(LTRIM(RTRIM(motivo_manual))) > 0)),
        CONSTRAINT CK_evento_seleccion_estacion
            CHECK ((origen = 'ESTACION' AND accion = 'ASISTENCIA'
                    AND seleccion_id IS NULL AND seleccion_edad_ms IS NULL)
                OR (origen = 'ESTACION' AND accion <> 'ASISTENCIA'
                    AND seleccion_id IS NOT NULL AND seleccion_edad_ms IS NOT NULL)
                OR (origen <> 'ESTACION'
                    AND seleccion_id IS NULL AND seleccion_edad_ms IS NULL)),
        CONSTRAINT CK_evento_seleccion_edad
            CHECK (seleccion_edad_ms IS NULL OR seleccion_edad_ms >= 0),
        CONSTRAINT CK_evento_motivo_salida
            CHECK ((accion = 'SALIDA_AULA' AND motivo_codigo IS NOT NULL)
                OR (accion <> 'SALIDA_AULA' AND motivo_codigo IS NULL)),
        CONSTRAINT CK_evento_operaciones_humanas
            CHECK (accion NOT IN ('REGISTRAR_INCIDENCIA', 'AMPLIAR_OBSERVACION')
                OR (origen IN ('NFC_MOVIL', 'MANUAL') AND operador_id IS NOT NULL)),
        CONSTRAINT CK_evento_credencial_persona
            CHECK (credencial_id IS NULL OR persona_id IS NOT NULL),
        CONSTRAINT CK_evento_matricula_persona
            CHECK (matricula_id IS NULL OR persona_id IS NOT NULL),
        CONSTRAINT FK_evento_persona
            FOREIGN KEY (persona_id) REFERENCES ai.persona (id),
        CONSTRAINT FK_evento_credencial_persona
            FOREIGN KEY (credencial_id, persona_id)
            REFERENCES ai.credencial_nfc (id, persona_id),
        CONSTRAINT FK_evento_estacion
            FOREIGN KEY (estacion_id) REFERENCES ai.estacion (id),
        CONSTRAINT FK_evento_configuracion_estacion
            FOREIGN KEY (estacion_configuracion_id, estacion_id)
            REFERENCES ai.estacion_configuracion (id, estacion_id),
        CONSTRAINT FK_evento_configuracion_operacion
            FOREIGN KEY (estacion_configuracion_id, accion)
            REFERENCES ai.estacion_operacion (estacion_configuracion_id, accion),
        CONSTRAINT FK_evento_aula
            FOREIGN KEY (aula_id) REFERENCES ai.aula (id),
        CONSTRAINT FK_evento_jornada
            FOREIGN KEY (jornada_id) REFERENCES ai.jornada (id),
        CONSTRAINT FK_evento_matricula_persona
            FOREIGN KEY (matricula_id, persona_id)
            REFERENCES ai.matricula (id, estudiante_id),
        CONSTRAINT FK_evento_motivo_salida
            FOREIGN KEY (motivo_codigo) REFERENCES ai.motivo_salida (codigo),
        CONSTRAINT FK_evento_canonico
            FOREIGN KEY (evento_canonico_id) REFERENCES ai.evento (id)
        /* FK operador_id -> dbo.AspNetUsers.Id: archivo 003.
           FK asignacion_id -> ai.asignacion.id: Bloque C. */
    );

    CREATE UNIQUE CLUSTERED INDEX CIX_evento_recibido_id
        ON ai.evento (recibido_utc, id);

    CREATE UNIQUE INDEX UX_evento_estacion_seleccion
        ON ai.evento (estacion_id, seleccion_id)
        WHERE estacion_id IS NOT NULL AND seleccion_id IS NOT NULL;

    CREATE INDEX IX_evento_persona_ocurrido
        ON ai.evento (persona_id, ocurrido_utc)
        INCLUDE (accion, estado, jornada_id, aula_id, recibido_utc)
        WHERE persona_id IS NOT NULL;

    CREATE INDEX IX_evento_estacion_recibido
        ON ai.evento (estacion_id, recibido_utc)
        INCLUDE (accion, estado, persona_id, estacion_configuracion_id)
        WHERE estacion_id IS NOT NULL;

    CREATE INDEX IX_evento_estado_recibido
        ON ai.evento (estado, recibido_utc)
        INCLUDE (accion, persona_id, estacion_id, codigo_resultado);

    CREATE INDEX IX_evento_evento_canonico
        ON ai.evento (evento_canonico_id)
        WHERE evento_canonico_id IS NOT NULL;

    CREATE TABLE ai.asistencia
    (
        id                  bigint IDENTITY(1,1) NOT NULL,
        estudiante_id       bigint NOT NULL,
        jornada_id          bigint NOT NULL,
        matricula_id        bigint NOT NULL,
        evento_llegada_id   uniqueidentifier NULL,
        llegada_utc         datetime2(3) NULL,
        calidad_hora        varchar(15) NOT NULL,
        estado              varchar(20) NOT NULL,
        puntualidad         varchar(12) NOT NULL,
        justificacion       nvarchar(1000) NULL,
        revisado_por        uniqueidentifier NULL,
        revisado_utc        datetime2(3) NULL,
        creado_utc          datetime2(3) NOT NULL
            CONSTRAINT DF_asistencia_creado_utc DEFAULT (SYSUTCDATETIME()),
        version             rowversion NOT NULL,

        CONSTRAINT PK_asistencia PRIMARY KEY CLUSTERED (id),
        CONSTRAINT UQ_asistencia_estudiante_jornada
            UNIQUE (estudiante_id, jornada_id),
        CONSTRAINT CK_asistencia_calidad_hora
            CHECK (calidad_hora IN ('FIABLE', 'DECLARADA', 'NO_FIABLE')),
        CONSTRAINT CK_asistencia_estado
            CHECK (estado IN ('PENDIENTE', 'PRESENTE', 'AUSENCIA_VERIFICADA')),
        CONSTRAINT CK_asistencia_puntualidad
            CHECK (puntualidad IN ('PENDIENTE', 'PUNTUAL', 'TARDIA', 'NO_APLICA')),
        CONSTRAINT CK_asistencia_revision
            CHECK ((revisado_por IS NULL AND revisado_utc IS NULL)
                OR (revisado_por IS NOT NULL AND revisado_utc IS NOT NULL)),
        CONSTRAINT CK_asistencia_llegada_calidad
            CHECK ((llegada_utc IS NULL AND calidad_hora = 'NO_FIABLE')
                OR (llegada_utc IS NOT NULL
                    AND calidad_hora IN ('FIABLE', 'DECLARADA'))),
        CONSTRAINT CK_asistencia_estado_datos
            CHECK ((estado = 'PENDIENTE')
                OR (estado = 'PRESENTE'
                    AND evento_llegada_id IS NOT NULL
                    AND llegada_utc IS NOT NULL)
                OR (estado = 'AUSENCIA_VERIFICADA'
                    AND evento_llegada_id IS NULL
                    AND llegada_utc IS NULL
                    AND revisado_por IS NOT NULL
                    AND revisado_utc IS NOT NULL
                    AND puntualidad = 'NO_APLICA')),
        CONSTRAINT CK_asistencia_clasificacion
            CHECK (puntualidad NOT IN ('PUNTUAL', 'TARDIA')
                OR (llegada_utc IS NOT NULL
                    AND (calidad_hora = 'FIABLE'
                        OR (calidad_hora = 'DECLARADA'
                            AND revisado_por IS NOT NULL
                            AND revisado_utc IS NOT NULL)))),
        CONSTRAINT CK_asistencia_justificacion
            CHECK (justificacion IS NULL
                OR LEN(LTRIM(RTRIM(justificacion))) > 0),
        CONSTRAINT FK_asistencia_estudiante
            FOREIGN KEY (estudiante_id) REFERENCES ai.estudiante (persona_id),
        CONSTRAINT FK_asistencia_jornada
            FOREIGN KEY (jornada_id) REFERENCES ai.jornada (id),
        CONSTRAINT FK_asistencia_matricula_estudiante
            FOREIGN KEY (matricula_id, estudiante_id)
            REFERENCES ai.matricula (id, estudiante_id),
        CONSTRAINT FK_asistencia_evento_llegada
            FOREIGN KEY (evento_llegada_id) REFERENCES ai.evento (id)
        /* FK revisado_por -> dbo.AspNetUsers.Id: archivo 003. */
    );

    CREATE INDEX IX_asistencia_jornada_estado
        ON ai.asistencia (jornada_id, estado)
        INCLUDE (estudiante_id, llegada_utc, puntualidad, calidad_hora);

    CREATE INDEX IX_asistencia_evento_llegada
        ON ai.asistencia (evento_llegada_id)
        WHERE evento_llegada_id IS NOT NULL;

    CREATE TABLE ai.auditoria
    (
        id                  bigint IDENTITY(1,1) NOT NULL,
        operador_id         uniqueidentifier NOT NULL,
        entidad             varchar(50) NOT NULL,
        registro_id         varchar(50) NOT NULL,
        operacion           varchar(20) NOT NULL,
        motivo              nvarchar(500) NOT NULL,
        antes_json          nvarchar(max) NULL,
        despues_json        nvarchar(max) NULL,
        evento_id           uniqueidentifier NULL,
        creado_utc          datetime2(3) NOT NULL
            CONSTRAINT DF_auditoria_creado_utc DEFAULT (SYSUTCDATETIME()),
        solicitud_id        uniqueidentifier NOT NULL,
        hash_solicitud      varbinary(32) NOT NULL,

        CONSTRAINT PK_auditoria PRIMARY KEY CLUSTERED (id),
        CONSTRAINT UQ_auditoria_solicitud_entidad_registro
            UNIQUE (solicitud_id, entidad, registro_id),
        CONSTRAINT CK_auditoria_entidad
            CHECK (entidad IN
                ('persona', 'estudiante', 'personal', 'permiso', 'concesion',
                 'aula', 'grupo', 'matricula', 'asignatura', 'horario_clase',
                 'jornada', 'credencial_nfc', 'estacion',
                 'estacion_configuracion', 'estacion_secreto',
                 'estacion_operacion', 'motivo_salida', 'evento', 'asistencia',
                 'salida_temporal', 'categoria_incidencia', 'tipo_incidencia',
                 'incidencia', 'observacion_incidencia', 'asignacion',
                 'necesidad_cobertura', 'verificacion_cobertura',
                 'falla_estacion', 'contexto_clase', 'zona_visual',
                 'indicador_visual', 'sesion_visual', 'medicion_visual')),
        CONSTRAINT CK_auditoria_registro_id
            CHECK (LEN(LTRIM(RTRIM(registro_id))) > 0),
        CONSTRAINT CK_auditoria_operacion
            CHECK (operacion IN
                ('CREAR', 'CORREGIR', 'ANULAR', 'JUSTIFICAR', 'PUBLICAR',
                 'ACTIVAR', 'REVOCAR', 'CERRAR', 'REABRIR')),
        CONSTRAINT CK_auditoria_motivo
            CHECK (LEN(LTRIM(RTRIM(motivo))) > 0),
        CONSTRAINT CK_auditoria_antes_json
            CHECK (antes_json IS NULL OR ISJSON(antes_json) = 1),
        CONSTRAINT CK_auditoria_despues_json
            CHECK (despues_json IS NULL OR ISJSON(despues_json) = 1),
        CONSTRAINT FK_auditoria_evento
            FOREIGN KEY (evento_id) REFERENCES ai.evento (id)
        /* FK operador_id -> dbo.AspNetUsers.Id: archivo 003. */
    );

    CREATE INDEX IX_auditoria_entidad_registro_fecha
        ON ai.auditoria (entidad, registro_id, creado_utc DESC)
        INCLUDE (operador_id, operacion, solicitud_id);

    CREATE INDEX IX_auditoria_operador_fecha
        ON ai.auditoria (operador_id, creado_utc DESC)
        INCLUDE (entidad, registro_id, operacion);

    /* ================================================================
       5. Catálogos iniciales
       ================================================================ */

    INSERT ai.permiso (codigo, descripcion)
    VALUES
        ('CUENTAS_ADMIN',              N'Administrar cuentas y su vinculación con personas.'),
        ('PERSONAS_ADMIN',             N'Administrar personas y perfiles institucionales.'),
        ('NFC_ADMIN',                  N'Asignar, revocar y reemplazar credenciales NFC.'),
        ('ESTACIONES_ADMIN',           N'Administrar estaciones, configuración, operaciones y secretos.'),
        ('HORARIOS_ADMIN',             N'Administrar grupos, matrículas, asignaturas, jornadas y horarios.'),
        ('ASISTENCIA_LEER',            N'Consultar asistencia dentro del ámbito concedido.'),
        ('ASISTENCIA_REGISTRAR',       N'Registrar asistencia manual o móvil autorizada.'),
        ('ASISTENCIA_CORREGIR',        N'Corregir o verificar asistencia con auditoría.'),
        ('MOVIMIENTOS_LEER',           N'Consultar movimientos dentro del ámbito concedido.'),
        ('MOVIMIENTOS_REGISTRAR',      N'Registrar salidas, regresos y otros movimientos.'),
        ('SALIDA_CAMPUS_CONFIRMAR',    N'Confirmar una salida de campus autorizada.'),
        ('CONDUCTA_LEER',              N'Consultar incidencias dentro del ámbito concedido.'),
        ('CONDUCTA_REGISTRAR',         N'Registrar incidencias y observaciones.'),
        ('CONDUCTA_CORREGIR',          N'Corregir o anular incidencias con auditoría.'),
        ('CONDUCTA_PUBLICAR',          N'Publicar observaciones autorizadas.'),
        ('CATEGORIAS_ADMIN',           N'Administrar categorías y tipos conductuales.'),
        ('COBERTURA_LEER',             N'Consultar asignaciones, cuidos y coberturas.'),
        ('COBERTURA_VERIFICAR',        N'Verificar presencia y cobertura humana.'),
        ('REPORTES_LEER',              N'Consultar reportes autorizados.'),
        ('CONTEXTO_EDITAR',            N'Registrar o corregir contexto de clase.'),
        ('VISION_LEER',                N'Consultar únicamente indicadores visuales agregados.'),
        ('VISION_CONFIGURAR',          N'Configurar sesiones, zonas e indicadores visuales.'),
        ('AUDITORIA_LEER',             N'Consultar el historial de auditoría autorizado.');

    INSERT ai.motivo_salida (codigo, nombre)
    VALUES
        ('BANO',           N'Baño'),
        ('CLINICA',        N'Clínica'),
        ('EMERGENCIA',     N'Emergencia'),
        ('ADMINISTRATIVA', N'Gestión administrativa'),
        ('OTRO',           N'Otro');

    /* ================================================================
       6. Vista de estado diario
       No materializa ausencias: si no existe asistencia, muestra PENDIENTE.
       ================================================================ */

    EXEC(N'
        CREATE VIEW ai.vw_estado_asistencia_diaria
        AS
        SELECT
            j.id AS jornada_id,
            j.fecha_local,
            j.estado AS estado_jornada,
            m.grupo_id,
            g.ciclo,
            g.grado,
            g.seccion,
            m.estudiante_id,
            p.nombres,
            p.apellidos,
            COALESCE(a.estado, ''PENDIENTE'') AS estado_asistencia,
            COALESCE(a.puntualidad, ''PENDIENTE'') AS puntualidad,
            a.llegada_utc,
            a.calidad_hora,
            a.evento_llegada_id,
            a.revisado_por,
            a.revisado_utc
        FROM ai.jornada AS j
        INNER JOIN ai.matricula AS m
            ON m.desde <= j.fecha_local
           AND (m.hasta IS NULL OR m.hasta > j.fecha_local)
           AND m.anulada = 0
        INNER JOIN ai.grupo AS g
            ON g.id = m.grupo_id
        INNER JOIN ai.estudiante AS e
            ON e.persona_id = m.estudiante_id
        INNER JOIN ai.persona AS p
            ON p.id = e.persona_id
        LEFT JOIN ai.asistencia AS a
            ON a.jornada_id = j.id
           AND a.estudiante_id = m.estudiante_id;
    ');

    /* ================================================================
       7. Primer trigger de auditoría sensible (1 de 3)

       Antes de UPDATE, la API debe establecer dentro de la transacción:
         ai.operador_id     uniqueidentifier
         ai.motivo          nvarchar(500)
         ai.solicitud_id    uniqueidentifier
         ai.hash_solicitud  varbinary(32)
         ai.operacion       CORREGIR o JUSTIFICAR

       DELETE físico se rechaza. El pool debe limpiar estas claves al terminar.
       ================================================================ */

    EXEC(N'
        CREATE TRIGGER ai.tr_asistencia_auditoria
        ON ai.asistencia
        AFTER UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS (SELECT 1 FROM deleted)
               AND NOT EXISTS (SELECT 1 FROM inserted)
            BEGIN
                THROW 51020,
                    ''DELETE físico de asistencia no permitido; use corrección autorizada.'',
                    1;
            END;

            IF NOT EXISTS
            (
                SELECT
                    i.id, i.estudiante_id, i.jornada_id, i.matricula_id,
                    i.evento_llegada_id, i.llegada_utc, i.calidad_hora,
                    i.estado, i.puntualidad, i.justificacion,
                    i.revisado_por, i.revisado_utc
                FROM inserted AS i
                INNER JOIN deleted AS d ON d.id = i.id
                EXCEPT
                SELECT
                    d.id, d.estudiante_id, d.jornada_id, d.matricula_id,
                    d.evento_llegada_id, d.llegada_utc, d.calidad_hora,
                    d.estado, d.puntualidad, d.justificacion,
                    d.revisado_por, d.revisado_utc
                FROM inserted AS i
                INNER JOIN deleted AS d ON d.id = i.id
            )
                RETURN;

            DECLARE @operador_id uniqueidentifier =
                TRY_CONVERT(uniqueidentifier, SESSION_CONTEXT(N''ai.operador_id''));
            DECLARE @motivo nvarchar(500) =
                TRY_CONVERT(nvarchar(500), SESSION_CONTEXT(N''ai.motivo''));
            DECLARE @solicitud_id uniqueidentifier =
                TRY_CONVERT(uniqueidentifier, SESSION_CONTEXT(N''ai.solicitud_id''));
            DECLARE @hash_solicitud varbinary(32) =
                TRY_CONVERT(varbinary(32), SESSION_CONTEXT(N''ai.hash_solicitud''));
            DECLARE @operacion varchar(20) =
                TRY_CONVERT(varchar(20), SESSION_CONTEXT(N''ai.operacion''));

            IF @operador_id IS NULL
               OR @solicitud_id IS NULL
               OR @hash_solicitud IS NULL
               OR DATALENGTH(@hash_solicitud) <> 32
               OR @motivo IS NULL
               OR LEN(LTRIM(RTRIM(@motivo))) = 0
               OR @operacion IS NULL
               OR @operacion NOT IN (''CORREGIR'', ''JUSTIFICAR'')
            BEGIN
                THROW 51021,
                    ''Falta contexto de auditoría válido para modificar asistencia.'',
                    1;
            END;

            INSERT ai.auditoria
            (
                operador_id, entidad, registro_id, operacion, motivo,
                antes_json, despues_json, evento_id, creado_utc,
                solicitud_id, hash_solicitud
            )
            SELECT
                @operador_id,
                ''asistencia'',
                CONVERT(varchar(50), i.id),
                @operacion,
                @motivo,
                (
                    SELECT
                        d.estudiante_id,
                        d.jornada_id,
                        d.matricula_id,
                        d.evento_llegada_id,
                        d.llegada_utc,
                        d.calidad_hora,
                        d.estado,
                        d.puntualidad,
                        d.justificacion,
                        d.revisado_por,
                        d.revisado_utc
                    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
                ),
                (
                    SELECT
                        i.estudiante_id,
                        i.jornada_id,
                        i.matricula_id,
                        i.evento_llegada_id,
                        i.llegada_utc,
                        i.calidad_hora,
                        i.estado,
                        i.puntualidad,
                        i.justificacion,
                        i.revisado_por,
                        i.revisado_utc
                    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
                ),
                i.evento_llegada_id,
                SYSUTCDATETIME(),
                @solicitud_id,
                @hash_solicitud
            FROM inserted AS i
            INNER JOIN deleted AS d ON d.id = i.id
            WHERE EXISTS
            (
                SELECT
                    i.estudiante_id, i.jornada_id, i.matricula_id,
                    i.evento_llegada_id, i.llegada_utc, i.calidad_hora,
                    i.estado, i.puntualidad, i.justificacion,
                    i.revisado_por, i.revisado_utc
                EXCEPT
                SELECT
                    d.estudiante_id, d.jornada_id, d.matricula_id,
                    d.evento_llegada_id, d.llegada_utc, d.calidad_hora,
                    d.estado, d.puntualidad, d.justificacion,
                    d.revisado_por, d.revisado_utc
            );
        END;
    ');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

PRINT N'Bloque A instalado correctamente.';
PRINT N'Pendiente: ejecutar 003_bloque_a_integracion_identity.sql después de la migración de Identity.';
PRINT N'Pendiente de Bloque C: FK ai.evento.asignacion_id -> ai.asignacion.id.';
GO

SELECT
    s.name AS esquema,
    COUNT(*) AS tablas_bloque_a
FROM sys.tables AS t
INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
WHERE s.name = N'ai'
GROUP BY s.name;

SELECT codigo, nombre, habilitado
FROM ai.motivo_salida
ORDER BY codigo;
GO
