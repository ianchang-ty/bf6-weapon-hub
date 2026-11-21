# 安全边界与最小权限声明 (Minimal-Privilege Architecture)

本项目从架构设计层面遵循 **最小权限原则（Principle of Least Privilege）**，手机端、桌面端与 GitHub Actions 构建流水线均将权限收缩至理论下限。

## 1. 手机端软件（Mobile PWA & Android APK）

| 维度 | 边界与收缩措施 |
|---|---|
| **系统权限** | **0 权限**。`AndroidManifest.xml` 中不声明任何 `<uses-permission>`（**甚至不申请 `android.permission.INTERNET` 联网权限**），亦无定位、相机、存储、通知、后台自启或剪贴板自动读取权限；`allowBackup="false"` 禁止云端备份提取。 |
| **WebView 沙箱** | Android 原生壳强制开启 `setBlockNetworkLoads(true)`、`setAllowContentAccess(false)`、`setAllowFileAccessFromFileURLs(false)`，并拦截所有非 `file:///android_asset/www/` 的请求。 |
| **CSP 内容安全策略** | PWA 启用严苛策略：`default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self' data:; manifest-src 'self'; connect-src 'self'` —— 零内联脚本、零外部 CDN、零统计埋点。 |
| **本地存储** | 仅使用浏览器同源 `localStorage` 存储用户点击星标的武器代号列表，不收集任何个人身份或设备指纹。 |

## 2. 电脑端程序（Windows `BF6WeaponHub.exe`）

| 维度 | 边界与收缩措施 |
|---|---|
| **执行权限级别** | 嵌入 `app.manifest` 强制声明 `<requestedExecutionLevel level="asInvoker" uiAccess="false" />`，**严禁申请管理员（UAC）提权**，禁止跨高完整性窗口穿透。 |
| **反作弊物理隔离** | **绝不触碰游戏进程**。无 `OpenProcess` / `ReadProcessMemory`、无进程枚举、无全局键盘鼠标钩子（`SetWindowsHookEx`）、无 DirectX/Vulkan 注入覆盖层。「窗口置顶」仅设置自身 WPF 窗口的 `Topmost` 属性，与 EA Javelin 反作弊 100% 物理隔离。 |
| **网络边界** | **零网络代码**。源码不引用 `HttpClient`、`WebClient`、`Socket` 或任何监听端口，不触发 Windows 防火墙弹窗，无任何后台遥测或自动更新请求。 |
| **文件系统边界** | **启动与运行零写盘**。343 KB GZip 数据包直接内嵌于 `.exe` 资源段中，在内存（`MemoryStream`）中流式解压，不向 `%TEMP%`、`%APPDATA%` 或注册表写入任何字节。仅当用户主动点击「导出配装单 (.txt)」并在系统原生保存对话框确认路径后，才写入该单一文本文件。 |

## 3. GitHub Actions CI/CD 权限边界

- 所有 Workflow 顶层默认锁定 `permissions: { contents: read }`（只读）。
- `pages.yml` 仅在部署步骤授予 `pages: write` 与 `id-token: write`，且仅发布 `mobile-pwa/` 静态目录。
- `release.yml` 在编译 Android APK 后自动调用 `apkanalyzer manifest permissions` 进行断言校验——一旦发现任何残留权限声明立即中止构建；仅在打 `v*` 标签发版阶段按需授予 `contents: write`。
