# 证据 · P0 执行三动作（A / B / C）

日期: 2026-10-05  分支: master  提交: b851c91（11 文件, +77 / -188, 工作区干净）

## A · 移除运行期网络请求与风控探针（红线 5 / 关联红线 7）

| 文件 | 动作 | 原因 |
| --- | --- | --- |
| Service/EllenWiki/EllenWikiDataService.cs | 删除 | 唯一运行期网络调用点（启动时 GET /api/attachment） |
| Service/EllenWiki/IEllenWikiDataService.cs | 删除 | 随实现移除 |
| Core/LifeCycle/AppActivation.cs | 修改 | 移除 InitializeAsync 调用；后台初始化仅剩元数据服务 |
| Core/EllenRuntime.cs | 修改 | 移除 GetDataEllenWikiDirectory（已无引用） |
| Service/Combat/AvatarCombatNoteService.cs | 修改 | 移除注入与 AttachmentUpdated 订阅；只读随包快照 |
| Web/Endpoint/Upstream/IInfrastructureManagementEndpoints.cs | 删除 | AmIBanned 风控探针的唯一定义 |
| Web/Endpoint/Upstream/IInfrastructureEndpoints.cs | 修改 | 移除该接口；直接补回 IInfrastructureRawRootAccess |
| Web/Upstream/EllenInfrastructureClient.cs | 修改 | 删除 AmIBannedAsync |

检索证据（src/，排除 obj、bin）：
  IEllenWikiDataService               0 处
  EllenWikiDataService                0 处
  GetDataEllenWikiDirectory           0 处
  AmIBanned                           0 处
  IInfrastructureManagementEndpoints  0 处
剩余 ellen.wiki 命中 6 处，全部为 XML 文档注释（数据溯源，应当保留）。

### A.1 关键事故：增量构建会放行编译不过的仓库

第一次验证走增量构建，得到 56 条错误、全部是 XAML 编译器 WMC*（WMC0001/WMC0110/WMC9999/WMC0091），CS 错误 0 条——极易误判为「XAML 环境问题」。

强制全量重建后暴露真因：8 x CS0540。
  IInfrastructureManagementEndpoints 原本是 RawRoot 契约的唯一继承来源，
  删除它导致 EllenEndpointsForAlpha{CN,OS,FJ} 与 ForRelease 的显式接口实现失去对应接口。

修复：在 IInfrastructureEndpoints 基列表中直接补回 IInfrastructureRawRootAccess。
修复后全量重建 0 错误。

结论（已升级为工程契约）：构建门禁必须使用 --no-incremental。

## B · 修复失效的上游元数据 URL

改动：Service/Git/GitRepositoryService.cs
  - https://github.com/DGP-Studio/Snap.Metadata.git
  + https://github.com/SnapHutaoRemasteringProject/Snap.Metadata.git

依据：
  - DGP-Studio/Snap.Metadata 返回 404（MCP 与直接 web 均 404）
  - DGP-Studio 组织现有 10 个仓库，不含 Snap.Metadata
  - SnapHutaoArchive 与 SnapGenshinArchive 均已 archived
  - SnapHutaoRemasteringProject/Snap.Metadata 存在且公开，Genshin/CHS 下的 36 个文件
    与本地内置快照逐项一致；其 LICENSE 为 MIT (Copyright (c) 2022 DGP Studio)，
    与本仓库根 LICENSE 的 Copyright (c) 2026 Snap.HutaoRemasteringProject 同源

影响：恢复 MetadataOptions 中「远端 git 仓库 -> 内置快照」的远端路径；用户仍可用
设置键 Ellen::Metadata::RepositoryUrl 覆盖。

## C · 署名（红线 6）

发现：应用内不存在「关于 / 许可」页；Resource/ThirdParty/ 仅是第三方登录图标。
首次运行协议步骤的 4 项均为「同意」勾选框，把数据来源做成勾选项语义不当。

采用既有机制：Installer/installer.iss 原本已有 LicenseFile=..\LICENSE，
安装包的许可页本就是权威的许可披露位置。

| 文件 | 动作 |
| --- | --- |
| THIRD-PARTY-NOTICES.txt | 新增：1) 应用代码 MIT（DGP Studio + Snap.HutaoRemasteringProject）；2) 元数据 MIT（Snap.Metadata, (c) 2022 DGP Studio）；3) 数据来源 艾莲的数据库（ellen.wiki）及条款原文；4) 非官方工具与不含游戏美术资产的声明 |
| Installer/installer.iss | LicenseFile 指向 THIRD-PARTY-NOTICES.txt；[Files] 新增一行使其随安装落到 {app} |

验证（真实编译，非静态检查）：
  ISCC.exe Installer\installer.iss  ->  Successful compile (102.610 sec), exit=0
  编译日志含: Compressing: ...\Installer\..\THIRD-PARTY-NOTICES.txt
  产物: publish/Ellen.Launcher-1.0.0.4-Setup.exe (114.7 MB)
  publish/ 已被 .gitignore:470 忽略

## 汇总验证

| 检查 | A | B | C |
| --- | --- | --- | --- |
| 代码检索 0 命中 | 通过 | n/a | n/a |
| dotnet build --no-incremental | 0 错误 / 132 警告 | 0 错误 / 132 警告 | n/a |
| 警告数 vs 基线 | 132 = 132（零净变化） | 132 = 132 | n/a |
| ISCC 编译 | n/a | n/a | exit=0，声明文件已进包 |

变更总量: 10 个文件修改/删除（+8 / -188）+ 1 个新增文件。
全部为纯 CRLF，无行尾污染。

## 未完成

- Resource/Formulas/Genshin/{dark,light}/*.png 打包 -> 改自渲染 LaTeX
- 启动抓包（需运行应用）
- 上述均为计划中 10-06 / 10-07 之后的项目
