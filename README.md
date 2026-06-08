# Дипломные работы — Blazor WebAssembly

[![Deploy to GitHub Pages](https://github.com/IT-GSTU/diplomas/actions/workflows/deploy.yml/badge.svg)](https://github.com/IT-GSTU/diplomas/actions/workflows/deploy.yml)

Простое веб-приложение для **просмотра, добавления, редактирования и удаления**
списка дипломных работ (как в `Дипломные работы ИТ-4_2026.xlsx`).

- **Просматривать могут все.** Изменять — **только администратор**.
- Стек: **ASP.NET Blazor WebAssembly, .NET 10**. **Без СУБД.**
- Размещение: **GitHub Pages** (статический хостинг).
- 🔗 **Сайт:** https://it-gstu.github.io/diplomas/

## Как это работает (без сервера и без БД)

GitHub Pages отдаёт только готовые файлы, поэтому всё работает в браузере:

- **Чтение (для всех):** данные загружаются из файла `data/diplomas.json` в
  репозитории по raw-URL — без токена.
- **Запись (для администратора):** изменения сохраняются прямо в
  `data/diplomas.json` через **GitHub Contents API**. Администратор входит по
  своему **персональному токену доступа**. Право на запись проверяет сам GitHub:
  без токена с доступом к репозиторию сохранить ничего нельзя — это и есть
  настоящая защита, а не просто скрытие кнопок в интерфейсе.

```
Браузер (Blazor WASM)
   ├── чтение  → raw.githubusercontent.com/<owner>/<repo>/<branch>/data/diplomas.json
   └── запись  → api.github.com (Contents API, с токеном администратора)
```

## Структура

```
.
├─ .github/workflows/deploy.yml         автоматическая публикация на GitHub Pages
├─ data/diplomas.json                   данные (изменяются через интерфейс администратором)
├─ tools/Convert-Xlsx.ps1               конвертер Excel → JSON
└─ DiplomasViewer/                      проект Blazor WebAssembly
   ├─ wwwroot/appsettings.json          настройки репозитория-хранилища
   ├─ wwwroot/sample-data/diplomas.json встроенная резервная копия (на случай, если основной источник недоступен)
   ├─ Models/                           Diploma, GitHubOptions
   ├─ Services/                         GitHubClient, DiplomaService, AdminState
   ├─ Pages/                            Home (список), Login (вход)
   └─ Components/DiplomaEditDialog.razor форма добавления/редактирования
```

## Настройка под свой репозиторий

Отредактируйте `DiplomasViewer/wwwroot/appsettings.json`:

```json
{
  "GitHub": {
    "Owner": "IT-GSTU",
    "Repo": "diplomas",
    "Branch": "main",
    "DataPath": "data/diplomas.json"
  }
}
```

`base href` для GitHub Pages выставляется автоматически при публикации по имени
репозитория (`/<repo>/`), отдельно менять не нужно.

## Запуск локально

```bash
cd DiplomasViewer
dotnet run
```

Откройте адрес из консоли. Без токена доступен только просмотр.

## Публикация на GitHub Pages

1. Создайте новый репозиторий (например `IT-GSTU/diplomas`) и отправьте в него
   содержимое этой папки в ветку `main`.
2. В репозитории: **Settings → Pages → Build and deployment → Source: GitHub
   Actions**.
3. Файл `deploy.yml` сам соберёт и опубликует сайт при отправке изменений в `main`.
4. Сайт будет доступен по адресу `https://<owner>.github.io/<repo>/`.

## Вход администратора и токен

1. GitHub → **Settings → Developer settings → Personal access tokens**.
2. Рекомендуется токен с точно настроенными правами доступа — в GitHub такой
   тип называется **fine-grained**: дайте ему доступ только к нужному
   репозиторию и право **Contents: Read and write**. (Подойдёт и токен
   старого образца — **classic** — с областью действия `public_repo`.)
3. В приложении откройте **«Вход администратора»**, вставьте токен, нажмите
   «Войти». Появятся кнопки добавления/редактирования/удаления.

Токен хранится только в текущей вкладке браузера (`sessionStorage`), нигде не
публикуется и не попадает в код. Никому его не передавайте.

## Обновление начальных данных из Excel

Положите `*.xlsx` в корень репозитория и выполните:

```powershell
powershell -File ./tools/Convert-Xlsx.ps1
```

Скрипт пересоздаст `data/diplomas.json` и встроенную резервную копию. Колонки:
`Группа, Студент, Тема, Руководитель, Краткое описание, URL репозитория с кодом,
URL инсталляционного файла, URL демо-версии, Год`.

## Замечания по безопасности

- Реальная защита записи — на стороне GitHub. Клиентская проверка только
  показывает/скрывает элементы интерфейса.
- Данные в публичном репозитории доступны на чтение всем — это ожидаемо
  (список публичный).
- raw-URL кэшируется CDN (примерно до 5 минут): администратор видит свои правки
  сразу (читает через API), остальные пользователи — после обновления кэша.
