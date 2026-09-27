# 资源依赖与清理

版本：1.1.0。分类目录：`asset-management/dv-clear`。

## 功能与使用场景

梳理特效资源依赖、查看谁引用了某个资源，辅助人工清理。

## 安装

将本目录中的 `Editor` 或 `Runtime` 子目录复制到 Unity 工程的 `Assets/EffectTools/dv-clear/`。只复制需要的工具；不要复制仓库根目录或 archive。已有同名脚本时应替换旧代码并保留目标工程的 `.meta`，不得安装重复类。

## 入口与操作

入口：**Effect Tools → DV Clear**。

选择资源或目录，选择正向依赖或反向引用，点击“开始扫描”；展开详情树查看关联资源，点击名称定位。先核对引用关系，再决定是否删除。

## 文件职责

| 文件 | 功能 |
|---|---|
| `Editor/DVClear.cs` | 资源依赖与清理 |

## 限制与注意事项

正向统计直接依赖；目录目标包含 Texture2D、Material、Prefab、Shader。反向扫描 Prefab、Material、Scene、Controller。0 只表示扫描范围内未找到，不代表资源无用；不覆盖动态路径加载等全部关系。删除使用 AssetDatabase.DeleteAsset，不能 Undo；务必先备份工程。

兼容性与实测范围见 [验证说明](../../../docs/VERIFICATION.md)。本次整理没有在用户工程执行功能验收。

## 独立更新

只更新本目录对应工具；同步 README、VERSION 和 CHANGELOG.md。多文件工具须一起更新。安装时保留工程原有 `.meta`，具体流程见 [维护说明](../../../CONTRIBUTING.md)。
