/*
    Aula Innova - integración del Bloque A con ASP.NET Core Identity

    Ejecutar DESPUÉS de que el responsable de la API haya aplicado la migración
    de Identity. Este archivo no crea ni altera dbo.AspNetUsers: solo valida el
    contrato acordado y agrega FK desde las tablas del dominio.

    Contrato requerido:
      dbo.AspNetUsers.Id        uniqueidentifier NOT NULL, PK o UNIQUE
      dbo.AspNetUsers.PersonaId bigint NULL

    La migración de la API es responsable de crear PersonaId y su relación con
    ai.persona. Este archivo falla sin cambios si el contrato no está completo.
*/

USE [AulaInnova];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NULL
    THROW 51030, N'No existe dbo.AspNetUsers. Aplique primero la migración de ASP.NET Core Identity.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.columns AS c
    INNER JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID(N'dbo.AspNetUsers')
      AND c.name = N'Id'
      AND ty.name = N'uniqueidentifier'
      AND c.is_nullable = 0
)
    THROW 51031, N'dbo.AspNetUsers.Id debe ser uniqueidentifier NOT NULL.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.columns AS c
    INNER JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID(N'dbo.AspNetUsers')
      AND c.name = N'PersonaId'
      AND ty.name = N'bigint'
)
    THROW 51032, N'Falta dbo.AspNetUsers.PersonaId bigint. Debe crearlo la migración de la API.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys AS fk
    INNER JOIN sys.foreign_key_columns AS fkc
        ON fkc.constraint_object_id = fk.object_id
    INNER JOIN sys.columns AS pc
        ON pc.object_id = fkc.parent_object_id
       AND pc.column_id = fkc.parent_column_id
    WHERE fk.parent_object_id = OBJECT_ID(N'dbo.AspNetUsers')
      AND fk.referenced_object_id = OBJECT_ID(N'ai.persona')
      AND pc.name = N'PersonaId'
)
    THROW 51033, N'Falta la FK de dbo.AspNetUsers.PersonaId hacia ai.persona.id en la migración de la API.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'ai.FK_concesion_usuario', N'F') IS NULL
        ALTER TABLE ai.concesion WITH CHECK
            ADD CONSTRAINT FK_concesion_usuario
            FOREIGN KEY (usuario_id) REFERENCES dbo.AspNetUsers (Id);

    IF OBJECT_ID(N'ai.FK_estacion_configuracion_usuario', N'F') IS NULL
        ALTER TABLE ai.estacion_configuracion WITH CHECK
            ADD CONSTRAINT FK_estacion_configuracion_usuario
            FOREIGN KEY (configurada_por) REFERENCES dbo.AspNetUsers (Id);

    IF OBJECT_ID(N'ai.FK_evento_operador', N'F') IS NULL
        ALTER TABLE ai.evento WITH CHECK
            ADD CONSTRAINT FK_evento_operador
            FOREIGN KEY (operador_id) REFERENCES dbo.AspNetUsers (Id);

    IF OBJECT_ID(N'ai.FK_asistencia_revisor', N'F') IS NULL
        ALTER TABLE ai.asistencia WITH CHECK
            ADD CONSTRAINT FK_asistencia_revisor
            FOREIGN KEY (revisado_por) REFERENCES dbo.AspNetUsers (Id);

    IF OBJECT_ID(N'ai.FK_auditoria_operador', N'F') IS NULL
        ALTER TABLE ai.auditoria WITH CHECK
            ADD CONSTRAINT FK_auditoria_operador
            FOREIGN KEY (operador_id) REFERENCES dbo.AspNetUsers (Id);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

PRINT N'Integración del Bloque A con ASP.NET Core Identity completada.';
GO
