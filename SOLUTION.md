# راه‌حل گام‌به‌گام — Code Challenge 01 (Assemblies، Plugin و Package Management)

این فایل توضیح می‌دهد هر Task چطور حل شده و **چرا**. کنار کد بخوانید. هر ۲۵ تست سبز است.

برای هر Task این‌ها آمده: مسئله، راه‌حل، نکته‌های کلیدی، و سؤال‌هایی که در مصاحبه یا آزمون از همین موضوع می‌پرسند.

---

## Task 1 — Package management

**فایل‌ها:** `Directory.Packages.props`، `Directory.Build.props` و csprojها. این Task را خودتان حل کردید.

### مسئله

هر csproj نسخه پکیج‌ها و سه تنظیم مشترک را تکرار می‌کرد. با زیاد شدن پروژه‌ها، نسخه‌ها از هم فاصله می‌گیرند و ارتقا سخت می‌شود.

### راه‌حل

**۱. Central Package Management.** در `Directory.Packages.props`:

```xml
<PropertyGroup>
  <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
</PropertyGroup>
<ItemGroup>
  <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
  ...
</ItemGroup>
```

در csprojها فقط نام پکیج می‌آید: `<PackageReference Include="Newtonsoft.Json" />`.

- `PackageVersion` می‌گوید **چه نسخه‌ای**؛ `PackageReference` می‌گوید **کدام پروژه به کدام پکیج نیاز دارد**.
- اگر `Version` روی `PackageReference` بماند، خطای **NU1008** می‌گیرید.
- اگر یک پکیج دو بار در `PackageVersion` تعریف شود، هشدار **NU1506**.

**۲. استثنا برای plugin.** plugin باید Newtonsoft 12 بگیرد، در حالی که نسخه مرکزی 13 است:

```xml
<PackageReference Include="Newtonsoft.Json" VersionOverride="12.0.1" />
```

`VersionOverride` راه رسمی استثنا قائل شدن برای یک پروژه است.

**۳. تنظیمات مشترک.** `TargetFramework`، `Nullable` و `ImplicitUsings` به `Directory.Build.props` رفتند. MSBuild این فایل را از پوشه پروژه به سمت بالا جستجو می‌کند و **قبل از** محتوای csproj import می‌کند، پس همه پروژه‌های زیر آن پوشه این تنظیمات را به ارث می‌برند.

**۴. ارجاع plugin به Contracts.**

```xml
<ProjectReference Include="..\..\src\Shop.Contracts\Shop.Contracts.csproj">
  <Private>false</Private>
  <ExcludeAssets>runtime</ExcludeAssets>
</ProjectReference>
```

- `Private=false` یعنی `Shop.Contracts.dll` در پوشه خروجی plugin **کپی نشود**.
- `ExcludeAssets=runtime` یعنی در `deps.json` پلاگین هم به‌عنوان وابستگی runtime نیاید.

### چرا مورد ۴ مهم است

این خط پایه Task 4 است. اگر plugin نسخه خودش از `Shop.Contracts.dll` را داشته باشد، resolver آن را پیدا می‌کند و **داخل context پلاگین** بارگذاری می‌کند. آن‌وقت دو `IPlugin` در process هست: یکی در Default context و یکی در context پلاگین. از نظر CLR این دو **type متفاوت**‌اند، چون هویت type برابر است با «نام کامل + assembly + load context». نتیجه: `typeof(IPlugin).IsAssignableFrom(pluginType)` مقدار `false` می‌دهد و هیچ pluginی پیدا نمی‌شود.

### دو نکته باقی‌مانده در کد شما

تست‌ها رد نمی‌کنند، ولی در review گفته می‌شود:

- `xunit.assert` و `xunit.extensibility.core` در `Directory.Packages.props` تعریف شده‌اند ولی هیچ پروژه‌ای از آن‌ها استفاده نمی‌کند.
- `EnableNETAnalyzers` در .NET 8 به‌صورت پیش‌فرض روشن است.

### سؤال‌های احتمالی

- `Directory.Build.props` و `Directory.Build.targets` چه فرقی دارند؟ props **قبل از** csproj import می‌شود (مقدار پیش‌فرض، که csproj می‌تواند عوضش کند). targets **بعد از** آن (برای چیزهایی که به مقادیر csproj وابسته‌اند، یا targetهای سفارشی).
- `PrivateAssets="all"` چه می‌کند؟ وابستگی به مصرف‌کننده‌های این پروژه **منتقل نمی‌شود** (برای analyzerها و ابزار build).
- transitive pinning چیست؟ با `CentralPackageTransitivePinningEnabled`، نسخه مرکزی روی وابستگی‌های غیرمستقیم هم اعمال می‌شود.

