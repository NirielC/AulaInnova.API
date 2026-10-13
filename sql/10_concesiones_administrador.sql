/*
    Aula Innova - permisos (ai.concesion) para los administradores

    Los endpoints que piden un permiso (VISION_CONFIGURAR para zonas,
    indicadores y sesiones visuales; VISION_LEER; COBERTURA_LEER; etc.)
    lo buscan en ai.concesion, y la API todavía no tiene una pantalla para
    concederlos. Este script da TODOS los permisos del catálogo, con ámbito
    INSTITUCION, a cada usuario con rol Administrador que aún no los tenga.

    Ejecutar DESPUÉS de 09 y de haber arrancado la API una vez (la API crea
    el usuario admin al arrancar). Se puede volver a correr sin problema
    cada vez que se cree otro administrador: no duplica nada.
*/

USE [AulaInnova];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ahora datetime2(3) = SYSUTCDATETIME();

INSERT ai.concesion
    (usuario_id, permiso_codigo, ambito, grupo_id, desde_utc, hasta_utc, habilitada)
SELECT u.Id, p.codigo, 'INSTITUCION', NULL, @ahora, NULL, 1
FROM dbo.AspNetUsers AS u
INNER JOIN dbo.AspNetUserRoles AS ur
    ON ur.UserId = u.Id
INNER JOIN dbo.AspNetRoles AS r
    ON r.Id = ur.RoleId
   AND r.Name = N'Administrador'
CROSS JOIN ai.permiso AS p
WHERE p.habilitado = 1
  AND NOT EXISTS
  (
      SELECT 1
      FROM ai.concesion AS c
      WHERE c.usuario_id = u.Id
        AND c.permiso_codigo = p.codigo
        AND c.ambito = 'INSTITUCION'
        AND c.grupo_id IS NULL
        AND c.habilitada = 1
  );

PRINT CONCAT(N'Concesiones nuevas para administradores: ', @@ROWCOUNT);
GO
