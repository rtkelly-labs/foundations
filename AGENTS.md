# foundations

Shared build and CI tooling, consumed by pinned commit SHA: C# files through Paket `github` dependencies, and the
reusable workflow through `uses:`. README.md is the consumer contract.

- Everything here is public. Keep it generic, and never name a consuming repo, a person, an email address or private data.
- A change here reaches consumers only when they bump their pin, so keep changes backward compatible or say in the PR how a consumer migrates.
- Never add a root `paket.dependencies`. Paket follows a remote repo's dependencies file transitively into every consumer.
- `tests/consumer` is a synthetic consumer. CI builds it with the shared settings and runs the scripts against its reports. Extend it when you change `dotnet/`.
- Pin every external GitHub Action to a full commit SHA. In workflow `run:` steps, pass inputs through `env:`, never by interpolating them.
- Changes go through PRs with Conventional Commits titles. Squash merge, delete branches, and keep history linear.
- Scratch files and worktrees belong in root `temp/` or `.worktrees/`. Keep a gitignored root TODO.md.

Validate locally from `tests/consumer`:

```sh
dotnet build src/Smoke -c Release
dotnet run ../../dotnet/scripts/coverage-floor.cs -- coverage/coverage.a.json coverage/coverage.b.json
```
