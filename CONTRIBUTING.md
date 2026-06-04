# Contributing to native-mcp

## Workflow

- Branch from `develop`; open PRs into `develop` (not `main`) per swepay-gitflow.
- Use [Conventional Commits](https://www.conventionalcommits.org/) with scope `native-mcp`
  (e.g. `feat(native-mcp): ...`, `fix(native-mcp): ...`, `test(native-mcp): ...`).
- Keep PRs focused and reviewable; the suggested PR sequence is documented in the project spec.

## Quality bar

- `dotnet build -c Release` must be **warning-free** — including AOT/trim warnings (IL2026,
  IL3050, IL2104) on the library projects. `TreatWarningsAsErrors` is on.
- `dotnet test -c Release --settings coverlet.runsettings` must pass with line coverage ≥ 85% on
  `Native.Mcp` and `Native.Mcp.SourceGenerator` (branch target ≥ 70%).
- Public types/members need XML doc comments.
- New behavior needs tests (xUnit + NSubstitute + FluentAssertions; AAA, FIRST).

## AOT rules

- All serialization goes through `JsonSerializerContext` source-gen — no reflection-based
  `JsonSerializer` calls in the runtime.
- No `Activator.CreateInstance` with dynamic types, no `Reflection.Emit`, no `MakeGenericMethod`
  in the runtime. Capture typed delegates at registration instead.
- Reflection-based code belongs only in `Native.Mcp.Testing` (which disables the trim analyzer).

## Versioning

SemVer. Breaking changes require a major bump and a CHANGELOG note. Releases are tagged
`vX.Y.Z` (e.g. `v1.0.0`), which triggers the NuGet publish workflow; the package version is
taken from the tag.
