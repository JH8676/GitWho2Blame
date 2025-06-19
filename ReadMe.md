# GitWho2Blame

## Overview

### Setup
1. Clone the repository
2. Add the following json to your `mcp.json`
```json
{
  "servers": {
    "gitwho2blame": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "<path-to-cloned-repo>/GitWho2Blame/src/GitWho2Blame/GitWho2Blame.csproj",
        "--git-context-provider",
        "github"
      ],
      "env": {
        "TOKEN": "your_pat_here",
        "AZURE_GIT_PROJECT_ID": "your_project_id_here (only needed for Azure DevOps)",
        "AZURE_GIT_ORG_URI": "your_organization_here (only needed for Azure DevOps)"
      }
    }
  }
}
```