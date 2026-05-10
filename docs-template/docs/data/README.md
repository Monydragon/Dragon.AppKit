# Human Data Map

Use this map to explain where local user data, QA evidence, exports, and support bundles live.

| Need | Human Source | Code Source | Runtime Data |
| --- | --- | --- | --- |
| App identity | `dragon-app.json` | `Directory.Build.props` and platform projects | Not runtime data |
| Product direction | prompt source or roadmap docs | screen/view-model tests | Not runtime data |
| Local data | this file | infrastructure path/provider classes | app local data directory |
| QA evidence | `docs/qa` | `scripts/*qa*.ps1` | `artifacts/qa` |
| Release evidence | `docs/release` | `scripts/publish-releases.ps1` | `artifacts/releases` |

