# BusinessOS local Docker

This guide runs BusinessOS on a Windows machine. It covers everyday coding, a containerized development stack, local staging, and a local production simulation. Nothing here publishes images or deploys to a cloud host.

The existing Windows database named `BusinessOS` on `localhost\SQLEXPRESS` is left in place. Docker environments use different database names.

## What is already on this machine

These values were read from the project and from Windows. They are not guesses.

| Item | Value |
|---|---|
| API project | `BusinessOS/BusinessOS.API` |
| Solution | `BusinessOS/BusinessOS.slnx` |
| Target framework | `net10.0` (SDK `10.0.401` on this machine) |
| API URL on Windows | `http://localhost:5162` (`Properties/launchSettings.json`) |
| Angular project | `BusinessOS.Web` in `BusinessOS_UI` |
| Package manager | npm, with `package-lock.json` |
| Production output | `dist/business-os.web/browser` |
| Rendering | Client-side. There is no Angular server bundle. |
| Dev API path | `/api/v1`, proxied by `proxy.conf.json` to `http://localhost:5162` |
| SignalR | `/hubs/notifications` |
| Existing development database | `Server=localhost\SQLEXPRESS;Database=BusinessOS;Integrated Security=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout=60` |
| SQL instance used by the app | `SQLEXPRESS` (`MSSQL16.SQLEXPRESS`). `SQLEXPRESS01` and `SQLEXPRESS02` are separate and are not changed. |
| SQL authentication | Mixed mode is already enabled. |
| SQL TCP/IP | Disabled on `SQLEXPRESS`, and SQL Server Browser is stopped. A Linux container cannot use Windows integrated security or shared memory. |
| Qdrant | Used when `Qdrant:Enabled` is true. The client port in configuration is `6334`. |
| Redis, RabbitMQ | Not used. |
| Email, Stripe, JazzCash, EasyPaisa, OpenAI, Cursor AI | Optional. Empty credentials leave those features off. |

## Ports

All published ports bind to `127.0.0.1` only.

| Environment | Frontend | API | Qdrant | Optional SQL container |
|---|---:|---:|---:|---:|
| Workflow A (API and Angular on Windows) | 4200 | 5162 | 6333 and 6334 | none |
| Workflow B (containers, project `businessos-dev`) | 4200 | 5162 | 6333 and 6334 | 14331 |
| Staging (`businessos-staging`) | 4201 | 5102 | not published | 14332 |
| Production simulation (`businessos-prodlocal`) | 4202 | 5103 | not published | 14333 |

Workflow A and workflow B both use 4200, 5162, and 6333. Run one of them, not both. Staging and the production simulation can run beside workflow A when the machine has enough RAM. This computer has 16 GB of RAM. Prefer one full container environment at a time, especially if it includes SQL Server.

## 1. Prerequisites

- Windows 10 Pro
- Docker Desktop, started and left open until the engine is ready
- .NET SDK 10
- Node.js 20 or newer and npm, for workflow A
- The existing SQL Server Express instance, for workflow A

From `BusinessOS`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\check-env.ps1
```

## 2. Create local configuration

From `BusinessOS`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\setup-local.ps1
```

The script creates these files only when they are missing:

- `deploy/env/.env.dev`
- `deploy/env/.env.staging`
- `deploy/env/.env.prod-local`
- `deploy/certs/localhost.crt` and `localhost.key`

Running it again does not replace those files. They are gitignored. A `.env` file is convenient on this machine. It is not a production secret store.

Each generated file has its own JWT signing key and its own SQL password. The default `SQL_MODE` is `container`, so Docker does not connect to the existing `BusinessOS` database.

Staging and the production simulation also trust the generated localhost certificate for HTTPS. If the trust step does not complete, import `deploy/certs/localhost.der` into **Current User > Trusted Root Certification Authorities**. Do not disable certificate validation in the browser or in the application.

Public frontend settings live in:

- `deploy/config/app-config.dev.json`
- `deploy/config/app-config.staging.json`
- `deploy/config/app-config.prod-local.json`

Those files may contain `apiUrl` and `googleClientId` only. Nginx serves the same Angular image everywhere and proxies `/api/` and `/hubs/` to that environment's API, so `apiUrl` stays `/api/v1`.

## 3. Workflow A — everyday coding

Use this while changing C# or Angular. Hot reload and the debugger stay in your normal tools. Docker runs Qdrant only.

```powershell
cd D:\Business_OS\BusinessOS
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\start-dev.ps1 -Workflow A
```

Then, in two other terminals:

```powershell
cd D:\Business_OS\BusinessOS
dotnet run --project BusinessOS.API\BusinessOS.API.csproj --launch-profile http
```

