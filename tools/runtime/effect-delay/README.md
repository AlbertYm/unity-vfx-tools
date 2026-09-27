# 特效延迟播放

版本：1.1.0。分类目录：`runtime/effect-delay`。

## 功能与使用场景

让特效启用后延迟出现，便于组合爆点、拖尾与分阶段表现。

## 安装

将本目录中的 `Editor` 或 `Runtime` 子目录复制到 Unity 工程的 `Assets/EffectTools/effect-delay/`。只复制需要的工具；不要复制仓库根目录或 archive。已有同名脚本时应替换旧代码并保留目标工程的 `.meta`，不得安装重复类。

## 入口与操作

入口：**GameObject → Add Component → EffectDelayHelper**。

两个脚本一起安装；只手动挂载 Helper，设置 delay，进入 Play 模式。Helper 暂时隐藏自身，由自动创建的 Runner 计时后恢复；物体显示后可勾选 replay 或调用 Replay() 重播。

## 文件职责

| 文件 | 功能 |
|---|---|
| `Runtime/EffectDelayHelper.cs` | 特效延迟播放 |
| `Runtime/EffectDelayRunner.cs` | 内部计时辅助，与 Helper 配套 |

## 限制与注意事项

延迟使用缩放时间，受 timeScale 影响。Awake 会把 Application.targetFrameRate 设为 60；totalTime 仅为兼容保留、不参与逻辑。不要把 Runner 单独挂载。

兼容性与实测范围见 [验证说明](../../../docs/VERIFICATION.md)。本次整理没有在用户工程执行功能验收。

## 独立更新

只更新本目录对应工具；同步 README、VERSION 和 CHANGELOG.md。多文件工具须一起更新。安装时保留工程原有 `.meta`，具体流程见 [维护说明](../../../CONTRIBUTING.md)。
