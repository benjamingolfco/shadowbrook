#!/bin/bash
# Verify frontend linting and TypeScript compilation pass before the agent finishes

cd "$(git rev-parse --show-toplevel 2>/dev/null || echo '.')"

# Detect package manager (prefer pnpm, fall back to npm)
if command -v pnpm &> /dev/null; then
  PKG_MGR="pnpm --dir src/web"
else
  PKG_MGR="npm --prefix src/web run"
fi

OUTPUT=$($PKG_MGR lint 2>&1)
if [ $? -ne 0 ]; then
  echo "Lint failed. Fix errors before finishing:" >&2
  echo "$OUTPUT" >&2
  exit 2
fi

OUTPUT=$($PKG_MGR build 2>&1)
if [ $? -ne 0 ]; then
  echo "Build failed. Fix TypeScript errors before finishing:" >&2
  echo "$OUTPUT" >&2
  exit 2
fi

exit 0
