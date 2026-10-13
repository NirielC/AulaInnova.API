# Aula Innova — API

## Requisitos

- Visual Studio 2022 17.14+ (con carga de trabajo "ASP.NET y desarrollo web") **o** .NET SDK 10 + terminal (el proyecto es `net10.0`).
- SQL Server 2022 (16.x) o superior. La opción más simple si no tienen uno propio:
  SQL Server LocalDB, que ya viene instalado con Visual Studio
  (`(localdb)\MSSQLLocalDB`). Verifiquen la versión con `SELECT @@VERSION;`
  antes de correr los bloques — si su LocalDB es más viejo que 2022, instalen
  o usen otra instancia SQL Server 2022.
- SQL Server Management Studio (SSMS) o la extensión de SQL de VS Code, para
  correr los scripts de `sql/`.

## Paso a paso (una sola vez por persona)

### 1. Clonar el repositorio

```
git clone https://github.com/NirielC/AulaInnova.API.git
cd AulaInnova.API
```

### 2. Crear la base de datos y correr los bloques SQL, en este orden exacto

Abran cada archivo de `sql/` en SSMS **completo** (sin seleccionar fragmentos)
contra su instancia local, en este orden:

```
sql/01_bloque_a.sql
sql/02_bloque_b.sql
sql/03_bloque_c.sql
sql/04_bloque_d.sql
```

Esto crea la base `AulaInnova` y todo el esquema `ai.*` (personas, aulas,
asistencia, incidencias, cobertura, visión por zonas, etc.). Ninguno de estos
cuatro archivos toca `dbo.AspNetUsers` todavía — eso lo crea la API en el
paso 4.

### 3. Revisar la cadena de conexión

`AulaInnova.API/appsettings.json` ya viene con una cadena que funciona para
LocalDB:

```
Server=(localdb)\MSSQLLocalDB;Database=AulaInnova;Trusted_Connection=True;TrustServerCertificate=True;
```

Si alguien usa una instancia con nombre distinto (por ejemplo
`.\SQLEXPRESS`), edítenla solo en su copia local — no hace falta subir ese
cambio.

> Nota: como es un proyecto escolar y no va a producción, el JWT secret y el
> usuario administrador de arranque (`BootstrapAdmin`) también quedan
> commiteados en `appsettings.json` para que a nadie le falte configuración.
> Si en algún momento esto se vuelve algo "real", roten `Jwt:Secret` y saquen
> esas credenciales del repo con `dotnet user-secrets`.

### 4. Restaurar paquetes y generar/aplicar la migración de Identity

Desde la carpeta `AulaInnova.API/` (donde está el `.csproj`):

```
dotnet restore
dotnet ef database update
```

`dotnet restore` baja los paquetes NuGet (necesita internet la primera vez;
Visual Studio lo hace solo al abrir el `.sln`). `dotnet ef database update`
aplica la migración que ya está en el repo (`Migrations/`) — **no hace falta
borrarla ni regenerarla**: a propósito solo contiene las tablas de Identity
(`AspNetUsers`, roles, etc.) más la columna `PersonaId` y su FK hacia
`ai.persona`. Todo lo demás (`ai.*`) lo maneja el SQL de los bloques, no EF.

Si no tienen la herramienta de EF instalada:

```
dotnet tool install --global dotnet-ef
```

### 5. Correr los scripts de integración con Identity

Ahora que ya existe `dbo.AspNetUsers`, en este orden:

```
sql/05_identity_bloque_a.sql
sql/06_identity_bloque_b.sql
sql/07_identity_bloque_c.sql
sql/08_identity_bloque_d.sql
sql/09_ajuste_auditoria_entidad_usuario.sql
```

Estos agregan las FK cruzadas entre `ai.*` y `dbo.AspNetUsers` (quién creó
qué, quién autorizó qué), y el último ajusta el catálogo de auditoría para
poder auditar altas de usuarios.

### 6. Levantar la API

Desde Visual Studio: abran `AulaInnova.sln` y usen el botón verde (perfil
`https`). Desde terminal:

```
dotnet run --project AulaInnova.API
```

Para que la **estación NFC y la cámara** la vean desde la red (no solo desde
`localhost`):

```
dotnet run --project AulaInnova.API --urls http://0.0.0.0:5077
```

No hay Swagger UI: el documento OpenAPI queda en `/openapi/v1.json` (en
Development). Para probar endpoints usen los archivos `.http`
(`AulaInnova.API/AulaInnova.API.http` y `dispositivos/configurar_sistema.http`)
con Visual Studio o VS Code.

### 7. Primer login

La API crea automáticamente un usuario administrador al arrancar (ver
`BootstrapAdmin` en `appsettings.json`):

```
Usuario:  admin
Password: Admin#2026!
```

Con eso ya pueden pedir un token en `POST /api/Auth/login` y probar el resto
de endpoints con los archivos `.http`.

### 8. Permisos del administrador

Varios endpoints piden un permiso de `ai.concesion` (por ejemplo
`VISION_CONFIGURAR` para zonas, indicadores y sesiones visuales). Con la API
ya arrancada una vez (para que exista el usuario `admin`), corran:

```
sql/10_concesiones_administrador.sql
```

Da todos los permisos, con ámbito INSTITUCION, a cada usuario con rol
Administrador. Se puede volver a correr cuando se cree otro administrador.

### 9. Jornadas y clases

La asistencia y la cámara necesitan una **jornada** por día y sus **clases**
(asignaciones). No hace falta crearlas una por una:

```
POST /api/admin/jornadas/rango
{ "desde": "2026-10-13", "hasta": "2026-11-30", "horaInicio": "06:30:00",
  "horaFin": "17:00:00", "horaLimitePuntual": "07:00:00", "dias": [1,2,3,4,5] }
```

Crea las jornadas del periodo y, para cada día, las asignaciones tipo CLASE a
partir del horario semanal (`dia_semana` 1 = lunes … 7 = domingo).

## Estaciones NFC y cámara

Firmware, módulo de visión, puesta en marcha y cómo viajan los datos hasta la
base: carpeta [`dispositivos/`](dispositivos/README.md), en especial
[`dispositivos/FLUJO_DE_DATOS.md`](dispositivos/FLUJO_DE_DATOS.md).

Los endpoints que llaman los dispositivos (`/api/estacion/*`,
`POST /api/Attendance`, `POST /api/Movement/exit|return`,
`POST /api/Visual/measurements`) no usan el login normal: cada estación se
autentica con su propio secreto (`X-Estacion-Id` + `X-Estacion-Clave`). Para
generarlo, como administrador:

```
POST /api/admin/estaciones/{id}/secret
```

La respuesta trae el secreto en claro una sola vez — guárdenlo, no se puede
volver a consultar. Los errores de negocio vuelven como
`400 { "codigo": "SIN_MATRICULA", "mensaje": "..." }` para que la estación
muestre el motivo.
