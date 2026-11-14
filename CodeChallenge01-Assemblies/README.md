# Code Challenge 01 — Assemblies، Plugin و Package Management

این تمرین شبیه code challenge آزمون قبلی شماست. یک solution کوچک دارید که کدش در جای درست قرار دارد، ولی منطق اصلی آن با `TODO` خالی گذاشته شده است. تست‌ها مشخص می‌کنند کار کی تمام است. **هدف: سبز شدن هر ۲۵ تست.**

- **زمان پیشنهادی:** ۷۵ دقیقه، مثل جلسه آزمون. اول بدون راهنما تلاش کنید و فقط اگر گیر کردید سراغ بخش «راهنمایی» بروید.
- **پیش‌نیاز:** .NET SDK 8

## ساختار solution

```
CodeChallenge01-Assemblies/
├── src/
│   ├── Shop.Contracts/      IPlugin — قرارداد مشترک host و plugin
│   ├── Shop.Core/           سرویس‌ها، markerها، handlerها + TODOهای Task 2 و 3
│   └── Shop.PluginHost/     PluginLoadContext و PluginLoader + TODOهای Task 4 (Newtonsoft.Json 13)
├── plugins/
│   └── Shop.Plugins.Discount/   plugin با Newtonsoft.Json 12 (نسخه قدیمی‌تر از host)
└── tests/
    └── Shop.Tests/          تست هر Task در یک فایل جدا
```

## اجرا

```bash
dotnet test
```

اجرای تست‌های فقط یک Task:

```bash
dotnet test --filter "FullyQualifiedName~Task2"
```

---

## Task 1 — Package management (فقط فایل‌های MSBuild)

الان هر csproj نسخه پکیج‌ها و تنظیمات `TargetFramework`، `Nullable` و `ImplicitUsings` را تکرار کرده است. این را مرتب کنید:

1. **Central Package Management** را در `Directory.Packages.props` (ریشه repo) فعال کنید و نسخه همه پکیج‌ها را آنجا تعریف کنید.
2. هیچ `PackageReference`ی در csproj‌ها نباید `Version` داشته باشد.
3. plugin باید همچنان **Newtonsoft.Json 12.0.1** بگیرد، در حالی که بقیه 13.0.3 می‌گیرند.
4. سه تنظیم مشترک بالا به `Directory.Build.props` منتقل شوند و در هیچ csprojی تکرار نشوند.
5. plugin نباید نسخه خودش از `Shop.Contracts.dll` را همراه داشته باشد. ارجاع آن را درست کنید.

تست‌ها: `Task1_PackageManagementTests`.

## Task 2 — ثبت خودکار سرویس‌ها با reflection

متد `AddServicesFromAssembly` در `src/Shop.Core/Registration/ServiceCollectionExtensions.cs` را پیاده‌سازی کنید:

- هر کلاسی که `IScopedService`، `ISingletonService` یا `ITransientService` را پیاده‌سازی کرده، با lifetime همان marker ثبت شود.
- کلاس برای **هر interface** خودش ثبت شود، به جز خود markerها.
- interface، کلاس abstract، open generic و کلاس دارای `[SkipRegistration]` نادیده گرفته شوند.
- کلاس‌های `internal` هم ثبت شوند.
- دو بار صدا زدن متد نباید ثبت تکراری بسازد.
- **Bonus:** `ReportService` هر دو interface خود را پیاده می‌کند. هر دو باید **یک نمونه singleton مشترک** را برگردانند.

تست‌ها: `Task2_ServiceRegistrationTests`.

## Task 3 — Handlerهای generic و dispatch با نوع runtime

1. **`AddHandlersFromAssembly`:** همه پیاده‌سازی‌های **بسته** `IHandler<T>` را به‌صورت transient ثبت کنید. `NotificationHandler` دو interface دارد و باید برای هر دو ثبت شود. `LoggingHandler<T>` (open generic) باید نادیده گرفته شود.
2. **`MessageDispatcher.Dispatch(object message)`:** handlerهای مربوط به **نوع runtime** پیام را از DI بگیرید، `Handle` را روی همه صدا بزنید و نتایج را برگردانید.
    - اگر handlerی نبود، لیست خالی برگردانید.
    - exception داخل handler باید با **نوع اصلی خودش** به caller برسد.
    - **Bonus (بدون تست):** برای هر نوع پیام فقط یک بار reflection انجام شود (cache).

