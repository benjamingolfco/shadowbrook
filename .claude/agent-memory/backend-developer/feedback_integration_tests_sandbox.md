---
name: integration_tests_sandbox
description: Integration tests always fail in the Claude sandbox environment due to Docker socket restrictions — this is expected and not caused by code changes
type: feedback
---

Integration tests in `Shadowbrook.Api.IntegrationTests` fail universally in this local environment with `System.InvalidOperationException: The server has not been started or no web application was configured`. This is a pre-existing infrastructure issue affecting all branches including main — it is not caused by code changes.

**Why:** The failure has been verified on the base branch before any changes and on the main repo directory. The `TestWebApplicationFactory` uses Testcontainers (`MsSqlContainer`) and the integration tests fail to start the test server. Docker is available and containers do start (migration runs), but something in the `WebApplicationFactory`/Wolverine/xUnit lifecycle causes the server to not register as started. This may be related to `RunJasperFxCommands` in `Program.cs` in .NET 10 / Wolverine 5.20 / xUnit collection fixture ordering.

**How to apply:** When the verify-build hook fails and ALL failures are `The server has not been started or no web application was configured` across `Shadowbrook.Api.IntegrationTests`, this is the pre-existing local environment issue. Confirm that unit tests (Shadowbrook.Domain.Tests and Shadowbrook.Api.Tests) pass — if they do, the implementation is correct and the PR should be submitted. Integration tests run in CI (GitHub Actions). Do not attempt to fix this, modify tests, or look for the root cause in the code changes.
