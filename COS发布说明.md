# COS 自动发布（ellen-launcher-1327452638 / ap-guangzhou）

## 一、桶的实测状态（2026-11-10）

| 检查 | 结果 | 含义 |
|---|---|---|
| `GET /` | 403 AccessDenied | 不允许匿名列举目录 ✅ |
| `GET /不存在.json` | 404 NoSuchKey | **允许匿名读对象**，即「公有读私有写」已生效 |
| `HEAD /latest.json` | 404 | 还没上传，属正常 |
| 协议 | HTTPS 可用 | 腾讯云自带证书，可直接给客户端用 |

结论：**不用再动权限**，直接传就能被公网匿名下载。上传需要签名（脚本里配密钥）。

## 二、目录约定

```
/                                          ← 桶根
├── latest.json                            # 客户端唯一入口，no-cache
└── releases/
    ├── 7.2.0.2/
    │   ├── Ellen.Launcher-7.2.0.2-Setup.exe   # immutable，一年强缓存
    │   └── sha256.txt
    └── 7.2.0.3/…
```

- 安装包**永不覆盖**：同版本重发会覆盖同名对象，所以升版本必须改 `installer.iss` 的 `MyAppVersion`。
- 缓存头由脚本写入，不靠控制台手点：
  - 安装包 `Cache-Control: public, max-age=31536000, immutable`
  - `latest.json` `Cache-Control: no-cache, no-store, must-revalidate`

## 三、三条发布路径

### 1. 本地一条命令（最常用）

```bash
# 一次性：把子账号密钥放进当前会话（不要写进仓库）
set COS_SECRET_ID=AKID...
set COS_SECRET_KEY=...

# 出包后直接发布（版本号读 installer.iss，安装包自动找 publish/ 下最新的）
python tools/CosDeploy/publish_cos.py

# 只看要传什么、不联网
python tools/CosDeploy/publish_cos.py --dry-run

# 看桶里已有哪些版本 + 线上 latest.json 内容
python tools/CosDeploy/publish_cos.py --status
```

脚本做的事：算 sha256 → 分块上传（10 MB/块、4 线程、**支持断点续传**，断了直接重跑）→ 写 `latest.json` → **回读线上校验**（比 version / sha256 / 字节数）。

### 2. GitHub Release 自动（零本地操作）

`.github/workflows/publish-cos.yml`：`release: published` 触发 → 用 `GITHUB_TOKEN` 下载 Release 里那个 `*-Setup.exe` → 跑同一个脚本 → 冒烟测试公网 URL。

也可手动 `workflow_dispatch` 输入 tag 补传历史版本。

### 3. 安装包里也可以用

`publish/Ellen.Launcher-<ver>-Setup.exe` 就是 `iscc` 的产物，脚本默认路径与它一致，出包后不用改参数。

## 四、第一次要做的 4 件事

1. **建子账号密钥**（不要用主账号密钥）
   访问管理 → 用户 → 新建子用户 → 只勾「编程访问」→ 创建 API 密钥。
   给这个子用户绑一条自定义策略：

   ```json
   {
     "version": "2.0",
     "statement": [{
       "effect": "allow",
       "action": [
         "cos:GetBucket", "cos:GetObject", "cos:PutObject", "cos:HeadObject",
         "cos:InitiateMultipartUpload", "cos:UploadPart", "cos:ListParts",
         "cos:CompleteMultipartUpload", "cos:AbortMultipartUpload", "cos:ListMultipartUploads"
       ],
       "resource": ["qcs::cos:ap-guangzhou:uid/1327452638:ellen-launcher-1327452638/*"]
     }]
   }
   ```

2. **加 GitHub Secrets**：仓库 Settings → Secrets and variables → Actions
   - `COS_SECRET_ID`
   - `COS_SECRET_KEY`

3. **本地跑一次**（可选但建议）：确认上传 + 校验通过，再看 `--status` 输出。

4. **（可选）绑自定义域名**：控制台 → 域名管理 → 添加 `dl.ellen.wiki`，按提示加 CNAME + 证书；
   之后设 `COS_PUBLIC_BASE=https://dl.ellen.wiki`，脚本会自动把 `latest.json` 里的下载地址换成新域名。
   > 默认域名的 URL 很长但完全能用；换域名只影响 `latest.json` 里写的那个 `url`，对象 Key 不变。

## 五、`latest.json` 结构

```json
{
  "schema": 1,
  "channel": "stable",
  "version": "7.2.0.2",
  "tag": "v7.2.0.2",
  "publishedAt": "2026-11-09T17:32:44Z",
  "generatedAt": "2026-11-10T14:52:37Z",
  "sha256": "23c5683…8a1b9",
  "installer": {
    "name": "Ellen.Launcher-7.2.0.2-Setup.exe",
    "size": 249669031,
    "sha256": "23c5683…8a1b9",
    "url": "https://ellen-launcher-1327452638.cos.ap-guangzhou.myqcloud.com/releases/7.2.0.2/Ellen.Launcher-7.2.0.2-Setup.exe"
  },
  "mirrors": ["https://…cos…/releases/…", "https://github.com/…/releases/download/…"],
  "notes": "https://github.com/lihua123123/ellen.launcher/releases/tag/v7.2.0.2"
}
```

有 `sha256` + `size`，客户端下完能自校验；`mirrors` 里第一条是 COS，第二条是 GitHub，可做失败回退。
（`7.2.0.2` 的 sha256 与 GitHub 资产 digest 实测一致，说明这条链路算出来的值可信。）

## 六、成本（按官网标准价量级，以控制台账单为准）

- 存储：0.118 元/GB/月 → 一个 238 MB 安装包约 0.03 元/月
- 外网下行：0.5 元/GB → 下载一次约 0.12 元
- 请求：可忽略

真正花钱的是「被下载次数」，不是存储。想压成本就绑 CDN（CDN 流量单价更低）或在 `--prune-keep` 里清旧版本。

## 七、清理旧版本

```bash
python tools/CosDeploy/publish_cos.py --prune-keep 3          # 只打印待删列表
python tools/CosDeploy/publish_cos.py --prune-keep 3 --yes    # 真删
```

也可以直接在控制台配**生命周期规则**（前缀 `releases/`，保留 N 个版本），不依赖脚本。

## 八、还没接的一环

launcher 目前**还不读** `latest.json`——`MainViewModel.CheckUpdateAsync()` 首行就 return，检测功能被 `Ellen::Update::Enabled=false` 关着。
这一轮只做了「发布侧」；客户端接检测（启动静默查 + 右下角通知 + 设置页手动检查）是另一件事，随时可以接，数据格式已经按它的需要定好了。

## 九、排查

| 现象 | 原因 |
|---|---|
| `403 AccessDenied` | 密钥没权限 / 策略 resource 写错（APPID 或桶名） |
| `SignatureDoesNotMatch` | 密钥错，或系统时间偏差 > 15 分钟 |
| 上传卡住不动 | 大文件走单 PUT 断了；重跑即可续传（脚本会尝试复用 uploadId） |
| 校验失败但对象在 | `latest.json` 被缓存了；脚本已带 `?t=<时间戳>` 绕开，仍失败就看 CDN 缓存规则 |
| 浏览器直接打开变下载 | 正常，`.exe` 就该下载 |