---

## Task 2 — ثبت خودکار سرویس‌ها با reflection

**فایل:** `src/Shop.Core/Registration/ServiceCollectionExtensions.cs`، متد `AddServicesFromAssembly`. بیشتر این Task را خودتان نوشتید؛ من یک بخش را تغییر دادم.

### مسئله

به‌جای نوشتن دستی `services.AddScoped<IOrderService, OrderService>()` برای هر کلاس، assembly را scan کنیم و هر کلاسی را که یک marker interface (`IScopedService`، `ISingletonService`، `ITransientService`) دارد ثبت کنیم.

### راه‌حل، مرحله به مرحله

**۱. گرفتن typeها و فیلتر کردن.**

```csharp
if (!implementationType.IsClass ||
    implementationType.IsAbstract ||
    implementationType.ContainsGenericParameters)
    continue;
```

- `IsClass` interfaceها و structها را کنار می‌گذارد.
- `IsAbstract` کلاس‌های abstract را (کلاس `static` هم در IL به شکل abstract sealed است).
- `ContainsGenericParameters` open genericها را؛ نمی‌توان از `Foo<T>` بدون دانستن `T` نمونه ساخت.

`assembly.GetTypes()` typeهای **internal** را هم برمی‌گرداند (برخلاف `GetExportedTypes`). برای همین `AuditService` که internal است ثبت می‌شود.

**۲. رد کردن کلاس‌های `[SkipRegistration]`** با `GetCustomAttribute<SkipRegistrationAttribute>()`.

**۳. تعیین lifetime از روی marker.** `typeof(IScopedService).IsAssignableFrom(implementationType)` یعنی «آیا این کلاس `IScopedService` را پیاده کرده؟». اگر کلاسی دو marker داشته باشد، کد شما exception می‌دهد. این تصمیم خوبی است: شکست زودهنگام بهتر از رفتار مبهم.

**۴. ثبت.** دو حالت هست و این همان بخشی است که تغییر دادم.

**حالت الف — کلاس یک interface دارد** (مثل `OrderService : IOrderService`):

```csharp
services.Add(ServiceDescriptor.Describe(serviceInterface, implementationType, lifetime));
```

یک descriptor ساده «interface ← نوع پیاده‌سازی».

**حالت ب — کلاس چند interface دارد** (مثل `ReportService : IReportReader, IReportWriter`). اگر هر دو را ساده ثبت کنیم:

```csharp
services.AddSingleton<IReportReader, ReportService>();
services.AddSingleton<IReportWriter, ReportService>();
```

DI **دو نمونه جدا** می‌سازد، چون هر descriptor مستقل است. چیزی که writer می‌نویسد reader نمی‌بیند. راه‌حل، **forwarding** است:

```csharp
services.AddSingleton<ReportService>();                                    // نمونه واقعی
services.AddSingleton<IReportReader>(sp => sp.GetRequiredService<ReportService>());
services.AddSingleton<IReportWriter>(sp => sp.GetRequiredService<ReportService>());
```

هر دو interface از همان ثبت concrete می‌خوانند، پس یک نمونه مشترک دارند. کلاس `ServiceFactory` در کد شما همین factory را می‌سازد.

### چه چیزی را تغییر دادم و چرا

کد شما **همه** interfaceها را با factory ثبت می‌کرد، حتی برای کلاس تک‌interface. مشکل: descriptorی که factory دارد، `ImplementationType` آن `null` است. تست `Abstract_and_skipped_classes_are_ignored` دقیقاً همین را بررسی می‌کرد (`Expected: OrderService`، `Actual: null`).

در عمل هم مهم است: ابزارهای تشخیصی، decoratorها (مثل Scrutor) و اعتبارسنجی container به `ImplementationType` نگاه می‌کنند. پس فقط جایی از factory استفاده می‌کنیم که لازم است: کلاس‌های چند-interface.

### جلوگیری از ثبت تکراری

کد شما قبل از هر `Add` با `services.Any(...)` بررسی می‌کند که descriptor مشابه نباشد. درست کار می‌کند. راه کوتاه‌تر framework:

- `TryAdd(descriptor)`: فقط اگر برای آن **service type** هیچ ثبتی نباشد.
- `TryAddEnumerable(descriptor)`: فقط اگر همان زوج **(service type، implementation type)** نباشد. برای وقتی که چند پیاده‌سازی از یک interface مجاز است.