```powershell
cd D:\Business_OS\BusinessOS_UI
npm start
```

Open `http://localhost:4200`. The API is `http://localhost:5162`. Scalar is `http://localhost:5162/scalar`.

`dotnet run` still uses the existing `BusinessOS` database and can apply migrations on startup because `Database:ApplyMigrationsOnStartup` is true in `appsettings.json`. That behavior was not changed. The container environments set the same switch to false.

Stop Qdrant without deleting its data:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\stop-env.ps1 -Environment qdrant
```

## 4. Use SQL Server Express from a container

Do this only if you want `SQL_MODE=host`. The container cannot use integrated security.

1. From an elevated PowerShell, enable TCP on `SQLEXPRESS` only:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\enable-sql-express-tcp.ps1 -Apply -Restart
```

`-Restart` restarts `MSSQL$SQLEXPRESS` only. It does not restart `SQLEXPRESS01` or `SQLEXPRESS02`.

2. Create a SQL login and a new database. Do not grant that login access to `BusinessOS`, `ExamDynamics`, `nopcommerce`, `ProdFalconDb`, or `SupportFlowDb`.

```powershell
sqlcmd -S localhost\SQLEXPRESS -E -Q "CREATE DATABASE [BusinessOS_DockerDev]; CREATE LOGIN [businessos_dev] WITH PASSWORD = '<password>'; USE [BusinessOS_DockerDev]; CREATE USER [businessos_dev] FOR LOGIN [businessos_dev]; ALTER ROLE db_owner ADD MEMBER [businessos_dev];"
```

Use a different database for staging and for the production simulation, and a different login for each.

3. Edit the gitignored env file. Set `SQL_MODE=host`, `SQL_SERVER=host.docker.internal,1433`, and the login you created. Keep `Encrypt=True;TrustServerCertificate=True`. The non-development environments refuse a connection string that contains `Encrypt=False`.

`host.docker.internal` is the Windows host as seen from a container. `localhost` inside a container is the container itself. Migration commands run on Windows, so the migration script rewrites `host.docker.internal` to `localhost` for `sqlcmd` and `dotnet ef`.

If Docker still cannot connect after TCP is enabled, allow inbound TCP 1433 from the Docker/WSL network only. Do not open 1433 to every network.

## 5. Optional SQL Server container

`SQL_MODE=container` starts `mcr.microsoft.com/mssql/server:2022-latest` with a named volume:

| Environment | Volume project prefix | Database | Loopback port |
|---|---|---|---:|
| dev | `businessos-dev` | `BusinessOS_DockerDev` | 14331 |
| staging | `businessos-staging` | `BusinessOS_Staging` | 14332 |
| prod-local | `businessos-prodlocal` | `BusinessOS_ProdLocal` | 14333 |

The image is large (about 1.5 GB) and the container is limited to 2 GB of RAM. `start-env.ps1` refuses to pull it when C: has less than 8 GB free, because the image plus database files need several gigabytes. One container is shared by the services in that environment. Three environments do not need three copies of the image, but three running containers would. Start one environment, test it, then stop it before starting the next if memory is tight.

The loopback port exists so `dotnet ef` on Windows can migrate. It is not published on the LAN. Restarting the container does not delete `sql_data`.

## 6. Start staging

```powershell
cd D:\Business_OS\BusinessOS
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\start-staging.ps1
```

The first run starts SQL Server and Qdrant, then stops before the API if migrations are pending. Apply them and start again:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\migrate-db.ps1 -Environment staging -Action update
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\start-staging.ps1
```

Open `https://127.0.0.1:4201`. The API is `http://127.0.0.1:5102`. Scalar is not registered. `ASPNETCORE_ENVIRONMENT` is `Staging`, so the production configuration guard requires an explicit host list, an HTTPS frontend URL, explicit CORS origins, and an encrypted database connection.

## 7. Start the production simulation

```powershell
cd D:\Business_OS\BusinessOS
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\migrate-db.ps1 -Environment prod-local -Action update
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\start-prod-local.ps1
```

Open `https://127.0.0.1:4202`. The API is `http://127.0.0.1:5103`. `ASPNETCORE_ENVIRONMENT` is `Production`. Scalar and the development admin seeder are off. Payment and email credentials in the container are empty, so this environment cannot charge a card or send mail.

This is a local simulation. It does not prove that a public deployment is ready. HTTPS is terminated by Nginx with a local certificate. The API process itself listens on HTTP inside the Docker network.

## 8. Start containerized development (workflow B)

```powershell
cd D:\Business_OS\BusinessOS
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\migrate-db.ps1 -Environment dev -Action update
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\start-dev.ps1 -Workflow B
```

Open `http://127.0.0.1:4200`. Scalar is available at `http://127.0.0.1:5162/scalar` because this environment uses `Development`.

