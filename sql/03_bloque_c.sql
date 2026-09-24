/*
    Aula Innova - Bloque C completo
    SQL Server 2022

    EJECUCION:
      1. Ejecutar primero los bloques A y B completos.
      2. Abrir este archivo completo en una sola consulta de SSMS.
      3. Ejecutarlo sin seleccionar fragmentos.

    Incluye:
      - catalogo institucional de 68 tipos de incidencia conductual;
      - fallas tecnicas de estaciones;
      - asignaciones concretas de clase, cuido y sustitucion;
      - necesidades de cobertura;
      - verificaciones humanas de presencia o ausencia;
      - FK de evento hacia asignacion;
      - indices para intervalos, concurrencia y consultas;
      - vista operativa de cobertura por aula.

    Clasificacion recibida:
      - primera lista: 20 tipos LEVE;
      - segunda lista: 33 tipos GRAVE;
      - tercera lista: 15 tipos MUY_GRAVE.

    Decisiones:
      - El enunciado completo recibido se conserva como descripcion.
      - Los nombres breves solo facilitan la seleccion en la interfaz.
      - Los codigos LEV-001, GRA-001 y MGR-001 son identificadores estables.
      - Solo se permite una incidencia vigente del mismo tipo por estudiante
        y jornada; las reiteraciones se agregan como observaciones.
      - Asignar una persona no prueba su presencia.
      - Una verificacion humana y una lectura NFC se muestran por separado.
      - Falta de lectura o comunicacion nunca se convierte automaticamente en ausencia.
      - No se agregan triggers: se mantienen exactamente los tres aprobados.

    Integracion con ASP.NET Core Identity:
      - Las columnas de usuario se crean ahora.
      - Sus FK se agregan despues mediante
        007_bloque_c_integracion_identity.sql.

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
    THROW 51201, N'El Bloque C requiere SQL Server 2022 (16.x) o superior.', 1;

IF SCHEMA_ID(N'ai') IS NULL
   OR OBJECT_ID(N'ai.persona', N'U') IS NULL
   OR OBJECT_ID(N'ai.personal', N'U') IS NULL
   OR OBJECT_ID(N'ai.aula', N'U') IS NULL
   OR OBJECT_ID(N'ai.grupo', N'U') IS NULL
   OR OBJECT_ID(N'ai.horario_clase', N'U') IS NULL
   OR OBJECT_ID(N'ai.jornada', N'U') IS NULL
   OR OBJECT_ID(N'ai.estacion', N'U') IS NULL
   OR OBJECT_ID(N'ai.evento', N'U') IS NULL
   OR OBJECT_ID(N'ai.tipo_incidencia', N'U') IS NULL
   OR OBJECT_ID(N'ai.categoria_incidencia', N'U') IS NULL
   OR OBJECT_ID(N'ai.auditoria', N'U') IS NULL
BEGIN
    THROW 51202,
        N'No se encontro una instalacion completa de los bloques A y B.',
        1;
END;

IF OBJECT_ID(N'ai.falla_estacion', N'U') IS NOT NULL
   OR OBJECT_ID(N'ai.asignacion', N'U') IS NOT NULL
   OR OBJECT_ID(N'ai.necesidad_cobertura', N'U') IS NOT NULL
   OR OBJECT_ID(N'ai.verificacion_cobertura', N'U') IS NOT NULL
BEGIN
    THROW 51203,
        N'El Bloque C ya parece instalado. No se hicieron cambios.',
        1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    /* ================================================================
       1. Catalogo institucional de incidencias conductuales

       La carga es idempotente por codigo. Si un codigo o nombre existente
       contradice el catalogo recibido, se detiene sin sobrescribir datos.
       ================================================================ */

    DECLARE @catalogo TABLE
    (
        categoria_codigo varchar(40) NOT NULL,
        codigo            varchar(50) NOT NULL PRIMARY KEY,
        nombre            nvarchar(150) NOT NULL UNIQUE,
        descripcion       nvarchar(500) NOT NULL
    );

    INSERT @catalogo (categoria_codigo, codigo, nombre, descripcion)
    VALUES
        ('LEVE', 'LEV-001', N'Prendas de abrigo reglamentarias',
         N'Utilizar como prendas de abrigo o de protección contra la lluvia las que, para estos propósitos, forman parte del uniforme del Colegio.'),
        ('LEVE', 'LEV-002', N'Útiles escolares completos',
         N'Asistir al Colegio con todos los útiles para poder seguir las clases sin dificultades.'),
        ('LEVE', 'LEV-003', N'Porte y conservación del carné',
         N'Portar el carné del Colegio en un lugar visible, en buen estado y sin ningún tipo de alteración. En caso de extravío, el estudiantado, o sus responsables familiares deberán pasar a Administración para realizar el trámite de reposición. El profesorado, auxiliares de grado o personal de servicios en ningún caso recibirán efectivo ni se harán cargo de esta gestión.'),
        ('LEVE', 'LEV-004', N'Cumplimiento de horarios, plazos y tareas',
         N'Cumplir y respetar los horarios y plazos aprobados para el desarrollo de las actividades del Colegio y presentar, completas y dentro de los plazos establecidos, las tareas solicitadas por los docentes, debidamente firmadas por sus responsables familiares.'),
        ('LEVE', 'LEV-005', N'Cuidado de pertenencias propias',
         N'Cada estudiante debe responsabilizarse del cuido de sus pertenencias.'),
        ('LEVE', 'LEV-006', N'Pintura o manchas en el cuerpo',
         N'No manchar, ni pintar con ninguna sustancia, ninguna parte de su cuerpo.'),
        ('LEVE', 'LEV-007', N'Pelotas no permitidas',
         N'No se aceptarán pelotas de cuero ni de materiales duros, solo de plástico blando y de una sola capa.'),
        ('LEVE', 'LEV-008', N'Permanencia en zonas de recreación',
         N'Permanecer durante los recesos en las zonas y espacios asignados para la recreación y descanso.'),
        ('LEVE', 'LEV-009', N'Alimentos en zonas y horarios autorizados',
         N'Ingerir alimentos en las zonas y horas asignadas para este fin.'),
        ('LEVE', 'LEV-010', N'Rellenado de botellas en horarios permitidos',
         N'Rellenar botellas, antes de la Asamblea inicial, durante los recesos u horas de almuerzo.'),
        ('LEVE', 'LEV-011', N'Cuidado del dinero personal',
         N'Si porta dinero, deberá cuidarlo, se recomienda que no sean sumas elevadas. El Colegio no se hace responsable por su pérdida.'),
        ('LEVE', 'LEV-012', N'Acceso a salas de profesores',
         N'Respetar las zonas de acceso restringidas, entre salas: las Salas de Profesores.'),
        ('LEVE', 'LEV-013', N'Salida del salón y permanencia entre clases',
         N'Para no interrumpir el proceso enseñanza — aprendizaje, los estudiantes podrán salir del salón, solo en caso de emergencia y portando el gafete correspondiente. Para dar inicio puntual a las clases deberán permanecer dentro del salón en los cambios de clases.'),
        ('LEVE', 'LEV-014', N'Orden en clase y permiso para levantarse',
         N'Mantener el orden en la clase y solicitar permiso al docente para levantarse dentro del salón ante una necesidad.'),
        ('LEVE', 'LEV-015', N'Consumo de chicle',
         N'Por salud, higiene dental y limpieza deben evitar masticar chicle.'),
        ('LEVE', 'LEV-016', N'Indicaciones docentes y turno de palabra',
         N'Acatar las indicaciones dadas por cada docente para el óptimo desarrollo de las actividades. Para solicitar la palabra durante las clases deberá levantar la mano y esperar su turno.'),
        ('LEVE', 'LEV-017', N'Participación y escucha en formaciones',
         N'Participar activamente durante las formaciones y practicar la escucha activa cuando se estén dirigiendo compañeros o docentes en la formación.'),
        ('LEVE', 'LEV-018', N'Ruido, tránsito e ingreso a salones ajenos',
         N'En los edificios de aulas se evitará hacer cualquier clase de ruido, para no dificultar el trabajo de las personas que laboran en ellos. Los estudiantes deberán entrar, salir y transitar por las instalaciones del colegio, ordenadamente, y sin producir bullicio. No podrán ingresar a un salón que no les corresponde, a excepción de cuando van a recibir refuerzo vespertino, fin de semana o exámenes diferidos, siempre que sean acompañados de un docente.'),
        ('LEVE', 'LEV-019', N'Higiene y presentación del cabello',
         N'Los responsables familiares deben ser cuidadosos con la higiene capilar de los estudiantes, para evitar parásitos y su proliferación, deben mantener el cabello presentable.'),
        ('LEVE', 'LEV-020', N'Accesorios, piercings y aretes no permitidos',
         N'No se permite el uso excesivo ni extravagante de collares, pulseras ni adornos complementarios a las piezas de los uniformes. No está permitido asistir al Colegio con piercings, ni aretes en el caso de los varones.'),

        ('GRAVE', 'GRA-001', N'Puntualidad en la Asamblea inicial',
         N'Asistir todos los días lectivos, puntualmente a la Asamblea inicial. La reincidencia de dos llegadas tardes a la semana, tres en quince días o cuatro en el mes, sin la debida justificación de fuerza mayor son consideradas como faltas graves y se tomarán las medidas descritas en este Manual de Convivencia.'),
        ('GRAVE', 'GRA-002', N'Puntualidad en clases',
         N'Asistir puntualmente a clases. El estudiantado del Colegio no debe incurrir en retrasos por tener que desplazarse entre salones o edificios, ni en la hora de entrada después de recesos ni de almuerzo.'),
        ('GRAVE', 'GRA-003', N'Uso de dispositivos, utensilios y juguetes',
         N'Cumplir con la Normativa sobre el ingreso y uso de dispositivos electrónicos, utensilios y juguetes en Colegio.'),
        ('GRAVE', 'GRA-004', N'Capturas audiovisuales sin autorización',
         N'Para la toma de fotografías, videos y audios dentro de las instalaciones del colegio, requerirá autorización institucional.'),
        ('GRAVE', 'GRA-005', N'Pertenencia a grupos digitales ofensivos',
         N'Evitar pertenecer a grupos en redes sociales en los que se difame, denigre, descalifique, ofenda o dañe la integridad psicológica o moral de cualquier miembro de la comunidad educativa, activo o no.'),
        ('GRAVE', 'GRA-006', N'Vocabulario y gestos inapropiados',
         N'Cuidar el vocabulario y los gestos. No se permite el empleo de formas expresivas vulgares o insultantes ni indecorosas.'),
        ('GRAVE', 'GRA-007', N'Orientaciones docentes sobre el aprendizaje',
         N'Seguir las orientaciones del profesorado respecto a su aprendizaje.'),
        ('GRAVE', 'GRA-008', N'Daño o uso incorrecto de bienes e instalaciones',
         N'Cuidar y utilizar correctamente los bienes muebles, el material y las instalaciones del Colegio. No destruir el mobiliario o cualquier estructura física de la Institución o que se encuentre en ella.'),
        ('GRAVE', 'GRA-009', N'Cuidado reglamentario de pupitres',
         N'Para Básica y Media, los pupitres deben mantenerse en buenas condiciones, siempre forrados, sin pegatinas, recortes, imágenes, ni manchas de ningún tipo. En caso de deterioro, el responsable familiar deberá enviar el material, para que el estudiante cambie el forro dañado.'),
        ('GRAVE', 'GRA-010', N'Participación en actividades cocurriculares',
         N'Participar en las actividades cocurriculares que la Institución promueva.'),
        ('GRAVE', 'GRA-011', N'Entrega y devolución de comunicaciones',
         N'Hacer llegar a sus padres o responsables familiares cualquier circular; boleta, citatorios, así como reintegrarlas a su tutor con acuse de recibo. Las circulares y citatorios, en todos los casos, deben ser reintegrados al Colegio el día lectivo siguiente, las boletas con un máximo de 72 horas.'),
        ('GRAVE', 'GRA-012', N'Limpieza y conservación de espacios',
         N'Colaborar con la conservación y limpieza del Colegio y su entorno. Los pasillos, aulas, bibliotecas, laboratorios, cuartos de baño, jardines y salas de estudia deberán permanecer limpios sin envases, papeles, basura ni agua derramada. El estudiantado deberá colocar, en todos los casos, la basura en los lugares asignados.'),
        ('GRAVE', 'GRA-013', N'Uso correcto del uniforme',
         N'Usar correctamente los uniformes dentro y fuera del Colegio.'),
        ('GRAVE', 'GRA-014', N'Cumplimiento del modelo de uniforme',
         N'Asegúrese de que el uniforme cumple con los preceptos expresados en este Manual. (Véase modelo de uniforme)'),
        ('GRAVE', 'GRA-015', N'Asistencia con uniforme',
         N'Asistir siempre, uniformado al Colegio. No importa si ya terminó el año lectivo.'),
        ('GRAVE', 'GRA-016', N'Uniforme para actividades sabatinas',
         N'Asistir a las actividades programadas para los sábados con el uniforme de deportes o si así se le precisase, con el uniforme de diario.'),
        ('GRAVE', 'GRA-017', N'Ingreso o salida de particular',
         N'El estudiantado, en ningún caso podrá asistir o salir de la institución de particular (esto incluye cuando finalizan las academias deportivas o cualquier actividad curricular o cocurricular).'),
        ('GRAVE', 'GRA-018', N'Presentación personal no permitida',
         N'No están permitidos, para el estudiantado, los tintes, las decoloraciones de cabello, peinados o cortes extravagantes, uñas largas, uñas esmaltadas, postizas, ni maquillaje, ni accesorios.'),
        ('GRAVE', 'GRA-019', N'Corte de cabello no reglamentario',
         N'El corte de cabello para los estudiantes es francesa clara.'),
        ('GRAVE', 'GRA-020', N'Calzado de uniforme no reglamentario',
         N'Los zapatos del uniforme deberán ser negros de lustrar. (Diseño clásico). Las estudiantes no deberán venir al colegio con zapatos que simulen el calzado de bailarina o cualquier otro diseño que se ponga de moda. Los zapatos de las señoritas son de empeine cerrado. Deberán limpiarlos a diario.'),
        ('GRAVE', 'GRA-021', N'Calzado deportivo no reglamentario',
         N'El calzado deportivo debe ser blanco o negro. Por la seguridad física a la hora de realizar una actividad deportiva o predeportiva, no se permite el uso de calzado de lona. Deben limpiarlos a diario.'),
        ('GRAVE', 'GRA-022', N'Portación de objetos cortopunzantes',
         N'No portar objetos cortopunzantes ni ningún tipo de objetos que pueda dañar la integridad física de los miembros de la comunidad educativa.'),
        ('GRAVE', 'GRA-023', N'Lanzamiento de objetos peligrosos',
         N'No lanzar objetos cortopunzantes (piedras, latas, lápices, reglas, entre otros).'),
        ('GRAVE', 'GRA-024', N'Asistencia previa a evaluaciones',
         N'Asistir a clases el día anterior (o anteriores) a la Prueba de Nivel, prueba PreAVANZO o a otras actividades evaluadas.'),
        ('GRAVE', 'GRA-025', N'Tránsito por zonas restringidas o peligrosas',
         N'Para evitar accidentes no se deberá transitar por zonas de acceso restringido: taludes, zonas que no corresponden a su nivel educativo, y zonas señalizadas como zonas de peligro o precaución. Por el mismo motivo el estudiantado deberá transitar por las aceras, utilizar los pasos de cebra y evitar sentarse en las calles.'),
        ('GRAVE', 'GRA-026', N'Violencia o comportamiento indecoroso',
         N'Fomentamos una cultura de respeto y convivencia pacífica en nuestra comunidad escolar. Por lo tanto, esperamos que nuestro estudiantado no se involucre en actos violentos o comportamientos indecorosos, presencial o virtualmente, y que tampoco alienten este tipo de conductas. Trabajamos juntos para crear un ambiente seguro y armonioso para todos.'),
        ('GRAVE', 'GRA-027', N'Retiro sin autorización escrita',
         N'Para retirarse del Colegio en horas lectivas, deberá contar con la autorización escrita y firmada por el Equipo de Gestión. El estudiantado se retirará con un responsable familiar.'),
        ('GRAVE', 'GRA-028', N'Uso indebido de servicios tecnológicos',
         N'Hacer buen uso de los servicios tecnológicos.'),
        ('GRAVE', 'GRA-029', N'Irrespeto de derechos de autor',
         N'Respetar derechos de autor.'),
        ('GRAVE', 'GRA-030', N'Falta de referencia de contenido generado con IA',
         N'Referenciar los apartados generados con Inteligencia Artificial generativo en todos los trabajos presentados.'),
        ('GRAVE', 'GRA-031', N'Ventas o intercambios no permitidos',
         N'No se permite realizar ventas, ni ninguna transacción de intercambios o similares, dentro de las instalaciones.'),
        ('GRAVE', 'GRA-032', N'Uso inadecuado de recursos naturales',
         N'Cuidar y proteger el medio ambiente y hacer uso racional de los recursos naturales (agua, uso adecuado de la electricidad, entre otros).'),
        ('GRAVE', 'GRA-033', N'Conducta inapropiada durante evaluaciones',
         N'Mantener el silencio, el orden y buena compostura durante la realización de evaluaciones objetivas (exámenes, laboratorios, Pruebas de nivel, entre otros).'),

        ('MUY_GRAVE', 'MGR-001', N'Irrespeto a la comunidad educativa',
         N'Mostrar respeto y consideración a sus docentes, personal administrativo, personal de servicio, vigilancia y a toda la comunidad educativa.'),
        ('MUY_GRAVE', 'MGR-002', N'Copia, intento de copia o plagio',
         N'En nuestra comunidad educativa, promovemos la integridad académica a través de nuestro valor fundamental de la sinceridad. Esto implica evitar copiar, intentar copiar o plagiar trabajos, exámenes u otras actividades o documentos. Vulnerar esta norma supondrá la pérdida de la beca otorgada por la Fundación Padre Arrupe.'),
        ('MUY_GRAVE', 'MGR-003', N'Discriminación o agresión',
         N'Respetar la dignidad e integridad de todos los miembros de la comunidad educativa y no discriminar a ninguna persona por cualquier circunstancia social, económica o personal. No se permite la agresión física, moral o psicológica contra otro miembro de la comunidad escolar, ya sea de forma presencial o virtual a través de medios electrónicos.'),
        ('MUY_GRAVE', 'MGR-004', N'Indisciplina u ofensa',
         N'No se permiten actos de indisciplina u ofensa hacia otro miembro, activo o no, de la comunidad escolar. Esto incluye la utilización de medios digitales para este fin.'),
        ('MUY_GRAVE', 'MGR-005', N'Creación o difusión en grupos digitales ofensivos',
         N'Evitar la creación, administración o moderación de grupos en redes sociales en los que se difame, denigre, descalifique, ofenda o dañe la integridad psicológica o moral de cualquier miembro de la comunidad educativa, activo o no, ya sea con usuario registrado a su nombre o anónimos, así como las reacciones, comentarios o difusión de estos.'),
        ('MUY_GRAVE', 'MGR-006', N'Irrespeto a pertenencias ajenas',
         N'Respetar las pertenencias de los otros miembros de la comunidad educativa.'),
        ('MUY_GRAVE', 'MGR-007', N'Tabaco, vapeadores, drogas, alcohol o energizantes',
         N'No se permite fumar ni portar cigarrillos, vapeadores ni ningún tipo de estupefacientes, ni portar o ingerir alcohol o bebidas energizantes en todo el recinto de la Institución Padre Arrupe, ni en las actividades en el exterior en las que participe el Colegio.'),
        ('MUY_GRAVE', 'MGR-008', N'Promoción o venta de sustancias y productos',
         N'No se permite la promoción, venta, ni ningún tipo de transacción de drogas, bebidas alcohólicas, energizantes, cigarrillos, vapeadores, o de cualquier producto, dentro de las instalaciones de la Fundación Padre Arrupe.'),
        ('MUY_GRAVE', 'MGR-009', N'Daños graves a personas o bienes',
         N'No se permite causar, a ninguna persona, daños graves por uso indebido en el material o instalaciones del centro o con pertenencias de otras personas.'),
        ('MUY_GRAVE', 'MGR-010', N'Falsificación o alteración de documentos',
         N'No falsificar, ni enmendar la firma o documentos de cualquier de los responsables familiares o documentos institucionales. Esto incluye documentación digital.'),
        ('MUY_GRAVE', 'MGR-011', N'Robo, hurto o intentos',
         N'No está permitido el robo o el intento de robo ni el hurto o intento de hurto.'),
        ('MUY_GRAVE', 'MGR-012', N'Armas, drogas o actuaciones contra la integridad',
         N'No se permiten actuaciones perjudiciales para la salud y la integridad personal, portación de armas o drogas.'),
        ('MUY_GRAVE', 'MGR-013', N'Utensilios para causar daño',
         N'No portar utensilios susceptibles de ser utilizados para dañar la integridad física de compañeros o cualquier otro miembro de la comunidad educativa.'),
        ('MUY_GRAVE', 'MGR-014', N'Representación institucional sin autorización',
         N'Para representar la Institución, en cualquier tipo de evento, o participar en cualquiera de estos, haciendo uso del nombre del Colegio, se deberá contar con la debida autorización por parte de la Dirección Académica.'),
        ('MUY_GRAVE', 'MGR-015', N'Apuestas',
         N'Los juegos que realice el estudiantado no deben incluir ningún tipo de apuestas monetarias o de cualquier bien.');

    IF (SELECT COUNT(*) FROM @catalogo) <> 68
        THROW 51204, N'El catalogo preparado no contiene los 68 tipos esperados.', 1;

    IF (SELECT COUNT(*)
        FROM ai.categoria_incidencia
        WHERE codigo IN ('LEVE', 'GRAVE', 'MUY_GRAVE')) <> 3
    BEGIN
        THROW 51205,
            N'Faltan una o mas categorias institucionales de incidencia.',
            1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM @catalogo AS c
        INNER JOIN ai.tipo_incidencia AS ti ON ti.codigo = c.codigo
        INNER JOIN ai.categoria_incidencia AS ci ON ci.id = ti.categoria_id
        WHERE ci.codigo <> c.categoria_codigo
           OR ti.nombre COLLATE Latin1_General_100_BIN2
                <> c.nombre COLLATE Latin1_General_100_BIN2
           OR ti.descripcion COLLATE Latin1_General_100_BIN2
                <> c.descripcion COLLATE Latin1_General_100_BIN2
    )
    BEGIN
        THROW 51206,
            N'Un codigo de tipo de incidencia ya existe con datos diferentes.',
            1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM @catalogo AS c
        INNER JOIN ai.tipo_incidencia AS ti ON ti.nombre = c.nombre
        WHERE ti.codigo <> c.codigo
    )
    BEGIN
        THROW 51207,
            N'Un nombre de incidencia ya existe asociado a otro codigo.',
            1;
    END;

    INSERT ai.tipo_incidencia
        (categoria_id, codigo, nombre, descripcion, habilitado)
    SELECT
        ci.id,
        c.codigo,
        c.nombre,
        c.descripcion,
        1
    FROM @catalogo AS c
    INNER JOIN ai.categoria_incidencia AS ci
        ON ci.codigo = c.categoria_codigo
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM ai.tipo_incidencia AS existente
        WHERE existente.codigo = c.codigo
    );

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'ai.incidencia')
          AND name = N'UX_incidencia_estudiante_jornada_tipo_vigente'
    )
    BEGIN
        IF EXISTS
        (
            SELECT 1
            FROM ai.incidencia
            WHERE vigente = 1
            GROUP BY estudiante_id, jornada_id, tipo_incidencia_id
            HAVING COUNT(*) > 1
        )
        BEGIN
            THROW 51208,
                N'Existen incidencias vigentes duplicadas. Deben revisarse y anularse los duplicados antes de instalar la unicidad.',
                1;
        END;

        CREATE UNIQUE INDEX UX_incidencia_estudiante_jornada_tipo_vigente
            ON ai.incidencia
                (estudiante_id, jornada_id, tipo_incidencia_id)
            WHERE vigente = 1;
    END;

    /* ================================================================
       2. Fallas tecnicas de estaciones
       ================================================================ */

    CREATE TABLE ai.falla_estacion
    (
        id              bigint IDENTITY(1,1) NOT NULL,
        estacion_id     bigint NOT NULL,
        reportado_por   uniqueidentifier NOT NULL,
        descripcion     nvarchar(1000) NOT NULL,
        estado          varchar(12) NOT NULL
            CONSTRAINT DF_falla_estacion_estado DEFAULT ('ABIERTA'),
        resuelta_utc    datetime2(3) NULL,
        creado_utc      datetime2(3) NOT NULL
            CONSTRAINT DF_falla_estacion_creado_utc DEFAULT (SYSUTCDATETIME()),
        version         rowversion NOT NULL,

        CONSTRAINT PK_falla_estacion PRIMARY KEY CLUSTERED (id),
        CONSTRAINT CK_falla_estacion_descripcion
            CHECK (LEN(LTRIM(RTRIM(descripcion))) > 0),
        CONSTRAINT CK_falla_estacion_estado
            CHECK (estado IN ('ABIERTA', 'RESUELTA')),
        CONSTRAINT CK_falla_estacion_resolucion
            CHECK ((estado = 'ABIERTA' AND resuelta_utc IS NULL)
                OR (estado = 'RESUELTA' AND resuelta_utc IS NOT NULL)),
        CONSTRAINT CK_falla_estacion_orden_horas
            CHECK (resuelta_utc IS NULL OR resuelta_utc >= creado_utc),
        CONSTRAINT FK_falla_estacion_estacion
            FOREIGN KEY (estacion_id) REFERENCES ai.estacion (id)
        /* FK reportado_por -> dbo.AspNetUsers.Id: archivo 007. */
    );

    CREATE INDEX IX_falla_estacion_estacion_estado_fecha
        ON ai.falla_estacion (estacion_id, estado, creado_utc DESC)
        INCLUDE (resuelta_utc, reportado_por);

    CREATE INDEX IX_falla_estacion_reportado_por_fecha
        ON ai.falla_estacion (reportado_por, creado_utc DESC)
        INCLUDE (estacion_id, estado);

    /* ================================================================
       3. Asignaciones concretas
       ================================================================ */

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'ai.horario_clase')
          AND name = N'UX_horario_clase_contexto'
    )
    BEGIN
        CREATE UNIQUE INDEX UX_horario_clase_contexto
            ON ai.horario_clase (id, grupo_id, docente_id, aula_id);
    END;

    CREATE TABLE ai.asignacion
    (
        id                  bigint IDENTITY(1,1) NOT NULL,
        jornada_id          bigint NOT NULL,
        grupo_id            bigint NOT NULL,
        horario_clase_id    bigint NULL,
        personal_id         bigint NOT NULL,
        aula_id             bigint NOT NULL,
        tipo                varchar(12) NOT NULL,
        inicio_utc          datetime2(3) NOT NULL,
        fin_utc             datetime2(3) NOT NULL,
        estado              varchar(12) NOT NULL
            CONSTRAINT DF_asignacion_estado DEFAULT ('ACTIVA'),
        sustituye_id        bigint NULL,
        actividad           nvarchar(150) NULL,
        creado_utc          datetime2(3) NOT NULL
            CONSTRAINT DF_asignacion_creado_utc DEFAULT (SYSUTCDATETIME()),
        version             rowversion NOT NULL,

        CONSTRAINT PK_asignacion PRIMARY KEY CLUSTERED (id),
        CONSTRAINT CK_asignacion_tipo
            CHECK (tipo IN ('CLASE', 'CUIDO', 'SUSTITUCION')),
        CONSTRAINT CK_asignacion_estado
            CHECK (estado IN ('ACTIVA', 'SUSTITUIDA', 'CANCELADA')),
        CONSTRAINT CK_asignacion_intervalo
            CHECK (fin_utc > inicio_utc),
        CONSTRAINT CK_asignacion_sustitucion
            CHECK ((tipo = 'SUSTITUCION' AND sustituye_id IS NOT NULL)
                OR (tipo IN ('CLASE', 'CUIDO') AND sustituye_id IS NULL)),
        CONSTRAINT CK_asignacion_no_autorreferencia
            CHECK (sustituye_id IS NULL OR sustituye_id <> id),
        CONSTRAINT CK_asignacion_actividad
            CHECK (actividad IS NULL OR LEN(LTRIM(RTRIM(actividad))) > 0),
        CONSTRAINT FK_asignacion_jornada
            FOREIGN KEY (jornada_id) REFERENCES ai.jornada (id),
        CONSTRAINT FK_asignacion_grupo
            FOREIGN KEY (grupo_id) REFERENCES ai.grupo (id),
        CONSTRAINT FK_asignacion_horario_contexto
            FOREIGN KEY (horario_clase_id, grupo_id, personal_id, aula_id)
            REFERENCES ai.horario_clase (id, grupo_id, docente_id, aula_id),
        CONSTRAINT FK_asignacion_personal
            FOREIGN KEY (personal_id) REFERENCES ai.personal (persona_id),
        CONSTRAINT FK_asignacion_aula
            FOREIGN KEY (aula_id) REFERENCES ai.aula (id),
        CONSTRAINT FK_asignacion_sustituye
            FOREIGN KEY (sustituye_id) REFERENCES ai.asignacion (id)
    );

    CREATE UNIQUE INDEX UX_asignacion_activa_exacta
        ON ai.asignacion
            (jornada_id, grupo_id, personal_id, aula_id,
             tipo, inicio_utc, fin_utc)
        WHERE estado = 'ACTIVA';

    CREATE UNIQUE INDEX UX_asignacion_sustitucion_segmento
        ON ai.asignacion (sustituye_id, inicio_utc, fin_utc)
        WHERE estado = 'ACTIVA' AND sustituye_id IS NOT NULL;

    CREATE INDEX IX_asignacion_personal_intervalo
        ON ai.asignacion (personal_id, estado, inicio_utc, fin_utc)
        INCLUDE (jornada_id, grupo_id, aula_id, tipo, sustituye_id);

    CREATE INDEX IX_asignacion_aula_intervalo
        ON ai.asignacion (aula_id, estado, inicio_utc, fin_utc)
        INCLUDE (jornada_id, grupo_id, personal_id, tipo);

    CREATE INDEX IX_asignacion_grupo_jornada_intervalo
        ON ai.asignacion (grupo_id, jornada_id, estado, inicio_utc, fin_utc)
        INCLUDE (personal_id, aula_id, tipo, horario_clase_id);

    CREATE INDEX IX_asignacion_horario
        ON ai.asignacion (horario_clase_id, jornada_id)
        INCLUDE (personal_id, grupo_id, aula_id, tipo, estado)
        WHERE horario_clase_id IS NOT NULL;

    /* ================================================================
       4. Necesidades de cobertura
       ================================================================ */

    CREATE TABLE ai.necesidad_cobertura
    (
        id                          bigint IDENTITY(1,1) NOT NULL,
        asignacion_origen_id        bigint NOT NULL,
        asignacion_cobertura_id     bigint NULL,
        estado                      varchar(12) NOT NULL
            CONSTRAINT DF_necesidad_cobertura_estado DEFAULT ('PENDIENTE'),
        motivo                      nvarchar(500) NOT NULL,
        creada_por                  uniqueidentifier NOT NULL,
        creado_utc                  datetime2(3) NOT NULL
            CONSTRAINT DF_necesidad_cobertura_creado_utc DEFAULT (SYSUTCDATETIME()),
        cerrado_por                 uniqueidentifier NULL,
        cerrado_utc                 datetime2(3) NULL,
        version                     rowversion NOT NULL,

        CONSTRAINT PK_necesidad_cobertura PRIMARY KEY CLUSTERED (id),
        CONSTRAINT UQ_necesidad_cobertura_origen UNIQUE (asignacion_origen_id),
        CONSTRAINT CK_necesidad_cobertura_estado
            CHECK (estado IN ('PENDIENTE', 'ASIGNADA', 'CANCELADA', 'CERRADA')),
        CONSTRAINT CK_necesidad_cobertura_motivo
            CHECK (LEN(LTRIM(RTRIM(motivo))) > 0),
        CONSTRAINT CK_necesidad_cobertura_asignaciones_distintas
            CHECK (asignacion_cobertura_id IS NULL
                OR asignacion_cobertura_id <> asignacion_origen_id),
        CONSTRAINT CK_necesidad_cobertura_contexto_estado
            CHECK ((estado = 'PENDIENTE'
                    AND asignacion_cobertura_id IS NULL
                    AND cerrado_por IS NULL
                    AND cerrado_utc IS NULL)
                OR (estado = 'ASIGNADA'
                    AND asignacion_cobertura_id IS NOT NULL
                    AND cerrado_por IS NULL
                    AND cerrado_utc IS NULL)
                OR (estado IN ('CANCELADA', 'CERRADA')
                    AND cerrado_por IS NOT NULL
                    AND cerrado_utc IS NOT NULL)),
        CONSTRAINT CK_necesidad_cobertura_orden_horas
            CHECK (cerrado_utc IS NULL OR cerrado_utc >= creado_utc),
        CONSTRAINT FK_necesidad_cobertura_origen
            FOREIGN KEY (asignacion_origen_id) REFERENCES ai.asignacion (id),
        CONSTRAINT FK_necesidad_cobertura_asignada
            FOREIGN KEY (asignacion_cobertura_id) REFERENCES ai.asignacion (id)
        /* FK creada_por y cerrado_por -> dbo.AspNetUsers.Id: archivo 007. */
    );

    CREATE UNIQUE INDEX UX_necesidad_cobertura_asignacion
        ON ai.necesidad_cobertura (asignacion_cobertura_id)
        WHERE asignacion_cobertura_id IS NOT NULL;

    CREATE INDEX IX_necesidad_cobertura_estado_fecha
        ON ai.necesidad_cobertura (estado, creado_utc)
        INCLUDE (asignacion_origen_id, asignacion_cobertura_id, motivo);

    CREATE INDEX IX_necesidad_cobertura_creada_por_fecha
        ON ai.necesidad_cobertura (creada_por, creado_utc DESC)
        INCLUDE (estado, asignacion_origen_id, asignacion_cobertura_id);

    CREATE INDEX IX_necesidad_cobertura_cerrado_por_fecha
        ON ai.necesidad_cobertura (cerrado_por, cerrado_utc DESC)
        INCLUDE (estado, asignacion_origen_id)
        WHERE cerrado_por IS NOT NULL;

    /* ================================================================
       5. Verificaciones humanas de cobertura
       ================================================================ */

    CREATE TABLE ai.verificacion_cobertura
    (
        id                  bigint IDENTITY(1,1) NOT NULL,
        asignacion_id       bigint NOT NULL,
        verificado_por      uniqueidentifier NOT NULL,
        estado              varchar(20) NOT NULL,
        observacion         nvarchar(500) NOT NULL,
        ocurrido_utc        datetime2(3) NOT NULL,
        creado_utc          datetime2(3) NOT NULL
            CONSTRAINT DF_verificacion_cobertura_creado_utc
            DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_verificacion_cobertura PRIMARY KEY CLUSTERED (id),
        CONSTRAINT CK_verificacion_cobertura_estado
            CHECK (estado IN ('PRESENCIA_VERIFICADA', 'AUSENCIA_VERIFICADA')),
        CONSTRAINT CK_verificacion_cobertura_observacion
            CHECK (LEN(LTRIM(RTRIM(observacion))) > 0),
        CONSTRAINT FK_verificacion_cobertura_asignacion
            FOREIGN KEY (asignacion_id) REFERENCES ai.asignacion (id)
        /* FK verificado_por -> dbo.AspNetUsers.Id: archivo 007. */
    );

    CREATE INDEX IX_verificacion_cobertura_asignacion_fecha
        ON ai.verificacion_cobertura
            (asignacion_id, ocurrido_utc DESC, creado_utc DESC, id DESC)
        INCLUDE (estado, observacion, verificado_por);

    CREATE INDEX IX_verificacion_cobertura_verificador_fecha
        ON ai.verificacion_cobertura (verificado_por, creado_utc DESC)
        INCLUDE (asignacion_id, estado, ocurrido_utc);

    /* ================================================================
       6. Enlace de eventos con asignaciones
       ================================================================ */

    IF OBJECT_ID(N'ai.FK_evento_asignacion', N'F') IS NULL
    BEGIN
        ALTER TABLE ai.evento WITH CHECK
            ADD CONSTRAINT FK_evento_asignacion
            FOREIGN KEY (asignacion_id) REFERENCES ai.asignacion (id);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'ai.evento')
          AND name = N'IX_evento_asignacion_recibido'
    )
    BEGIN
        CREATE INDEX IX_evento_asignacion_recibido
            ON ai.evento (asignacion_id, recibido_utc DESC, id)
            INCLUDE (accion, persona_id, estacion_id, ocurrido_utc,
                     calidad_hora, estado, codigo_resultado)
            WHERE asignacion_id IS NOT NULL;
    END;

    /* ================================================================
       7. Vista operativa de cobertura

       No transforma ausencia de datos en ausencia de personal. La ultima
       lectura aceptada y la ultima verificacion humana permanecen visibles
       como evidencias distintas. El estado descriptivo usa la mas reciente.
       ================================================================ */

    EXEC(N'
        CREATE VIEW ai.vw_cobertura_aula
        AS
        SELECT
            asi.id AS asignacion_id,
            asi.jornada_id,
            j.fecha_local,
            asi.grupo_id,
            g.ciclo,
            g.grado,
            g.seccion,
            asi.personal_id,
            p.nombres AS personal_nombres,
            p.apellidos AS personal_apellidos,
            asi.aula_id,
            a.codigo AS codigo_aula,
            a.nombre AS nombre_aula,
            asi.tipo AS tipo_asignacion,
            asi.inicio_utc,
            asi.fin_utc,
            asi.estado AS estado_asignacion,
            asi.sustituye_id,
            asi.actividad,
            nc.id AS necesidad_cobertura_id,
            nc.estado AS estado_necesidad,
            nc.asignacion_cobertura_id,
            ue.id AS ultimo_evento_id,
            ue.accion AS ultima_accion_personal,
            ue.ocurrido_utc AS ultimo_evento_ocurrido_utc,
            ue.recibido_utc AS ultimo_evento_recibido_utc,
            ue.calidad_hora AS ultimo_evento_calidad_hora,
            ue.estacion_id AS ultima_estacion_id,
            es.codigo_logico AS ultima_estacion_codigo,
            es.ultima_comunicacion_utc AS ultima_estacion_comunicacion_utc,
            uv.id AS ultima_verificacion_id,
            uv.estado AS ultima_verificacion_estado,
            uv.ocurrido_utc AS ultima_verificacion_ocurrido_utc,
            uv.creado_utc AS ultima_verificacion_creado_utc,
            CASE
                WHEN uv.id IS NOT NULL
                 AND (ue.id IS NULL
                      OR uv.ocurrido_utc >= COALESCE(ue.ocurrido_utc,
                                                     ue.recibido_utc))
                    THEN uv.estado
                WHEN ue.accion = ''LLEGADA_PERSONAL''
                    THEN ''LECTURA_LLEGADA''
                WHEN ue.accion = ''SALIDA_PERSONAL''
                    THEN ''LECTURA_SALIDA''
                ELSE ''SIN_EVIDENCIA''
            END AS evidencia_mas_reciente
        FROM ai.asignacion AS asi
        INNER JOIN ai.jornada AS j
            ON j.id = asi.jornada_id
        INNER JOIN ai.grupo AS g
            ON g.id = asi.grupo_id
        INNER JOIN ai.personal AS per
            ON per.persona_id = asi.personal_id
        INNER JOIN ai.persona AS p
            ON p.id = per.persona_id
        INNER JOIN ai.aula AS a
            ON a.id = asi.aula_id
        LEFT JOIN ai.necesidad_cobertura AS nc
            ON nc.asignacion_origen_id = asi.id
        OUTER APPLY
        (
            SELECT TOP (1)
                ev.id,
                ev.accion,
                ev.ocurrido_utc,
                ev.recibido_utc,
                ev.calidad_hora,
                ev.estacion_id
            FROM ai.evento AS ev
            WHERE ev.asignacion_id = asi.id
              AND ev.accion IN (''LLEGADA_PERSONAL'', ''SALIDA_PERSONAL'')
              AND ev.estado = ''ACEPTADO''
            ORDER BY COALESCE(ev.ocurrido_utc, ev.recibido_utc) DESC,
                     ev.recibido_utc DESC,
                     ev.id DESC
        ) AS ue
        LEFT JOIN ai.estacion AS es
            ON es.id = ue.estacion_id
        OUTER APPLY
        (
            SELECT TOP (1)
                vc.id,
                vc.estado,
                vc.ocurrido_utc,
                vc.creado_utc
            FROM ai.verificacion_cobertura AS vc
            WHERE vc.asignacion_id = asi.id
            ORDER BY vc.ocurrido_utc DESC,
                     vc.creado_utc DESC,
                     vc.id DESC
        ) AS uv
        WHERE asi.estado = ''ACTIVA'';
    ');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

PRINT N'Bloque C de Aula Innova instalado correctamente.';
PRINT N'Objetos: 4 tablas, 16 indices propios, 1 vista y 0 triggers nuevos.';
PRINT N'Integridad adicional: unicidad de incidencia vigente por estudiante, jornada y tipo.';
PRINT N'Catalogo: 68 tipos de incidencia (20 leves, 33 graves y 15 muy graves).';
GO
