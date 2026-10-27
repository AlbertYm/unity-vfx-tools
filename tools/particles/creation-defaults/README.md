# 新建粒子默认关闭 Play On Awake

版本：1.0.0。Unity Editor 工具。

## 安装与使用

把 Editor 文件夹复制到 `Assets/EffectTools/creation-defaults/`，等待编译后自动生效，无需菜单或手动挂组件。已有同名类时替换脚本内容并保留原 .meta。

通过 Unity ObjectFactory 创建或添加 ParticleSystem 组件时，将 Main 模块的 **Play On Awake** 默认设为关闭。之后仍可手动勾选。不会扫描修改已有粒子，不处理运行模式或持久化资源对象。

## 边界与验证

依赖 `ObjectFactory.componentWasAdded` 通知；其他脚本创建、复制对象、导入 Prefab 等路径是否触发该通知不能保证。本工具不修改 Unity 全局首选项。

2026-10-27：本机 Unity 2022.3.62f3 程序集独立编译通过。新建、添加组件、复制、重启后行为及其他 Unity 版本未在本次公开发布中逐项实测。该工具与粒子批量命名工具的用户验收记录分开，不借用后者的验收结论。

## 更新、卸载与回滚

更新前备份旧脚本到 Assets 外，保留原 .meta。删除本工具脚本后重新编译可停止应用默认值；已创建粒子的属性不会自动恢复，需要手动勾选 Play On Awake 或恢复场景备份。