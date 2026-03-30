# Entra External ID Setup Guide

This guide walks through the Azure portal configuration required to set up authentication for Shadowbrook using Microsoft Entra External ID (CIAM).

## Prerequisites

- An Azure subscription
- Permissions to create tenants and app registrations

---

## Step 1: Create an External ID Tenant

1. Sign in to the [Azure portal](https://portal.azure.com).
2. Search for **Microsoft Entra ID** and open it.
3. Select **Manage tenants** from the top menu, then click **Create**.
4. Choose **Customer** as the tenant type and click **Next: Configuration**.
5. Fill in the tenant details:
   - **Organization name:** Shadowbrook Auth (or your preferred name)
   - **Initial domain name:** `shadowbrookauth` (this becomes `shadowbrookauth.onmicrosoft.com`)
   - **Location:** Select your region
6. Click **Review + Create**, then **Create**.
7. Wait for provisioning to complete, then navigate to the new tenant.
8. Note the **Tenant ID** from the tenant's Overview page — you will need it later.

---

## Step 2: Create the API App Registration

This registration represents the backend API.

1. Inside the External ID tenant, navigate to **App registrations** and click **New registration**.
2. Fill in the details:
   - **Name:** `Shadowbrook API`
   - **Supported account types:** Accounts in this organizational directory only
3. Click **Register**.
4. Note the **Application (client) ID** — this is `<api-client-id>`.
5. Expose an API scope:
   - Navigate to **Expose an API** in the left menu.
   - Click **Add a scope**.
   - If prompted to set an Application ID URI, accept the default (`api://<api-client-id>`) and click **Save and continue**.
   - Fill in the scope:
     - **Scope name:** `access_as_user`
     - **Admin consent display name:** `Access Shadowbrook API as user`
     - **Admin consent description:** `Allows the app to access the Shadowbrook API on behalf of the signed-in user.`
     - **State:** Enabled
   - Click **Add scope**.

---

## Step 3: Create the SPA App Registration

This registration represents the React frontend.

1. Navigate to **App registrations** and click **New registration**.
2. Fill in the details:
   - **Name:** `Shadowbrook SPA`
   - **Supported account types:** Accounts in this organizational directory only
3. Under **Redirect URI**, select **Single-page application (SPA)** as the platform and enter `http://localhost:3000`.
4. Click **Register**.
5. Note the **Application (client) ID** — this is `<spa-client-id>`.
6. Add the production redirect URI:
   - Navigate to **Authentication** in the left menu.
   - Under **Single-page application**, click **Add URI** and enter your production URL (e.g., `https://app.shadowbrook.golf`).
   - Click **Save**.
7. Grant the API permission:
   - Navigate to **API permissions** and click **Add a permission**.
   - Select **My APIs** and choose **Shadowbrook API**.
   - Select **Delegated permissions**, check `access_as_user`, and click **Add permissions**.
   - Click **Grant admin consent for [tenant name]** and confirm.

> The SPA uses the implicit/PKCE flow and does not require a client secret.

---

## Step 4: Create the Sign-Up / Sign-In User Flow

1. In the External ID tenant, navigate to **User flows** (under **Identities** or **External Identities**).
2. Click **New user flow**.
3. Select **Sign up and sign in** and choose the **Recommended** version.
4. Configure the user flow:
   - **Name:** `signupsignin` (the full name will be `B2C_1_signupsignin`)
   - **Identity providers:** Email with password
5. Under **User attributes**, select the attributes to collect during sign-up:
   - Email address
   - Display name
6. Under **Token claims**, ensure the following claims are returned in the token:
   - `oid` (Object ID)
   - `email`
   - `displayName`
7. Click **Create**.

---

## Step 5: Configure the Shadowbrook Application

### Backend (`appsettings.json` or environment variables)

Update `src/backend/Shadowbrook.Api/appsettings.json`:

```json
"AzureAd": {
  "Instance": "https://shadowbrookauth.ciamlogin.com",
  "TenantId": "<tenant-id>",
  "ClientId": "<api-client-id>"
}
```

For production, set these as environment variables or Azure Container Apps secrets rather than committing real values:

```
AzureAd__Instance=https://shadowbrookauth.ciamlogin.com
AzureAd__TenantId=<tenant-id>
AzureAd__ClientId=<api-client-id>
```

### Frontend (`.env.local` for dev, environment config for production)

Create `src/web/.env.local`:

```
VITE_ENTRA_AUTHORITY=https://shadowbrookauth.ciamlogin.com/
VITE_ENTRA_CLIENT_ID=<spa-client-id>
VITE_API_SCOPE=api://<api-client-id>/access_as_user
```

### Disable Dev Auth in Production

The API ships with a development authentication bypass (`Auth:UseDevAuth`). Ensure this is `false` in production:

```json
"Auth": {
  "UseDevAuth": false
}
```

Or as an environment variable:

```
Auth__UseDevAuth=false
```

---

## Step 6: Create the First Admin User

Shadowbrook auto-provisions users on first login with the `Staff` role. To elevate an account to `Admin`:

1. Start the app and navigate to `http://localhost:3000` (or your production URL).
2. Sign up using the Entra login page — this creates the user in both Entra and the `AppUsers` table with the `Staff` role.
3. Promote the user to `Admin` directly in the database:

   ```sql
   UPDATE AppUsers SET Role = 'Admin' WHERE Email = 'your-email@example.com';
   ```

   Alternatively, if you know the user's Entra Object ID, update by `IdentityId`:

   ```sql
   UPDATE AppUsers SET Role = 'Admin' WHERE IdentityId = '<object-id>';
   ```

   The Object ID is visible in the Azure portal under **Users** in the External ID tenant.

---

## Step 7: Verify

1. Run `make dev` to start the API and frontend.
2. Navigate to `http://localhost:3000`.
3. The app should redirect to the Entra External ID login page.
4. Sign in with the admin account created in Step 6.
5. After successful login, you should land on the operator dashboard.

If login succeeds but the dashboard is inaccessible, confirm the `Role` column in `AppUsers` is set to `Admin` for the logged-in user.