Workflow B does not mount the source tree. File watchers and hot reload are unreliable for this repository on Windows bind mounts. Rebuild after code changes (section 15). Use workflow A for day-to-day editing.

## 9. Run more than one environment

Start workflow A, then staging, then the production simulation if `docker stats` still shows free memory. Do not start workflow A and workflow B together.

Each environment has its own Compose project, network, and volumes:

- `businessos-dev_net` uses `172.30.10.0/24`
- `businessos-staging_net` uses `172.30.20.0/24`
- `businessos-prodlocal_net` uses `172.30.30.0/24`

Stopping one project does not stop the others.

## 10. Stop and restart

Stop, and keep containers and volumes:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\stop-env.ps1 -Environment dev
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\stop-env.ps1 -Environment staging
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\stop-env.ps1 -Environment prod-local
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\stop-env.ps1 -Environment qdrant
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\stop-env.ps1 -Environment all
```

Remove containers and keep volumes:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\stop-env.ps1 -Environment staging -RemoveContainers
```

Start the same environment again with its start script. SQL Server data is in the named volume `sql_data`. Qdrant data is in `qdrant_data`. Workflow A Qdrant data is in `qdrant_storage`.

`docker compose down -v` deletes those volumes. Do not use it as a stop command.

## 11. Logs, health, and resource use

```powershell
docker compose -p businessos-staging -f compose.yaml -f compose.staging.yaml -f compose.sql.yaml --env-file deploy\env\.env.staging ps
docker compose -p businessos-staging -f compose.yaml -f compose.staging.yaml -f compose.sql.yaml --env-file deploy\env\.env.staging logs -f api
docker stats
```

Leave `-f compose.sql.yaml` out when that environment uses `SQL_MODE=host`.

| Check | URL | Meaning |
|---|---|---|
| Liveness | `/api/health` | The process is serving HTTP. It does not call SQL Server or Qdrant. |
| Readiness | `/api/health/ready` | SQL Server must answer. Qdrant failure is degraded, not fatal, because semantic search is optional. |

Inside the containers, Docker uses the readiness URL for the API and `/nginx-health` for Nginx. Logs are JSON, capped at 10 MB times 3 files per container. The API also writes Serilog files under the `api_logs` volume. Health and request logs do not include the connection string, JWT, or payment fields.

## 12. Migrations

Run these from `BusinessOS`. `list` is read-only. `update` applies migrations to the named environment only. Nothing is dropped.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\migrate-db.ps1 -Environment dev -Action list
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\migrate-db.ps1 -Environment dev -Action update
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\migrate-db.ps1 -Environment staging -Action update
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\migrate-db.ps1 -Environment prod-local -Action update
```

The existing development database:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\migrate-db.ps1 -Environment host -Action list
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\migrate-db.ps1 -Environment host -Action update -ConfirmExistingDevelopmentDatabase
```

`update` against `host` will not run unless `-ConfirmExistingDevelopmentDatabase` is present.

An idempotent SQL script, for review before it is applied by hand:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\migrate-db.ps1 -Environment staging -Action script
```

The script is written to `deploy/migrations/`, which is gitignored. `dotnet ef` 10 is the migration tool. The API image does not run migrations on startup.

The script refuses `BusinessOS` and the other databases already present on this SQL Server Express instance (`ExamDynamics`, `nopcommerce`, `ProdFalconDb`, `SupportFlowDb`, and the system databases).

## 13. Backup and restore

Take a backup before a migration you care about. Restore overwrites the database you name. Do not restore a staging backup onto `BusinessOS`.

Windows SQL Server Express, from a path the SQL Server service account can write:

```powershell
sqlcmd -S localhost\SQLEXPRESS -E -Q "BACKUP DATABASE [BusinessOS] TO DISK = N'C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\BusinessOS.bak' WITH INIT, CHECKSUM"
sqlcmd -S localhost\SQLEXPRESS -E -Q "RESTORE DATABASE [BusinessOS] FROM DISK = N'C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\BusinessOS.bak' WITH REPLACE"
```

The `RESTORE` statement above replaces the existing development database. Run it only when that is the intended result.

SQL Server container:

```powershell
docker exec businessos-dev-sqlserver-1 mkdir -p /var/opt/mssql/backup
docker exec businessos-dev-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $env:SQL_SA_PASSWORD -C -Q "BACKUP DATABASE [BusinessOS_DockerDev] TO DISK = N'/var/opt/mssql/backup/BusinessOS_DockerDev.bak' WITH INIT, CHECKSUM"
docker cp businessos-dev-sqlserver-1:/var/opt/mssql/backup/BusinessOS_DockerDev.bak .\deploy\backups\
```

Set `SQL_SA_PASSWORD` in that terminal from the gitignored env file. Do not paste the password into a command that you commit. If `sqlcmd` is not at the `mssql-tools18` path, use `/opt/mssql-tools/bin/sqlcmd` and omit `-C`.

Copy a backup back into the container before `RESTORE`. Restoring replaces only the database named in the statement.

## 14. Troubleshooting

**Port already in use.** `check-env.ps1` lists the listeners. Stop the other environment, or stop the Windows `dotnet` and `ng serve` processes, then start again.

**API container restarts.** `docker logs businessos-dev-api-1` shows the reason. A missing JWT of at least 32 bytes, an empty connection string, or a pending migration are the usual causes. Staging and production also refuse `AllowedHosts=*`, an empty CORS list, `Encrypt=False`, and a frontend URL that does not start with `https://`. The API also exits before it listens if SQL Server is unreachable, because role seeding runs during startup. That is expected until the database container is healthy and migrations have been applied. Stop that API with `stop-env.ps1` rather than leaving it restarting.

