# MCP Environment Setup Guide

Firebase MCP + Unity MCP setup for Claude Code.

---

## Prerequisites

Node.js v22+ must be installed:

```powershell
winget install OpenJS.NodeJS.LTS
```

---

## Firebase MCP

### Install

```powershell
npm install -g firebase-tools
```

### Login (Manual — browser auth)

```powershell
firebase login
```

### Verify

```powershell
firebase --version   # 15.0.0+
```

---

## Unity MCP

### Install

In Unity Editor:

1. **Window → Package Manager**
2. **+** → **Add package from git URL**
3. Enter: `https://github.com/CoderGamester/mcp-unity.git`

The MCP server JS file will be at:

```
Library/PackageCache/com.gamelovers.mcp-unity@<hash>/Server~/build/index.js
```

### Verify

Unity Console에 `[MCP Unity] WebSocket server started on localhost:8092` 로그 확인.

---

## .mcp.json (Project Root)

```json
{
  "mcpServers": {
    "mcp-unity": {
      "command": "node",
      "args": [
        "Library/PackageCache/com.gamelovers.mcp-unity@d176a9d737cc/Server~/build/index.js"
      ]
    },
    "firebase": {
      "command": "npx",
      "args": ["-y", "firebase-tools@latest", "mcp"]
    }
  }
}
```

> **Note:** `mcp-unity` 경로의 해시(`d176a9d737cc`)는 PC마다 다를 수 있음.
> `Library/PackageCache/` 에서 실제 폴더명 확인 후 수정.

---

## Troubleshooting

### MCP Unity "Component not found"
Unity Editor를 포커스해서 스크립트 리컴파일 트리거. `recompile_scripts` MCP 명령은 절대 자동 실행 금지 (서버 크래시 위험).

### Firebase MCP 연결 안 됨
`firebase login` 완료 여부 확인. `npx firebase-tools@latest mcp` 수동 실행해서 에러 확인.

### mcp-unity 경로 불일치
`Library/PackageCache/` 폴더에서 실제 `com.gamelovers.mcp-unity@<hash>` 폴더명 확인 후 `.mcp.json` 수정.
