# foundations

Shared build and CI tooling for the rtkelly-labs ecosystem. Today it holds the C# baseline. A repo pulls it in
through mechanisms it already uses, pinned to one commit, instead of keeping its own copy.

| Path | What it is | How a repo uses it |
| --- | --- | --- |
| `dotnet/Common.props` | Shared MSBuild settings: nullable on, warnings as errors, every analyzer on, deterministic builds, isolation from outer checkouts, banned APIs | Imported first from `Directory.Build.props` |
| `dotnet/Common.targets` | Sets `PrivateAssets` on Paket's package references after restore | Imported from `Directory.Build.targets` |
| `dotnet/BannedSymbols.txt` | APIs every repo bans | Added by `Common.props`. A repo adds more in its own root `BannedSymbols.txt` |
| `dotnet/scripts/coverage-floor.cs` | Line and branch coverage ratchet over coverlet JSON reports | Run by the reusable workflow |
| `dotnet/scripts/require-jobs-succeeded.cs` | Fails unless every needed job succeeded or was skipped | `dotnet run` from an aggregate `ci` job |
| `.github/workflows/dotnet-ci.yml` | Reusable workflow: analyze, format, sharded tests with coverage, coverage ratchet | `uses:` from the repo's CI |

## Adopting it in a C# repo

Choose one foundations commit and pin its full SHA everywhere below. One SHA per repo keeps the files and the
workflow in step.

### 1. Fetch the files with Paket

Add a `Build` group to `paket.dependencies`. Do not add `source` or `nuget` lines to it.

```
group Build
  github rtkelly-labs/foundations:<sha> dotnet/Common.props
  github rtkelly-labs/foundations:<sha> dotnet/Common.targets
  github rtkelly-labs/foundations:<sha> dotnet/BannedSymbols.txt
  github rtkelly-labs/foundations:<sha> dotnet/scripts/coverage-floor.cs
  github rtkelly-labs/foundations:<sha> dotnet/scripts/require-jobs-succeeded.cs
```

Then do three things:

- Run `dotnet paket install`. Only the new `GROUP Build` block should appear in `paket.lock`.
- Add `paket-files/` to `.gitignore`.
- Restore with `dotnet paket restore` before anything else evaluates a project. That includes `dotnet restore`, `dotnet build`, `dotnet run` and IDE loads, because `Directory.Build.props` imports the restored files.

```sh
dotnet tool restore && dotnet paket restore
```

### 2. Import the settings

`Directory.Build.props`:

```xml
<Project>
  <Import Project="$(MSBuildThisFileDirectory)paket-files/build/rtkelly-labs/foundations/dotnet/Common.props" />

  <!-- The coverage ratchet reads these literally from this file. -->
  <PropertyGroup>
    <CoverageFloor>80</CoverageFloor>
    <CoverageTarget>95</CoverageTarget>
    <CoverageSlack>2</CoverageSlack>
  </PropertyGroup>
</Project>
```

`Directory.Build.targets`:

```xml
<Project>
  <PropertyGroup>
    <!-- Build-time-only packages that must not flow to referencing projects. The banned-API analyzer is always private. -->
    <PaketPrivatePackages>Microsoft.EntityFrameworkCore.Design</PaketPrivatePackages>
  </PropertyGroup>
  <Import Project="$(MSBuildThisFileDirectory)paket-files/build/rtkelly-labs/foundations/dotnet/Common.targets" />
</Project>
```

Keep these files in the repo itself:

- `global.json`, which the SDK reads before MSBuild runs.
- `.globalconfig`, which `Common.props` reads from the repo root and which must exist only once.
- `.editorconfig`.

The banned list applies only to projects that reference `Microsoft.CodeAnalysis.BannedApiAnalyzers`. List it in
`paket.dependencies` and in each project's `paket.references`.

The import is unconditional on purpose. A checkout that skipped `dotnet paket restore` fails at once with the missing
path, instead of building without the shared settings.

### 3. Call the reusable workflow

```yaml
jobs:
  changes:
    # ... the repo's own change detection, with an output such as `code`
  dotnet:
    needs: changes
    if: needs.changes.outputs.code == 'true'
    permissions:
      contents: read
    uses: rtkelly-labs/foundations/.github/workflows/dotnet-ci.yml@<sha>
    with:
      solution: app.slnx
      shards: >-
        [{"shard": "core", "projects": "tests/App.Core.Tests/App.Core.Tests.csproj"},
         {"shard": "app", "projects": "tests/App.Cli.Tests/App.Cli.Tests.csproj tests/App.Specs/App.Specs.csproj"}]
      test-args: --coverlet-include [App.*]* --coverlet-exclude-by-file **/Migrations/**
  ci:
    if: always()
    needs: [changes, dotnet]
    runs-on: ubuntu-latest
    permissions:
      contents: read
    steps:
      - uses: actions/checkout@<sha>
      - uses: actions/setup-dotnet@<sha>
        with:
          global-json-file: global.json
      - run: dotnet tool restore && dotnet paket restore --group Build
      - env:
          NEEDS_JSON: ${{ toJSON(needs) }}
        run: dotnet run paket-files/build/rtkelly-labs/foundations/dotnet/scripts/require-jobs-succeeded.cs
```

| Input | Default | Meaning |
| --- | --- | --- |
| `solution` | (required) | Solution to restore, analyze and format |
| `shards` | (required) | JSON array of `{shard, projects, playwright?}`. `projects` is space-separated. `playwright: true` installs Chromium after the build |
| `test-args` | `""` | Extra `dotnet test` arguments. Split on whitespace, never globbed |
| `analyze-args` | `""` | Extra arguments for the analyzer build, such as `-warnaserror` |
| `timeout-minutes` | `10` | Per job |
| `foundations-path` | `paket-files/build/rtkelly-labs/foundations` | Where Paket restores the files |

The jobs report as `<caller job> / analyze`, `<caller job> / format`, `<caller job> / dotnet (<shard>)` and
`<caller job> / dotnet-coverage`. Make the aggregate `ci` job the required check, not these.

## Updating

Bump the SHA in three places, all in one pull request:

- the `uses:` ref
- the `paket.dependencies` lines
- `paket.lock`, by running `dotnet paket update --group Build`

Dependency bots do not update Paket `github` pins.

## Licence

MIT. See [LICENSE](LICENSE).
