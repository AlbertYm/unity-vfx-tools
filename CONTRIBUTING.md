# 维护与独立更新

## 推荐方式

以本仓库为维护源，桌面旧目录作为来源备份。不要改完旧目录后假设 GitHub 会自动更新；本仓库没有监控或自动上传。

每个工具单独维护 VERSION 和 CHANGELOG.md。单文件工具只改该文件；DV Clear 两个文件、缩略图三个文件、延迟播放两个文件应作为一个工具维护，避免接口错配。分类路径固定，不为每次更新创建新文件夹。

## 使用 Git 更新一个工具

在本仓库本地目录打开终端。开始前检查 `git status --short`，确认没有混入其他工作；确认本地干净且可以同步时执行 `git pull --ff-only`。

修改所需脚本，更新该工具 README、VERSION、CHANGELOG.md，并更新 catalog.json 中对应版本与当前源码 SHA256。docs/SOURCE_MANIFEST.json 是首次导入来源证据，不随代码演进重写。

在自己的 Unity 测试工程验证后，按目录提交。例如只更新子弹时间：

```powershell
git diff -- tools/runtime/bullet-time catalog.json
git add -- tools/runtime/bullet-time catalog.json
git diff --cached --stat
git commit -m "fix(bullet-time): 描述这次修改"
git push origin main
```

需要让代理代为推送时，明确要求它同步 GitHub。不要使用 `git add .` 混入无关修改。网页直接编辑适合小范围说明修订；源码修改优先本地 Unity 验证后提交。

## 安装更新与回滚

先备份工程中的对应代码到 Assets 外。更新代码时保留该工程原有 .meta，不从历史归档复制另一套 GUID。回滚时从本工具 Git 历史提取旧代码到工程外，再覆盖回原位置。不要同时保留两份同名类。

## 发布前检查

- 在 tools 外不要新增会意外编译的 C# 副本。
- 不提交凭据、个人路径、商业素材、Unity 缓存和工程 UserSettings。
- 写明实际验证的 Unity 版本和渲染管线；编译通过与场景运行通过分开记录。
- 资源删除、重命名和粒子修改的行为变化必须写进该工具更新记录。
