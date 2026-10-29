# Timeline 子弹时间轨道

版本：0.1.0，实验性原样收录。依赖 Unity Timeline 包。

## 安装与使用

将 Editor 目录复制到 Assets/EffectTools/timeline-time-scale/。已有同名类时不要重复安装；更新保留工程原有 .meta。

1. 选中带 Playable Director 的物体，打开 Timeline。
2. 在轨道列表右键添加 Time Scale Track（TimeScaleTrack），无需绑定物体。
3. 在轨道空白处右键添加 Time Scale Clip。
4. 调整片段位置和长度，在片段 Inspector 设置 Time Scale：默认 0.2，1 为正常，0.1 为十分之一，范围 0.01–5。
5. 播放观察；片段外恢复为 1。支持片段 Blending。

## 限制与验证

原始脚本字节和注释编码保持不变。编辑模式通过播放图速度预览；Play 模式同时设置全局 Time.timeScale 与播放图速度，Director 使用 Game Time 时可能叠加减速，尚未实测。暂停、停止及与其他时间控制器并用行为待验收；播放图销毁时恢复所记录的全局时间值。不调整 fixedDeltaTime。

脚本放在 Editor 下，当前用于编辑器，不提供 Player 构建支持。本次仅验证复制字节与 SHA-256、目录登记；没有执行 Unity 编译、界面或场景验收，不承诺跨版本兼容。

更新前在 Assets 外备份。卸载前移除 Timeline 中对应轨道，再移除本工具；回滚恢复备份代码并保留 .meta。