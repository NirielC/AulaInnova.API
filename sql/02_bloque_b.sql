/*
    Aula Innova - Bloque B completo
    SQL Server 2022

    EJECUCION:
      1. Ejecutar primero AulaInnova_Bloque_A_COMPLETO.sql.
      2. Abrir este archivo completo en una sola consulta de SSMS.
      3. Ejecutarlo sin seleccionar fragmentos.

    Incluye:
      - episodios de salida temporal y regreso al aula;
      - categorias LEVE, GRAVE y MUY_GRAVE;
      - catalogo administrable de tipos de incidencia;
      - incidencias y sus observaciones;
      - indices para operacion y concurrencia;
      - vista de salidas abiertas;
      - triggers 2 y 3 de auditoria sensible.

    Decisiones funcionales:
      - La salida al bano u otro motivo no exige autorizacion digital previa.
      - Si el profesor permite la salida, se registra el evento; si no, no existe registro.
      - Los resultados del evento siguen siendo ACEPTADO, REPETIDO, RECHAZADO o
        POR_REVISAR. No se crea un estado adicional de permiso.
      - evento.id evita duplicados por reenvio. Una unica salida ABIERTA por alumno
        evita aperturas simultaneas.
      - Solo puede existir una incidencia VIGENTE del mismo tipo para el mismo
        estudiante y jornada. Las reiteraciones se documentan como observaciones.
      - Este bloque crea el catalogo administrable; el Bloque C carga los 68
        tipos oficiales recibidos sin alterar sus enunciados completos.

    Integracion con ASP.NET Core Identity:
      - incidencia.creada_por y observacion_incidencia.autor_id se crean ahora.
      - Sus FK hacia dbo.AspNetUsers se agregan despues mediante
        004_bloque_b_integracion_identity.sql.

    Seguridad:
      - No borra ni reemplaza objetos existentes.
      - Todo el bloque es transaccional y falla si detecta una instalacion previa.
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
    THROW 51101, N'El Bloque B requiere SQL Server 2022 (16.x) o superior.', 1;

IF SCHEMA_ID(N'ai') IS NULL
   OR OBJECT_ID(N'ai.persona', N'U') IS NULL
   OR OBJECT_ID(N'ai.estudiante', N'U') IS NULL
   OR OBJECT_ID(N'ai.aula', N'U') IS NULL
   OR OBJECT_ID(N'ai.grupo', N'U') IS NULL
   OR OBJECT_ID(N'ai.matricula', N'U') IS NULL
   OR OBJECT_ID(N'ai.jornada', N'U') IS NULL
   OR OBJECT_ID(N'ai.motivo_salida', N'U') IS NULL
   OR OBJECT_ID(N'ai.evento', N'U') IS NULL
   OR OBJECT_ID(N'ai.auditoria', N'U') IS NULL
BEGIN
    THROW 51102,
        N'No se encontro una instalacion completa del Bloque A en AulaInnova.',
        1;
END;

IF OBJECT_ID(N'ai.salida_temporal', N'U') IS NOT NULL
   OR OBJECT_ID(N'ai.categoria_incidencia', N'U') IS NOT NULL
   OR OBJECT_ID(N'ai.tipo_incidencia', N'U') IS NOT NULL
   OR OBJECT_ID(N'ai.incidencia', N'U') IS NOT NULL
   OR OBJECT_ID(N'ai.observacion_incidencia', N'U') IS NOT NULL
BEGIN
    THROW 51103,
        N'El Bloque B ya parece instalado. No se hicieron cambios.',
        1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    /* ================================================================
       1. Salidas temporales del aula
       ================================================================ */

    CREATE TABLE ai.salida_temporal
    (
        id                  bigint IDENTITY(1,1) NOT NULL,
        estudiante_id       bigint NOT NULL,
        jornada_id          bigint NOT NULL,
        matricula_id        bigint NOT NULL,
        aula_id             bigint NOT NULL,
        motivo_codigo       varchar(25) NOT NULL,
        evento_salida_id    uniqueidentifier NOT NULL,
        evento_regreso_id   uniqueidentifier NULL,
        salida_utc          datetime2(3) NULL,
        regreso_utc         datetime2(3) NULL,
        expira_utc          datetime2(3) NULL,
        horas_fiables       bit NOT NULL
            CONSTRAINT DF_salida_temporal_horas_fiables DEFAULT (0),
        estado              varchar(12) NOT NULL
            CONSTRAINT DF_salida_temporal_estado DEFAULT ('ABIERTA'),
        creado_utc          datetime2(3) NOT NULL
            CONSTRAINT DF_salida_temporal_creado_utc DEFAULT (SYSUTCDATETIME()),
        version             rowversion NOT NULL,

        CONSTRAINT PK_salida_temporal PRIMARY KEY CLUSTERED (id),
        CONSTRAINT UQ_salida_temporal_evento_salida UNIQUE (evento_salida_id),
        CONSTRAINT CK_salida_temporal_estado
            CHECK (estado IN ('ABIERTA', 'CERRADA', 'ANULADA', 'EXPIRADA')),
        CONSTRAINT CK_salida_temporal_estado_contexto
            CHECK ((estado = 'ABIERTA'
                    AND evento_regreso_id IS NULL
                    AND regreso_utc IS NULL)
                OR (estado = 'CERRADA'
                    AND evento_regreso_id IS NOT NULL)
                OR (estado = 'EXPIRADA'
                    AND expira_utc IS NOT NULL
                    AND evento_regreso_id IS NULL
                    AND regreso_utc IS NULL)
                OR estado = 'ANULADA'),
        CONSTRAINT CK_salida_temporal_eventos_distintos
            CHECK (evento_regreso_id IS NULL
                OR evento_regreso_id <> evento_salida_id),
        CONSTRAINT CK_salida_temporal_orden_horas
            CHECK (salida_utc IS NULL
                OR regreso_utc IS NULL
                OR regreso_utc >= salida_utc),
        CONSTRAINT CK_salida_temporal_horas_fiables
            CHECK (horas_fiables = 0
                OR (estado = 'CERRADA'
                    AND salida_utc IS NOT NULL
                    AND regreso_utc IS NOT NULL
                    AND regreso_utc >= salida_utc)),
        CONSTRAINT FK_salida_temporal_estudiante
            FOREIGN KEY (estudiante_id) REFERENCES ai.estudiante (persona_id),
        CONSTRAINT FK_salida_temporal_jornada
            FOREIGN KEY (jornada_id) REFERENCES ai.jornada (id),
        CONSTRAINT FK_salida_temporal_matricula_estudiante
            FOREIGN KEY (matricula_id, estudiante_id)
            REFERENCES ai.matricula (id, estudiante_id),
        CONSTRAINT FK_salida_temporal_aula
            FOREIGN KEY (aula_id) REFERENCES ai.aula (id),
        CONSTRAINT FK_salida_temporal_motivo
            FOREIGN KEY (motivo_codigo) REFERENCES ai.motivo_salida (codigo),
        CONSTRAINT FK_salida_temporal_evento_salida
            FOREIGN KEY (evento_salida_id) REFERENCES ai.evento (id),
        CONSTRAINT FK_salida_temporal_evento_regreso
            FOREIGN KEY (evento_regreso_id) REFERENCES ai.evento (id)
    );

    CREATE UNIQUE INDEX UX_salida_temporal_estudiante_abierta
        ON ai.salida_temporal (estudiante_id)
        WHERE estado = 'ABIERTA';

    CREATE UNIQUE INDEX UX_salida_temporal_evento_regreso
        ON ai.salida_temporal (evento_regreso_id)
        WHERE evento_regreso_id IS NOT NULL;

    CREATE INDEX IX_salida_temporal_jornada_estado
        ON ai.salida_temporal (jornada_id, estado, salida_utc)
        INCLUDE (estudiante_id, matricula_id, aula_id, motivo_codigo,
                 regreso_utc, horas_fiables);

    CREATE INDEX IX_salida_temporal_matricula_fecha
        ON ai.salida_temporal (matricula_id, creado_utc DESC)
        INCLUDE (estado, motivo_codigo, salida_utc, regreso_utc);

    CREATE INDEX IX_salida_temporal_aula_estado
        ON ai.salida_temporal (aula_id, estado)
        INCLUDE (estudiante_id, jornada_id, motivo_codigo, salida_utc);

    /* ================================================================
       2. Catalogos conductuales
       ================================================================ */

    CREATE TABLE ai.categoria_incidencia
    (
        id              bigint IDENTITY(1,1) NOT NULL,
        codigo          varchar(40) NOT NULL,
        nombre          nvarchar(100) NOT NULL,
        descripcion     nvarchar(400) NOT NULL,
        habilitada      bit NOT NULL
            CONSTRAINT DF_categoria_incidencia_habilitada DEFAULT (1),
        version         rowversion NOT NULL,

        CONSTRAINT PK_categoria_incidencia PRIMARY KEY CLUSTERED (id),
        CONSTRAINT UQ_categoria_incidencia_codigo UNIQUE (codigo),
        CONSTRAINT UQ_categoria_incidencia_nombre UNIQUE (nombre),
        CONSTRAINT CK_categoria_incidencia_codigo
            CHECK (LEN(LTRIM(RTRIM(codigo))) > 0),
        CONSTRAINT CK_categoria_incidencia_codigo_permitido
            CHECK (codigo IN ('LEVE', 'GRAVE', 'MUY_GRAVE')),
        CONSTRAINT CK_categoria_incidencia_nombre
            CHECK (LEN(LTRIM(RTRIM(nombre))) > 0),
        CONSTRAINT CK_categoria_incidencia_descripcion
            CHECK (LEN(LTRIM(RTRIM(descripcion))) > 0)
    );

    CREATE TABLE ai.tipo_incidencia
    (
        id              bigint IDENTITY(1,1) NOT NULL,
        categoria_id    bigint NOT NULL,
        codigo          varchar(50) NOT NULL,
        nombre          nvarchar(150) NOT NULL,
        descripcion     nvarchar(500) NOT NULL,
        habilitado      bit NOT NULL
            CONSTRAINT DF_tipo_incidencia_habilitado DEFAULT (1),
        version         rowversion NOT NULL,

        CONSTRAINT PK_tipo_incidencia PRIMARY KEY CLUSTERED (id),
        CONSTRAINT UQ_tipo_incidencia_codigo UNIQUE (codigo),
        CONSTRAINT UQ_tipo_incidencia_nombre UNIQUE (nombre),
        CONSTRAINT CK_tipo_incidencia_codigo
            CHECK (LEN(LTRIM(RTRIM(codigo))) > 0),
        CONSTRAINT CK_tipo_incidencia_nombre
            CHECK (LEN(LTRIM(RTRIM(nombre))) > 0),
        CONSTRAINT CK_tipo_incidencia_descripcion
            CHECK (LEN(LTRIM(RTRIM(descripcion))) > 0),
        CONSTRAINT FK_tipo_incidencia_categoria
            FOREIGN KEY (categoria_id) REFERENCES ai.categoria_incidencia (id)
    );

    CREATE INDEX IX_tipo_incidencia_categoria_habilitado
        ON ai.tipo_incidencia (categoria_id, habilitado)
        INCLUDE (codigo, nombre);

    INSERT ai.categoria_incidencia (codigo, nombre, descripcion)
    VALUES
        ('LEVE',
         N'Leve',
         N'Incidencia clasificada institucionalmente como leve.'),
        ('GRAVE',
         N'Grave',
         N'Incidencia clasificada institucionalmente como grave.'),
        ('MUY_GRAVE',
         N'Muy grave',
         N'Incidencia clasificada institucionalmente como muy grave.');

    /* ================================================================
       3. Incidencias y observaciones
       ================================================================ */

    CREATE TABLE ai.incidencia
    (
        id                      bigint IDENTITY(1,1) NOT NULL,
        estudiante_id           bigint NOT NULL,
        jornada_id              bigint NOT NULL,
        tipo_incidencia_id      bigint NOT NULL,
        evento_origen_id        uniqueidentifier NOT NULL,
        creada_por              uniqueidentifier NOT NULL,
        vigente                 bit NOT NULL
            CONSTRAINT DF_incidencia_vigente DEFAULT (1),
        creado_utc              datetime2(3) NOT NULL
            CONSTRAINT DF_incidencia_creado_utc DEFAULT (SYSUTCDATETIME()),
        version                 rowversion NOT NULL,

        CONSTRAINT PK_incidencia PRIMARY KEY CLUSTERED (id),
        CONSTRAINT UQ_incidencia_evento_origen UNIQUE (evento_origen_id),
        CONSTRAINT FK_incidencia_estudiante
            FOREIGN KEY (estudiante_id) REFERENCES ai.estudiante (persona_id),
        CONSTRAINT FK_incidencia_jornada
            FOREIGN KEY (jornada_id) REFERENCES ai.jornada (id),
        CONSTRAINT FK_incidencia_tipo
            FOREIGN KEY (tipo_incidencia_id) REFERENCES ai.tipo_incidencia (id),
        CONSTRAINT FK_incidencia_evento_origen
            FOREIGN KEY (evento_origen_id) REFERENCES ai.evento (id)
        /* FK creada_por -> dbo.AspNetUsers.Id: archivo 004. */
    );

    CREATE INDEX IX_incidencia_estudiante_jornada_tipo
        ON ai.incidencia (estudiante_id, jornada_id, tipo_incidencia_id)
        INCLUDE (vigente, evento_origen_id, creada_por, creado_utc);

    CREATE UNIQUE INDEX UX_incidencia_estudiante_jornada_tipo_vigente
        ON ai.incidencia (estudiante_id, jornada_id, tipo_incidencia_id)
        WHERE vigente = 1;

    CREATE INDEX IX_incidencia_jornada_vigente
        ON ai.incidencia (jornada_id, vigente, creado_utc DESC)
        INCLUDE (estudiante_id, tipo_incidencia_id, creada_por);

    CREATE INDEX IX_incidencia_tipo_fecha
        ON ai.incidencia (tipo_incidencia_id, creado_utc DESC)
        INCLUDE (estudiante_id, jornada_id, vigente);

    CREATE INDEX IX_incidencia_creada_por_fecha
        ON ai.incidencia (creada_por, creado_utc DESC)
        INCLUDE (estudiante_id, jornada_id, tipo_incidencia_id, vigente);

    CREATE TABLE ai.observacion_incidencia
    (
        id              bigint IDENTITY(1,1) NOT NULL,
        incidencia_id   bigint NOT NULL,
        evento_id       uniqueidentifier NOT NULL,
        autor_id        uniqueidentifier NOT NULL,
        texto           nvarchar(2000) NOT NULL,
        hecho_utc       datetime2(3) NULL,
        comunicable     bit NOT NULL
            CONSTRAINT DF_observacion_incidencia_comunicable DEFAULT (0),
        vigente         bit NOT NULL
            CONSTRAINT DF_observacion_incidencia_vigente DEFAULT (1),
        creado_utc      datetime2(3) NOT NULL
            CONSTRAINT DF_observacion_incidencia_creado_utc DEFAULT (SYSUTCDATETIME()),
        version         rowversion NOT NULL,

        CONSTRAINT PK_observacion_incidencia PRIMARY KEY CLUSTERED (id),
        CONSTRAINT UQ_observacion_incidencia_evento UNIQUE (evento_id),
        CONSTRAINT CK_observacion_incidencia_texto
            CHECK (LEN(LTRIM(RTRIM(texto))) > 0),
        CONSTRAINT FK_observacion_incidencia_incidencia
            FOREIGN KEY (incidencia_id) REFERENCES ai.incidencia (id),
        CONSTRAINT FK_observacion_incidencia_evento
            FOREIGN KEY (evento_id) REFERENCES ai.evento (id)
        /* FK autor_id -> dbo.AspNetUsers.Id: archivo 004. */
    );

    CREATE INDEX IX_observacion_incidencia_incidencia_fecha
        ON ai.observacion_incidencia (incidencia_id, vigente, creado_utc)
        INCLUDE (hecho_utc, comunicable, evento_id);

    CREATE INDEX IX_observacion_incidencia_comunicable
        ON ai.observacion_incidencia (incidencia_id, creado_utc)
        INCLUDE (texto, hecho_utc)
        WHERE vigente = 1 AND comunicable = 1;

    CREATE INDEX IX_observacion_incidencia_autor_fecha
        ON ai.observacion_incidencia (autor_id, creado_utc DESC)
        INCLUDE (incidencia_id, comunicable, vigente);

    /* ================================================================
       4. Vista operativa de salidas abiertas

       La vista no interpreta una salida como permiso. Solo presenta el
       episodio ya registrado y su motivo declarado.
       ================================================================ */

    EXEC(N'
        CREATE VIEW ai.vw_salidas_abiertas
        AS
        SELECT
            st.id AS salida_temporal_id,
            st.estudiante_id,
            p.nombres,
            p.apellidos,
            e.codigo AS codigo_estudiante,
            st.jornada_id,
            j.fecha_local,
            st.matricula_id,
            m.grupo_id,
            g.ciclo,
            g.grado,
            g.seccion,
            st.aula_id,
            a.codigo AS codigo_aula,
            a.nombre AS nombre_aula,
            st.motivo_codigo,
            ms.nombre AS motivo_declarado,
            st.evento_salida_id,
            st.salida_utc,
            ev.calidad_hora AS calidad_hora_salida,
            ev.origen AS origen_evento,
            st.creado_utc,
            st.version
        FROM ai.salida_temporal AS st
        INNER JOIN ai.estudiante AS e
            ON e.persona_id = st.estudiante_id
        INNER JOIN ai.persona AS p
            ON p.id = e.persona_id
        INNER JOIN ai.jornada AS j
            ON j.id = st.jornada_id
        INNER JOIN ai.matricula AS m
            ON m.id = st.matricula_id
           AND m.estudiante_id = st.estudiante_id
        INNER JOIN ai.grupo AS g
            ON g.id = m.grupo_id
        INNER JOIN ai.aula AS a
            ON a.id = st.aula_id
        INNER JOIN ai.motivo_salida AS ms
            ON ms.codigo = st.motivo_codigo
        INNER JOIN ai.evento AS ev
            ON ev.id = st.evento_salida_id
        WHERE st.estado = ''ABIERTA'';
    ');

    /* ================================================================
       5. Segundo trigger de auditoria sensible: incidencia

       Antes del UPDATE, la aplicacion debe establecer en la misma conexion
       y transaccion:
         ai.operador_id     uniqueidentifier
         ai.motivo          nvarchar(500)
         ai.solicitud_id    uniqueidentifier
         ai.hash_solicitud  varbinary(32)
         ai.operacion       CORREGIR o ANULAR

       DELETE fisico y reactivacion se rechazan.
       ================================================================ */

    EXEC(N'
        CREATE TRIGGER ai.tr_incidencia_auditoria
        ON ai.incidencia
        AFTER UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS (SELECT 1 FROM deleted)
               AND NOT EXISTS (SELECT 1 FROM inserted)
            BEGIN
                THROW 51120,
                    ''DELETE fisico de incidencia no permitido; use anulacion autorizada.'',
                    1;
            END;

            IF EXISTS
            (
                SELECT 1
                FROM deleted AS d
                FULL OUTER JOIN inserted AS i ON i.id = d.id
                WHERE d.id IS NULL OR i.id IS NULL
            )
            BEGIN
                THROW 51121, ''El identificador de incidencia es inmutable.'', 1;
            END;

            IF NOT EXISTS
            (
                SELECT
                    i.id, i.estudiante_id, i.jornada_id, i.tipo_incidencia_id,
                    i.evento_origen_id, i.creada_por, i.vigente, i.creado_utc
                FROM inserted AS i
                INNER JOIN deleted AS d ON d.id = i.id
                EXCEPT
                SELECT
                    d.id, d.estudiante_id, d.jornada_id, d.tipo_incidencia_id,
                    d.evento_origen_id, d.creada_por, d.vigente, d.creado_utc
                FROM inserted AS i
                INNER JOIN deleted AS d ON d.id = i.id
            )
                RETURN;

            IF EXISTS
            (
                SELECT 1
                FROM inserted AS i
                INNER JOIN deleted AS d ON d.id = i.id
                WHERE i.evento_origen_id <> d.evento_origen_id
                   OR i.creada_por <> d.creada_por
                   OR i.creado_utc <> d.creado_utc
            )
            BEGIN
                THROW 51122,
                    ''El evento de origen, el creador y la fecha de una incidencia son inmutables.'',
                    1;
            END;

            IF EXISTS
            (
                SELECT 1
                FROM inserted AS i
                INNER JOIN deleted AS d ON d.id = i.id
                WHERE d.vigente = 0
            )
            BEGIN
                THROW 51123,
                    ''Una incidencia anulada no puede modificarse ni reactivarse.'',
                    1;
            END;

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
               OR @operacion NOT IN (''CORREGIR'', ''ANULAR'')
            BEGIN
                THROW 51124,
                    ''Falta contexto de auditoria valido para modificar la incidencia.'',
                    1;
            END;

            IF @operacion = ''CORREGIR''
               AND EXISTS
               (
                   SELECT 1
                   FROM inserted AS i
                   INNER JOIN deleted AS d ON d.id = i.id
                   WHERE i.vigente <> d.vigente
               )
            BEGIN
                THROW 51125,
                    ''CORREGIR no puede cambiar la vigencia de una incidencia.'',
                    1;
            END;

            IF @operacion = ''ANULAR''
               AND EXISTS
               (
                   SELECT 1
                   FROM inserted AS i
                   INNER JOIN deleted AS d ON d.id = i.id
                   WHERE d.vigente <> 1
                      OR i.vigente <> 0
                      OR i.estudiante_id <> d.estudiante_id
                      OR i.jornada_id <> d.jornada_id
                      OR i.tipo_incidencia_id <> d.tipo_incidencia_id
               )
            BEGIN
                THROW 51126,
                    ''ANULAR solo puede cambiar vigente de 1 a 0.'',
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
                ''incidencia'',
                CONVERT(varchar(50), i.id),
                @operacion,
                @motivo,
                (
                    SELECT
                        d.estudiante_id,
                        d.jornada_id,
                        d.tipo_incidencia_id,
                        d.evento_origen_id,
                        d.creada_por,
                        d.vigente,
                        d.creado_utc
                    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
                ),
                (
                    SELECT
                        i.estudiante_id,
                        i.jornada_id,
                        i.tipo_incidencia_id,
                        i.evento_origen_id,
                        i.creada_por,
                        i.vigente,
                        i.creado_utc
                    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
                ),
                i.evento_origen_id,
                SYSUTCDATETIME(),
                @solicitud_id,
                @hash_solicitud
            FROM inserted AS i
            INNER JOIN deleted AS d ON d.id = i.id
            WHERE EXISTS
            (
                SELECT
                    i.estudiante_id, i.jornada_id, i.tipo_incidencia_id,
                    i.evento_origen_id, i.creada_por, i.vigente, i.creado_utc
                EXCEPT
                SELECT
                    d.estudiante_id, d.jornada_id, d.tipo_incidencia_id,
                    d.evento_origen_id, d.creada_por, d.vigente, d.creado_utc
            );
        END;
    ');

    /* ================================================================
       6. Tercer trigger de auditoria sensible: observacion

       Operaciones admitidas:
         CORREGIR: modifica texto o fecha del hecho.
         PUBLICAR: cambia comunicable de 0 a 1.
         ANULAR: cambia vigente de 1 a 0.

       DELETE fisico, despublicacion y reactivacion se rechazan.
       ================================================================ */

    EXEC(N'
        CREATE TRIGGER ai.tr_observacion_incidencia_auditoria
        ON ai.observacion_incidencia
        AFTER UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS (SELECT 1 FROM deleted)
               AND NOT EXISTS (SELECT 1 FROM inserted)
            BEGIN
                THROW 51130,
                    ''DELETE fisico de observacion no permitido; use anulacion autorizada.'',
                    1;
            END;

            IF EXISTS
            (
                SELECT 1
                FROM deleted AS d
                FULL OUTER JOIN inserted AS i ON i.id = d.id
                WHERE d.id IS NULL OR i.id IS NULL
            )
            BEGIN
                THROW 51131, ''El identificador de observacion es inmutable.'', 1;
            END;

            IF NOT EXISTS
            (
                SELECT
                    i.id, i.incidencia_id, i.evento_id, i.autor_id,
                    i.texto, i.hecho_utc, i.comunicable, i.vigente, i.creado_utc
                FROM inserted AS i
                INNER JOIN deleted AS d ON d.id = i.id
                EXCEPT
                SELECT
                    d.id, d.incidencia_id, d.evento_id, d.autor_id,
                    d.texto, d.hecho_utc, d.comunicable, d.vigente, d.creado_utc
                FROM inserted AS i
                INNER JOIN deleted AS d ON d.id = i.id
            )
                RETURN;

            IF EXISTS
            (
                SELECT 1
                FROM inserted AS i
                INNER JOIN deleted AS d ON d.id = i.id
                WHERE i.incidencia_id <> d.incidencia_id
                   OR i.evento_id <> d.evento_id
                   OR i.autor_id <> d.autor_id
                   OR i.creado_utc <> d.creado_utc
            )
            BEGIN
                THROW 51132,
                    ''La incidencia, el evento, el autor y la fecha de una observacion son inmutables.'',
                    1;
            END;

            IF EXISTS
            (
                SELECT 1
                FROM inserted AS i
                INNER JOIN deleted AS d ON d.id = i.id
                WHERE d.vigente = 0
            )
            BEGIN
                THROW 51133,
                    ''Una observacion anulada no puede modificarse ni reactivarse.'',
                    1;
            END;

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
               OR @operacion NOT IN (''CORREGIR'', ''PUBLICAR'', ''ANULAR'')
            BEGIN
                THROW 51134,
                    ''Falta contexto de auditoria valido para modificar la observacion.'',
                    1;
            END;

            IF @operacion = ''CORREGIR''
               AND EXISTS
               (
                   SELECT 1
                   FROM inserted AS i
                   INNER JOIN deleted AS d ON d.id = i.id
                   WHERE i.comunicable <> d.comunicable
                      OR i.vigente <> d.vigente
               )
            BEGIN
                THROW 51135,
                    ''CORREGIR solo puede cambiar el texto o la fecha del hecho.'',
                    1;
            END;

            IF @operacion = ''PUBLICAR''
               AND EXISTS
               (
                   SELECT 1
                   FROM inserted AS i
                   INNER JOIN deleted AS d ON d.id = i.id
                   WHERE d.comunicable <> 0
                      OR i.comunicable <> 1
                      OR i.vigente <> 1
                      OR i.vigente <> d.vigente
                      OR i.texto <> d.texto
                      OR (i.hecho_utc <> d.hecho_utc
                          OR (i.hecho_utc IS NULL AND d.hecho_utc IS NOT NULL)
                          OR (i.hecho_utc IS NOT NULL AND d.hecho_utc IS NULL))
               )
            BEGIN
                THROW 51136,
                    ''PUBLICAR solo puede cambiar comunicable de 0 a 1.'',
                    1;
            END;

            IF @operacion = ''ANULAR''
               AND EXISTS
               (
                   SELECT 1
                   FROM inserted AS i
                   INNER JOIN deleted AS d ON d.id = i.id
                   WHERE d.vigente <> 1
                      OR i.vigente <> 0
                      OR i.comunicable <> d.comunicable
                      OR i.texto <> d.texto
                      OR (i.hecho_utc <> d.hecho_utc
                          OR (i.hecho_utc IS NULL AND d.hecho_utc IS NOT NULL)
                          OR (i.hecho_utc IS NOT NULL AND d.hecho_utc IS NULL))
               )
            BEGIN
                THROW 51137,
                    ''ANULAR solo puede cambiar vigente de 1 a 0.'',
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
                ''observacion_incidencia'',
                CONVERT(varchar(50), i.id),
                @operacion,
                @motivo,
                (
                    SELECT
                        d.incidencia_id,
                        d.evento_id,
                        d.autor_id,
                        d.texto,
                        d.hecho_utc,
                        d.comunicable,
                        d.vigente,
                        d.creado_utc
                    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
                ),
                (
                    SELECT
                        i.incidencia_id,
                        i.evento_id,
                        i.autor_id,
                        i.texto,
                        i.hecho_utc,
                        i.comunicable,
                        i.vigente,
                        i.creado_utc
                    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
                ),
                i.evento_id,
                SYSUTCDATETIME(),
                @solicitud_id,
                @hash_solicitud
            FROM inserted AS i
            INNER JOIN deleted AS d ON d.id = i.id
            WHERE EXISTS
            (
                SELECT
                    i.incidencia_id, i.evento_id, i.autor_id, i.texto,
                    i.hecho_utc, i.comunicable, i.vigente, i.creado_utc
                EXCEPT
                SELECT
                    d.incidencia_id, d.evento_id, d.autor_id, d.texto,
                    d.hecho_utc, d.comunicable, d.vigente, d.creado_utc
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

PRINT N'Bloque B de Aula Innova instalado correctamente.';
PRINT N'Objetos: 5 tablas, 14 indices adicionales, 1 vista y 2 triggers.';
PRINT N'Categorias cargadas: LEVE, GRAVE y MUY_GRAVE.';
GO
