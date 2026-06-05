---
name: comet-archive
description: "Comet 阶段 5：归档。用 /comet-archive 调用。同步 delta spec 到主 spec，归档 change。"
---

# Comet 阶段 5：归档（Archive）

## 前置条件

- 验证已通过（阶段 4 完成）
- 分支已处理
- `openspec/changes/<name>/.comet.yaml` 中 `verify_result: pass`

## 步骤

### 0. 入口状态验证（Entry Check）

执行入口验证：

```bash
COMET_ENV="${COMET_ENV:-$(find . "$HOME"/.*/skills "$HOME/.config" "$HOME/.gemini" -path '*/comet/scripts/comet-env.sh' -type f -print -quit 2>/dev/null)}"
if [ -z "$COMET_ENV" ]; then
  echo "ERROR: comet-env.sh not found. Ensure the comet skill is installed." >&2
  return 1
fi
. "$COMET_ENV"
"$COMET_BASH" "$COMET_STATE" check <name> archive
```

验证通过后继续 Step 1。验证失败时脚本会输出具体失败原因。

### 1. 执行归档

运行归档脚本，自动完成以下全部步骤：

```bash
"$COMET_BASH" "$COMET_ARCHIVE" "<change-name>"
```

脚本自动执行：
1. 入口状态验证（phase=archive, verify_result=pass, archived=false）
2. Delta spec 同步到主 spec
3. Design doc 前置元数据标注（archived-with, status）
4. Plan 前置元数据标注（archived-with）
5. 移动 change 到归档目录
6. 通过 `comet-state transition <archive-name> archived` 更新 `archived: true`

如脚本返回非零退出码，报告错误并停止。
如脚本返回零退出码，归档完成。
脚本摘要中的 `X/Y steps succeeded` 以真实执行步骤计数，不会因 delta spec 同步或文档标注重复累计。

当待同步的 delta spec 与已有主 spec 不一致时，脚本会在覆盖前打印 unified diff 预览，帮助确认归档同步内容。

如需预览而不实际执行，使用 `--dry-run` 参数。

### 2. 生命周期闭环

Spec 生命周期在此完成：
```
brainstorming → delta spec → 实施 → 验证 → 主 spec 覆盖 → design doc 标注 → 归档
```

## 退出条件

- 归档脚本执行成功（退出码 0）
- 归档目录 `openspec/changes/archive/YYYY-MM-DD-<change-name>/` 存在
- 归档后的 `.comet.yaml` 中 `archived: true`

归档脚本会把 `openspec/changes/<name>/` 移动到 `openspec/changes/archive/YYYY-MM-DD-<name>/`。归档成功后**不要再对原 change 名运行** `"$COMET_BASH" "$COMET_GUARD" <change-name> archive`，因为原活跃目录已经不存在。归档完整性以脚本退出码和归档目录状态为准。

## 完成

Comet 流程全部完成。如需开始新工作，调用 `/comet` 或 `/comet-open`。

询问用户是否同步更新项目 README：

> "Comet 流程已完成。是否将此次 proposal 内容同步到项目 README？"

- **是：** 按以下规则自行处理（独立于 Comet 工作流，不影响已完成的状态）

  Proposal 格式可能变更，不按章节名硬匹配，按**内容类型**分类映射：

  | 内容类型 | README 章节 | 策略 |
  |---------|------------|------|
  | 🔵 **项目目的/定位**（描述 Why、背景、动机） | `## 简介` | 覆盖，以最新 proposal 为准 |
  | 🟢 **项目细节/功能**（描述做了什么、范围、变更内容） | `## 功能特性` | 整合，保留之前仍适用的细节 |
  | 🟡 **实现状态**（已实现和规划中的能力） | `## 路线图` | 整合，分 ✅ 已实现 / ⏳ 规划中 |

  根据 `README.md` 是否存在走不同分支：

  **不存在：** 按时间升序遍历 `openspec/changes/archive/*/proposal.md` + 当前 proposal，整合生成，提交 `git commit -m "docs: init README from proposals"`

  **已存在：** 读取当前 proposal.md，更新 README 对应章节，保留其他手动编辑部分，提交 `git commit -m "docs: update README with <change-name>"`

- **否：** 跳过
