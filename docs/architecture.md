# Архитектура ЯсноТекст

Документ описывает архитектурные решения проекта.

## Общая схема

Приложение разделено на три проекта:

```
YasnoText.sln
├── YasnoText.Core      ← бизнес-логика (без UI)
├── YasnoText.UI        ← WPF-приложение
└── YasnoText.Tests     ← юнит-тесты
```

Такое разделение даёт:
- Возможность тестировать логику без запуска интерфейса.
- Чёткое разграничение ответственности.
- В будущем можно сделать другой UI (например, веб-версию)
  без переписывания ядра.

## YasnoText.Core

Содержит всю бизнес-логику, не зависящую от WPF.

### Модули

**DocumentReaders/**
Чтение документов разных форматов. Используется паттерн Strategy:
общий интерфейс `IDocumentReader`, конкретные реализации
`PdfReader` и `DocxReader`.

**OcrEngine/**
Обёртка над Tesseract.NET. Принимает изображение, возвращает текст.
Изолирует приложение от деталей библиотеки OCR.

**Profiles/**
Модели профилей доступности и их сохранение в JSON.
Файл хранится в `%APPDATA%/YasnoText/profiles.json`.

**TextProcessing/**
Постобработка текста после OCR: удаление переносов, склейка строк,
нормализация пробелов.

## YasnoText.UI

WPF-приложение, построенное по паттерну MVVM.

### Структура

```
YasnoText.UI/
├── Views/              ← XAML-окна
│   ├── MainWindow.xaml
│   └── ProfileEditorWindow.xaml
├── ViewModels/         ← логика представлений
│   ├── MainViewModel.cs
│   └── ProfileEditorViewModel.cs
├── Themes/             ← словари ресурсов под каждый профиль
│   ├── StandardTheme.xaml
│   ├── LowVisionTheme.xaml
│   └── DyslexiaTheme.xaml
├── Resources/          ← шрифты (OpenDyslexic)
└── App.xaml
```

### Переключение тем

Каждая тема — это `ResourceDictionary` с одинаковыми ключами,
но разными значениями. При смене профиля приложение очищает
текущие словари и подгружает нужный:

```csharp
public void ApplyProfile(ReadingProfile profile)
{
    var themeUri = new Uri($"/Themes/{profile.ThemeName}.xaml", UriKind.Relative);
    var theme = (ResourceDictionary)Application.LoadComponent(themeUri);

    Application.Current.Resources.MergedDictionaries.Clear();
    Application.Current.Resources.MergedDictionaries.Add(theme);
}
```

Все XAML-элементы используют `DynamicResource` (не `StaticResource`),
поэтому реагируют на смену словаря автоматически.

## YasnoText.Tests

Юнит-тесты на xUnit. Покрывают бизнес-логику в Core:
- Сериализация/десериализация профилей.
- Постобработка текста после OCR.
- Корректное определение типа документа.

UI-тестов в MVP нет — слишком трудоёмко для семестрового проекта.

## Поток обработки документа

1. Пользователь нажимает «Открыть».
2. По расширению файла выбирается нужный `IDocumentReader`.
3. Если PDF и в нём нет текстового слоя — страницы рендерятся
   в изображения и отправляются в `OcrEngine`.
4. Полученный текст проходит через `TextProcessing` (удаление
   переносов и т.п.).
5. Текст попадает в `MainViewModel` и отображается через
   `FlowDocumentScrollViewer` в области чтения.
6. К отображению применяется текущий профиль.

## Хранение данных

Приложение не использует базу данных. Всё хранится в JSON-файле
профилей. Это упрощает разработку и не требует прав администратора
для установки.

## Что не входит в MVP

- Облачная синхронизация профилей.
- Распознавание формул и таблиц.
- Поддержка DjVu (требует внешних библиотек).
- Мобильная версия.
- Совместная работа.
