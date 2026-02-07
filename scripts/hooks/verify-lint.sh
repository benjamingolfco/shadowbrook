#!/bin/bash
# Verify frontend linting passes before the agent finishes

cd "$(git rev-parse --show-toplevel 2>/dev/null || echo '.')"

OUTPUT=$(pnpm --dir src/web lint 2>&1)
if [ $? -ne 0 ]; then
  echo "Lint failed. Fix errors before finishing:" >&2
  echo "$OUTPUT" >&2
  exit 2
fi

exit 0
