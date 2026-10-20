# Azure deployment

How the Price Lookup Service gets to Azure App Service, with its settings and the SAP client certificate in Key Vault (assumptions [A2, A3](assumptions.md#context-and-deployment), [A22 to A24](assumptions.md#configuration-and-secrets)).
The picture of the target is in [architecture.md, Deployment](architecture.md#deployment).

These are notes with commands, not something the repository runs. Nothing here has been run against Azure from this repository; the publish and zip steps were checked locally.

## What gets deployed

- **Only `src/PriceLookup.Api`** (it brings `PriceLookup.Core` with it). `tests/PriceLookup.SapStub` and `tests/PriceLookup.Tests` are never published or deployed (A34).
- **One App Service app setting: `KeyVault__Uri`.** Every other value comes from Key Vault; secret names use `--` as the section separator (`Erp--RetryCount` is `Erp:RetryCount`). Key Vault values override `appsettings.json`, and the vault is read once at startup, so a change needs a restart (A22).
- **`ASPNETCORE_ENVIRONMENT` is not set**, so the app runs as `Production`: `Sap:BaseUrl` must be `https` and the client certificate is required (A8, A24). Never set it to `Development` in Azure; that is the only relaxation the code has.
- If a setting is missing or invalid, the app refuses to start and the log names the setting, never its value.

## Prerequisites

- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli), signed in with `az login` and the right subscription selected with `az account set --subscription <id>`.
- .NET 10 SDK (the same one `global.json` asks for).
- Permission to create role assignments on the resource group (`Owner` or `User Access Administrator`).
- The SAP client certificate as a PFX file **with its private key**, and its password if it has one. The SAP team must map it to a technical user on their side before the first call (see [open questions](assumptions.md#open-questions-for-a-real-project)).

## 1. Variables

Run every block in the same bash session. Names of the web app and the vault are global in Azure, so change the suffix.

```bash
# Git Bash on Windows only: stop MSYS from rewriting arguments that start with "/"
# (the role scope /subscriptions/... and the value /sap/opu/...).
export MSYS_NO_PATHCONV=1

RG=rg-price-lookup
LOCATION=westeurope
PLAN=asp-price-lookup
APP=app-price-lookup-001   # globally unique, becomes $APP.azurewebsites.net
KV=kv-price-lookup-001     # globally unique, 3 to 24 letters, digits and hyphens
```

## 2. Resource group and App Service plan

A Linux plan in one region (A2). `B1` is the smallest tier that supports VNet integration; use a `P*v3` tier for production load.

```bash
az group create --name "$RG" --location "$LOCATION"

az appservice plan create --name "$PLAN" --resource-group "$RG" --location "$LOCATION" \
  --is-linux --sku B1
```

## 3. Web app with .NET 10 and a managed identity

```bash
az webapp create --name "$APP" --resource-group "$RG" --plan "$PLAN" \
  --runtime "DOTNETCORE:10.0"

# System-assigned managed identity: DefaultAzureCredential uses it to read Key Vault (A22).
az webapp identity assign --name "$APP" --resource-group "$RG"
PRINCIPAL_ID=$(az webapp identity show --name "$APP" --resource-group "$RG" --query principalId -o tsv)
```

If the CLI rejects the runtime name, `az webapp list-runtimes --os linux` lists the current ones.

## 4. Key Vault and roles

An RBAC vault: access is given with Azure roles, not with access policies. An RBAC vault gives nobody access to secrets by default, not even the person who created it, so you need a role too.

```bash
az keyvault create --name "$KV" --resource-group "$RG" --location "$LOCATION" \
  --enable-rbac-authorization true
KV_ID=$(az keyvault show --name "$KV" --query id -o tsv)
KV_URI=$(az keyvault show --name "$KV" --query properties.vaultUri -o tsv)

# The app reads secrets.
az role assignment create --assignee-object-id "$PRINCIPAL_ID" --assignee-principal-type ServicePrincipal \
  --role "Key Vault Secrets User" --scope "$KV_ID"

# You write secrets (steps 5 and rotation).
ME=$(az ad signed-in-user show --query id -o tsv)
az role assignment create --assignee-object-id "$ME" --assignee-principal-type User \
  --role "Key Vault Secrets Officer" --scope "$KV_ID"
```

A new role assignment can take a few minutes to apply. Until then `secret set` and the app's startup fail with `403 Forbidden`.

## 5. Secrets

Every `secret set` uses `--output none`, because the command prints the stored value back.

**SAP connection and certificate** (required):

```bash
az keyvault secret set --vault-name "$KV" --name Sap--BaseUrl \
  --value "https://<sap-gateway-host>" --output none

# The certificate as one line of base64 (A23). --file keeps it out of the shell history
# and the process list. A Key Vault secret holds up to 25 KB, enough for a normal PFX.
base64 -w0 client.pfx > client.pfx.b64
az keyvault secret set --vault-name "$KV" --name Sap--ClientCertificateBase64 \
  --file client.pfx.b64 --output none
rm client.pfx.b64

# Only if the PFX has a password. read -s does not echo it and keeps it out of the history.
read -rs -p "PFX password: " PFX_PASSWORD; echo
az keyvault secret set --vault-name "$KV" --name Sap--ClientCertificatePassword \
  --value "$PFX_PASSWORD" --output none
unset PFX_PASSWORD
```

**Timeouts and retries** (optional; these are the `appsettings.json` defaults). With these values in the vault, tuning is a secret change and a restart, not a new deployment. The ranges are in [A24](assumptions.md#configuration-and-secrets).

```bash
az keyvault secret set --vault-name "$KV" --name Erp--TotalTimeoutMs   --value 7000 --output none
az keyvault secret set --vault-name "$KV" --name Erp--AttemptTimeoutMs --value 3000 --output none
az keyvault secret set --vault-name "$KV" --name Erp--RetryCount       --value 3    --output none
az keyvault secret set --vault-name "$KV" --name Erp--RetryDelayMs     --value 200  --output none
```

**SAP service overrides** (only when the real SAP service differs from the defaults `/sap/opu/odata/sap/ZSD_PRODUCT_PRICE_SRV/` and `100`):

```bash
az keyvault secret set --vault-name "$KV" --name Sap--ServicePath \
  --value "/sap/opu/odata/sap/<SERVICE_NAME>_SRV/" --output none
az keyvault secret set --vault-name "$KV" --name Sap--SapClient --value "<three digits>" --output none
```

## 6. App setting, startup command and health check

```bash
# The only app setting (A22). App Service turns KeyVault__Uri into the configuration key KeyVault:Uri.
az webapp config appsettings set --name "$APP" --resource-group "$RG" \
  --settings KeyVault__Uri="$KV_URI"

# Start the API explicitly instead of letting the platform guess the entry assembly.
az webapp config set --name "$APP" --resource-group "$RG" \
  --startup-file "dotnet PriceLookup.Api.dll"

# App Service pings /health and takes an instance that does not answer 200 out of rotation.
az webapp config set --name "$APP" --resource-group "$RG" \
  --generic-configurations '{"healthCheckPath": "/health"}'

# Send the container's stdout (the startup errors) to the log stream.
az webapp log config --name "$APP" --resource-group "$RG" --docker-container-logging filesystem
```

## 7. Publish and deploy

Run from the repository root. `artifacts/` is in `.gitignore`.

```bash
dotnet publish src/PriceLookup.Api -c Release -o artifacts/publish

# Linux and macOS:
(cd artifacts/publish && zip -r ../app.zip .)
# Git Bash on Windows has no zip; the tar that ships with Windows writes one:
(cd artifacts/publish && /c/Windows/System32/tar.exe -a -c -f ../app.zip *)

az webapp deploy --name "$APP" --resource-group "$RG" --src-path artifacts/app.zip --type zip
```

The package contains `PriceLookup.Api.dll`, `PriceLookup.Core.dll`, their dependencies and `appsettings*.json`, and nothing from `tests/`. `appsettings.Development.json` is in the package but is read only in `Development`, so the app never uses it in Azure. `az webapp deploy` restarts the app.

## 8. Check

The default host name is public until network isolation is in place (A3, see [Not covered](#not-covered)), so run these checks before you lock it down, or from inside the network.

```bash
curl -s -i "https://$APP.azurewebsites.net/health"
# expected: 200, body Healthy

curl -s -i "https://$APP.azurewebsites.net/products/<a real SKU>/price"
# expected: 200 with { sku, price, currency, lastUpdated }, or a ProblemDetails with a code

az webapp log tail --name "$APP" --resource-group "$RG"
```

If the app does not start:

| Log line | Cause | Fix |
|---|---|---|
| `Sap:ClientCertificateBase64 is required outside Development.` | `KeyVault__Uri` is not set, or the secret is missing | Check step 6 and the secret name (two hyphens) |
| `Azure.RequestFailedException` with `403` / `Forbidden` | The managed identity has no `Key Vault Secrets User` role, or it has not applied yet | Check step 4, wait a few minutes, `az webapp restart` |
| `Sap:BaseUrl must use https (http is allowed only in Development).` | `Sap--BaseUrl` is `http` | Set an `https` URL |
| `Sap:BaseUrl must have no path, query or fragment (only scheme, host and port); the path belongs in Sap:ServicePath.` | `Sap--BaseUrl` contains a path (for example `/sap/opu/odata/`), a query or a fragment | Set `Sap--BaseUrl` to `https://<sap-gateway-host>` (optionally with a port) and put the path in `Sap--ServicePath` |
| `Sap:ServicePath must start and end with "/".` | `Sap--ServicePath` is missing the leading or the trailing slash | Set `Sap--ServicePath` to `/sap/opu/odata/sap/<SERVICE_NAME>_SRV/` |
| `Sap:ClientCertificateBase64 could not be read as a PFX with Sap:ClientCertificatePassword (damaged data or wrong password).` | Wrong password, or the file was not base64 of the PFX | Set both secrets again |
| `The certificate in Sap:ClientCertificateBase64 has no private key; ...` | The file is a `.cer` / `.crt`, not a PFX with the key | Export a PFX that includes the private key |

Calls that start but fail at SAP show up as `502 erp_auth_failed` (SAP rejects the certificate) or `502 erp_unavailable` / `504 erp_timeout` (SAP cannot be reached), see [A20](assumptions.md#errors).

## Certificate rotation

The certificate is loaded once at startup (A23), so rotation is a new secret version and a restart:

1. Get the new PFX with its private key. Make sure the SAP team has mapped the new certificate before you switch.
2. Store it as a new version of the same secret. The old version stays in the vault.
   ```bash
   base64 -w0 new-client.pfx > new-client.pfx.b64
   az keyvault secret set --vault-name "$KV" --name Sap--ClientCertificateBase64 \
     --file new-client.pfx.b64 --output none
   rm new-client.pfx.b64
   ```
   If the password changed, set `Sap--ClientCertificatePassword` too (step 5) **before** the restart: the app reads both together.
3. Restart. Every instance restarts, so there is a short gap.
   ```bash
   az webapp restart --name "$APP" --resource-group "$RG"
   ```
4. Run the checks in step 8.

**Rollback.** If the new certificate does not load, the app refuses to start and the health check fails. Store the previous version again as the newest one and restart:

```bash
az keyvault secret list-versions --vault-name "$KV" --name Sap--ClientCertificateBase64 \
  --query "[].{id: id, created: attributes.created}" -o table
az keyvault secret download --id "<id of the previous version>" --file old-client.pfx.b64
az keyvault secret set --vault-name "$KV" --name Sap--ClientCertificateBase64 \
  --file old-client.pfx.b64 --output none
rm old-client.pfx.b64
az webapp restart --name "$APP" --resource-group "$RG"
```

Automated rotation and an expiry alert are next steps, not part of this service.

## Not covered

- **Network isolation.** The inbound API is plain HTTP with no authentication, and only the network protects it (A3). In a real deployment, add a private endpoint or access restrictions so only the BFF and other internal services can reach the app. If SAP allows only known source addresses, add VNet integration with a NAT gateway so outbound calls have a fixed IP.
- Deployment slots for restarts and releases without a gap.
- Infrastructure as code (Bicep or Terraform) for the resources above.
- A deploy job in CI (OIDC `azure/login`); add it only if the author asks for it (plan step 13).
