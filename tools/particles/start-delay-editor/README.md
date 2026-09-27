# 批量编辑粒子起始延迟（实验）

版本：0.1.0。分类目录：`particles/start-delay-editor`。

## 功能与使用场景

批量设置、增加或减少所选对象及活动子对象的 ParticleSystem.startDelay。

## 安装

将本目录中的 `Editor` 或 `Runtime` 子目录复制到 Unity 工程的 `Assets/EffectTools/start-delay-editor/`。只复制需要的工具；不要复制仓库根目录或 archive。已有同名脚本时应替换旧代码并保留目标工程的 `.meta`，不得安装重复类。

## 入口与操作

入口：**Window → 粒子参数编辑**。

先备份 Prefab/场景，选中包含粒子系统的 GameObject，打开窗口，选“批量设置”“基础值加”或“基础值减”，输入有效数字并确认。减法结果最低为 0。

## 文件职责

| 文件 | 功能 |
|---|---|
| `Editor/ParticleSystemEditorExtension.cs` | 批量编辑粒子起始延迟（实验） |

## 限制与注意事项

原代码保留：无选中对象、空值或非数字输入会报错；没有 Undo.RecordObject 和完善的脏标记/Prefab 持久化处理，保存结果需人工确认。读取 constant 并写为常量，可能改变原曲线/随机模式；不包含未激活子对象。原文件误放 Runtime，本仓库移至 Editor，源码字节未改。

兼容性与实测范围见 [验证说明](../../../docs/VERIFICATION.md)。本次整理没有在用户工程执行功能验收。

## 独立更新

只更新本目录对应工具；同步 README、VERSION 和 CHANGELOG.md。多文件工具须一起更新。安装时保留工程原有 `.meta`，具体流程见 [维护说明](../../../CONTRIBUTING.md)。
