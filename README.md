# odoo_mes_example

# How to setup your api key, your secret to use this project. Don't push these to the repository
appsettings.json: Acts as your baseline configuration file. It is committed to source control and should contain default, non-sensitive settings or fallback values (such as standard logging levels or public URLs). If you put sensitive keys here, they will be pushed to your repository.

appsettings.Development.json: Used specifically for local development running on your machine. As long as it is listed in your .gitignore file, it is safe to hold your local development database connection strings, local API keys, and testing URLs.

# Prevent pushing code that contains password, secret key, api key
To prevent committing and pushing sensitive data like API keys, secret keys, or passwords in a .NET repository, you should use a combination of .gitignore, Secret Manager (for local development), environment variables, and pre-commit hooks.

Here are the step-by-step methods to secure your .NET repository:

Step 1: Exclude Sensitive Files via .gitignore
Ensure your configuration files (such as appsettings.json or appsettings.Development.json if they contain local secrets) are never tracked by Git.

Open or create a .gitignore file in the root of your repository.

Add the following entries to ignore user-specific and sensitive config files:

Plaintext
# Ignore local application settings
**/appsettings.Development.json
**/appsettings.Local.json
*.user
*.suo
.secrets.json
How to verify: Run git status in your terminal. Ensure that your configuration files containing secrets do not appear in the list of modified or untracked files.

Step 2: Use Safe Templates (appsettings.json)
Instead of hardcoding secrets, use template files with placeholder values that are safely checked into source control.

Keep a clean version named appsettings.json with empty or placeholder keys:

JSON
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;"
  },
  "ApiSettings": {
    "ApiKey": "YOUR_API_KEY_HERE"
  }
}
Copy this file locally, rename it to appsettings.Development.json (which is ignored by Git), and put your real secrets there.

Step 3: Use .NET Secret Manager for Local Development
For local .NET development, the Secret Manager tool keeps secrets outside of your project tree.

Open your terminal in the project directory (where the .csproj file is located).

Initialize the user secrets ID:

Bash
dotnet user-secrets init
Add your secret key-value pairs:

Bash
dotnet user-secrets set "ApiSettings:ApiKey" "your-real-secret-key"
Access these secrets safely in your Program.cs:

C#
var apiKey = builder.Configuration["ApiSettings:ApiKey"];
How to verify: Check your user secrets storage path (stored securely outside the repo in your user profile folder) to confirm the key is saved without modifying project files.

Step 4: Implement a Pre-Commit Hook (Client-Side Protection)
To automatically scan your staged code for accidental commits of passwords or API keys before they happen, you can use Git hooks or tools like GitGuardian (ggshield) or TruffleHog.

Install a pre-commit framework (requires Python):

Bash
pip install pre-commit
Create a file named .pre-commit-config.yaml in your repository root:

YAML
repos:
  - repo: https://github.com/gitleaks/gitleaks
    rev: v8.18.2
    hooks:
      - id: gitleaks
Install the git hook scripts:

Bash
pre-commit install
How to verify: Try staging a dummy file containing a mock API key and run git commit. The pre-commit hook should block the commit and output an alert.

What if secrets are already in your Git history?
If you accidentally pushed a secret already, adding it to .gitignore is not enough because it remains in your commit history. You must remove it using a tool like BFG Repo-Cleaner or git filter-repo, and immediately revoke/rotate the compromised API key or password from the provider dashboard.

# CPS MudBlazor Dashboard – Setup and Odoo Integration

## 1. Prerequisites

Before running the CPS Dashboard, ensure that you have:

- Visual Studio with ASP.NET and web development workload
- .NET 10 SDK
- Access to the GitHub repository
- An Odoo account with API access (only required for sending CPS data)

## 2. Run the CPS Dashboard

1. Clone the GitHub repository.
2. Open the CPS solution in Visual Studio.
3. Restore NuGet packages if required.
4. Build and run the project.

The dashboard displays simulated live sensor readings, including Mixer RPM, Torque, Temperature, Vacuum and Humidity, along with real-time charts.

**Note:** The dashboard simulation can run without an Odoo connection.

## 3. Configure Odoo API Using User Secrets

The Odoo API configuration is stored locally using .NET User Secrets to prevent sensitive credentials from being committed to GitHub.

1. Open the CPS project in Visual Studio.
2. Right-click the **CPS project** in Solution Explorer.
3. Select **Manage User Secrets**.
4. Add the following configuration to `secrets.json`:

```json
{
  "Odoo": {
    "BaseUrl": "https://your-odoo-instance.odoo.com",
    "Database": "your-database-name",
    "ApiKey": "YOUR_AUTHORIZED_API_KEY"
  }
}
```

Replace the placeholders with the correct values:

- `BaseUrl`: The URL of the target Odoo instance.
- `Database`: The actual Odoo database name.
- `ApiKey`: A valid API key belonging to an authorized Odoo user.

Save the file and restart the application.

**Security:** Never push API keys, passwords or local secrets to GitHub.

## 4. Odoo Database Requirements

The CPS HTTP POST integration has been successfully tested using the `ikitest` Odoo environment.

The integration sends sensor data to the following custom model:

`x_cps_sensor_log`

The target Odoo database must contain these fields:

| Sensor | Technical Field Name |
|---|---|
| Mixer RPM | `x_studio_mixer_rpm` |
| Torque | `x_studio_torque` |
| Temperature | `x_studio_temperature` |
| Vacuum | `x_studio_vacuum` |
| Humidity | `x_studio_humidity` |

The model also uses the `x_name` field for the record name.

If a different Odoo database is used, the same model and fields must be available, and the API user must have permission to create records.

## 5. Send CPS Data to Odoo

1. Run the CPS Dashboard.
2. Wait for the simulated KPI readings to update.
3. Click **Send CPS Data** once.
4. Wait for the success message.
5. Open Odoo → Manufacturing → CPS Sensor Log.
6. Refresh the Odoo page to view the newly saved record.

Each successful button click should create one CPS Sensor Log record.

## 6. Troubleshooting

| Error | Possible Cause |
|---|---|
| Odoo configuration is missing | BaseUrl or ApiKey has not been configured |
| HTTP 401 | Invalid or expired API key |
| HTTP 403 | Insufficient access permissions |
| HTTP 404 | Incorrect API endpoint, Odoo instance or unavailable custom model |
| HTTP 500 | Backend configuration or processing error |

## 7. Important Notes

- The dashboard currently uses simulated sensor readings.
- Odoo credentials are not included in the GitHub repository.
- Each machine running the backend requires its own authorized Odoo configuration.
- The CPS Sensor Log model must exist in the target Odoo database.
- The Odoo record list may require manual refreshing to display newly saved data.