تست‌ها: `Task3_HandlerTests`.

## Task 4 — بارگذاری plugin در AssemblyLoadContext جدا

1. **`PluginLoadContext`:**
    - وابستگی‌های خود plugin (مثل Newtonsoft.Json 12) **داخل همین context** بارگذاری شوند.
    - هر چیزی که plugin همراه ندارد (مثل `Shop.Contracts`) از Default context بیاید.
    - context باید **قابل unload** باشد.
2. **`PluginLoader.Load(path)`:** assembly را در یک `PluginLoadContext` جدید بارگذاری کنید و از هر نوع **غیر abstract** که `IPlugin` را پیاده‌سازی کرده یک نمونه بسازید.

بعد از انجام درست، host با Newtonsoft 13 و plugin با Newtonsoft 12 **هم‌زمان در یک process** اجرا می‌شوند. یکی از تست‌ها همین را بررسی می‌کند.

تست‌ها: `Task4_PluginTests`.

---

## نکات و دام‌ها

- بعد از تغییر csproj یا فایل‌های props، `dotnet build` بزنید. اگر restore گیر کرد، `dotnet clean` کمک می‌کند.
- هشدار **NU1903** روی plugin عمدی است: Newtonsoft.Json 12.0.1 یک آسیب‌پذیری شناخته‌شده دارد. این همان چیزی است که NuGetAudit در .NET 8 گزارش می‌دهد. در پروژه واقعی، plugin باید ارتقا داده شود.
- تست unload عمداً `Execute()` را صدا نمی‌زند. Newtonsoft.Json اطلاعات reflection درباره typeهای plugin را در Default context cache می‌کند و همین reference جلوی unload شدن context را می‌گیرد. این یک مشکل واقعی در سیستم‌های plugin است و برای مصاحبه نکته خوبی است.

<details>
<summary><b>راهنمایی (فقط اگر گیر کردید)</b></summary>

- **Task 1:**
    - `ManagePackageVersionsCentrally`، `PackageVersion`، و `VersionOverride` برای plugin.
    - برای Contracts: `<Private>false</Private>` و `<ExcludeAssets>runtime</ExcludeAssets>` روی `ProjectReference`.
- **Task 2:**
    - `assembly.GetTypes()` (همراه `ReflectionTypeLoadException`)، `IsAssignableFrom`، `IsDefined(typeof(SkipRegistrationAttribute))`، و `TryAddEnumerable`.
    - برای Bonus: کلاس را یک بار با نوع خودش ثبت کنید و هر interface را با factory به آن ارجاع دهید (`sp => sp.GetRequiredService(impl)`).
    - **دام:** `TryAddEnumerable` با یک descriptor که factory دارد exception می‌دهد، چون نوع پیاده‌سازی را نمی‌تواند تشخیص دهد. برای این حالت از `TryAdd` استفاده کنید.
- **Task 3:**
    - برای پیدا کردن handlerها: `i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandler<>)`.
    - برای dispatch: `typeof(IHandler<>).MakeGenericType(message.GetType())`.
    - دو راه برای فراخوانی:
        - `Invoke` همراه unwrap کردن `TargetInvocationException` (با `ExceptionDispatchInfo`).
        - یک متد generic کمکی با `MakeGenericMethod` و `CreateDelegate`، که هم سریع‌تر است و هم exception را wrap نمی‌کند.
- **Task 4:**
    - `base(name, isCollectible: true)` و `AssemblyDependencyResolver`.
    - در `Load`، اگر resolver مسیری پیدا نکرد `null` برگردانید تا Default context بارگذاری کند.
    - در Loader: `context.LoadFromAssemblyPath(...)`، سپس `GetTypes()` با فیلتر `IsAbstract` و `IsAssignableFrom`.

</details>

وقتی همه تست‌ها سبز شد (یا وقت تمام شد)، کدتان را برای review بفرستید. مثل یک reviewer ارشد بررسی‌اش می‌کنم و جاهایی را که در آزمون نمره کم می‌کنند نشان می‌دهم.
