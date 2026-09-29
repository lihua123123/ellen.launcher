# Ellen.Launcher 开发与维护手册

> 本文只写**结构与流程**：项目结构、数据源、怎么改、怎么编译、怎么打包。
> 不含任何功能说明或产品介绍。

---

## 1. 环境要求

| 项 | 版本/位置 |
| --- | --- |
| .NET SDK | 10.0.x（目标框架 `net10.0-windows10.0.26100.0`） |
| 平台 | 仅 `x64`（解决方案只声明了 x64 平台） |
| Inno Setup | 6.x，`ISCC.exe` 默认在 `%LOCALAPPDATA%\Programs\Inno Setup 6\`（不在 PATH） |
| 打包依赖 | `Installer\VC_redist.x64.exe`（安装包会按需安装 VC++ 运行库） |
| 可选 | Python 3（跑 `tools/GenAvatarCombatNotes.py`） |

NuGet 源见根目录 `NuGet.Config`（nuget.org + Microsoft CsWin32 + CommunityToolkit Main/Labs）。

---

## 2. 项目结构

```
ellen-launcher/
├─ .config/dotnet-tools.json        本地工具清单（见「已知坑」）
├─ .gitignore
├─ LICENSE
├─ NuGet.Config                     NuGet 源
├─ README.md
├─ Installer/                       安装包工程（Inno Setup）
│  ├─ installer.iss                 安装脚本（AppId、协议、图标、VC 运行库、可选签名）
│  ├─ ChineseSimplified.isl         简体中文向导文案
│  ├─ VC_redist.x64.exe             VC++ 运行库（打包用）
│  └─ Publish/                      ★ 打包暂存目录（由构建产物复制而来，勿手工改）
├─ publish/                         ★ 安装包输出目录（ISCC 产物）
├─ tools/                           开发期辅助工具（不参与主程序构建）
│  ├─ GenAvatarCombatNotes.py       由 ellen-wiki 的 md 生成角色战斗备注 JSON
│  ├─ XamlStyleAudit.ps1            静态检查 ThemeResource Style 的 TargetType 错配
│  └─ GuideDataCheck/               幽境 Boss 攻略数据的解析校验控制台工具
└─ src/
   ├─ SourceGeneration/             源生成器 + 分析器（被主工程以 Analyzer 引用）
   │  ├─ Automation/                ConstructorGenerator / ApiEndpointsGenerator 等
   │  ├─ Resx/                      ResxGenerator2：resx → SH 类
   │  ├─ DependencyInjection/       HttpClient / Service 注册生成
   │  ├─ Identity/                  IdentityStructs.json → 标识类型
   │  ├─ Enum/ Xaml/ Model/ Primitive/ Extension/
   │  └─ UniversalAnalyzer.cs
   └─ Ellen.Launcher/
      ├─ Ellen.Launcher.slnx        解决方案（x64）
      ├─ .editorconfig             代码风格 + 分析器规则
      ├─ .vsconfig                 VS 组件清单
      ├─ ResXManager.config.xml    ResX Manager 配置
      ├─ settings.xamlstyler       XAML Styler 配置
      ├─ Ellen.Launcher/           ★ 主工程
      └─ Ellen.Launcher.Test/      测试工程
