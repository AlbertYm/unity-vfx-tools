# 资源引用搜索

版本：1.1.0。分类目录：`asset-management/reference-checker`。

## 功能与使用场景

查找哪些 Prefab、场景、材质或 .asset 文件包含目标资源 GUID，辅助迁移和排错。

## 安装

将本目录中的 `Editor` 或 `Runtime` 子目录复制到 Unity 工程的 `Assets/EffectTools/reference-checker/`。只复制需要的工具；不要复制仓库根目录或 archive。已有同名脚本时应替换旧代码并保留目标工程的 `.meta`，不得安装重复类。

## 入口与操作

入口：**Unity Tool → Reference Checker**。

把 Project 资源拖到 Object to Check，点击 Find References；点击结果定位资源，扫描进度支持取消。

## 文件职责

| 文件 | 功能 |
|---|---|
| `Editor/FindReference.cs` | 资源引用搜索 |

## 限制与注意事项

只扫描 Assets 下 .prefab、.unity、.mat、.asset 文本内容，不是完整依赖分析。二进制序列化、Packages、动态加载和未列出的扩展名可能漏报；大项目同步扫描会占用编辑器。

兼容性与实测范围见 [验证说明](../../../docs/VERIFICATION.md)。本次整理没有在用户工程执行功能验收。

## 独立更新

只更新本目录对应工具；同步 README、VERSION 和 CHANGELOG.md。多文件工具须一起更新。安装时保留工程原有 `.meta`，具体流程见 [维护说明](../../../CONTRIBUTING.md)。
