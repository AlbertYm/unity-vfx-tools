# 相机黑白背景切换

版本：1.1.0。分类目录：`runtime/screen-color`。

## 功能与使用场景

检查特效在深色和浅色背景下的边缘、透明度和可读性。

## 安装

将本目录中的 `Editor` 或 `Runtime` 子目录复制到 Unity 工程的 `Assets/EffectTools/screen-color/`。只复制需要的工具；不要复制仓库根目录或 archive。已有同名脚本时应替换旧代码并保留目标工程的 `.meta`，不得安装重复类。

## 入口与操作

入口：**GameObject → Add Component → ScreenColorControl**。

场景中准备标记为 MainCamera 的相机并使用纯色背景，挂载组件，设置 color1/color2，运行时按空格切换。

## 文件职责

| 文件 | 功能 |
|---|---|
| `Runtime/ScreenColorControl.cs` | 相机黑白背景切换 |

## 限制与注意事项

默认黑白两色；Skybox 或 HDRP 独立背景配置可能覆盖效果。新输入系统独占模式需要工程已安装 Input System；自定义 asmdef 需引用 Unity.InputSystem。

兼容性与实测范围见 [验证说明](../../../docs/VERIFICATION.md)。本次整理没有在用户工程执行功能验收。

## 独立更新

只更新本目录对应工具；同步 README、VERSION 和 CHANGELOG.md。多文件工具须一起更新。安装时保留工程原有 `.meta`，具体流程见 [维护说明](../../../CONTRIBUTING.md)。
