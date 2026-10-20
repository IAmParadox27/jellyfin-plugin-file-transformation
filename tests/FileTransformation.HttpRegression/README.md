# HTTP regression checks

Run with the .NET 10 SDK:

```bash
dotnet run --project tests/FileTransformation.HttpRegression
```

This console runner compiles the production response middleware and its read
interface directly. It uses ASP.NET Core's real static-file middleware with
temporary fixture files, so no Jellyfin instance, plugin package, listening
server or additional NuGet test dependency is needed.

Coverage includes ordinary and compressed GET/HEAD parity, ignored ranges,
transformed conditional ETags, native behavior for unregistered files,
virtual files, other HTTP methods, non-Web routes and downstream failures.
It does not claim a complete Jellyfin plugin build or live deployment test.

The original-file validator bug tracked in issue #90 / PR #91 is separate.
These checks preserve the existing transformed-ETag behavior; they do not
duplicate that PR's cache-validator fix.
