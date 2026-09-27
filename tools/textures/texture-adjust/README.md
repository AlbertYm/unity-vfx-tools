# 特效贴图调整

版本：1.1.0。分类目录：`textures/texture-adjust`。

## 功能与使用场景

在 Unity 内旋转、去色和调整普通颜色贴图的明暗，生成新的 PNG 变体。

## 安装

将本目录中的 `Editor` 或 `Runtime` 子目录复制到 Unity 工程的 `Assets/EffectTools/texture-adjust/`。只复制需要的工具；不要复制仓库根目录或 archive。已有同名脚本时应替换旧代码并保留目标工程的 `.meta`，不得安装重复类。

## 入口与操作

入口：**Effect Tools → Texture Adjust → 贴图调整**。

选择或拖入贴图，调整角度、扩展画布、去色、曝光、黑白场、中间调和对比度，点击“另存为新 PNG”。完整参数见本目录 DETAILS.md。

## 文件职责

| 文件 | 功能 |
|---|---|
| `Editor/VFXTextureAdjust.cs` | 特效贴图调整 |

## 限制与注意事项

输出 8 位 PNG，不覆盖原图，不自动替换材质贴图。不是法线、HDR 或数据贴图处理器；任意角度会重采样 Alpha，扩展画布会增加内存。

兼容性与实测范围见 [验证说明](../../../docs/VERIFICATION.md)。本次整理没有在用户工程执行功能验收。

## 独立更新

只更新本目录对应工具；同步 README、VERSION 和 CHANGELOG.md。多文件工具须一起更新。安装时保留工程原有 `.meta`，具体流程见 [维护说明](../../../CONTRIBUTING.md)。
