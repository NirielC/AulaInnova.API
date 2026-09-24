# Aula Innova — API

## Requisitos

- Visual Studio 2022 (con carga de trabajo "ASP.NET y desarrollo web") **o** .NET SDK 8/9 + terminal.
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

Swagger queda en `https://localhost:{puerto}/swagger` (el puerto exacto lo
dice la consola al arrancar).

### 7. Primer login

La API crea automáticamente un usuario administrador al arrancar (ver
`BootstrapAdmin` en `appsettings.json`):

```
Usuario:  admin
Password: Admin#2026!
```

Con eso ya pueden pedir un token en `POST /api/Auth/login` y probar el resto
de endpoints desde Swagger con el botón "Authorize".

## Nota sobre las estaciones NFC

Los endpoints `POST /api/Attendance`, `POST /api/Movement/exit`,
`POST /api/Movement/return` y `POST /api/Visual/measurements` no usan el
login normal — los llama el dispositivo físico, autenticado con un secreto
propio (`X-Estacion-Id` + `X-Estacion-Clave`). Para generar ese secreto,
como administrador:

```
POST /api/admin/estaciones/{id}/secret
```

La respuesta trae el secreto en claro una sola vez — guárdenlo, no se puede
volver a consultar.
