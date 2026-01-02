# IntegrationTesting

This repository contains a small ASP.NET Core Web API sample project `IntegrationTesting.API` (targeting .NET 8.0).

Quick start:

1. Build:

```bash
dotnet build IntegrationTesting.API/IntegrationTesting.API.csproj
```

2. Run locally:

```bash
dotnet run --project IntegrationTesting.API/IntegrationTesting.API.csproj
```

3. Suggested Git workflow (run from repository root):

```bash
git init
git add .
git commit -m "Initial commit"
# Create GitHub repo and push (option A: using GitHub CLI `gh`)
gh repo create <OWNER>/<REPO> --public --source=. --remote=origin --push
# or (option B: create repo on github.com, then):
git remote add origin https://github.com/<OWNER>/<REPO>.git
git branch -M main
git push -u origin main
```

If you'd like, I can initialize the local Git repo and push this project to a new GitHub repository for you.
