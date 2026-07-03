# AI Quota Monitor

![CI](https://github.com/qaz6750/AiQuotaMonitor/actions/workflows/ci.yml/badge.svg)
![Release](https://github.com/qaz6750/AiQuotaMonitor/actions/workflows/release.yml/badge.svg)

基于 **WinUI 3 (Windows App SDK 1.6) + .NET 8** 的原生 Windows 桌面应用，监控 AI 服务配额用量。支持**智谱 GLM**、**Kimi Code**、**GitHub Copilot**、**小米 MiMo**、**MiniMax** 与 **Factory Droid**，多账号管理、实时刷新、趋势统计。

> 原生、清爽、自动跟随系统深浅色，Mica 材质背景，半透明标题栏。

---

## ✨ 功能特性

### 多提供商架构
- **智谱 GLM** — Coding Plan 订阅制 / API 按量付费，API Key 鉴权，5h/周/MCP 配额 + 用量趋势 + 等价花费
- **Kimi Code** — Coding Plan 订阅制，API Key / OAuth Token 鉴权，5h/周额度
- **GitHub Copilot** — Coding Plan，GitHub Token 鉴权，Premium Requests / Chat / Completions 配额
- **小米 MiMo** — Token Plan 按量计费，Cookie 鉴权，套餐/月度 token 用量，一键获取 Cookie
- **MiniMax Token Plan** — Token Plan 订阅 Key，5h/周额度
- **Factory Droid** — API 按量付费，Factory API Key / Bearer Token 鉴权，Standard / Premium token 用量
- **Feature Flags 系统** — `ProviderCapabilities` 声明每个提供商支持的功能，UI 自动显隐
- 每个提供商独立客户端（`GlmClient` / `MiMoClient` / `KimiClient` / `MiniMaxClient` / `CopilotClient` / `FactoryClient`），实现 `IPlatformClient` 接口，新增平台只需加一个文件

### 多账号管理
- 添加任意数量账号，每账号独立 DPAPI 加密存储
- 概览页显示全部账号今日合计 + 每账号一行卡片（点击进入详情）
- 按账号品牌色堆叠区分

### 用量监控与统计
- **概览** — 多账号合计 + 趋势图 + 每账号配额概况
- **详情** — 配额进度环/条 + 重置倒计时 + 耗尽预估 + 今日 24h 用量图 + MCP 月度
- **统计** — 今日/7天/30天范围切换，按模型堆叠柱状图，坐标轴 + 悬停 tooltip
- **自定义范围** — 选起止日期/小时计算 token 总量，点柱子设定起始时间
- **配色进度条** — 按用量自动变色（蓝→黄→红）

### 体验与安全
- **半透明标题栏** — Mica 材质 + 透视效果
- **自动深浅色** — 跟随系统主题
- **安全存储** — DPAPI（CurrentUser）加密，绝不上传
- **强制 HTTPS** — 所有请求 HTTPS
- **性能优化** — HttpClient 连接池复用、并发去重、图表防抖 + 跳过相同重绘
- **日志系统** — 文件日志（自动清理 7 天），便于调试

---

## 📥 下载安装

### 从 Release 下载
1. 前往 [Releases](../../releases) 页面
2. 下载对应架构的 zip（`x64` = 大多数 PC，`arm64` = Surface 等 ARM 设备）
3. 解压后运行 `AiQuotaMonitor.exe`

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
- GitHub Copilot：填写具备 Copilot 权限的 GitHub Token
- MiMo Cookie：点「一键获取」→ 应用内 WebView2 登录 → 自动提取
- MiniMax：填写 Subscription Key
- Factory Droid：填写 Factory API Key（`fk-...`）或 Bearer Token

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

- 所有 API Key / Cookie 经 **DPAPI（CurrentUser 范围）** 加密后存于 `%LOCALAPPDATA%\AiQuotaMonitor\settings.json`
- 凭据仅以 `Authorization` 头或 `Cookie` 头发送至你配置的接口地址，**不经过任何第三方**
- 自定义提供商强制 HTTPS
- 「清理全部数据」一键删除本地目录（账号 / 凭据 / 偏好 / 缓存 / 日志）

---

## 📄 许可

MIT