**دام:** `TryAddEnumerable` با descriptor دارای factory exception می‌دهد، چون نمی‌تواند نوع پیاده‌سازی را تشخیص دهد. برای همین بررسی دستی شما با `ServiceFactory.ImplementationType` در حالت forwarding لازم است.

### سؤال‌های احتمالی

- چرا `GetTypes()` ممکن است exception بدهد؟ اگر یکی از typeها به assemblyی وابسته باشد که بارگذاری نمی‌شود، `ReflectionTypeLoadException` می‌دهد. property آن به نام `Types` فهرست typeهای بارگذاری‌شده را دارد (با `null` برای ناموفق‌ها).
- `AppDomain.GetAssemblies()` چه مشکلی برای scan دارد؟ فقط assemblyهای **بارگذاری‌شده تا آن لحظه** را می‌دهد. CLR بارگذاری را lazy انجام می‌دهد، پس assemblyی که هنوز کدی از آن اجرا نشده در فهرست نیست. راه مطمئن: دادن صریح assembly (مثل `typeof(Marker).Assembly`).
- captive dependency چیست؟ تزریق سرویس scoped در singleton. سرویس scoped تا پایان عمر برنامه زنده می‌ماند. `ValidateScopes` این را تشخیص می‌دهد.

---

## Task 3 — Handlerهای generic و dispatch با نوع runtime

**فایل‌ها:** متد `AddHandlersFromAssembly` در `ServiceCollectionExtensions.cs` و `src/Shop.Core/Messaging/MessageDispatcher.cs`.

### بخش الف — ثبت handlerها

**مسئله:** همه کلاس‌هایی را پیدا کن که `IHandler<چیزی>` را پیاده کرده‌اند.

`IHandler<T>` generic است، پس نمی‌توان نوشت `typeof(IHandler).IsAssignableFrom(type)`. باید پرسید: «آیا این interface از **تعریف generic** یعنی `IHandler<>` ساخته شده؟»

```csharp
var handlerInterfaces = type.GetInterfaces()
    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandler<>));
```

- `typeof(IHandler<>)` (با براکت خالی) **open generic type definition** است.
- `typeof(IHandler<OrderPlaced>)` یک **closed constructed type** است.
- `GetGenericTypeDefinition()` از دومی به اولی می‌رسد.
- `i.IsGenericType` باید اول بررسی شود، وگرنه `GetGenericTypeDefinition` روی interface غیر generic exception می‌دهد.

ثبت:

```csharp
services.TryAddEnumerable(ServiceDescriptor.Transient(handlerInterface, type));
```

- `NotificationHandler` دو interface دارد (`IHandler<OrderPlaced>` و `IHandler<PaymentReceived>`)، پس حلقه دو بار اجرا می‌شود و برای هر دو ثبت می‌شود.
- `TryAddEnumerable` اجازه می‌دهد برای `IHandler<OrderPlaced>` **چند handler** ثبت شود (`OrderPlacedHandler` و `NotificationHandler`)، ولی یک زوج را دو بار ثبت نمی‌کند.

**رد کردن open generic:** `LoggingHandler<T>` با شرط `type.IsGenericTypeDefinition` کنار می‌رود. اگر رد نمی‌شد، interface آن `IHandler<T>` با `T` نامشخص بود و ثبتش بی‌معنی.

### بخش ب — Dispatcher

**مسئله:** `Dispatch(object message)` فقط یک `object` می‌گیرد. باید handlerهای `IHandler<نوع واقعی پیام>` را از DI بگیرد و `Handle` را صدا بزند. نوع در زمان compile معلوم نیست.

**راه ساده و مشکل آن:**

```csharp
var handlerType = typeof(IHandler<>).MakeGenericType(message.GetType());
var handlers = provider.GetServices(handlerType);
foreach (var h in handlers)
    handlerType.GetMethod("Handle")!.Invoke(h, [message]);   // دو مشکل
```

1. `MethodInfo.Invoke` exception داخل متد را در **`TargetInvocationException`** می‌پیچد. caller به‌جای `InvalidOperationException` چیز دیگری می‌بیند. باید دستی unwrap شود (`ExceptionDispatchInfo.Capture(ex.InnerException).Throw()` تا stack trace حفظ شود).
2. reflection در هر فراخوانی **کند** است.

**راه انتخاب‌شده: متد generic کمکی و delegate cache‌شده.**

گام ۱، یک متد generic که وقتی `T` معلوم باشد کار را بدون reflection انجام می‌دهد:

```csharp
private static IReadOnlyList<string> DispatchTyped<T>(IServiceProvider provider, object message)
{
    return provider.GetServices<IHandler<T>>()
        .Select(handler => handler.Handle((T)message))
        .ToList();
}
```

داخل این متد همه چیز strongly typed است: `GetServices<IHandler<T>>()` و `handler.Handle(...)` فراخوانی معمولی‌اند.

گام ۲، بستن این متد روی نوع runtime و تبدیلش به delegate:

```csharp
DispatchTypedMethod.MakeGenericMethod(messageType).CreateDelegate<Invoker>()
```

- `MakeGenericMethod(typeof(OrderPlaced))` متد `DispatchTyped<OrderPlaced>` را می‌سازد.
- `CreateDelegate` آن را به یک delegate تبدیل می‌کند. فراخوانی delegate تقریباً به سرعت فراخوانی مستقیم است و **exception را نمی‌پیچد**.

گام ۳، cache:

```csharp
private static readonly ConcurrentDictionary<Type, Invoker> Invokers = new();
var invoker = Invokers.GetOrAdd(message.GetType(), ...);
```

reflection برای هر نوع پیام فقط **یک بار** اجرا می‌شود.

### نکته‌ها

- `message.GetType()` نوع **runtime** است. نوع compile-time پارامتر فقط `object` است.
- حالت «handlerی نیست» به کد جدا نیاز ندارد: `GetServices<T>()` برای سرویس ثبت‌نشده دنباله خالی برمی‌گرداند (برخلاف `GetRequiredService` که exception می‌دهد).
- ترتیب نتایج همان ترتیب ثبت است؛ `GetServices` این را تضمین می‌کند.

### سؤال‌های احتمالی

- `MakeGenericType` و `MakeGenericMethod` چه فرقی دارند؟ اولی یک **type** generic را می‌بندد، دومی یک **متد** generic را.
- جایگزین‌های `MethodInfo.Invoke` چیست؟ `CreateDelegate` (سریع، برای امضای معلوم)، expression tree کامپایل‌شده (انعطاف بیشتر)، `dynamic` (ساده، با cache داخلی DLR)، و source generator (بدون reflection در runtime).
- `dynamic` چه محدودیتی دارد؟ extension methodها را پیدا نمی‌کند، روی عضو explicit interface کار نمی‌کند، و خطاها به runtime منتقل می‌شوند.

---

## Task 4 — بارگذاری plugin در AssemblyLoadContext جدا

**فایل‌ها:** `src/Shop.PluginHost/PluginLoadContext.cs` و `PluginLoader.cs`.

### مسئله

host از Newtonsoft.Json **13** استفاده می‌کند و plugin از نسخه **12**. در یک load context فقط یک نسخه از هر assembly می‌تواند باشد. می‌خواهیم هر دو کنار هم کار کنند، و plugin قابل unload باشد.

### AssemblyLoadContext چیست

یک «ظرف» برای assemblyهای بارگذاری‌شده. هر process یک **Default context** دارد. می‌توان contextهای اضافه ساخت که هر کدام نسخه‌های خودشان از assemblyها را داشته باشند. در .NET Core و بعد از آن، ALC جایگزین AppDomain برای ایزولاسیون است.

### PluginLoadContext

```csharp
public PluginLoadContext(string pluginAssemblyPath)
    : base(name: ..., isCollectible: true)
{
    _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
}
```

- **`isCollectible: true`:** بدون آن `Unload()` exception می‌دهد. context قابل جمع‌آوری سربار کمی دارد، ولی تنها راه unload است.
- **`AssemblyDependencyResolver`:** فایل `Shop.Plugins.Discount.deps.json` را می‌خواند و می‌داند وابستگی‌های plugin کجا هستند. برای اینکه `deps.json` و وابستگی‌ها در پوشه خروجی plugin باشند، csproj پلاگین `EnableDynamicLoading=true` دارد.

متد اصلی:

```csharp
protected override Assembly? Load(AssemblyName assemblyName)
{
    var path = _resolver.ResolveAssemblyToPath(assemblyName);

    if (path is not null)
        return LoadFromAssemblyPath(path);   // plugin این را همراه دارد: همین‌جا بارگذاری کن

    return null;                             // ندارد: به Default context واگذار کن
}
```

runtime هر وقت کدی در این context به assemblyی نیاز داشته باشد، **اول** `Load` را صدا می‌زند:

