# Entra Member Accounts

How to create member (non-guest) user accounts in the Benjamin Golf Co Entra tenant and grant directory roles.

## When to use this

- Setting up an owner/admin account for a team member
- Creating accounts for service signups that need to be tied to the company identity
- Any account that should be a full member of the tenant (not a guest invite)

## Prerequisites

- Logged in to Azure CLI as a Global Administrator: `az login`
- The tenant's default verified domain is `id.benjamingolfco.com` (see [custom domain setup](custom-domain-setup.md))

## Create a member user

```bash
az rest --method POST \
  --uri "https://graph.microsoft.com/v1.0/users" \
  --body '{
    "accountEnabled": true,
    "displayName": "First Last",
    "mailNickname": "first",
    "userPrincipalName": "first@id.benjamingolfco.com",
    "passwordProfile": {
      "forceChangePasswordNextSignIn": true,
      "password": "'$(openssl rand -base64 16)'"
    }
  }'
```

The user will need to sign in at https://myaccount.microsoft.com and change their password on first login. Note the generated password from the command output — you'll need to share it securely with the user.

## Grant Global Administrator

Look up the user's object ID from the create response, then assign the role:

```bash
az rest --method POST \
  --uri "https://graph.microsoft.com/v1.0/directoryRoles/roleTemplateId=62e90394-69f5-4237-9190-012177145e10/members/\$ref" \
  --body '{"@odata.id": "https://graph.microsoft.com/v1.0/users/{user-object-id}"}'
```

## Verify

List the user's directory roles:

```bash
az rest --method GET \
  --uri "https://graph.microsoft.com/v1.0/users/{upn}/memberOf?\$select=displayName,roleTemplateId"
```

## Common directory role template IDs

| Role | Template ID |
|------|------------|
| Global Administrator | `62e90394-69f5-4237-9190-012177145e10` |
| User Administrator | `fe930be7-5e62-47db-91af-98c3a49a38b1` |
| Application Administrator | `9b895d92-2cd3-44c7-9d02-a6ac2d5ea5c3` |

## Notes

- UPNs use `@id.benjamingolfco.com` because the root domain is held by GoDaddy's shadow tenant. Once the [domain takeover](production-checklist.md#domain-takeover-deferred) is complete, UPNs can be updated to `@benjamingolfco.com`.
- Member accounts are different from B2B guest invites. Guests are external users invited to collaborate; members are native to the tenant.
- For app-level users (operators, admins in the Teeforce app), use the API's user creation flow instead — that creates an AppUser record and sends a B2B invitation.