**Not enough disk for SQL Server.** `start-env.ps1` will not pull the SQL Server image when C: has less than 8 GB free. Qdrant and the web container can still be started on their own. The API needs the database before it will stay running.

**SQL Express from Docker fails.** TCP is disabled until `enable-sql-express-tcp.ps1 -Apply -Restart` has been run. Integrated security from a Linux container will not work. Use a SQL login and `host.docker.internal,1433`.

**Browser warns on staging or the production simulation.** Trust `deploy/certs/localhost.der`, then restart the browser.

**Frontend calls the wrong API.** Each Nginx server proxies `/api` and `/hubs` to the `api` service on its own network. `/app-config.json` must keep `apiUrl` at `/api/v1` unless you intentionally set an absolute URL. Staging and production simulation reject an `apiUrl` that points at port 5162 or 4200.

**A route refresh returns 404.** Nginx `try_files` falls back to `index.html`. Rebuild the web image if an older config is running.

## 15. Rebuild after code changes

```powershell
docker compose -p businessos-dev -f compose.yaml -f compose.dev.yaml -f compose.sql.yaml --env-file deploy\env\.env.dev up -d --build
```

Omit `-f compose.sql.yaml` for `SQL_MODE=host`. Repeat with `businessos-staging` or `businessos-prodlocal` and the matching override file. The web image is built from `..\BusinessOS_UI`.

Workflow A does not need an image rebuild. Restart `dotnet run` or `ng serve`.

## 16. Clean up without deleting data

```powershell
docker image prune
docker builder prune
```

`docker image prune` removes dangling images. It does not remove named volumes. To see volumes:

```powershell
docker volume ls
```

Remove a volume only when you mean to delete that environment's database or Qdrant data, and only after the containers that use it are stopped. Example for a volume you have already decided to discard:

```powershell
docker volume rm businessos-staging_sql_data
```

That command deletes the staging SQL Server data files. It does not affect `BusinessOS` on SQL Server Express.

## 17. External services

These features stay inactive until their secrets are added to the gitignored env file. Do not put them in an image or in a committed file.

| Setting | Feature |
|---|---|
| `AI_API_KEY` | Cursor AI API |
| `OPENAI_API_KEY` | OpenAI chat, embeddings, and speech |
| `GOOGLE_CLIENT_ID` | Google sign-in. The same public client id belongs in `app-config.json` and `Google__ClientId`. |
| Email settings | Password reset mail. `Email__Enabled` is false in the containers. |
| Stripe, JazzCash, EasyPaisa | Payments. Provider secrets are empty and the providers are disabled. Do not use production payment keys in these environments. |

Qdrant runs locally and does not need a cloud key. Semantic search stays degraded if Qdrant is down. The ERP still starts.

## Images and update policy

| Image | Use |
|---|---|
| `mcr.microsoft.com/dotnet/sdk:10.0` | Build |
| `mcr.microsoft.com/dotnet/aspnet:10.0` | API runtime, user `app` |
| `node:22-bookworm-slim` | Angular build |
| `nginxinc/nginx-unprivileged:1.28-alpine` | Frontend runtime |
| `qdrant/qdrant:v1.15.4` | Vector search |
| `mcr.microsoft.com/mssql/server:2022-latest` | Optional SQL Server |

Rebuild images when you want a newer base. Do not use `latest` for the application images. The SQL Server tag floats inside the 2022 line. Pin a CU tag later if you need a repeatable pull.

The API container is not read-only because Serilog writes to `/app/Logs` and the runtime uses temporary files. Nginx is read-only aside from `/tmp` and `/var/cache/nginx`. No container is privileged, and the Docker socket is not mounted.