```

### 主工程 `src/Ellen.Launcher/Ellen.Launcher/`

| 目录/文件 | 职责 |
| --- | --- |
| `App.xaml(.cs)` | Application、激活与单实例入口 |
| `Bootstrap.cs` | `StartupObject`，进程引导 |
| `GlobalUsing.cs` | 全局 using（改名前先看这里） |
| `Core/` | 运行时基础设施：`EllenRuntime`（版本/数据目录/身份）、`Setting/`（`SettingKeys` + `DbStoreOptions`）、`DependencyInjection/`、`LifeCycle/`、`Threading/`、`IO/`、`Database/`、`Logging/`、`ExceptionService/` |
| `Web/` | 全部网络层：`Hoyolab/`（米游社/国际服客户端、`DataSigning/`、`Passport/`、`Takumi/`）、`Response/`、`Request/Builder/`、`Bridge/`（WebView2 JSBridge）、`ServerDomain.cs` |
| `Service/` | 业务服务。`User/`、`DailyNote/`、`SignIn/`、`Metadata/`、`Game/`、`GachaLog/`、`Achievement/`、`AvatarInfo/`、`Backpack/`、`Cultivation/`、`Endgame/`、`Update/`、`Notification/`、`Git/`、`Geetest/` 等 |
| `ViewModel/` | 各页面的 ViewModel（与 `UI/Xaml/View/Page/` 一一对应） |
| `UI/Xaml/` | `View/`（Window / Page / Card / Dialog / Specialized）、`Control/`、`Behavior/`、`Markup/`、`Data/` |
| `Model/` | `Entity/`（EF Core 实体）、`InterChange/`（UIAF/UIIF 等交换格式）、`Metadata/`、`Primitive/` |
| `Migrations/` | EF Core 迁移 + `AppDbContextModelSnapshot.cs` |
| `Factory/` | `ContentDialog/`（弹窗）、`ContentDialog/IContentDialogFactory`、`Picker/`、`Process/` 等 |
| `Resource/` | `Localization/`（resx）、`Metadata/`（内置元数据快照）、`CombatNotes/`、`Icon/`、`ItemIcon/`、`GuideLocalization/`、`Navigation/`、`Font/`、`ThirdParty/` |
| `Win32/` | CsWin32 原生交互（`EllenNative*`、`EllenString`、`MemoryUtilities`） |
| `Assets/` | `Logo.ico`（唯一图标源）、`InAppLogo.png` |
| `ApiEndpoints.csv` | ★ 全部后端接口表，源生成为 `IApiEndpoints` |
| `IdentityStructs.json` | 标识结构定义（源生成） |
| `BannedSymbols.txt` / `CodeMetricsConfig.txt` | 分析器禁用符号 / 代码度量配置 |
| `app.manifest` | 应用清单 |
| `Properties/launchSettings.json` | 启动配置 |

**注意**：`bin/`、`obj/` 中的内容一律不手工编辑，其中的 `.g.cs` 全部由 `src/SourceGeneration` 生成。

---

## 3. 数据源

### 3.1 接口表

所有 HTTP 接口地址集中在 **`src/Ellen.Launcher/Ellen.Launcher/ApiEndpoints.csv`**，
由 `SourceGeneration/Automation/ApiEndpointsGenerator.cs` 生成 `IApiEndpoints`（按 `ApiEndpointsKind` 分 CN / Oversea 两套）。

CSV 行格式：`"签名", CN 地址, Oversea 地址`。改域名、加接口、改签到活动 ID 等**只改这个文件**。

主要上游：

| 用途 | 域名 |
| --- | --- |
| 米游社 CN API（签到、战绩、绑定等） | `api-takumi.mihoyo.com` |
| 战绩专用（深境螺旋 / 幻想真境剧诗 / 月谕圣牌 / 幽境危战 / 角色详情） | `api-takumi-record.mihoyo.com` |
| 通行证 / 登录 | `passport-api.mihoyo.com` |
| 米游社 BBS | `bbs-api.mihoyo.com` |
| 国际服 | `sg-hk4e-api.hoyoverse.com`、`api-account-os.hoyoverse.com`、`bbs-api-os.hoyolab.com` 等 |
| Ellen 自建服务 | `ServerDomain.cs`：主 `api.snaphutaorp.org`，备用 `hutaorp.org` |

### 3.2 游戏元数据

| 来源 | 位置 | 说明 |
| --- | --- | --- |
| 远端仓库 | `<数据目录>\Repository\Snap.Metadata\Genshin\<locale>` | `GitRepositoryService.DefaultMetadataRepositoryUrl` = `https://github.com/DGP-Studio/Snap.Metadata.git` |
| 内置快照 | `src/.../Resource/Metadata/Genshin/CHS/**` | 随安装包发布，中文环境首启可完全离线；命中快照时启动不发起任何网络请求 |

优先级（`Service/Metadata/MetadataOptions.cs`）：本地 git 仓库 → 内置快照 → 待初始化仓库目录。

### 3.3 本地化

`src/.../Resource/Localization/SH.*.resx`（15 种语言）+ `SHRegex.*.resx`。
`SourceGeneration/Resx/ResxGenerator2.cs` 把 neutral `SH.resx` 生成为 `SH` 静态类（含 `Format*` 系列方法）。

