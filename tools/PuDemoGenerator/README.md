# Parts Unlimited Demo Generator для GitHub

Утилита наполняет организацию GitHub демонстрационными данными: доской, спринтами,
рабочими элементами и историей их выполнения. Это аналог **Azure DevOps Demo Generator**
(`microsoft/AzDevOpsDemoGenerator`) из практической работы 2.0, переписанный под GitHub.

## Что делает

1. Заводит типы рабочих элементов организации `Epic` и `Product Backlog Item`
   (`Task`, `Bug`, `Feature` существуют по умолчанию).
2. Создаёт метки в репозитории с кодом.
3. Копирует проект из доски-шаблона Team planning — так в новом проекте появляется
   поле `Iteration`, которое через API создать нельзя.
4. Ждёт, пока в интерфейсе будут настроены три спринта (прошедший, текущий, будущий),
   и считывает их даты.
5. Создаёт поля `Area`, `Activity`, `Remaining Work`.
6. Создаёт рабочие элементы: эпики → функциональности → элементы бэклога → задачи,
   плюс ошибки. Связывает их как вложенные задачи, добавляет на доску, проставляет
   статус, область, спринт, оценку и трудоёмкость, часть элементов закрывает.

## Требования

- .NET SDK 8.0 или новее
- Personal Access Token (fine-grained) с правами:
  - организация: `Members: read`, `Projects: read and write`, `Issue types: read and write`
  - репозиторий: `Issues: read and write`, `Metadata: read`

## Сборка и запуск

```powershell
# сборка
dotnet build

# запуск
dotnet run

# сборка-публикация исполняемого файла
dotnet publish -c Release -r win-x64 --self-contained
```

Токен можно ввести в диалоге (ввод скрыт) или передать переменной окружения:

```powershell
$env:GITHUB_TOKEN = "<токен>"
dotnet run
```

## Соответствие шагам методички

| Azure DevOps Demo Generator | Эта утилита |
|---|---|
| `1. Create a new project using the demo generator project template` | Тот же пункт меню |
| `3 | Parts Unlimited` | Тот же выбор шаблона |
| `2. Personal Access Token (PAT)` | Тот же способ авторизации |
| Имя организации | Имя организации GitHub |
| Имя проекта `Unlimited Parts` | Имя создаваемого проекта |
