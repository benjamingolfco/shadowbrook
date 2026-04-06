# Custom Domain Setup

## Entra Tenant Domain

**Required for invite email delivery.** Azure sends B2B invitation emails from `invites@<your-domain>`. The default `.onmicrosoft.com` domain has poor sender reputation — Gmail and other providers silently drop these emails (they don't even reach spam). Adding a verified custom domain fixes this.

### Why a subdomain?

The root domain `benjamingolfco.com` is claimed by GoDaddy's auto-created tenant (`NETORG18575244.onmicrosoft.com`) for email hosting. A domain can only belong to one Entra tenant at a time. Using `id.benjamingolfco.com` avoids this conflict. If GoDaddy email is retired in the future, the root domain can be moved to the workforce tenant and the subdomain removed.

### Steps

1. Add the subdomain to the tenant:
   ```bash
   az rest --method POST \
     --uri "https://graph.microsoft.com/v1.0/domains" \
     --body '{"id": "id.benjamingolfco.com"}'
   ```

2. Get the DNS verification record:
   ```bash
   az rest --method GET \
     --uri "https://graph.microsoft.com/v1.0/domains/id.benjamingolfco.com/verificationDnsRecords" \
     -o json
   ```

3. Add the **TXT record** to your DNS provider (GoDaddy):

   | Type | Host | Value | TTL |
   |------|------|-------|-----|
   | TXT | `id` | `MS=ms________` (from step 2) | 3600 |

4. Wait a few minutes for DNS propagation, then verify:
   ```bash
   az rest --method POST \
     --uri "https://graph.microsoft.com/v1.0/domains/id.benjamingolfco.com/verify"
   ```

5. Set as the primary domain so invite emails come from `invites@id.benjamingolfco.com`:
   ```bash
   az rest --method PATCH \
     --uri "https://graph.microsoft.com/v1.0/domains/id.benjamingolfco.com" \
     --body '{"isDefault": true}'
   ```
   Setting `isDefault: true` makes Azure use this domain as the sender for B2B invitation emails and as the default for new user principal names. Only one domain can be default at a time — the previous default (`benjamingolfco.onmicrosoft.com`) loses its default status automatically.
