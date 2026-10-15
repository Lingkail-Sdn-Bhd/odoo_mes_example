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
