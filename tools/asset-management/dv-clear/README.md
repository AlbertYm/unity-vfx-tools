# 资源依赖与清理（DV Clear）

版本：1.2.0。分类目录：`asset-management/dv-clear`。

## 功能与使用场景

查看资源“依赖了谁”和“被谁引用”，追踪多层引用关系，导出诊断报告，并在人工审阅后备份、清理资源。适合特效素材整理、迁移前检查和引用排错。

## 安装与升级

把本目录整个 `Editor` 文件夹复制到 `Assets/EffectTools/dv-clear/`。**DVClear.cs 与 DVClearIndex.cs 必须一起安装、更新，并处于同一个编辑器程序集。** 不需要挂载组件。

从旧版升级：在原位置替换 DVClear.cs 内容并保留工程原有 .meta；新增 DVClearIndex.cs，由 Unity 为新文件生成 .meta。若工程已经有这两个脚本，只更新代码并保留两份原有 .meta。不要再复制一套同名类，也不要从其他工程覆盖 GUID。本次发布未附带来源工程的 .meta。

## 入口与操作

入口：**Effect Tools → DV Clear**。

1. 拖入 Project 中的资源或文件夹，选择正向依赖或反向引用，点击“开始扫描”。首次扫描建立全项目索引；后续可复用未过期的索引。
2. 查看进度，可点击“取消扫描”；工程资源变化后结果会标记过期，需重扫，或主动“重建索引”。
3. 用路径筛选、引用排序、分页、详情引用树查看结果。主列表每页 100 项，引用树最多展开 8 层；点击资源名称定位。
4. 点击“导出报告 / 诊断”，输出 CSV，包含可见结果、引用依据、读取问题及未解析 GUID 候选。
5. 需要清理时，勾选资源并点击核验清理按钮。工具刷新资源并重建索引，检查阻止条件，显示清单；用户确认后才执行备份与回收站移动。

## 文件职责

| 文件 | 功能 |
|---|---|
| `Editor/DVClear.cs` | 编辑器窗口、分批扫描调度、筛选分页、报告导出、清理核验与备份流程 |
| `Editor/DVClearIndex.cs` | 建立正反向引用索引，合并 Unity 直接依赖、文本/.meta GUID 候选与 Shader include |

## 扫描范围与限制

- 索引 AssetDatabase 可见的 Assets、Packages 文件，以及 ProjectSettings 顶层文件；目录目标不再局限于旧版的几种资源类型。
- 合并 Unity 直接依赖、支持扩展名的文本 GUID、导入配置 .meta GUID、项目内 Shader include，并标记依据。
- 文本命中可能只是注释或普通字符串；动态字符串路径加载和外部代码逻辑仍可能漏报。**引用数为 0 不代表安全可删。**
- 文本补充扫描上限为单文件 16 MB；二进制资源主要依靠 Unity 依赖 API。未解析 GUID 候选最多记录 5000 项。Unity 内置 include 不在项目文件解析范围内。
- 扫描以编辑器 update 分批执行，但单文件读取及初始化仍可能耗时；不是后台线程，也不保证大项目完全无卡顿。

## 清理保护与恢复

索引过期、不完整、有读取问题、存在未保存场景/资源修改，或发现所选集合之外的已知引用时，会阻止清理。仅允许 Assets 中带 .meta 的单个文件；Packages、目录、代码/Shader/场景、Resources/StreamingAssets/Editor 目录及带 AssetBundle 名称的资源受到保护。

确认清理后，在项目根目录 `DVClearBackups/<时间戳>/` 备份资源及 .meta，并校验 SHA256，再通过 `AssetDatabase.MoveAssetToTrash` 移入系统回收站。完整预览清单位于 `Logs/DVClearCleanupPreview.txt`；备份目录保存选择清单、移动成功/失败记录与恢复说明。

这不是 Unity Undo，也不是全有或全无事务：失败时可能已有部分资源移动。请查看结果与备份。恢复时关闭 Unity，按备份中的原相对路径放回资源及 .meta，遇到新文件冲突先人工核对，不要盲目覆盖。系统回收站的实际行为取决于操作系统及存储环境。

## 可选批处理诊断入口

`EffectTools.Cleanup.DVClear.WriteProjectDiagnostics` 只读取依赖并写入 `Logs/DVClearProjectAudit.json`，不执行清理。沿用来源代码中的单个特定 Shader 路径诊断字段；该路径未必存在于其他项目，字段为空不代表整体扫描失败。本次发布保持来源代码不变，未运行此入口。

## 验证与独立更新

本次只做静态编译和发布检查，未在来源工程触发扫描、备份、回收站移动或恢复。详见 [验证说明](../../../docs/VERIFICATION.md)。更新时两个脚本必须配套；同步本目录 VERSION、CHANGELOG 和仓库 catalog.json，其他工具版本不变。流程见 [维护说明](../../../CONTRIBUTING.md)。
