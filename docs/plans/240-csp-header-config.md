# Plan: Make CSP Auth Domain Configurable (#240)

## Problem

`Program.cs` hardcodes `https://login.microsoftonline.com` in the CSP `connect-src` and `frame-src` directives (lines 180-181). The same domain already exists in `appsettings.json` at `AzureAd:Instance`. It should be read from config, not duplicated.

## Approach

Derive the auth domain from `AzureAd:Instance` (already configured). No new config key needed -- the value is already there with a trailing slash (`https://login.microsoftonline.com/`).

## Changes

**Modify: `src/backend/Shadowbrook.Api/Program.cs`**

In the CSP middleware (lines 172-183):

1. Read `AzureAd:Instance` from `app.Configuration` and trim the trailing slash
2. Interpolate it into the `connect-src` and `frame-src` directives instead of the hardcoded URL

Pseudocode:
```
var authInstance = app.Configuration["AzureAd:Instance"]?.TrimEnd('/') ?? "https://login.microsoftonline.com";

// Then in the CSP string:
$"connect-src 'self' {authInstance}; " +
$"frame-src {authInstance};"
```

No other files need changes. No migration. No new config keys.