> **新增文案键**只需加到 `SH.resx`（中文）与 `SH.en.resx`（英文）即可，其余语言自动回退；
> 键名 **不能以 `Format` 结尾**（生成器诊断 SH405）。

### 3.4 其他数据

| 数据 | 来源 | 生成方式 |
| --- | --- | --- |
| 角色「元素附着/产球」战斗备注 | `ellen-wiki` 仓库的 `content/attachment/元素附着及产球.md` | `python tools/GenAvatarCombatNotes.py` → `Resource/CombatNotes/AvatarCombatNotes.json` |
| 幽境 Boss 攻略 | 外部 JSON + 图片目录 | 用 `tools/GuideDataCheck` 校验 |
| 标识类型（UID、Region、AvatarId…） | `IdentityStructs.json` | `SourceGeneration/Identity/IdentityGenerator.cs` |
| 静态资源 / 公告 | 米游社官方 CDN + `static.snaphutaorp.org` | 运行期下载，缓存在数据目录 |

### 3.5 运行时数据目录

由 `Core/EllenRuntime.cs → InitializeDataDirectory()` 决定：

1. 若设置键 `Ellen::Application::DataFolderPath` 有值 → 用它；
2. 否则若 `%USERPROFILE%\Documents\Ellen` 已存在 → 用它；
3. 否则 `%LOCALAPPDATA%\EllenLauncher`（Alpha 构建 `EllenAlpha`，Canary 构建 `EllenCanary`）。

内部布局：`Userdata.db`（EF Core SQLite 主库）、`Repository\`、`UpdateCache\`、`ServerCache\`、`Background\`、`Screenshot\`、`WebView2\`、`Cache\`、`settings.json`。

---

## 4. 编译产物

主工程已开启 `SelfContained=true` + `WindowsAppSDKSelfContained=true`，**不需要接收方预装 .NET 或 Windows App SDK**（仅需 VC++ 运行库，由安装包负责）。

```powershell
# 在仓库根目录执行
dotnet build "src\Ellen.Launcher\Ellen.Launcher\Ellen.Launcher.csproj" -c Release -p:Platform=x64
```

产物目录：

```
src\Ellen.Launcher\Ellen.Launcher\bin\Release\net10.0-windows10.0.26100.0\win-x64\
```

（`AppendPlatformToOutputPath=false`，但 `RuntimeIdentifier=win-x64`，所以路径里仍有 `win-x64`。）

Debug 构建把 `-c Release` 换成 `-c Debug` 即可。

### 版本号

程序版本 = 程序集版本（`Core/AppVersion.cs` 读 `Assembly.GetName().Version`）。
`Ellen.Launcher.csproj` 未设置 `<Version>`，因此默认为 `1.0.0.0`。
**要升版本就在 `Ellen.Launcher.csproj` 的 `PropertyGroup` 里加 `<Version>x.y.z.w</Version>`**，并让 `Installer/installer.iss` 的 `MyAppVersion` 与其保持一致。

### 测试

测试工程：`src/Ellen.Launcher/Ellen.Launcher.Test/`。

```powershell
dotnet test "src\Ellen.Launcher\Ellen.Launcher.Test\Ellen.Launcher.Test.csproj" -c Release -p:Platform=x64
```

---

## 5. 打包

三步，全部在**仓库根目录**执行。

### 5.1 构建

```powershell
dotnet build "src\Ellen.Launcher\Ellen.Launcher\Ellen.Launcher.csproj" -c Release -p:Platform=x64
```

### 5.2 暂存到 `Installer\Publish`

`Installer/installer.iss` 的 `[Files]` 直接把 `Installer\Publish\*` 递归收进安装包，
所以这个目录必须与构建产物**完全一致**。建议整目录覆盖：

```powershell
$src = "src\Ellen.Launcher\Ellen.Launcher\bin\Release\net10.0-windows10.0.26100.0\win-x64"
Remove-Item "Installer\Publish\*" -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item "$src\*" "Installer\Publish\" -Recurse -Force
```

> 直接覆盖而不清空会残留上一次的旧 DLL（`[Files]` 是 `recursesubdirs`，会把它们一并打进去）。

### 5.3 编译安装包

```powershell
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" "Installer\installer.iss"
```

输出：`publish\Ellen.Launcher-<MyAppVersion>-Setup.exe`

覆盖版本号（不必改 iss）：

```powershell
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" /DMyAppVersion=1.0.1.0 "Installer\installer.iss"
```

可选代码签名：定义 `CodeSigningCertificatePath` / `CodeSigningCertificateThumbprint`
（见 `installer.iss` 顶部的 `#ifdef` 分支），安装时会把它装入 `TrustedPeople`，卸载时按标记移除。

