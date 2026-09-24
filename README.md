# Store Debt Desktop | دفتر ديون المحل

برنامج Windows مكتبي عربي لإدارة حسابات الزبائن والديون والتسديدات داخل المحل.

> **Phase 1 — Foundation:** بناء الأساس التقني، قاعدة البيانات المحلية، الواجهة العربية، وإدارة العمليات اليومية الأساسية.

## التقنية

- C# / .NET 10
- WPF + XAML
- MVVM
- SQLite
- Windows 10/11
- واجهة عربية RTL

## ما تتضمنه المرحلة الأولى

- لوحة رئيسية للمحل.
- البحث عن الزبائن.
- إضافة زبون جديد.
- صفحة حساب الزبون.
- تسجيل دين جديد.
- تسجيل تسديد.
- سجل آخر الحركات.
- إجمالي الديون وعدد الزبائن وتحصيلات اليوم.
- إنشاء قاعدة البيانات تلقائياً عند أول تشغيل.
- اختصارات لوحة مفاتيح مبدئية.
- GitHub Actions للتأكد من نجاح البناء على Windows.

## هيكل المشروع

```text
src/StoreDebt.Desktop/
├── Data/
├── Models/
├── Services/
├── ViewModels/
├── Infrastructure/
├── Views/
└── Resources/
```

## التشغيل

يتطلب Windows و .NET 10 SDK للتطوير.

```powershell
dotnet restore src/StoreDebt.Desktop/StoreDebt.Desktop.csproj
dotnet run --project src/StoreDebt.Desktop/StoreDebt.Desktop.csproj
```

## الحالة

المرحلة الأولى قيد البناء. النسخ الاحتياطي والسحابة مؤجلان لمرحلة لاحقة بعد استقرار تجربة الاستخدام الأساسية.

---

Developed by **Radwan Abd alhady Ahmed**.
