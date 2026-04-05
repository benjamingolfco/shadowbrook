#!/bin/bash
# Verify the solution builds and unit tests pass before the agent finishes.
# Integration tests (Shadowbrook.Api.IntegrationTests) require Docker
# networking via Testcontainers and only run reliably in CI (GitHub Actions).
# They are excluded here to avoid false negatives in the local environment.

cd "$(git rev-parse --show-toplevel 2>/dev/null || echo '.')"

OUTPUT1=$(dotnet test tests/Shadowbrook.Domain.Tests 2>&1)
EXIT_CODE1=$?
OUTPUT2=$(dotnet test tests/Shadowbrook.Api.Tests 2>&1)
EXIT_CODE2=$?
OUTPUT="$OUTPUT1
$OUTPUT2"
EXIT_CODE=$(( EXIT_CODE1 | EXIT_CODE2 ))

# Check for actual test failures in the summary lines.
# TaskCanceledException cleanup failures cause a non-zero exit but are not test
# failures — the summary line still shows "Passed!" with "Failed: 0".
# Only fail if a summary line explicitly reports failed tests.
if echo "$OUTPUT" | grep -qE "^Failed!|Failed:\s+[1-9]"; then
  echo "Tests failed. Fix errors before finishing:" >&2
  echo "$OUTPUT" >&2
  exit 2
fi

# If the exit code was non-zero for reasons other than test failures (e.g.,
# build errors, no test assemblies found), surface that too.
if [ $EXIT_CODE -ne 0 ] && ! echo "$OUTPUT" | grep -qE "^Passed!"; then
  echo "Build or test run failed. Fix errors before finishing:" >&2
  echo "$OUTPUT" >&2
  exit 2
fi

exit 0
