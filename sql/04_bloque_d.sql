/*
    Aula Innova - Bloque D completo
    SQL Server 2022

    EJECUCION:
      1. Ejecutar primero los bloques A, B y C completos.
      2. Abrir este archivo completo en una sola consulta de SSMS.
      3. Ejecutarlo sin seleccionar fragmentos.

    Incluye:
      - contexto de clase declarado por una persona autorizada;
      - zonas visuales versionadas con geometria normalizada;
      - sesiones autorizadas de procesamiento visual transitorio;
      - definiciones versionadas de indicadores observables;
      - mediciones agregadas por zona, indicador e intervalo;
      - restricciones e indices de integridad y consulta.

    Privacidad y alcance:
      - No almacena video, fotografias, audio, rostros ni imagenes.
      - No contiene FK hacia estudiante, carnet o credencial NFC.
      - No identifica personas a partir de la camara.
      - No mide concentracion, atencion, emocion, sueno, conducta,
        aprendizaje, desorden ni calidad docente.
      - La cantidad visible no equivale a asistencia nominal.
      - Las variaciones, maximos y minimos se derivan de la serie temporal;
        no se guardan como hechos duplicados.

    Integracion con ASP.NET Core Identity:
      - contexto_clase.autor_id y sesion_visual.autorizada_por se crean ahora.
      - Sus FK se agregan despues mediante
        009_bloque_d_integracion_identity.sql.

    Seguridad:
      - No borra ni reemplaza objetos existentes.
      - No agrega triggers ni procedimientos almacenados.
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
    THROW 51301, N'El Bloque D requiere SQL Server 2022 (16.x) o superior.', 1;

IF SCHEMA_ID(N'ai') IS NULL
   OR OBJECT_ID(N'ai.aula', N'U') IS NULL
   OR OBJECT_ID(N'ai.asignacion', N'U') IS NULL
   OR OBJECT_ID(N'ai.persona', N'U') IS NULL
   OR OBJECT_ID(N'ai.auditoria', N'U') IS NULL
BEGIN
    THROW 51302,
        N'No se encontro una instalacion completa de los bloques A, B y C.',
        1;
END;

IF OBJECT_ID(N'ai.contexto_clase', N'U') IS NOT NULL
   OR OBJECT_ID(N'ai.zona_visual', N'U') IS NOT NULL
   OR OBJECT_ID(N'ai.sesion_visual', N'U') IS NOT NULL
   OR OBJECT_ID(N'ai.indicador_visual', N'U') IS NOT NULL
   OR OBJECT_ID(N'ai.medicion_visual', N'U') IS NOT NULL
BEGIN
    THROW 51303,
        N'El Bloque D ya parece instalado. No se hicieron cambios.',
        1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    /* ================================================================
       1. Contexto de clase declarado

       La descripcion procede del docente u operador autorizado. Nunca se
       infiere de las mediciones visuales.
       ================================================================ */

    CREATE TABLE ai.contexto_clase
    (
        id              bigint IDENTITY(1,1) NOT NULL,
        asignacion_id   bigint NOT NULL,
        autor_id        uniqueidentifier NOT NULL,
        inicio_utc      datetime2(3) NOT NULL,
        fin_utc         datetime2(3) NOT NULL,
        descripcion     nvarchar(1000) NOT NULL,
        creado_utc      datetime2(3) NOT NULL
            CONSTRAINT DF_contexto_clase_creado_utc DEFAULT (SYSUTCDATETIME()),
        version         rowversion NOT NULL,

        CONSTRAINT PK_contexto_clase PRIMARY KEY CLUSTERED (id),
        CONSTRAINT CK_contexto_clase_intervalo
            CHECK (fin_utc > inicio_utc),
        CONSTRAINT CK_contexto_clase_descripcion
            CHECK (LEN(LTRIM(RTRIM(descripcion))) > 0),
        CONSTRAINT FK_contexto_clase_asignacion
            FOREIGN KEY (asignacion_id) REFERENCES ai.asignacion (id)
        /* FK autor_id -> dbo.AspNetUsers.Id: archivo 009. */
    );

    CREATE INDEX IX_contexto_clase_asignacion_intervalo
        ON ai.contexto_clase (asignacion_id, inicio_utc, fin_utc)
        INCLUDE (autor_id, descripcion, creado_utc);

    CREATE INDEX IX_contexto_clase_autor_fecha
        ON ai.contexto_clase (autor_id, creado_utc DESC)
        INCLUDE (asignacion_id, inicio_utc, fin_utc);

    /* ================================================================
       2. Zonas visuales versionadas

       geometria_json solo guarda coordenadas normalizadas. Las revisiones
       utilizadas por mediciones permanecen referenciables aunque se deshabiliten.
       ================================================================ */

    CREATE TABLE ai.zona_visual
    (
        id                  bigint IDENTITY(1,1) NOT NULL,
        aula_id             bigint NOT NULL,
        codigo              varchar(30) NOT NULL,
        revision            int NOT NULL,
        geometria_json      nvarchar(2000) NOT NULL,
        habilitada          bit NOT NULL
            CONSTRAINT DF_zona_visual_habilitada DEFAULT (1),

        CONSTRAINT PK_zona_visual PRIMARY KEY CLUSTERED (id),
        CONSTRAINT UQ_zona_visual_aula_codigo_revision
            UNIQUE (aula_id, codigo, revision),
        CONSTRAINT CK_zona_visual_codigo
            CHECK (LEN(LTRIM(RTRIM(codigo))) > 0),
        CONSTRAINT CK_zona_visual_revision
            CHECK (revision > 0),
        CONSTRAINT CK_zona_visual_geometria_json
            CHECK (ISJSON(geometria_json) = 1),
        CONSTRAINT FK_zona_visual_aula
            FOREIGN KEY (aula_id) REFERENCES ai.aula (id)
    );

    CREATE UNIQUE INDEX UX_zona_visual_habilitada
        ON ai.zona_visual (aula_id, codigo)
        INCLUDE (id, revision, geometria_json)
        WHERE habilitada = 1;

    /* ================================================================
       3. Sesiones visuales autorizadas

       La asignacion debe corresponder a una CLASE y coincidir con aula e
       intervalo. Esa validacion multitabla se realiza en la transaccion de
       apertura; las FK preservan las referencias aceptadas.
       ================================================================ */

    CREATE TABLE ai.sesion_visual
    (
        id                          bigint IDENTITY(1,1) NOT NULL,
        asignacion_id               bigint NOT NULL,
        aula_id                     bigint NOT NULL,
        autorizada_por              uniqueidentifier NOT NULL,
        referencia_autorizacion     nvarchar(200) NOT NULL,
        inicio_utc                  datetime2(3) NOT NULL,
        fin_utc                     datetime2(3) NULL,
        version_proceso             varchar(50) NOT NULL,
        estado                      varchar(12) NOT NULL
            CONSTRAINT DF_sesion_visual_estado DEFAULT ('ACTIVA'),

        CONSTRAINT PK_sesion_visual PRIMARY KEY CLUSTERED (id),
        CONSTRAINT CK_sesion_visual_referencia_autorizacion
            CHECK (LEN(LTRIM(RTRIM(referencia_autorizacion))) > 0),
        CONSTRAINT CK_sesion_visual_version_proceso
            CHECK (LEN(LTRIM(RTRIM(version_proceso))) > 0),
        CONSTRAINT CK_sesion_visual_estado
            CHECK (estado IN ('ACTIVA', 'CERRADA', 'FALLIDA')),
        CONSTRAINT CK_sesion_visual_intervalo
            CHECK (fin_utc IS NULL OR fin_utc >= inicio_utc),
        CONSTRAINT CK_sesion_visual_estado_intervalo
            CHECK ((estado = 'ACTIVA' AND fin_utc IS NULL)
                OR (estado IN ('CERRADA', 'FALLIDA') AND fin_utc IS NOT NULL)),
        CONSTRAINT FK_sesion_visual_asignacion
            FOREIGN KEY (asignacion_id) REFERENCES ai.asignacion (id),
        CONSTRAINT FK_sesion_visual_aula
            FOREIGN KEY (aula_id) REFERENCES ai.aula (id)
        /* FK autorizada_por -> dbo.AspNetUsers.Id: archivo 009. */
    );

    CREATE UNIQUE INDEX UX_sesion_visual_aula_activa
        ON ai.sesion_visual (aula_id)
        WHERE estado = 'ACTIVA';

    CREATE INDEX IX_sesion_visual_asignacion_fecha
        ON ai.sesion_visual (asignacion_id, inicio_utc DESC)
        INCLUDE (aula_id, fin_utc, estado, version_proceso);

    CREATE INDEX IX_sesion_visual_aula_fecha
        ON ai.sesion_visual (aula_id, inicio_utc DESC)
        INCLUDE (asignacion_id, fin_utc, estado, version_proceso);

    CREATE INDEX IX_sesion_visual_autorizada_por_fecha
        ON ai.sesion_visual (autorizada_por, inicio_utc DESC)
        INCLUDE (asignacion_id, aula_id, fin_utc, estado);

    /* ================================================================
       4. Definiciones versionadas de indicadores

       No se insertan indicadores por defecto: unidad, metodo, parametros y
       validacion deben documentarse antes de habilitar una revision.
       ================================================================ */

    CREATE TABLE ai.indicador_visual
    (
        id                      bigint IDENTITY(1,1) NOT NULL,
        codigo                  varchar(40) NOT NULL,
        revision                int NOT NULL,
        unidad                  nvarchar(60) NOT NULL,
        metodo                  nvarchar(1500) NOT NULL,
        configuracion_json      nvarchar(2000) NOT NULL,
        referencia_validacion   nvarchar(1000) NOT NULL,
        habilitado              bit NOT NULL
            CONSTRAINT DF_indicador_visual_habilitado DEFAULT (0),

        CONSTRAINT PK_indicador_visual PRIMARY KEY CLUSTERED (id),
        CONSTRAINT UQ_indicador_visual_codigo_revision
            UNIQUE (codigo, revision),
        CONSTRAINT CK_indicador_visual_codigo
            CHECK (LEN(LTRIM(RTRIM(codigo))) > 0),
        CONSTRAINT CK_indicador_visual_revision
            CHECK (revision > 0),
        CONSTRAINT CK_indicador_visual_unidad
            CHECK (LEN(LTRIM(RTRIM(unidad))) > 0),
        CONSTRAINT CK_indicador_visual_metodo
            CHECK (LEN(LTRIM(RTRIM(metodo))) > 0),
        CONSTRAINT CK_indicador_visual_configuracion_json
            CHECK (ISJSON(configuracion_json) = 1),
        CONSTRAINT CK_indicador_visual_referencia_validacion
            CHECK (LEN(LTRIM(RTRIM(referencia_validacion))) > 0)
    );

    CREATE UNIQUE INDEX UX_indicador_visual_habilitado
        ON ai.indicador_visual (codigo)
        INCLUDE (id, revision, unidad)
        WHERE habilitado = 1;

    /* ================================================================
       5. Mediciones visuales agregadas

       Cada fila representa un unico indicador para una zona y ventana. Es
       append-only para la cuenta operativa; una rectificacion se registra de
       forma autorizada sin guardar el material visual transitorio.
       ================================================================ */

    CREATE TABLE ai.medicion_visual
    (
        id                  bigint IDENTITY(1,1) NOT NULL,
        sesion_id           bigint NOT NULL,
        zona_id             bigint NOT NULL,
        indicador_id        bigint NOT NULL,
        inicio_utc          datetime2(3) NOT NULL,
        fin_utc             datetime2(3) NOT NULL,
        valor               decimal(18,4) NULL,
        valida              bit NOT NULL,
        motivo_invalidez    nvarchar(200) NULL,
        muestras_validas    int NOT NULL,
        muestras_totales    int NOT NULL,
        creado_utc          datetime2(3) NOT NULL
            CONSTRAINT DF_medicion_visual_creado_utc DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_medicion_visual PRIMARY KEY CLUSTERED (id),
        CONSTRAINT UQ_medicion_visual_ventana
            UNIQUE (sesion_id, zona_id, indicador_id, inicio_utc, fin_utc),
        CONSTRAINT CK_medicion_visual_intervalo
            CHECK (fin_utc > inicio_utc),
        CONSTRAINT CK_medicion_visual_muestras
            CHECK (muestras_validas >= 0
                AND muestras_totales >= 0
                AND muestras_validas <= muestras_totales),
        CONSTRAINT CK_medicion_visual_validez
            CHECK ((valida = 1
                    AND valor IS NOT NULL
                    AND valor >= 0
                    AND muestras_validas > 0
                    AND motivo_invalidez IS NULL)
                OR (valida = 0
                    AND valor IS NULL
                    AND motivo_invalidez IS NOT NULL
                    AND LEN(LTRIM(RTRIM(motivo_invalidez))) > 0)),
        CONSTRAINT FK_medicion_visual_sesion
            FOREIGN KEY (sesion_id) REFERENCES ai.sesion_visual (id),
        CONSTRAINT FK_medicion_visual_zona
            FOREIGN KEY (zona_id) REFERENCES ai.zona_visual (id),
        CONSTRAINT FK_medicion_visual_indicador
            FOREIGN KEY (indicador_id) REFERENCES ai.indicador_visual (id)
    );

    CREATE INDEX IX_medicion_visual_sesion_inicio
        ON ai.medicion_visual (sesion_id, inicio_utc, fin_utc)
        INCLUDE (zona_id, indicador_id, valor, valida,
                 muestras_validas, muestras_totales);

    CREATE INDEX IX_medicion_visual_indicador_inicio
        ON ai.medicion_visual (indicador_id, inicio_utc, fin_utc)
        INCLUDE (sesion_id, zona_id, valor, valida,
                 muestras_validas, muestras_totales);

    CREATE INDEX IX_medicion_visual_zona_inicio
        ON ai.medicion_visual (zona_id, inicio_utc, fin_utc)
        INCLUDE (sesion_id, indicador_id, valor, valida,
                 muestras_validas, muestras_totales);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

PRINT N'Bloque D de Aula Innova instalado correctamente.';
PRINT N'Objetos: 5 tablas, 11 indices explicitos, 0 vistas y 0 triggers nuevos.';
PRINT N'Base de dominio completa: 34 tablas en el esquema ai.';
PRINT N'Privacidad: no se almacenan videos, imagenes, rostros ni identificadores visuales de alumnos.';
GO