### 5.4 打包注意事项

- **打包前必须关闭正在运行的 `Installer\Publish\Ellen.Launcher.exe`**，否则 5.2 的覆盖会因文件占用失败（error 32），或 5.3 压缩到旧文件。
- `Installer\Publish` 与 `%LOCALAPPDATA%\EllenLauncher` 是**两回事**：前者是打包暂存，后者是运行期数据目录，不要互相复制。
- 安装程序会写入 `HKLM`（协议 `ellen://`、`AppUserModelId`），需要管理员权限。
- 改了图标后必须重新 build 再打包：唯一图标源是 `src/Ellen.Launcher/Ellen.Launcher/Assets/Logo.ico`。

---

## 6. 怎么改：常见改动入口

| 想改什么 | 改哪里 |
| --- | --- |
| 接口地址、接口参数、活动 ID | `src/.../ApiEndpoints.csv` |
| 请求头 / 签名 | `Web/Hoyolab/HoyolabOptions.cs`、`Web/Hoyolab/DataSigning/`、`Web/Hoyolab/SaltConstants.cs` |
| 上游自建域名 | `Web/ServerDomain.cs` |
| 界面文案 | `Resource/Localization/SH.resx`（+ `SH.en.resx`） |
| 应用名 / 版本 / 图标 / 打包属性 | `Ellen.Launcher.csproj`、`Assets/Logo.ico`、`Installer/installer.iss` |
| 数据目录、协议、AUMID、UA | `Core/EllenRuntime.cs`、`Core/RuntimeEnvironment.cs` |
| 新增设置项 | 先在 `Core/Setting/SettingKeys.cs` 加键，再在某个 `*Options : DbStoreOptions` 里 `CreateProperty(...)` |
| 新增服务（可注入） | 在 `Service/` 下加类，标注 `[Service(ServiceLifetime.X, typeof(IX))]`、`[GeneratedConstructor]`；只声明 `readonly` 字段，构造函数由源生成器按类型注入 |
| 新增页面 | `UI/Xaml/View/Page/` 放 XAML，`ViewModel/` 放对应 VM，再把导航项加到导航注册处 |
| 列表/图标滚动卡顿 | `UI/Xaml/Control/Image/CachedImage.cs` 的 `DecodeWidth`（只按宽度解码、另一轴按比例缩放，设 0 时按控件 `Width`/`Height`/`ActualWidth` 自动推断，避免按原图全尺寸解码）；图标控件见 `UI/Xaml/Control/ItemIcon.xaml` |
| 数据库结构 | 改 `Model/Entity/**` → 手写 `Migrations/` → **同步更新 `Migrations/AppDbContextModelSnapshot.cs`** |
| 本地化资源键 | `Resource/Localization/*.resx`（需 UTF-8，见「已知坑」） |
| 角色战斗备注 | 改 `ellen-wiki` 的 md → `python tools/GenAvatarCombatNotes.py --report` |
| 元数据 | 改上游 `DGP-Studio/Snap.Metadata`，或替换 `Resource/Metadata/Genshin/CHS/**` 内置快照 |

### 代码约定

- **所有依赖都走 `[GeneratedConstructor]`**：只声明 `readonly` 字段（不能有初始化器），构造函数体由 `ConstructorGenerator` 生成；未加 `readonly` 的字段（如状态位）会被跳过。
- **不要硬编码用户可见字符串**，一律用 `SH.*`（`[LocalizationRequired]`）。
- `ITaskContext.SwitchToBackgroundAsync()/SwitchToMainThreadAsync()` 返回自定义 awaitable，**不能**接 `.ConfigureAwait(...)`；普通的 `Task`/`ValueTask` 才需要 `ConfigureAwait(false)`。
- 后台即发即忘用 `.SafeForget()`（`Core/Threading/TaskExtension.cs`）。
- 弹窗统一走 `Factory/ContentDialog/IContentDialogFactory`；从后台服务弹窗前先确认 `ICurrentXamlWindowReference.Window is not null`。

