---
name: RHF register ref must not be split with useCallback and empty deps
description: Splitting form.register()'s ref into a useCallback with empty [] causes stale ref after form.reset(), making teeTime undefined on second submit
type: feedback
---

Never split `form.register('field')` ref into a `useCallback` with empty `[]` deps. `form.register` returns a fresh ref callback on every render. After `form.reset()`, a new ref callback is returned — if it's not forwarded to the DOM element, RHF loses the element reference and reads the field value as `undefined` on the next submit.

**Why:** Discovered in PostTeeTimeForm. The original code did `const { ref: rhfRef, ...teeTimeRegister } = form.register('teeTime')` then wrapped `rhfRef` in a `useCallback([], [])`. After `form.reset()` caused a re-render, the new `rhfRef` was never called with the DOM element (React doesn't re-invoke a ref callback unless its function reference changes). Next submit got `teeTime: undefined` from RHF, producing a Zod parse error.

**How to apply:** Spread `form.register('field')` results directly onto the input — including the `ref` — without splitting. If focus management is needed after reset, use `form.setFocus('fieldName')` (RHF API) rather than a manually tracked `useRef<HTMLInputElement>`. This eliminates the ref-forwarding problem and is React 19 lint compliant.
