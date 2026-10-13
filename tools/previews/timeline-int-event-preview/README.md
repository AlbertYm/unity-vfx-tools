# Timeline 多轨道 Int 动画事件预览

版本：1.0.0。分类目录：`previews/timeline-int-event-preview`。

## 功能与使用场景

在编辑模式拖动 Timeline 时读取动画中的 int 事件，应用当前时间对应的表情状态，无需进入 Play Mode。单窗口配置多条轨道，支持折叠、一键开启、一键停止及单条控制，不设固定轨道数量上限。

适用于按编号设置表情等可重复调用的状态切换，不用于伤害、网络消息、累计事件、粒子生成或完整技能模拟。

## 安装

将本工具完整 `Editor` 文件夹复制到 `Assets/EffectTools/timeline-int-event-preview/`。需要已安装 Unity Timeline。附带 asmdef 引用 `Unity.Timeline` 和 `Unity.Timeline.Editor`，不要放进包含其他工具的 Editor 根目录。已有同名脚本时替换对应内容并保留工程原有 .meta，避免重复类。

## 入口与基本操作

打开 **Tools → Timeline → Int Event Preview**。

1. 打开场景中的 Timeline 实例，点击“使用当前 Timeline 的 Director”。
2. 在 Timeline 单击需要预览的动画片段，点击“使用 Timeline 当前选中的动画片段”。轨道列表显示静音状态和片段名，避免选中静音分组里的同名轨道。
3. 事件函数默认 `OnChangeFace`，可改为项目实际函数名；匹配区分大小写。
4. 选择下面两种预览方式之一。
5. 点击“添加轨道配置”配置其他角色，完成后“一键开启全部”，开启 Timeline Preview 并拖动时间轴。
6. 结束后“一键停止全部并恢复”；关闭窗口或保存场景也会停止预览。折叠不会停止，移除条目会先恢复。

### 方式 A：现有接收组件

点击“从轨道绑定角色查找接收组件”，或从场景 Inspector 拖入组件标题栏。方法签名必须为 `void 方法名(int)`，允许公开或非公开实例方法。初始 / 停止恢复编号由用户指定，不是原状态快照。

原方法必须能够在编辑模式工作。依赖运行时初始化可能失败；共享材质、资产和其他副作用由原方法决定，工具不能自动还原任意代码。优先在预览场景测试。不接受 Prefab 资产上的接收组件。

### 方式 B：表情配置资产（无接收组件）

1. 在 Project 右键 **Create → Effect Tools → Timeline Face Preview Config** 创建配置。
2. 在 Bindings 添加模型配置。Mesh 优先填写场景模型使用的 Mesh；未填写时用 Renderer Name 精确匹配。一个配置必须唯一匹配一个 SkinnedMeshRenderer。
3. Material Index 从 0 开始，指定脸部材质槽；Texture Property 填写 Shader 的纹理属性名，例如 `_MainTex` 或 `_BaseMap`。
4. Textures 按动画事件 Int 排列：编号 0 使用第 0 张贴图，编号 1 使用第 1 张。必须使用有权使用的本地素材，仓库不提供贴图。
5. Wink Keyword 和 Sequence Keyword 为可选 Shader 约定，没有对应逻辑时留空。眨眼关键字仅在非正方形贴图且非原序列模式时开启；序列关键字仅原材质已启用时，编号 0 开启、其他编号关闭。这些规则不适合所有 Shader。
6. 接收组件留空，将资产拖入“表情配置资产”。也可点击自动查找，工具按模型匹配所有此类配置资产，唯一匹配才选择；多匹配需手动指定。

无组件方式克隆指定槽的材质，只修改临时材质。停止时恢复原材质并销毁副本，不修改原配置和材质资产。此方式仅支持默认 `OnChangeFace` 事件名称，其他事件使用方式 A。

## 文件职责

| 文件 | 功能 |
|---|---|
| `Editor/TimelineIntEventPreview.cs` | 多轨道窗口、事件时间换算、组件调用、临时材质及恢复 |
| `Editor/TimelineFacePreviewConfig.cs` | 通用模型与表情贴图配置资产 |
| `Editor/TimelineIntEventPreview.Editor.asmdef` | 隔离 Editor 编译并引用 Timeline |

三个文件必须一起安装和更新。

## 验证与公开版改写

项目适配版由用户实际验收：编辑模式下表情变化、多轨道折叠配置和对应配置查找已确认可用。该证据适用于原适配场景，不能替代公开版的通用配置测试。

公开版保留时间采样和总控逻辑，将固定项目配置改写为 ScriptableObject；移除内部目录、业务角色名、业务组件类型、专用 Shader 名及项目素材。公开代码用 Unity 2022.3.62f3 编译器及 Timeline 1.7.7 程序集完成独立编译。公开配置创建、资产自动查找、不同 Shader 的可视效果仍待使用者在目标工程验证；不宣称所有 Unity 版本或渲染管线已实测。

## 限制与回滚

- 同一模型不能被多条配置同时控制，后启动项会提示冲突；动画片段重叠时不猜测表情归属。
- 批量开启不是事务：失败项报告错误，其他有效项继续运行。
- 只在 Timeline 时间变化时刷新，修改事件或静音设置后点击刷新按钮。
- 无限动画轨道仅提供基础非循环采样；复杂嵌套 Timeline、混合与程序动画未完整验证。
- 不提供跨 Unity 重启的配置保存；数量无固定上限，性能需要目标场景验证。
- 更新前停止全部并备份旧代码到 Assets 外；回滚时恢复旧文件并保留工程 .meta。卸载前停止预览再删除本工具，不同时导入多份。

## 独立更新

仅更新本工具目录、VERSION、CHANGELOG、相关说明及根 catalog 的版本与源码 SHA256，遵循 [维护说明](../../../CONTRIBUTING.md)。不重新上传商业素材或其他工具。
