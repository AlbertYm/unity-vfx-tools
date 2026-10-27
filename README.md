# Unity VFX Tools · Unity 常用特效脚本

按用途整理的 Unity 特效工作工具集：**一个仓库，13 个独立工具，19 个当前 C# 文件（含 1 个 Windows 启动器源码）**。中文说明，每个工具可单独安装、更新和查看历史。采用 [MIT 许可](LICENSE)。

## 工具目录

| 分类 | 工具 | 使用场景 | 状态 |
|---|---|---|---|
| asset-management | [资源依赖与清理](tools/asset-management/dv-clear/README.md) | 梳理特效资源依赖、查看谁引用了某个资源，辅助人工清理。 | 主版本 |
| asset-management | [按贴图名称重命名材质](tools/asset-management/material-rename/README.md) | 统一特效材质命名，便于依据材质使用的贴图寻找对应资源。 | 主版本 |
| asset-management | [资源引用搜索](tools/asset-management/reference-checker/README.md) | 查找哪些 Prefab、场景、材质或 .asset 文件包含目标资源 GUID，辅助迁移和排错。 | 主版本 |
| asset-management | [P4 → Unity 资源定位](tools/asset-management/p4-unity-locator/README.md) | 从 P4V 双击材质或右键文件，定位到已打开 Unity 的对应资源。 | 1.0.0；原项目定位已验证，公开版完整入口待验收 |
| textures | [特效贴图调整](tools/textures/texture-adjust/README.md) | 在 Unity 内旋转、去色和调整普通颜色贴图的明暗，生成新的 PNG 变体。 | 主版本 |
| previews | [特效预制体选帧缩略图](tools/previews/prefab-thumbnail/README.md) | 为粒子特效 Prefab 选择有辨识度的缩略图，方便素材库浏览。 | 主版本 |
| previews | [Timeline 多轨道 Int 动画事件预览](tools/previews/timeline-int-event-preview/README.md) | 在编辑模式拖动时间轴，同步预览多个角色的 int 状态事件与表情。 | 主版本（公开适配待实测） |
| runtime | [子弹时间](tools/runtime/bullet-time/README.md) | 在 Play 模式减慢全局时间，用于观察特效细节或制作慢动作表现。 | 主版本 |
| runtime | [特效延迟播放](tools/runtime/effect-delay/README.md) | 让特效启用后延迟出现，便于组合爆点、拖尾与分阶段表现。 | 主版本 |
| runtime | [相机黑白背景切换](tools/runtime/screen-color/README.md) | 检查特效在深色和浅色背景下的边缘、透明度和可读性。 | 主版本 |
| particles | [批量编辑粒子起始延迟（实验）](tools/particles/start-delay-editor/README.md) | 批量设置、增加或减少所选对象及活动子对象的 ParticleSystem.startDelay。 | 实验 |

新增粒子工具：

| 分类 | 工具 | 使用场景 | 状态 |
|---|---|---|---|
| particles | [粒子按贴图批量命名](tools/particles/texture-rename/README.md) | 窗口预览或右键直接按主贴图命名，支持多选和撤销。 | 1.0.0；原项目用户已实测 |
| particles | [新建粒子默认关闭 Play On Awake](tools/particles/creation-defaults/README.md) | 添加粒子组件时自动关闭 Play On Awake。 | 1.0.0；编译已验证，行为待逐项验收 |

## 开始使用

1. 下载本仓库 ZIP，或使用 Git 克隆仓库。
2. 在上表选择需要的工具，先阅读它的 README。
3. 把该工具的 Editor/Runtime 子目录复制到 `Assets/EffectTools/<工具名>/`，等待 Unity 编译。
4. 按说明打开菜单或挂载组件，在备份后的测试工程验收。

**不要将整个仓库放进 Assets；不要把新旧版本同时导入。** DV Clear 需要 DVClear 和 DVClearIndex 两个脚本，Prefab 缩略图需要三个脚本，延迟播放需要 Helper 和 Runner 两个脚本。

## 已安装旧版时

只替换目标工程对应 `.cs` 内容，保留其原有 `.meta` 和 GUID。`Blcak_Write.cs` 原类名是 `ScreenColorControl`，`NewBehaviourScript.cs` 原类名是 `EffectDelayHelper`；请先在 Unity Project 窗口改成与类名一致的文件名，再更新代码。不要通过删除重建脚本完成改名。

## 版本与兼容性

Timeline 事件预览当前版本为 1.0.1，入口已迁移至 Effect Tools；首次公开版本为 1.0.0，新增通用配置资产，公开改写部分尚未进行 Unity 界面验收；原项目适配版已由用户验收。DV Clear 已独立更新为 1.2.0，含配套引用索引；其他维护工具以现有 1.1.0 兼容包为来源；粒子起始延迟编辑器单独标记为实验工具 0.1.0。原包目标范围为 Unity 2019–Unity 6，但不等于全部版本实测通过。[验证说明](docs/VERIFICATION.md)区分本次检查与历史记录。

DV Clear 1.2.0 清理前重新核验、备份并移入系统回收站，扫描为 0 仍不代表可安全删除；粒子编辑器没有完整输入校验和 Undo。使用前阅读对应限制。

## 后续只更新一个脚本

各工具位于固定的 `tools/<分类>/<工具名>/` 路径，拥有自己的 VERSION 和 CHANGELOG。只修改、提交对应目录即可，不用新建仓库，也不用给其他工具升级版本。详细步骤见 [维护与独立更新](CONTRIBUTING.md)。

## 旧版与来源

[archive](archive/README.md) 保存原始脚本、2026-09-25 修改版、两个 1.0.0 工具及兼容包原貌。为避免重复编译，历史 `.cs` 和 `.meta` 使用 `.txt` 后缀，内容保留。原始文件到归档的映射和 SHA256 见 [来源清单](docs/SOURCE_MANIFEST.json)，该清单为首次导入快照；后续 DV Clear 更新的来源摘要与验证见 [1.2.0 检查记录](docs/DVCLEAR_1.2.0_CHECKS.json)。当前工具机器可读目录见 [catalog.json](catalog.json)。

此仓库不附带完整 Unity 工程、第三方插件、Unity 引擎文件或商业项目素材。
