# 特效预制体选帧缩略图

版本：1.1.0。分类目录：`previews/prefab-thumbnail`。

## 功能与使用场景

为粒子特效 Prefab 选择有辨识度的缩略图，方便素材库浏览。

## 安装

将本目录中的 `Editor` 或 `Runtime` 子目录复制到 Unity 工程的 `Assets/EffectTools/prefab-thumbnail/`。只复制需要的工具；不要复制仓库根目录或 archive。已有同名脚本时应替换旧代码并保留目标工程的 `.meta`，不得安装重复类。

## 入口与操作

入口：**Effect Tools → Prefab Thumbnail → 设置特效缩略图**。

在 Project 选择 Prefab，打开窗口，播放或拖动时间轴，调整视角与背景，点击“保存当前帧为缩略图”；复杂特效可指定 PNG。完整操作见本目录 DETAILS.md。

## 文件职责

| 文件 | 功能 |
|---|---|
| `Editor/VFXPrefabThumbnailWindow.cs` | 窗口入口 |
| `Editor/VFXThumbnailPreview.cs` | 预览渲染与粒子模拟 |
| `Editor/VFXThumbnailStore.cs` | 缩略图存储与 Project 绘制 |

## 限制与注意事项

三个脚本必须一起安装和更新。主要支持内置粒子及常规 Renderer，不执行自定义 MonoBehaviour，不完整模拟 VFX Graph、UI 或游戏逻辑。数据在 UserSettings/VFXPrefabThumbnails，按 GUID 关联，本机设置不会自动分享给同事；已有覆盖备份与恢复默认入口。

兼容性与实测范围见 [验证说明](../../../docs/VERIFICATION.md)。本次整理没有在用户工程执行功能验收。

## 独立更新

只更新本目录对应工具；同步 README、VERSION 和 CHANGELOG.md。多文件工具须一起更新。安装时保留工程原有 `.meta`，具体流程见 [维护说明](../../../CONTRIBUTING.md)。
