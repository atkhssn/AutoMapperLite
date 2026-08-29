# Contributing to AutoMapperLite

Thanks for your interest in contributing! AutoMapperLite is a small, focused library, so the bar for contributions is simplicity and correctness.

## Getting started

```bash
git clone https://github.com/atkhssn/AutoMapperLite.git
cd AutoMapperLite
dotnet build
dotnet test
```

## Making changes

1. Fork the repository and create a branch from `main` for your change.
2. Keep changes focused — one logical change per pull request.
3. Match the existing code style (nullable reference types enabled, file-scoped where already used, no unnecessary abstractions).
4. Add or update unit tests in `AutoMapperLite.Tests` for any behavioral change.
5. Update `README.md` if you change or add public API.
6. Run `dotnet test` and `dotnet build` before submitting — both must pass.

## Pull requests

- Describe what the change does and why.
- Reference any related issue.
- Keep the public API (`IMapper`, `IMapperConfig`, `MapBuilder<,>`, `Profile`) stable unless the PR is explicitly about a breaking change — call this out clearly in the PR description if so.

## Reporting bugs / requesting features

Open a GitHub issue with a minimal repro (source/destination types and the mapping profile involved) for bugs, or a clear description of the use case for feature requests.

## Code of Conduct

This project follows the [Code of Conduct](CODE_OF_CONDUCT.md). By participating, you agree to abide by its terms.
