# Архитектура ЯсноТекст

Документ описывает архитектурные решения проекта.

## Общая схема

Приложение разделено на три проекта:

```
YasnoText.slnx
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
`PdfTextReader`, `DocxTextReader`, `OcrImageReader`, `OcrPdfReader`.
`PdfTextOrOcrReader` — составной ридер: сначала пробует текстовый
слой PDF, а если его нет, откатывается к рендеру страниц через
`IPdfRenderer` (`PdfiumPdfRenderer`) и OCR.

**Ocr/**
Обёртка над Tesseract (`IOcrService` → `TesseractOcrService`).
Принимает изображение, возвращает текст. Изолирует приложение
от деталей библиотеки OCR.

**Profiles/**
Модели профилей доступности (`ReadingProfile`, `ColorScheme`) и их
сохранение в JSON через `ProfileManager`. Встроенные профили —
`BuiltInProfiles`, применение темы — интерфейс `IThemeApplier`,
список недавних файлов — `RecentFilesService`.
Файл профилей хранится в `%APPDATA%/YasnoText/profiles.json`.

**TextProcessing/**
Постобработка текста после OCR: `TextPostProcessor` склеивает слова,
разорванные переносом строки; `SentenceSplitter` разбивает текст на
предложения для подсветки при озвучке.

**Tts/**
Абстракция синтезатора речи: интерфейс `ITextToSpeechService`,
состояния (`SpeechState`) и события прогресса
(`SpeechProgressEventArgs`). Конкретная реализация живёт в UI-проекте,
потому что зависит от `System.Speech`.

## YasnoText.UI

WPF-приложение, построенное по паттерну MVVM.

### Структура

```
YasnoText.UI/
├── MainWindow.xaml          ← главное окно
├── ProfileNameDialog.xaml   ← диалог имени профиля
├── ViewModels/              ← логика представлений
│   ├── MainViewModel.cs
│   ├── ProfileItemViewModel.cs
│   ├── RelayCommand.cs
│   └── ViewModelBase.cs
├── Themes/                  ← словари ресурсов под каждый профиль
│   ├── StandardTheme.xaml
│   ├── LowVisionTheme.xaml
│   ├── DyslexiaTheme.xaml
│   ├── ThemeManager.cs
│   └── WpfThemeApplier.cs
├── Tts/                     ← реализация TTS через System.Speech
│   └── SystemSpeechService.cs
├── Resources/               ← шрифты (OpenDyslexic)
└── App.xaml
```

### Переключение тем

Каждая тема — это `ResourceDictionary` с одинаковыми ключами,
но разными значениями. При смене профиля `ThemeManager` очищает
текущие словари и подгружает нужный:

```csharp
public static void ApplyTheme(string themeName)
{
    var themeUri = new Uri(
        $"/YasnoText.UI;component/Themes/{themeName}Theme.xaml",
        UriKind.Relative);
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
- Постобработка текста после OCR и разбивка на предложения.
- Выбор нужного ридера и корректное определение типа документа.

UI-тестов в MVP нет — слишком трудоёмко для семестрового проекта.

## Поток обработки документа

1. Пользователь нажимает «Открыть».
2. По расширению файла выбирается нужный `IDocumentReader`.
3. Если PDF и в нём нет текстового слоя — страницы рендерятся
   в изображения через `IPdfRenderer` и отправляются в OCR
   (`IOcrService`).
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
