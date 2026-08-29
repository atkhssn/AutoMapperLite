# Security Policy

## Supported Versions

Only the latest published version of the `AutoMapperLite` NuGet package receives
security fixes.

| Version | Supported          |
| ------- | ------------------ |
| 3.0.x   | :white_check_mark: |
| < 3.0   | :x:                |

## Reporting a Vulnerability

If you discover a security vulnerability, please report it privately rather
than opening a public GitHub issue.

- Email: atik.hassan@outlook.com
- Include a description of the vulnerability, steps to reproduce, and the
  affected version(s).

You should expect an initial response within a few days. Once a fix is
available, a new patch version will be published to NuGet and the fix will be
noted in the release notes.

## Scope

AutoMapperLite is a reflection-based object mapping library. Reports involving
denial-of-service through pathological mapping configurations, reflection
misuse, or unintended code execution via mapping profiles are in scope.
General usage questions are not security reports — please use GitHub issues
for those.