---

## 7. 辅助工具

```powershell
# 检查 XAML 中 ThemeResource Style 的 TargetType 错配（WinUI 只在运行期抛异常）
powershell -File tools\XamlStyleAudit.ps1

# 由 ellen-wiki 的「元素附着及产球.md」生成角色战斗备注
python tools\GenAvatarCombatNotes.py --report

# 校验幽境 Boss 攻略 JSON
dotnet run --project tools\GuideDataCheck -- <HardChallengeBosses.json> <图片目录>
```

---

## 8. 已知坑

1. **`dotnet ef` 在本仓库不可用**
   `.config/dotnet-tools.json` 来自另一台机器，`dotnet tool restore` 会报「可能阻止…以保护此计算机」。
   迁移只能**手写**（且没有 `IDesignTimeDbContextFactory`），写完务必同步 `AppDbContextModelSnapshot.cs`。
2. **批量改 resx 必须用脚本文件**
   在终端里直接跑多行中文 PowerShell 容易被转义打乱；请写成 `.ps1` 并以 **UTF-8 with BOM** 保存，否则 PS 5.1 按 GBK 读取会把中文写坏。脚本内尽量用 `$PSScriptRoot` 相对定位，避免出现中文路径。
3. **只增不改 `ApiEndpoints.csv` 的顺序无意义**
   生成的 `IApiEndpoints` 由 CSV 顺序驱动，插入新行后重新 build 才会生效。
4. **`Installer\Publish` 被占用** → 见 5.4。
5. **别把中间产物当源码提交**
   `bin/`、`obj/`、`publish/` 已被 `.gitignore` 排除；`Installer/Publish/`（打包暂存，上千个文件）同样已显式排除。
6. **`ViewModel/RoleCombat` 命名空间会遮蔽同名类型**
   该命名空间的尾段与 `Web/Hoyolab/Takumi/GameRecord/RoleCombat/RoleCombat.cs` 的类型同名，
   于是在 `Ellen.Launcher.ViewModel.RoleCombat` 下的文件里直接写 `RoleCombat` 会被解析成命名空间（CS0118）。
   用 `using RoleCombatRecord = Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.RoleCombat.RoleCombat;` 起别名。
7. **XAML 里的导航页类型是在解析期用字符串解析的**
   `shuxc:NavigationViewItemHelper.NavigateTo="shuxvp:XxxPage"` 由 XAML 解析器解析为 `Type`，
   再由 `frame.Navigate(pageType, data)` 用无参构造创建页面，**不需要在 DI 里注册页面**；
   但新页面必须把文件放进 `UI/Xaml/View/Page/` 并让 csproj 能把它当 `Page` 编译（默认通配即可）。新增导航图标需同时补 `Resource\Navigation\*.png` 与 csproj 里的 `<Content Include>`。
8. **背包页的分类列表必须「按需发布」**
   `Pivot` 会测量它内部的**所有** `PivotItem`，因此只要给全部 9 个分类的 `ItemsSource` 都赋了值，
   9 个 `GridView` 会同时各实例化一屏卡片（并各自发起图片加载）——这正是「东西一多就整页卡死」的原因。
   `BackpackViewModel.UpdateItemsAsync` 只发布当前分类，其余留空，等切到该标签页时由
   `OnSelectedCategoryIndexChanged` → `UpdateItemsFilter` 首次填充；`UpdateItemsFilter` 里还有一层
   结果比对，结果未变则**不重新赋值 `ItemsSource`**（换源会让 GridView 重建全部已实例化的容器）。
   维护时不要把「一次性发布全部分类」改回来。
   `UpdateItemsAsync` 的物品投影/排序/分词必须留在 `Task.Run` 里：前面的元数据 `await` 命中缓存时
   会同步完成，`ConfigureAwait(false)` **不会**切线程，搬出去就会卡在 UI 线程上。
