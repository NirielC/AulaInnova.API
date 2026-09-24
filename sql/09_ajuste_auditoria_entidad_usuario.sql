/*
    Aula Innova - ajuste de auditoria para dbo.AspNetUsers

    Ejecutar DESPUES de:
      1. sql/01_bloque_a.sql (crea ai.auditoria y su CHECK).
      2. sql/05_identity_bloque_a.sql (crea FK_auditoria_operador).

    Motivo:
      CK_auditoria_entidad no incluye ningun valor para auditar altas o
      cambios sobre dbo.AspNetUsers. AdminUserService audita esas
      operaciones con entidad = 'aspnet_user'; este script agrega ese
      valor sin quitar ninguno de los ya aprobados.

    Seguridad:
      Idempotente: si 'aspnet_user' ya esta en la lista, no hace nada.
*/

USE [AulaInnova];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'ai.auditoria', N'U') IS NULL
    THROW 51401, N'No se encontro ai.auditoria. Aplique primero el Bloque A.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_auditoria_entidad'
      AND parent_object_id = OBJECT_ID(N'ai.auditoria')
)
    THROW 51402, N'No se encontro CK_auditoria_entidad en ai.auditoria.', 1;

-- Ya ajustado: no hacer nada.
IF EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_auditoria_entidad'
      AND parent_object_id = OBJECT_ID(N'ai.auditoria')
      AND definition LIKE '%aspnet_user%'
)
BEGIN
    PRINT N'CK_auditoria_entidad ya incluye ''aspnet_user''. No se hicieron cambios.';
    RETURN;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    ALTER TABLE ai.auditoria
        DROP CONSTRAINT CK_auditoria_entidad;

    ALTER TABLE ai.auditoria
        ADD CONSTRAINT CK_auditoria_entidad
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
             'indicador_visual', 'sesion_visual', 'medicion_visual',
             'aspnet_user'));

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

PRINT N'Ajuste de auditoria para dbo.AspNetUsers completado.';
GO
