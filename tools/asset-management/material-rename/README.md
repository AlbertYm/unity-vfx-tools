# 按贴图名称重命名材质

版本：1.1.0。分类目录：`asset-management/material-rename`。

## 功能与使用场景

统一特效材质命名，便于依据材质使用的贴图寻找对应资源。

## 安装

将本目录中的 `Editor` 或 `Runtime` 子目录复制到 Unity 工程的 `Assets/EffectTools/material-rename/`。只复制需要的工具；不要复制仓库根目录或 archive。已有同名脚本时应替换旧代码并保留目标工程的 `.meta`，不得安装重复类。

## 入口与操作

入口：**Unity Tool → Material Rename**。

在 Project 选择一个或多个材质，打开窗口，点击 Rename Selected Materials；也可使用右键 TD → Rename Material Textures。脚本按 Shader 贴图属性顺序收集已赋值贴图名称，以 @ 连接为材质新名称。

## 文件职责

| 文件 | 功能 |
|---|---|
| `Editor/ChangeName.cs` | 按贴图名称重命名材质 |

## 限制与注意事项

会直接修改材质资源名称并保存，不修改贴图名；未提供批量预览与 Undo。先备份，留意同名或含特殊字符的名称；代码未展示 RenameAsset 返回错误。

兼容性与实测范围见 [验证说明](../../../docs/VERIFICATION.md)。本次整理没有在用户工程执行功能验收。

## 独立更新

只更新本目录对应工具；同步 README、VERSION 和 CHANGELOG.md。多文件工具须一起更新。安装时保留工程原有 `.meta`，具体流程见 [维护说明](../../../CONTRIBUTING.md)。