| assembly درخواستی | resolver | نتیجه |
|---|---|---|
| `Newtonsoft.Json` | مسیر نسخه 12 را در پوشه plugin پیدا می‌کند | در context پلاگین بارگذاری می‌شود، جدا از نسخه 13 host |
| `Shop.Contracts` | چیزی پیدا نمی‌کند (به‌خاطر `Private=false` در Task 1) | `null`، پس Default context همان نسخه host را می‌دهد |
| `System.Runtime` و بقیه framework | چیزی پیدا نمی‌کند | `null`، پس از Default context |

**`return null` مهم‌ترین خط است.** معنی‌اش «من نمی‌دانم، از Default بپرس» است. همین باعث می‌شود host و plugin **یک** `IPlugin` مشترک داشته باشند.

`LoadUnmanagedDll` همین منطق را برای کتابخانه‌های native دارد.

### PluginLoader

```csharp
var fullPath = Path.GetFullPath(pluginAssemblyPath);
var context = new PluginLoadContext(fullPath);
var assembly = context.LoadFromAssemblyPath(fullPath);

var plugins = assembly.GetTypes()
    .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IPlugin).IsAssignableFrom(type))
    .Select(type => (IPlugin)Activator.CreateInstance(type)!)
    .ToList();
```

- مسیر باید **مطلق** باشد؛ `LoadFromAssemblyPath` مسیر نسبی نمی‌پذیرد.
- `IsAbstract: false` کلاس `PluginBase` را کنار می‌گذارد.
- `typeof(IPlugin)` نوع **host** است. این شرط فقط به این دلیل `true` می‌شود که `Shop.Contracts` پلاگین هم از Default context آمده است.
- `Activator.CreateInstance` به constructor بدون پارامتر نیاز دارد.

### Unload چطور کار می‌کند

`context.Unload()` فقط **درخواست** است. context واقعاً وقتی آزاد می‌شود که **هیچ ارجاعی** به آن، به assemblyهایش، به typeهایش و به نمونه‌های آن typeها نمانده باشد و GC اجرا شود. برای همین تست:

- کار با plugin را در یک متد جدا با `[MethodImpl(NoInlining)]` انجام می‌دهد، تا متغیرهای محلی آن متد از stack خارج شوند.
- فقط یک `WeakReference` به context نگه می‌دارد.
- در حلقه `GC.Collect()` و `GC.WaitForPendingFinalizers()` صدا می‌زند.

**دام واقعی:** تست unload عمداً `Execute()` را صدا نمی‌زند. وقتی plugin با Newtonsoft چیزی serialize می‌کند، اطلاعات reflection درباره typeها در cacheهای static نگه داشته می‌شود و context زنده می‌ماند. در سیستم‌های plugin واقعی هم هر cache static، event handler، thread یا timer که به type پلاگین ارجاع دارد جلوی unload را می‌گیرد.

### سؤال‌های احتمالی

- هویت یک type از چه چیزهایی تشکیل می‌شود؟ نام کامل، assembly و **load context**. یک assembly که در دو context بارگذاری شود، typeهایش با هم ناسازگارند و cast بینشان `InvalidCastException` می‌دهد.
- چرا contract باید در assembly جدا و مشترک باشد؟ تا host و plugin به **یک** نسخه از interface ارجاع دهند.
- `LoadFromAssemblyPath` و `Assembly.LoadFrom` چه فرقی دارند؟ اولی در **همان context** که رویش صدا زده شده بارگذاری می‌کند. دومی همیشه در Default context، بدون ایزولاسیون.
- ALC با AppDomain چه فرقی دارد؟ ALC فقط **بارگذاری assembly** را ایزوله می‌کند. مرز امنیتی، ایزولاسیون حافظه و static مشترک را جدا نمی‌کند (static هر type در هر context جداست، چون type جداست).

---

## خلاصه در یک نگاه

| Task | ایده اصلی | مهم‌ترین خط |
|---|---|---|
| ۱ | نسخه‌ها در یک جا؛ plugin نسخه خودش از Contracts را نداشته باشد | `<Private>false</Private>` |
| ۲ | lifetime از marker؛ چند interface به یک نمونه | `sp => sp.GetRequiredService(impl)` |
| ۳ الف | تشخیص interface generic بسته | `i.GetGenericTypeDefinition() == typeof(IHandler<>)` |
| ۳ ب | reflection یک بار، بعد delegate | `MakeGenericMethod(t).CreateDelegate<Invoker>()` |
| ۴ | وابستگی plugin در context خودش، بقیه از Default | `return null;` در `Load` |
