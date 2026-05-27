# NxGrid — Contributing

---

## How to build and use the package locally

Use this workflow when you want to test NxGrid changes in another project on the same machine before publishing.

### 1. Build and pack

```bash
dotnet build src/NxGrid/NxGrid.csproj -c Release
dotnet pack  src/NxGrid/NxGrid.csproj -c Release --no-build -o build/nupkg
```

The `.nupkg` file lands in `build/nupkg/`.

### 2. Add a local NuGet source

Register the output folder as a NuGet source once — this persists across projects on your machine. Replace `<repo-root>` with the absolute path to where you cloned this repo (e.g. `C:\Users\you\source\repos\nxgrid`):

```bash
dotnet nuget add source "<repo-root>\build\nupkg" --name NxGridLocal
```

Or drop a `nuget.config` next to the consuming project's `.sln` so the local source is scoped to that solution only:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="NxGridLocal" value="<repo-root>\build\nupkg" />
    <add key="nuget.org"   value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

### 3. Reference the package

Add the package reference to the consuming project:

```bash
dotnet add package NxGrid --version 0.1.0
```

Or edit the `.csproj` directly:

```xml
<PackageReference Include="NxGrid" Version="0.1.0" />
```

### 4. Iterating on changes

Each time you change NxGrid source, rebuild and repack (step 1). NuGet copies local packages into the global packages cache (`~/.nuget/packages/`), so the local folder is not re-read on subsequent restores. Clear the cached entry after each repack:

```powershell
Remove-Item "$env:USERPROFILE\.nuget\packages\nxgrid" -Recurse -Force
```

Or combine pack and cache-clear into one command:

```powershell
dotnet pack src/NxGrid/NxGrid.csproj -c Release --no-build -o build/nupkg; Remove-Item "$env:USERPROFILE\.nuget\packages\nxgrid" -Recurse -Force
```

Then run `dotnet restore` in the consuming project and it will pick up the new `.nupkg` from your local source. There is no need to bump the version number for local iteration.

---

## How to publish the package to NuGet.org

### 1. Set the version

Update `VersionPrefix` (and optionally `VersionSuffix` for pre-releases) in `src/NxGrid/NxGrid.csproj`:

```xml
<VersionPrefix>1.0.0</VersionPrefix>
<!-- pre-release: <VersionSuffix>beta.1</VersionSuffix> -->
```

### 2. Build and pack

```bash
dotnet build src/NxGrid/NxGrid.csproj -c Release
dotnet pack  src/NxGrid/NxGrid.csproj -c Release --no-build -o build/nupkg
```

### 3. Get a NuGet API key

1. Sign in at [nuget.org](https://www.nuget.org).
2. Go to **Account settings → API keys → Create**.
3. Scope the key to the `NxGrid` package ID (or `*` for all packages you own).
4. Copy the key — it is only shown once.

### 4. Push the package

```bash
dotnet nuget push "build/nupkg/NxGrid.1.0.0.nupkg" \
  --api-key <YOUR_API_KEY> \
  --source https://api.nuget.org/v3/index.json
```

Replace `1.0.0` with the actual version in the filename. The package is typically available on nuget.org within a few minutes.

### 5. Verify

```bash
dotnet nuget search NxGrid --source https://api.nuget.org/v3/index.json
```

Or check the package page directly at `https://www.nuget.org/packages/NxGrid`.

### Pre-release packages

Append a suffix to produce a pre-release version (`-alpha.1`, `-beta.2`, `-rc.1`). Consumers must opt in to pre-releases explicitly:

```bash
dotnet add package NxGrid --version 1.0.0-beta.1 --prerelease
```
