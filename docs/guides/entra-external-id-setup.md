# Entra ID Setup Guide

This guide walks through the Azure portal configuration required to set up authentication for Teeforce using Microsoft Entra ID.

## Architecture

Teeforce uses two types of Entra tenants for different audiences:

| Tenant | Type | Audience | Purpose |
|--------|------|----------|---------|
| Benjamin Golf Co (`benjamingolfco.onmicrosoft.com`) | Workforce | Developers, operators | Non-prod and operator-facing auth. Invite-only. |
| *(future)* Production CIAM | External ID (CIAM) | Golfers, public | Customer self-service sign-up for production. |

The workforce tenant is for controlled access — only users you explicitly invite can sign in. The CIAM tenant (future) is for consumer-facing sign-up where golfers register themselves.

## Prerequisites

- An Azure subscription
- Permissions to create tenants and app registrations (Application Developer role or higher)

---

## Step 1: Create a Workforce Tenant

> Skip this step if you already have a workforce tenant.

1. Sign in to the [Microsoft Entra admin center](https://entra.microsoft.com).
2. Navigate to **Entra ID** > **Manage tenants** > **Create**.
3. Choose **Workforce** as the tenant type.
4. Fill in the tenant details:
   - **Organization name:** Benjamin Golf Co
   - **Initial domain name:** `benjamingolfco` (becomes `benjamingolfco.onmicrosoft.com`)
   - **Location:** Select your region
5. Click **Review + Create**, then **Create**.
6. Note the **Tenant ID** from the Overview page.
7. If the tenant creator's `mail` property is null (common for personal Microsoft accounts), set it via Graph API:
   ```bash
   TOKEN=$(az account get-access-token --tenant <tenant-id> --resource https://graph.microsoft.com --query accessToken -o tsv)
   curl -X PATCH -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
     -d '{"mail": "your-email@example.com"}' \
     "https://graph.microsoft.com/v1.0/users/<user-object-id>"
   ```

---

## Step 2: Register the API App

1. In the [Entra admin center](https://entra.microsoft.com), switch to the workforce tenant via **Settings** icon > **Directories + subscriptions**.
2. Navigate to **Entra ID** > **App registrations** > **New registration**.
3. Fill in the details:
   - **Name:** `Teeforce API (Non-Prod)`
   - **Supported account types:** Accounts in this organizational directory only
4. Leave Redirect URI blank — APIs don't need one.
5. Click **Register**.
6. Note the **Application (client) ID**.
7. Under **Manage** > **Owners** > **Add owners** — add yourself (required for the API to appear under "My APIs" when configuring the SPA).

### Expose an API Scope

1. In the API app registration, navigate to **Manage** > **Expose an API**.
2. Next to **Application ID URI**, click **Add**, accept the default (`api://<client-id>`), and click **Save**.
3. Click **Add a scope** and fill in:
   - **Scope name:** `access_as_user`
   - **Who can consent:** Admins and users
   - **Admin consent display name:** `Access Teeforce API`
   - **Admin consent description:** `Allow the application to access Teeforce API on behalf of the signed-in user.`
   - **State:** Enabled
4. Click **Add scope**.

---

## Step 3: Register the SPA App

1. Navigate to **Entra ID** > **App registrations** > **New registration**.
2. Fill in the details:
   - **Name:** `Teeforce SPA (Non-Prod)`
   - **Supported account types:** Accounts in this organizational directory only
3. Click **Register**.
4. Note the **Application (client) ID**.
5. Configure the platform:
   - Navigate to **Manage** > **Authentication** > **Add a platform**.
   - Select **Single-page application**.
   - Redirect URI: `http://localhost:3000`
   - Click **Configure**.
6. Add production redirect URIs as needed (e.g., `https://app.teeforce.golf`).

### Grant API Permission

1. In the SPA app registration, navigate to **Manage** > **API permissions** > **Add a permission**.
2. Select the **My APIs** tab and choose **Teeforce API (Non-Prod)**.
3. Select **Delegated permissions**, check `access_as_user`, and click **Add permissions**.
4. Click **Grant admin consent for Benjamin Golf Co** and confirm.

### Pre-authorize the SPA (Optional)

This suppresses the consent prompt so users don't see a "this app wants to access..." dialog.

1. Go to the **API** app registration > **Expose an API**.
2. Under **Authorized client applications**, click **Add a client application**.
3. Enter the SPA's Application (client) ID.
4. Check the `access_as_user` scope and click **Add application**.

---

## Step 4: Configure the Teeforce Application

### Backend (`appsettings.json` or environment variables)

Update `src/backend/Teeforce.Api/appsettings.json`:

```json
"AzureAd": {
  "Instance": "https://login.microsoftonline.com/",
  "TenantId": "<tenant-id>",
  "ClientId": "<api-client-id>"
}
```

For production, set these as environment variables or Azure Container Apps secrets:

```
AzureAd__Instance=https://login.microsoftonline.com/
AzureAd__TenantId=<tenant-id>
AzureAd__ClientId=<api-client-id>
```

### Frontend (`.env.development` for dev, environment config for production)

```
VITE_ENTRA_AUTHORITY=https://login.microsoftonline.com/<tenant-id>
VITE_ENTRA_CLIENT_ID=<spa-client-id>
VITE_API_SCOPE=api://<api-client-id>/access_as_user
```

### Disable Dev Auth in Production

```json
"Auth": {
  "UseDevAuth": false
}
```

---

## Step 5: Invite Users

Teeforce uses invite-only access. Users must be explicitly added to the tenant.

### Invite an Operator or Developer

1. In the Entra admin center, navigate to **Users** > **Invite external user**.
2. Enter the user's email address and an optional invitation message.
3. Click **Review and invite** > **Invite**.
4. The user receives an email, clicks **Accept invitation**, and authenticates with their existing account (Microsoft, Google via email OTP, etc.).
5. A guest user object is created in your directory.

### Lock Down Access (Recommended)

For maximum control, enable **user assignment** on the enterprise app:

1. Navigate to **Entra ID** > **Enterprise apps** > find your SPA app.
2. Under **Manage** > **Properties**, set **Assignment required?** to **Yes**.
3. Under **Users and groups** > **Add user/group**, assign specific users to the app.

Only assigned users can sign in. This provides defense-in-depth beyond the single-tenant restriction.

---

## Step 6: Create the First Admin User

Teeforce auto-provisions users on first login with the `Staff` role. To elevate an account to `Admin`:

1. Start the app and navigate to `http://localhost:3000`.
2. Sign in via the Entra login page — this creates the user in the `AppUsers` table with the `Staff` role.
3. Promote the user to `Admin` directly in the database:

   ```sql
   UPDATE AppUsers SET Role = 'Admin' WHERE Email = 'your-email@example.com';
   ```

   Alternatively, configure seed admin emails in `appsettings.json`:

   ```json
   "Auth": {
     "SeedAdminEmails": "your-email@example.com"
   }
   ```

   Users whose email matches a seed admin email are automatically assigned the `Admin` role on first login.

---

## Step 7: Verify

1. Run `make dev` to start the API and frontend.
2. Navigate to `http://localhost:3000`.
3. The app should redirect to the Microsoft login page (`login.microsoftonline.com`).
4. Sign in with an account that exists in the workforce tenant.
5. After successful login, you should land on the operator dashboard.

If login succeeds but the dashboard is inaccessible, confirm the `Role` column in `AppUsers` is set to `Admin` for the logged-in user.
