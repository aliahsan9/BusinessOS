# BusinessOS backup and recovery

This is the operator procedure for the SQL Server database. Copying `.mdf` / `.ldf` files is not a backup.

## Targets

| Target | Value | Basis |
|---|---|---|
| Recovery Point Objective | 15 minutes | Requires FULL recovery model and transaction-log backups every 15 minutes |
| Recovery Time Objective | 2 hours | Restore plus application configuration check on a prepared host |

These are operating targets. They are not a tested guarantee until a restore drill is recorded.

## Schedule

1. Full backup every night.
2. Differential backup every 6 hours while the business is open.
3. Transaction-log backup every 15 minutes when the database uses the FULL recovery model.
4. Keep 14 daily full backups, 7 days of differentials, and 7 days of log backups.
5. Store backups outside the database server. Encrypt the backup share. Alert when a job fails or `RESTORE VERIFYONLY` fails.

Confirm the recovery model before relying on log backups:

```sql
SELECT name, recovery_model_desc FROM sys.databases WHERE name = 'BusinessOS';
```

SIMPLE recovery cannot meet a 15-minute RPO. Switch to FULL only after log backups are scheduled, or the log will grow without bound.

## Backup

`deploy/scripts/backup-database.ps1` writes a backup and runs `RESTORE VERIFYONLY`. It does not restore unless `-RestoreTo` is passed, and it refuses to restore over the source database.

```powershell
./deploy/scripts/backup-database.ps1 `
  -Server "localhost" `
  -Database "BusinessOS" `
  -BackupPath "D:\Backups\BusinessOS\BusinessOS-full.bak"
```

## Restore drill

Restore into a new database name. Never use the development or production database name.

```powershell
./deploy/scripts/backup-database.ps1 `
  -Server "localhost" `
  -Database "BusinessOS" `
  -BackupPath "D:\Backups\BusinessOS\BusinessOS-full.bak" `
  -RestoreTo `
  -RestoreDatabase "BusinessOS_RestoreTest"
```

After a drill:

1. Confirm expected tables exist.
2. Confirm a known product, customer, and order row.
3. Read the `DBCC CHECKDB` result.
4. Point a disposable API instance at the restored database and open `/api/health/ready`.
5. Drop only the `*_RestoreTest` database when the drill is finished.

## Application recovery

1. Restore the database to the chosen point in time with the full, differential, and log chain.
2. Restore environment variables and secrets from the secret store. They are not in Git.
3. Start the API with `Database__ApplyMigrationsOnStartup=false` until the restored schema is checked.
4. Apply pending EF migrations only when the restored schema is older than the build being started.
5. Confirm `/api/health/ready` and a tenant login.

## This audit

A restore drill was not executed in this pass. The development database was not backed up or overwritten from this session.
