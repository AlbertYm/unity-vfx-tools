# 子弹时间

版本：1.1.0。分类目录：`runtime/bullet-time`。

## 功能与使用场景

在 Play 模式减慢全局时间，用于观察特效细节或制作慢动作表现。

## 安装

将本目录中的 `Editor` 或 `Runtime` 子目录复制到 Unity 工程的 `Assets/EffectTools/bullet-time/`。只复制需要的工具；不要复制仓库根目录或 archive。已有同名脚本时应替换旧代码并保留目标工程的 `.meta`，不得安装重复类。

## 入口与操作

入口：**GameObject → Add Component → BulletTimeController**。

挂载组件，设置 slowMagnitude（相对当前速度的倍率，默认 0.1）和 slowDuration（真实秒数，默认 2），运行后勾选 enableBulletTime。结束、取消、禁用或销毁时恢复触发前的时间设置。

## 文件职责

| 文件 | 功能 |
|---|---|
| `Runtime/BulletTimeController.cs` | 子弹时间 |

## 限制与注意事项

影响全局 Time.timeScale 和 fixedDeltaTime；与其他时间管理器可能冲突。多个本工具实例仅一个接管，暂停状态不触发。

兼容性与实测范围见 [验证说明](../../../docs/VERIFICATION.md)。本次整理没有在用户工程执行功能验收。

## 独立更新

只更新本目录对应工具；同步 README、VERSION 和 CHANGELOG.md。多文件工具须一起更新。安装时保留工程原有 `.meta`，具体流程见 [维护说明](../../../CONTRIBUTING.md)。
