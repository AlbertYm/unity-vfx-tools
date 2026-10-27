# P4 → Unity 资源定位

版本：1.0.0。Windows 本地工具，在 P4V 中打开资源后，通知对应的已打开 Unity 项目，在 Project 窗口选中并高亮资源。工具不改名、不保存场景、不修改资源，不执行 P4 提交或同步。

## 安装 Unity 端

将本工具的 `Editor` 文件夹复制到项目的 `Assets/EffectTools/P4UnityLocator/` 下，等待编译完成。
菜单：`Tools → Effect Tools → P4 资源定位 → 检查连接`。
已有原项目版本时只保留一套监听器；更新保留目标工程已有 .meta，不同时导入两份。

## 构建 Windows 启动器

本仓库提供源码，不包含预编译 EXE。需要 Windows 和 .NET Framework 4.x 的系统编译器，不需要管理员权限或外部 AI。
在 PowerShell 中运行本工具的脚本（以下路径以当前位于工具目录为例）：

```powershell
& '.\Launcher~\Build-Launcher.ps1'
```

默认生成到 `%LOCALAPPDATA%\P4UnityLocator`：

- `P4UnityLocator.exe`：由 P4V 传入资源路径启动。
- `P4V-UnityLocator.xml`：根据当前用户目录生成的右键工具导入文件。
- `SHA256.txt`：当前生成的 EXE 校验值。EXE 为 AnyCPU，未签名。

可使用 `-InstallRoot` 指定输出目录；重新构建前会保留已有 EXE 备份。脚本不会自动修改 P4V。
`Launcher~` 的末尾 `~` 让 Unity 忽略启动器源码，避免将 Windows 程序编入工程。

## 在 P4V 设置材质双击

请打开 **P4V** 的设置窗口：`Edit → Preferences → File Editors`，不是 Unity 的 Preferences。

1. 点击 Add，扩展名选择/填写 `.mat`（部分界面使用不带点的 `mat`）。
2. 程序选择上一步生成的 `P4UnityLocator.exe`。使用文件选择按钮定位实际路径，勿在程序栏直接填未展开的环境变量。
3. 勾选 `Always use the selected application to open files of this type`，保存。
4. 确认 P4V 的文件双击行为为 Open。保持对应 Unity 项目打开，再双击待提交列表中的本地材质。

该关联适用于本机 P4V；其他项目也需要安装 Unity 端。成功后在 Unity Project 中改名，并按项目原有 P4 流程检查移动/增删记录。
不要将所有 `.meta` 统一关联到此工具，可用右键入口定位对应资源。

## 可选右键入口

P4V → `Tools → Manage Custom Tools`（部分版本为 Manage Tools）→ `Import tools`，导入构建目录中的 `P4V-UnityLocator.xml`。
之后选择本地文件，右键“在 Unity 中定位资源”。`%f` 传递工作区文件路径，支持材质、贴图、Prefab 和 `.meta`。

也可正常关闭 P4V 后运行：

```powershell
& '.\Launcher~\Install-ContextMenu.ps1'
```

如构建时自定义目录，安装时使用相同的 `-InstallRoot`。可选安装器只添加右键入口，不修改双击关联。
它先备份 `.p4qt/customtools.xml`，合并后检查新增入口，失败时自动恢复。运行中的 P4V 会覆盖外部设置，因此脚本发现 P4V 未关闭时会停止；它不会替用户关闭进程。

## 工作方式与限制

- 启动器按资源路径查找项目根目录；只接受该项目本地 Assets 内存在的文件。
- Unity 端在项目 Library/P4UnityLocator 写心跳，通过本机请求文件接收定位请求。无网络监听、账号或凭据。
- `.meta` 路径自动去掉后缀并定位实际资源；Unity 返回实际 Selection 路径与 GUID。
- 多开项目按根目录隔离；目标项目须打开、监听器已加载。不会自动启动 Unity。
- 编译、导入或模态对话框可能阻塞；30 秒未响应会提示重试，过期请求不会延迟定位。
- 不支持未同步文件、已删除文件，以及 P4V 下载到临时目录的历史版本。
- 不停止播放、不修改运行模式、不保存场景。Inspector 若被锁定，可能不会跟随选择。
- Console 输出 `[P4 资源定位] 已选中`；最后一次成功结果在 `Library/P4UnityLocator/last-result.txt`。

## 验证状态

2026-10-27，原项目 Windows / Unity 2022.3.62f3：编辑器程序集实际编译加载，启动器传入带空格的真实材质及对应 .meta，均收到实际选中路径/GUID；不存在文件返回失败。
公开版仅调整命名空间、程序集名和菜单，并增加通用构建安装脚本；公开版编辑器代码已使用 Unity 2022.3.62f3 自带引用程序集编译通过；两个 PowerShell 脚本通过语法检查，通用构建脚本成功生成启动器及 XML，生成的启动器向原项目监听器发出真实请求并成功定位。公开版监听器尚未在 Unity 内重新加载运行。
用户反馈工具可用，但未提供具体版本和逐项操作记录；不将其等同于所有入口的完整验收。
尚未完成公开版的 P4V 双击/右键全流程、窗口前台切换与高亮视觉、其他 Unity 版本、其他机器/DPI、存在中文路径的实测。
定位过程离线；P4V 本身连接服务器仍取决于原有环境。

## 更新与回滚

更新时备份原文件，保留工程中已有 .meta。程序版本以 `Launcher~/P4UnityLocator.cs` 的程序集版本属性为准，与 VERSION 同步。
P4V File Editors 中移除本工具的 .mat 关联或恢复原程序；Manage Custom Tools 中移除右键入口，即可停用 P4V 接入。
完整 XML 备份位于构建目录 Backups；恢复前正常退出 P4V。完整恢复可能覆盖备份后其他设置，优先在界面单独移除入口。
如需移除 Unity 端，仅删除本工具对应的 Editor 文件，遵循项目版本控制流程；不删除资源素材。