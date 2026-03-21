#!/bin/bash
# Verify the solution builds and tests pass before the agent finishes

cd "$(git rev-parse --show-toplevel 2>/dev/null || echo '.')"

OUTPUT=$(dotnet test shadowbrook.slnx 2>&1)
EXIT_CODE=$?

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
