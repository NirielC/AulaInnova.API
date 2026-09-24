/*
    Aula Innova - integracion del Bloque D con ASP.NET Core Identity

    Ejecutar DESPUES de:
      1. La migracion de ASP.NET Core Identity.
      2. Las integraciones Identity de los bloques A, B y C
         (05_identity_bloque_a.sql, 06_identity_bloque_b.sql,
         07_identity_bloque_c.sql).
      3. 04_bloque_d.sql.

    Este archivo no crea ni modifica dbo.AspNetUsers. Solo valida el contrato
    acordado y agrega las dos FK de usuario del Bloque D.

    Contrato requerido:
      dbo.AspNetUsers.Id        uniqueidentifier NOT NULL, PK o UNIQUE
      dbo.AspNetUsers.PersonaId bigint NULL
*/

USE [AulaInnova];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NULL
    THROW 51340, N'No existe dbo.AspNetUsers. Aplique primero la migracion de ASP.NET Core Identity.', 1;

IF OBJECT_ID(N'ai.contexto_clase', N'U') IS NULL
   OR OBJECT_ID(N'ai.sesion_visual', N'U') IS NULL
    THROW 51341, N'No se encontro una instalacion completa del Bloque D.', 1;

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
    THROW 51342, N'dbo.AspNetUsers.Id debe ser uniqueidentifier NOT NULL.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes AS ix
    INNER JOIN sys.index_columns AS ic
        ON ic.object_id = ix.object_id
       AND ic.index_id = ix.index_id
    INNER JOIN sys.columns AS c
        ON c.object_id = ic.object_id
       AND c.column_id = ic.column_id
    WHERE ix.object_id = OBJECT_ID(N'dbo.AspNetUsers')
      AND ix.is_unique = 1
      AND ix.has_filter = 0
      AND ic.key_ordinal = 1
      AND c.name = N'Id'
      AND NOT EXISTS
      (
          SELECT 1
          FROM sys.index_columns AS extra
          WHERE extra.object_id = ix.object_id
            AND extra.index_id = ix.index_id
            AND extra.key_ordinal > 1
      )
)
    THROW 51343, N'dbo.AspNetUsers.Id debe tener una PK o restriccion UNIQUE de una sola columna.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.columns AS c
    INNER JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID(N'dbo.AspNetUsers')
      AND c.name = N'PersonaId'
      AND ty.name = N'bigint'
)
    THROW 51344, N'Falta dbo.AspNetUsers.PersonaId bigint. Debe crearlo la migracion de la API.', 1;

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
    THROW 51345, N'Falta la FK de dbo.AspNetUsers.PersonaId hacia ai.persona.id en la migracion de la API.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'ai.FK_contexto_clase_autor', N'F') IS NULL
        ALTER TABLE ai.contexto_clase WITH CHECK
            ADD CONSTRAINT FK_contexto_clase_autor
            FOREIGN KEY (autor_id) REFERENCES dbo.AspNetUsers (Id);

    IF OBJECT_ID(N'ai.FK_sesion_visual_autorizador', N'F') IS NULL
        ALTER TABLE ai.sesion_visual WITH CHECK
            ADD CONSTRAINT FK_sesion_visual_autorizador
            FOREIGN KEY (autorizada_por) REFERENCES dbo.AspNetUsers (Id);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

PRINT N'Integracion del Bloque D con ASP.NET Core Identity completada.';
GO
