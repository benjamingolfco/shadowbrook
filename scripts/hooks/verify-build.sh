#!/bin/bash
# Verify the solution builds and tests pass before the agent finishes

cd "$(git rev-parse --show-toplevel 2>/dev/null || echo '.')"

OUTPUT=$(dotnet test shadowbrook.slnx 2>&1)
if [ $? -ne 0 ]; then
  echo "Tests failed. Fix errors before finishing:" >&2
  echo "$OUTPUT" >&2
  exit 2
fi

exit 0
