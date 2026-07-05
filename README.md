# AI Quota Monitor

![CI](https://github.com/qaz6750/AiQuotaMonitor/actions/workflows/ci.yml/badge.svg)
![Release](https://github.com/qaz6750/AiQuotaMonitor/actions/workflows/release.yml/badge.svg)

基于 **WinUI 3 (Windows App SDK 1.6) + .NET 8** 的原生 Windows 桌面应用，监控 AI 服务配额用量。参考 CodexBar / Quotio 的 provider 设计，支持 **GLM**、**Kimi Code**、**MiMo**、**MiniMax**、**Factory Droid**、**OpenAI GPT**、**Anthropic Claude**、**OpenRouter**、**DeepSeek**、**Moonshot / Kimi API** 与 **ElevenLabs**，多账号管理、实时刷新、趋势统计与费用换算。

> 原生、清爽、自动跟随系统深浅色，Mica 材质背景，半透明标题栏。

---

## ✨ 功能特性

### 多提供商架构
- **智谱 GLM** — Coding Plan 订阅制 / API 按量付费，API Key 鉴权，5h/周/MCP 配额 + 用量趋势 + 等价花费
- **Kimi Code** — Coding Plan 订阅制，API Key / OAuth Token 鉴权，5h/周额度
- **小米 MiMo** — Token Plan 按量计费，Cookie 鉴权，套餐/月度 token 用量，一键获取 Cookie
- **MiniMax Token Plan** — Token Plan 订阅 Key，5h/周额度
- **Factory Droid** — API 按量付费，Factory API Key / Bearer Token 鉴权，Standard / Premium token 用量
- **OpenAI GPT** — API 按量付费，组织 Usage / Costs API（Admin Key）
- **Anthropic Claude** — API 按量付费，组织 Usage Report API（Admin Key）
- **OpenRouter** — API 按量付费，Credits API + Key API，展示余额、日/周/月消费
- **DeepSeek** — API 按量付费，Balance API，展示 paid / granted 余额
- **Moonshot / Kimi API** — API 按量付费，Balance API，展示现金 / 赠金余额
- **ElevenLabs** — API 按量付费，Subscription API，展示字符额度、Voice Slots 与重置时间
- **Feature Flags 系统** — `ProviderCapabilities` 声明每个提供商支持的功能，UI 自动显隐
- 每个提供商独立客户端（`GlmClient` / `MiMoClient` / `KimiClient` / `MiniMaxClient` / `FactoryClient` / `OpenAiClient` / `ClaudeClient` / `BalanceApiClients`），实现 `IPlatformClient` 接口，新增平台只需加一个文件

### 多账号管理
- 添加任意数量账号，每账号独立 DPAPI 加密存储
- 概览页显示全部账号今日合计 + 每账号一行卡片（点击进入详情）
- 按账号品牌色堆叠区分

### 用量监控与统计
- **概览** — 多账号合计 + 趋势图 + 每账号配额概况
- **AIUsage / Quotio 风格 UI** — 固定侧边栏、品牌图标、卡片式提供商选择、仪表盘时段图
- **详情** — 配额进度环/条 + 重置倒计时 + 耗尽预估 + 今日 24h 用量图 + MCP 月度
- **统计** — 今日/7天/30天范围切换，按模型堆叠柱状图，坐标轴 + 悬停 tooltip
- **费用换算** — API 按量付费优先展示官方费用；没有官方费用时按模型 token 定价估算
- **自定义范围** — 选起止日期/小时计算 token 总量，点柱子设定起始时间
- **配色进度条** — 按用量自动变色（蓝→黄→红）

### 体验与安全
- **半透明标题栏** — Mica 材质 + 透视效果
- **自动深浅色** — 跟随系统主题
- **安全存储** — DPAPI（CurrentUser）加密，绝不上传
- **Cookie 账号识别** — Cookie 登录型提供商会提示接入方式，并优先用 Cookie 中的账户名 / 用户 ID 作为默认显示名
- **强制 HTTPS** — 所有请求 HTTPS
- **性能优化** — HttpClient 连接池复用、并发去重、图表防抖 + 跳过相同重绘
- **日志系统** — 文件日志（自动清理 7 天），便于调试

---

## 📥 下载安装

### 从 Release 下载
1. 前往 [Releases](../../releases) 页面
2. 下载对应架构的便携版 zip（命名格式：`AiQuotaMonitor-v版本-短commit-win-架构-portable.zip`，`x64` = 大多数 PC，`arm64` = Surface 等 ARM 设备）
3. 解压后运行 `AiQuotaMonitor.exe`；应用会在程序目录旁创建 `data/` 保存配置与日志

### 从源码构建
```bash
git clone https://github.com/qaz6750/AiQuotaMonitor.git
cd AiQuotaMonitor
dotnet build -p:Platform=x64
dotnet run --project AiQuotaMonitor/AiQuotaMonitor.csproj
```

或用 Visual Studio 2022 打开 `AiQuotaMonitor.sln`，F5 运行。

---

## 🚀 快速开始

### 环境
- Windows 10 19041+ / Windows 11
- .NET 8 SDK
- Windows App SDK 1.6 运行时
- WebView2 Runtime（Windows 11 内置，Windows 10 需安装）

### 配置账号
1. 首次启动 → 欢迎页 → 「添加账号」
2. 先选套餐类型（Coding / Token / API 按量付费）→ 再选该分类下的提供商 → 填凭据
3. 保存后自动刷新

**凭据获取：**
- 智谱 API Key：[open.bigmodel.cn](https://open.bigmodel.cn) → 控制台 → API Keys
- Kimi Code：在 Kimi Code Console 创建 API Key / OAuth Token
- MiMo Cookie：点「一键获取」→ 应用内 WebView2 登录 → 自动提取
- MiniMax：填写 Subscription Key
- Factory Droid：填写 Factory API Key（`fk-...`）、Bearer Token，或点「一键获取」使用网页登录 Cookie
- OpenAI GPT：填写组织级 Admin Key，用于读取 Usage / Costs API
- Anthropic Claude：填写组织级 Admin Key，用于读取 Usage Report API
- OpenRouter：填写 Management Key，用于读取 Credits / Key API
- DeepSeek：填写 DeepSeek API Key，用于读取 `/user/balance`
- Moonshot / Kimi API：填写 Moonshot API Key，用于读取 `/v1/users/me/balance`
- ElevenLabs：填写 ElevenLabs API Key，用于读取 `/v1/user/subscription`

**Cookie 接入说明：**
- 目前需要或支持网页登录 Cookie 的提供商：**小米 MiMo**、**Factory Droid**。
- 保存 Cookie 账号时，如果 Cookie 中包含 `email`、`username`、`nickname`、`userId` 等字段，设置页会默认显示该账户名 / 用户 ID；也可以手动填写账号名称覆盖。
- GPT、Claude、OpenRouter、DeepSeek、Moonshot、ElevenLabs 均使用官方 API/Admin Key，不采集网页登录 Cookie。

---

## 🏗️ 项目结构

```
AiQuotaMonitor/
├── .github/workflows/
│   ├── ci.yml                  # CI 构建验证
│   └── release.yml             # Tag 触发自动发布
├── Models/
│   ├── ProviderModels.cs       # 提供商描述符 + Capabilities 标志系统
│   ├── AccountModels.cs        # 账号 / PlanType / 持久化
│   └── UsageModels.cs          # 用量数据模型
├── Services/
│   ├── PlatformClients/
│   │   ├── IPlatformClient.cs  # 接口 + 工厂
│   │   ├── GlmClient.cs        # 智谱 GLM
│   │   └── MiMoClient.cs       # 小米 MiMo
│   ├── SettingsService.cs      # 多账号 + DPAPI 加密
│   └── UsageDataService.cs     # 并发去重 + 定时刷新
├── ViewModels/                 # MVVM（CommunityToolkit.Mvvm）
├── Views/                      # WinUI 页面
├── Controls/                   # 图表 / 进度环 / 进度条 / 卡片
└── Helpers/                    # 颜色 / 格式 / 主题 / 日志 / 转换器
```

---

## 🔒 隐私与安全

- 所有 API Key / Cookie 经 **DPAPI（CurrentUser 范围）** 加密后存于程序当前目录 `data/settings.json`，方便 zip 解压后便携使用
- 旧版 `%LOCALAPPDATA%\AiQuotaMonitor\settings.json` 会在首次启动时自动迁移到当前目录；「清理全部数据」会同时清理新旧位置
- 凭据仅以 `Authorization` 头或 `Cookie` 头发送至你配置的接口地址，**不经过任何第三方**
- 自定义提供商强制 HTTPS
- 「清理全部数据」一键删除本地目录（账号 / 凭据 / 偏好 / 缓存 / 日志）

---

## 🧾 版本与构建信息

- 设置页和侧边栏底部显示当前版本与短 commit，便于反馈问题时定位构建。
- Release CI 会把 `SourceRevisionId` 写入程序集 `InformationalVersion`，应用内显示格式为 `v版本 · 短commit`。
- 发布包统一命名为 `AiQuotaMonitor-v版本-短commit-win-架构-portable.zip`。

---

## � 致谢与参考

本项目在 provider 接入方式、用量接口调研与界面设计上参考了以下优秀开源项目：

- [**AIUsage**](https://github.com/) — 桌面端 AI 用量监控，本项目的整体界面风格、侧边栏布局与仪表盘设计灵感来源。
- [**CodexBar**](https://github.com/) — menu-bar 风格用量监控，`docs/providers.md` 为多家提供商（OpenRouter / DeepSeek / Moonshot / ElevenLabs 等）的接入方式与官方接口提供了重要参考。
- [**Quotio**](https://github.com/) — 多 provider 配额聚合，热力图 / dashboard 卡片 / 选中态高亮等视觉元素借鉴其设计。

### 技术栈致谢

- [WinUI 3 / Windows App SDK](https://learn.microsoft.com/windows/apps/windows-app-sdk/) — 原生 Windows UI 框架
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — MVVM 源生成器（`[ObservableProperty]` / `[RelayCommand]`）
- [Microsoft.Extensions.Hosting](https://learn.microsoft.com/dotnet/core/extensions/generic-host) — 依赖注入与主机
- [WebView2](https://learn.microsoft.com/microsoft-edge/webview2/) — MiMo / Factory 网页登录 Cookie 一键获取

> 各 AI 服务名称与品牌图标（Logo 色块）版权归对应厂商所有，本项目仅用于个人用量监控展示。

---

## �📄 许可

MIT
