# MediCare — Azure Deployment & Infrastructure Setup Guide

This guide provides exact, step-by-step Azure CLI (`az`) commands to provision all required cloud infrastructure for the **MediCare** clinic management platform, including **Azure App Service**, **Azure SQL Database**, **Application Insights**, firewall rules, and production **App Settings**.

---

## 1. Prerequisites

1. **Azure CLI**: Install the Azure CLI (`az`) from [aka.ms/installazurecliwindows](https://aka.ms/installazurecliwindows).
2. **Azure Account**: An active Microsoft Azure subscription.
3. **SendGrid Account**: An active SendGrid API key and verified sender address.

Log in to Azure and select your subscription:
```bash
az login
az account set --subscription "<YOUR_AZURE_SUBSCRIPTION_ID>"
```

---

## 2. Configuration Variables

Define your environment variables in PowerShell or Bash before executing the commands. Replace all placeholder values in `<...>` with your preferred names.

### In PowerShell:
```powershell
$RESOURCE_GROUP  = "rg-medicare-prod"
$LOCATION        = "eastus"                     # e.g., eastus, westeurope, uksouth
$APP_SERVICE_PLAN= "asp-medicare-prod"
$APP_NAME        = "medicare-app-<UNIQUE_SUFFIX>" # Global unique name, e.g. medicare-app-7841
$SQL_SERVER_NAME = "sql-medicare-<UNIQUE_SUFFIX>" # Global unique name, e.g. sql-medicare-7841
$SQL_DATABASE    = "sqldb-medicare"
$SQL_ADMIN_USER  = "medicareadmin"
$SQL_ADMIN_PASS  = "<YOUR_STRONG_SQL_PASSWORD>" # e.g. MediCare#2026!Secure

# Secrets & Configuration
$ADMIN_EMAIL     = "admin@medicare.clinic"
$ADMIN_PASS      = "<YOUR_ADMIN_PASSWORD>"      # Strong password for the admin account
$SENDGRID_KEY    = "<YOUR_SENDGRID_API_KEY>"    # e.g. SG.xxxxxxxxxxxxxxxxxxxxxx
$SENDGRID_EMAIL  = "noreply@yourdomain.com"     # Verified Sender email in SendGrid
$SENDGRID_NAME   = "MediCare Clinic"
$APPINSIGHTS_NAME= "appi-medicare-prod"
```

---

## 3. Step-by-Step Provisioning

### Step 3.1: Create Resource Group
```bash
az group create \
  --name $RESOURCE_GROUP \
  --location $LOCATION
```

---

### Step 3.2: Create Azure SQL Server and Database
Provision the logical SQL Server and the database (Basic tier for lowest cost):

```bash
# Create logical SQL Server
az sql server create \
  --name $SQL_SERVER_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --admin-user $SQL_ADMIN_USER \
  --admin-password $SQL_ADMIN_PASS

# Create Azure SQL Database (Basic tier, 5 DTUs - lowest cost suitable for development/evaluation)
az sql db create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name $SQL_DATABASE \
  --service-objective Basic \
  --edition Basic \
  --max-size 2GB
```

---

### Step 3.3: Configure Azure SQL Firewall Rules

Allow Azure services (such as Azure App Service) to connect to your SQL Server:
```bash
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name "AllowAzureServices" \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0
```

*(Optional)* Allow your local machine IP for local administration or running migrations:
```bash
# Replace <YOUR_CLIENT_IP> with your public IP address
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name "AllowLocalDevIP" \
  --start-ip-address "<YOUR_CLIENT_IP>" \
  --end-ip-address "<YOUR_CLIENT_IP>"
```

---

### Step 3.4: Create App Service Plan & Web App (.NET 8 LTS)

Create the App Service Plan (tier `B1` Basic, or `F1` Free if available in your region) and deploy on .NET 8 LTS Linux:

```bash
# Create Linux App Service Plan (Tier B1: 1 Core, 1.75 GB RAM)
az appservice plan create \
  --name $APP_SERVICE_PLAN \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --is-linux \
  --sku B1

# Create the Web App with .NET 8 LTS Linux runtime
az webapp create \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --plan $APP_SERVICE_PLAN \
  --runtime "DOTNETCORE:8.0"
```

Configure HTTPS only and HTTP 2.0:
```bash
az webapp update \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --https-only true

az webapp config set \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --http20-enabled true \
  --min-tls-version "1.2"
```

---

### Step 3.5: Enable Application Insights & Availability Monitoring (KPI Uptime 99%)

To satisfy **NFR-14** (99% availability during evaluation):

```bash
# Install / ensure application-insights extension
az extension add --name application-insights

# Create Application Insights resource
az monitor app-insights component create \
  --app $APPINSIGHTS_NAME \
  --location $LOCATION \
  --resource-group $RESOURCE_GROUP \
  --application-type web

# Retrieve Application Insights connection string
APPINSIGHTS_CONN_STRING=$(az monitor app-insights component show \
  --app $APPINSIGHTS_NAME \
  --resource-group $RESOURCE_GROUP \
  --query connectionString \
  --output tsv)
```

Link Application Insights to the Web App:
```bash
az webapp config appsettings set \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings \
    APPLICATIONINSIGHTS_CONNECTION_STRING="$APPINSIGHTS_CONN_STRING" \
    ApplicationInsightsAgent_EXTENSION_VERSION="~3"
```

#### Setting up Availability Ping Test (Uptime Monitoring):
1. In the [Azure Portal](https://portal.azure.com), navigate to your **Application Insights** resource (`appi-medicare-prod`).
2. In the left navigation menu, click **Availability** > **+ Add Standard Test**.
3. Configure the test:
   - **Test name:** `MediCare-HealthCheck`
   - **URL:** `https://<APP_NAME>.azurewebsites.net/`
   - **Test frequency:** 5 minutes
   - **Test locations:** Select 3+ locations (e.g. East US, West Europe, UK South)
   - **Success criteria:** HTTP Status 200
4. Click **Save**. This monitors your site 24/7 and generates uptime SLA metrics.

---

### Step 3.6: Configure Production Connection String & App Settings

Construct the Azure SQL connection string:
```powershell
$SQL_CONN_STR = "Server=tcp:${SQL_SERVER_NAME}.database.windows.net,1433;Initial Catalog=${SQL_DATABASE};Persist Security Info=False;User ID=${SQL_ADMIN_USER};Password=${SQL_ADMIN_PASS};MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
```

Set the connection string in the Web App:
```bash
az webapp config connection-string set \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --connection-string-type SQLAzure \
  --settings DefaultConnection="$SQL_CONN_STR"
```

Configure all required App Settings (Environment Variables):
```bash
az webapp config appsettings set \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings \
    ASPNETCORE_ENVIRONMENT="Production" \
    Admin__Email="$ADMIN_EMAIL" \
    Admin__Password="$ADMIN_PASS" \
    SendGrid__ApiKey="$SENDGRID_KEY" \
    SendGrid__FromEmail="$SENDGRID_EMAIL" \
    SendGrid__FromName="$SENDGRID_NAME" \
    Otp__ResendCooldownSeconds="60" \
    Booking__CancelLeadHours="2" \
    Database__MigrateOnStartup="true"
```

> [!NOTE]
> `Database__MigrateOnStartup="true"` executes `await db.Database.MigrateAsync()` on startup in production, ensuring zero-touch automatic database migration. Alternatively, set this to `"false"` if you prefer applying the idempotent `migrations.sql` generated by the CI/CD pipeline.

---

## 4. Retrieve Publish Profile for GitHub Actions Deployment

To connect GitHub Actions CI/CD to Azure without hardcoding secrets:

```bash
az webapp deployment list-publishing-profiles \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --xml
```

Copy the entire XML output. In your GitHub repository:
1. Go to **Settings** > **Secrets and variables** > **Actions**.
2. Click **New repository secret**.
3. Name: `AZURE_WEBAPP_PUBLISH_PROFILE`
4. Value: Paste the XML content.
5. Click **Add secret**.

Also add the Web App name as a repository variable or secret:
- Name: `AZURE_WEBAPP_NAME`
- Value: `<YOUR_APP_NAME>` (e.g. `medicare-app-7841`)

---

## 5. Applying Database Migrations (Controlled Options)

### Option A: Automatic Startup Migration (Default)
When `Database__MigrateOnStartup` is set to `"true"` in Azure App Settings, the application automatically inspects `__EFMigrationsHistory` and applies pending migrations during startup. Roles and the Admin account are seeded idempotently.

### Option B: Idempotent SQL Script via Azure Query Editor or CLI
The GitHub Actions workflow compiles an idempotent SQL script `migrations.sql` on every build.
To execute it manually:
1. In the [Azure Portal](https://portal.azure.com), navigate to your Azure SQL Database (`sqldb-medicare`).
2. Click **Query editor (preview)** in the left menu.
3. Log in with SQL authentication (`medicareadmin` and your password).
4. Paste the content of `migrations.sql` (downloaded from GitHub Actions build artifacts) and click **Run**.
5. Once executed, verify tables: `Appointments`, `AspNetUsers`, `DoctorSchedules`, `MedicalRecords`, etc.

---

## 6. Verification Checklist

- [ ] Web app loads over HTTPS: `https://<YOUR_APP_NAME>.azurewebsites.net`
- [ ] Attempting HTTP redirects to HTTPS automatically (NFR-05).
- [ ] Log in as Administrator using `$ADMIN_EMAIL` and `$ADMIN_PASS`.
- [ ] Admin dashboard (`/Admin`) loads with daily report and charts.
- [ ] SendGrid delivers test verification emails.
- [ ] Application Insights availability test shows Healthy (100% uptime).
