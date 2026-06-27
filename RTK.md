# Repo Token Rules

Use `codegraph` first for code navigation in this repo.

- Prefer `codegraph explore <question>` or `codegraph node <symbol>` before `rg`, `find`, or broad file reads.
- Use `rtk` for shell commands when you want compact output.
- Keep commands narrow and avoid dumping full files unless the exact lines are needed.

Examples:

```powershell
codegraph explore "where is auth seeded"
codegraph node "DbSeeder"
rtk git status
rtk dotnet build backend\Quan4CulinaryTourism.Api\Quan4CulinaryTourism.Api.csproj
rtk npm run build
```

Useful `rtk` commands:

```powershell
rtk gain
rtk config
rtk trust
rtk proxy <command>
```
