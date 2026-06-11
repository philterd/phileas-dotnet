# Contributing to Phileas (.NET)

Thanks for your interest in contributing to **phileas-dotnet**, the .NET port of
[Phileas](https://github.com/philterd/phileas).

## Code of Conduct

In the interest of fostering an open and welcoming environment, we as contributors and maintainers pledge to
make participation in our project and community a harassment-free experience for everyone. Please read and
follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## GitHub Workflow

We take contributions as GitHub pull requests:

1. Create a fork of [philterd/phileas-dotnet](https://www.github.com/philterd/phileas-dotnet).
2. Create a feature branch.
3. Build and test your local changes (see below).
4. Commit your changes (include the Apache 2.0 license header on any new source files — see existing files).
5. Open a pull request and participate in code review.

## Prerequisites

* The **.NET 10 SDK** (the repo targets `net10.0`; see `global.json`).
* The **`phisql` repository checked out as a sibling directory** (see below) — this is required to build.
* *(Optional)* **Docker**, if you would rather build without installing the .NET SDK.

### The `phisql` sibling repository

phileas-dotnet is built on [PhiSQL](https://github.com/philterd/phisql) for all policy operations. Because
PhiSQL is not yet published to NuGet, `src/Phileas/Phileas.csproj` references it with a relative
`ProjectReference` (`../../../phisql/reference/dotnet/PhiSql/PhiSql.csproj`). **You must clone `phisql`
next to this repository, in a directory named `phisql`:**

```shell
git clone https://www.github.com/philterd/phileas-dotnet
git clone https://github.com/philterd/phisql

# Resulting layout:
#   code/
#   ├── phileas-dotnet/   <- this repository
#   └── phisql/        <- sibling, so the ProjectReference resolves
```

The CI workflow (`.github/workflows/build.yml`) does the same thing — it checks out `philterd/phisql` as a
sibling before building.

## Building and Testing

With the .NET SDK installed (and `phisql` checked out as a sibling):

```shell
dotnet build Phileas.slnx
dotnet test  Phileas.slnx
```

### Building with Docker (no .NET SDK required)

If you have Docker but not the .NET SDK, use the provided script, which builds and tests inside the official
.NET 10 SDK image:

```shell
./build.sh            # Release (default)
./build.sh Debug      # Debug
```

It mounts the parent directory of both repos so the `phisql` sibling reference resolves inside the container.

### Native dependencies (PDF redaction)

PDF redaction rasterizes pages using PDFium (via PDFtoImage) and SkiaSharp, which include native binaries. On
Linux you may need the appropriate `SkiaSharp.NativeAssets.Linux*` package for your environment; the test
project already references it. The PDFium native library is **not** thread-safe, so the PDF tests run in a
non-parallel xUnit collection — keep new PDF tests in that `[Collection("Pdf")]` collection.

## Coding Guidelines

* **Keep the build warning-free.** `dotnet build` should report **0 warnings**. If a third-party analyzer
  warning is unavoidable for a specific tree, scope a suppression with a `.editorconfig` in that directory
  (see the existing examples under `src/Phileas/Services/Pdf/` and `src/Phileas/Data/`).
* **Match the surrounding code.** Follow the existing naming, comment density, and idioms of the file you are
  editing.
* **Add the Apache 2.0 license header** to any new source file (copy it from an existing file).
* **Write tests for new behavior.** Tests use [xUnit](https://xunit.net/) and live in
  `tests/Phileas.Tests/`. New filters, strategies, services, and bug fixes should come with tests that assert
  on behavior, not just construct the type.
* **Keep the docs in sync.** User documentation lives in `docs/` (published with MkDocs via `mkdocs.yml`). If
  you change public API or behavior, update the relevant page.

## A Note on Parity with the Java Implementation

phileas-dotnet aims to stay close to the Java [Phileas](https://github.com/philterd/phileas) reference
implementation, but it may never reach exact, one-to-one parity: the two libraries serve overlapping but not
identical use cases, and the Java and .NET ecosystems differ (available libraries, APIs, platform
conventions). When porting a feature, prefer the idiomatic .NET approach over a literal translation, and note
any intentional divergence in the code and/or docs.

## Reporting Issues

Please open issues on the [phileas-dotnet issue tracker](https://www.github.com/philterd/phileas-dotnet/issues). For
bugs, include the policy (or a minimal reproduction of it), the input, the expected and actual output, and
your platform.
