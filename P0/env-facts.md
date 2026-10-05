# env-facts — 已验证的环境事实

记录日期：2026-10-05。本文件只写**跑过命令、看到输出**的事实，不写计划与预期。
「已安装」不等于「已验证」——因此每条都附取得方式。

## 已安装（有证据）

| 项 | 值 | 取得方式 |
|---|---|---|
| DevEco Studio | `26.0.0.851` / build `DS-261.23567.138.36.2600851` | 注册表卸载项 + `product-info.json` |
| 安装路径 | `D:\DevEco Studio` | 注册表 `InstallLocation` + 全盘根目录扫描 |
| OpenHarmony SDK | `apiVersion 26` / `platformVersion 26.0.0` / `releaseType Release` / `version 26.0.0.105` | `sdk\default\openharmony\ets\oh-uni-package.json` |
| hvigorw | `6.26.8` | `hvigorw.bat --version` |
| ohpm | `26.0.0.630` | `ohpm.bat --version` |
| hdc | `3.2.0f` | `hdc.exe -v` |
| 可用 kit | 47 项（`@kit.AbilityKit` / `@kit.ArkData` / …） | `sdk\default\openharmony\ets\kits\` |
| 系统 node / npm | `v24.16.0` / `12.0.1` | `node --version` |

## 路径（全部不在 PATH，须用绝对路径）

```
hvigorw   D:\DevEco Studio\tools\hvigor\bin\hvigorw.bat
ohpm      D:\DevEco Studio\tools\ohpm\bin\ohpm.bat
hdc       D:\DevEco Studio\sdk\default\openharmony\toolchains\hdc.exe
java      D:\DevEco Studio\jbr\bin\java.exe     ← 系统 java 未安装，仅 jbr 自带
node      D:\DevEco Studio\tools\node\node.exe
```

## 尚未验证（无证据，不得记为进度）

| 项 | 原因 |
|---|---|
| 真机 API Level | 无设备；`hdc list targets` 未跑 |
| 签名证书 / 密钥 | 未申请 |
| 鸿蒙工程骨架 | 仓库内 `hvigor` / `*.ets` / `*json5` 全 0 |
| `hvigorw assembleHap` | 无工程可构建；**从未构建过** |
| 冷启动 P95 | 无真机 |

## 一条反向证据

`%USERPROFILE%\.hvigor` 与 `.ohpm` 均不存在 → **hvigor 从未在本机执行过任何构建**。
这条比「已安装 DevEco」更有信息量：安装只解锁可执行性，构建一次都没发生过。

## 一个自查记录

首次探测只查了 C 盘常见安装位置，得出「未安装」的结论；随后用注册表与全盘扫描
才找到 `D:\DevEco Studio`。这与本次会话中已犯过的「抽样后归纳」是同一类错误
（EnemyArchives 只看文件名、抗性主源只看首条键）。**全盘/全量复核是唯一补救。**
