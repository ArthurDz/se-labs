using System.Text.Json;

namespace PuDemoGenerator;

/// <summary>
/// Parts Unlimited Demo Generator для GitHub.
/// Аналог Azure DevOps Demo Generator: наполняет организацию демонстрационным проектом
/// с досками, спринтами, рабочими элементами и историей выполнения.
/// </summary>
public static class Program
{
    private static GitHubClient _gh = null!;
    private static string _org = "";
    private static string _repo = "";
    private static string _login = "";
    private static string _projectId = "";
    private static int _projectNumber;
    private static readonly Dictionary<string, Issue> _existing = new();

    /// <summary>Синонимы названий статусов в разных шаблонах досок.</summary>
    private static readonly Dictionary<string, string[]> StatusAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Backlog"] = ["Backlog", "Todo", "To do", "New"],
        ["In progress"] = ["In progress", "In Progress", "Doing", "Committed", "Active"],
        ["Done"] = ["Done", "Closed", "Completed"],
    };

    public static async Task<int> Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Banner();

        try
        {
            if (!MainMenu())
            {
                return 0;
            }

            ChooseTemplate();
            ChooseAuth();

            _org = Ask("Введите имя организации GitHub", "ArthurDz");
            var token = AskSecret("Введите Personal Access Token (ввод скрыт)");
            _repo = Ask("Введите репозиторий с кодом проекта", "PartsUnlimited");
            var sourceNumber = int.Parse(Ask("Номер проекта-шаблона (Team planning)", "1"));
            var title = Ask("Введите имя создаваемого проекта", "Unlimited Parts");

            _gh = new GitHubClient(token);
            _login = (await _gh.GetAsync("user")).GetProperty("login").GetString()!;
            Step($"Авторизация выполнена: {_login}");

            await EnsureIssueTypesAsync();
            await EnsureLabelsAsync();
            await CopyProjectAsync(sourceNumber, title);

            var iterations = await WaitForSprintsAsync();
            var fields = await EnsureFieldsAsync();
            await SeedAsync(fields, iterations);

            Console.WriteLine();
            Step($"Готово. Проект: https://github.com/orgs/{_org}/projects/{_projectNumber}");
            Console.WriteLine("Поздравляю, Вы — Великолепны!");
            return 0;
        }
        catch (GitHubException ex)
        {
            Console.WriteLine();
            Error("Ошибка обращения к GitHub:");
            Console.WriteLine(ex.Message);
            return 1;
        }
    }

    // ---------------- Диалог ----------------

    private static void Banner()
    {
        Console.WriteLine("============================================================");
        Console.WriteLine("  Parts Unlimited Demo Generator для GitHub");
        Console.WriteLine("  Практическая работа 2.0 «Подготовка тестовых данных»");
        Console.WriteLine("============================================================");
        Console.WriteLine();
    }

    private static bool MainMenu()
    {
        Console.WriteLine("1. Create a new project using the demo generator project template");
        Console.WriteLine("2. Exit");
        var choice = Ask("Выберите пункт", "1");
        return choice == "1";
    }

    private static void ChooseTemplate()
    {
        Console.WriteLine();
        Console.WriteLine("Доступные шаблоны:");
        Console.WriteLine("  1 | Agile");
        Console.WriteLine("  2 | Scrum");
        Console.WriteLine("  3 | Parts Unlimited");
        var choice = Ask("Выберите шаблон", "3");
        if (choice != "3")
        {
            Console.WriteLine("В этой версии реализован только шаблон Parts Unlimited, используется он.");
        }
    }

    private static void ChooseAuth()
    {
        Console.WriteLine();
        Console.WriteLine("Способ авторизации:");
        Console.WriteLine("  1. Device flow (не реализовано)");
        Console.WriteLine("  2. Personal Access Token (PAT)");
        Ask("Выберите способ авторизации", "2");
    }

    private static string Ask(string prompt, string def)
    {
        Console.Write($"{prompt} [{def}]: ");
        var value = Console.ReadLine();
        return string.IsNullOrWhiteSpace(value) ? def : value.Trim();
    }

    private static string AskSecret(string prompt)
    {
        var fromEnv = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            Console.WriteLine($"{prompt}: взят из переменной окружения GITHUB_TOKEN");
            return fromEnv.Trim();
        }

        Console.Write($"{prompt}: ");
        var token = "";
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace)
            {
                if (token.Length > 0) token = token[..^1];
                continue;
            }
            token += key.KeyChar;
        }

        Console.WriteLine();
        return token.Trim();
    }

    private static void Step(string text)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[OK] {text}");
        Console.ResetColor();
    }

    private static void Info(string text) => Console.WriteLine($"     {text}");

    private static void Error(string text)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    // ---------------- Шаги наполнения ----------------

    /// <summary>Типы рабочих элементов: Epic и Product Backlog Item (Task, Bug, Feature есть по умолчанию).</summary>
    private static async Task EnsureIssueTypesAsync()
    {
        var existing = await _gh.GetAsync($"orgs/{_org}/issue-types");
        var names = existing.EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToHashSet();

        (string Name, string Color, string Description)[] wanted =
        [
            ("Epic", "purple", "Крупная цель, объединяющая функциональности"),
            ("Product Backlog Item", "blue", "Элемент невыполненной работы (пользовательская история)"),
        ];

        foreach (var type in wanted)
        {
            if (names.Contains(type.Name))
            {
                Info($"Тип рабочего элемента «{type.Name}» уже существует");
                continue;
            }

            await _gh.PostAsync($"orgs/{_org}/issue-types", new
            {
                name = type.Name,
                is_enabled = true,
                description = type.Description,
                color = type.Color,
            });
            Step($"Создан тип рабочего элемента «{type.Name}»");
        }
    }

    private static async Task EnsureLabelsAsync()
    {
        foreach (var (name, color, description) in SeedData.Labels)
        {
            var created = await _gh.TryPostAsync($"repos/{_org}/{_repo}/labels",
                new { name, color, description });
            Info(created ? $"Создана метка «{name}»" : $"Метка «{name}» уже существует");
        }

        Step("Метки проекта готовы");
    }

    private static async Task CopyProjectAsync(int sourceNumber, string title)
    {
        // Повторный запуск не должен плодить проекты: если проект с таким именем уже есть — берём его.
        var existing = await _gh.GraphQlAsync(
            """
            query($org: String!) {
              organization(login: $org) {
                projectsV2(first: 50) { nodes { id number title url } }
              }
            }
            """,
            new { org = _org });

        foreach (var node in existing.GetProperty("organization").GetProperty("projectsV2")
                     .GetProperty("nodes").EnumerateArray())
        {
            if (!string.Equals(node.GetProperty("title").GetString(), title, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _projectId = node.GetProperty("id").GetString()!;
            _projectNumber = node.GetProperty("number").GetInt32();
            Step($"Используется существующий проект «{title}»: {node.GetProperty("url").GetString()}");
            return;
        }

        var data = await _gh.GraphQlAsync(
            """
            query($org: String!, $number: Int!) {
              organization(login: $org) {
                id
                projectV2(number: $number) { id title }
              }
            }
            """,
            new { org = _org, number = sourceNumber });

        var ownerId = data.GetProperty("organization").GetProperty("id").GetString()!;
        var sourceId = data.GetProperty("organization").GetProperty("projectV2").GetProperty("id").GetString()!;

        var copy = await _gh.GraphQlAsync(
            """
            mutation($owner: ID!, $source: ID!, $title: String!) {
              copyProjectV2(input: {ownerId: $owner, projectId: $source, title: $title}) {
                projectV2 { id number url }
              }
            }
            """,
            new { owner = ownerId, source = sourceId, title });

        var project = copy.GetProperty("copyProjectV2").GetProperty("projectV2");
        _projectId = project.GetProperty("id").GetString()!;
        _projectNumber = project.GetProperty("number").GetInt32();

        Step($"Создан проект «{title}»: {project.GetProperty("url").GetString()}");
    }

    /// <summary>
    /// Поле типа Iteration через API не создаётся и не настраивается,
    /// поэтому даты спринтов задаются в интерфейсе, а утилита их считывает.
    /// </summary>
    private static async Task<List<Iteration>> WaitForSprintsAsync()
    {
        Console.WriteLine();
        Console.WriteLine("--- Требуется настройка спринтов ------------------------------");
        Console.WriteLine($"1. Откройте https://github.com/orgs/{_org}/projects/{_projectNumber}/settings");
        Console.WriteLine("2. В разделе Fields выберите поле Iteration и задайте три спринта:");
        Console.WriteLine("     Sprint 1 — завершившийся (две недели назад)");
        Console.WriteLine("     Sprint 2 — текущий");
        Console.WriteLine("     Sprint 3 — следующий");
        Console.WriteLine("3. Вернитесь сюда и нажмите Enter.");
        Console.WriteLine("---------------------------------------------------------------");
        Console.ReadLine();

        while (true)
        {
            var iterations = await ReadIterationsAsync();
            if (iterations.Count >= 3)
            {
                Step($"Найдено спринтов: {iterations.Count}");
                foreach (var it in iterations)
                {
                    Info($"{it.Title}: {it.StartDate:yyyy-MM-dd} — {it.EndDate:yyyy-MM-dd}");
                }

                return iterations;
            }

            Error($"Найдено спринтов: {iterations.Count}, требуется не менее трёх. Исправьте и нажмите Enter.");
            Console.ReadLine();
        }
    }

    private static async Task<List<Iteration>> ReadIterationsAsync()
    {
        var data = await _gh.GraphQlAsync(
            """
            query($id: ID!) {
              node(id: $id) {
                ... on ProjectV2 {
                  field(name: "Iteration") {
                    ... on ProjectV2IterationField {
                      id
                      configuration {
                        iterations { id title startDate duration }
                        completedIterations { id title startDate duration }
                      }
                    }
                  }
                }
              }
            }
            """,
            new { id = _projectId });

        var field = data.GetProperty("node").GetProperty("field");
        var result = new List<Iteration>();

        foreach (var key in new[] { "completedIterations", "iterations" })
        {
            foreach (var it in field.GetProperty("configuration").GetProperty(key).EnumerateArray())
            {
                var start = DateOnly.Parse(it.GetProperty("startDate").GetString()!);
                var duration = it.GetProperty("duration").GetInt32();
                result.Add(new Iteration(
                    it.GetProperty("id").GetString()!,
                    it.GetProperty("title").GetString()!,
                    start,
                    start.AddDays(duration - 1),
                    field.GetProperty("id").GetString()!));
            }
        }

        return result.OrderBy(i => i.StartDate).ToList();
    }

    private static async Task<Dictionary<string, ProjectField>> EnsureFieldsAsync()
    {
        var fields = await ReadFieldsAsync();

        if (!fields.ContainsKey("Area"))
        {
            await CreateSingleSelectAsync("Area", SeedData.Areas, "GRAY");
        }

        if (!fields.ContainsKey("Activity"))
        {
            await CreateSingleSelectAsync("Activity", SeedData.Activities, "BLUE");
        }

        if (!fields.ContainsKey("Remaining Work"))
        {
            await CreateFieldAsync("Remaining Work", "NUMBER");
        }

        return await ReadFieldsAsync();
    }

    private static async Task CreateSingleSelectAsync(string name, string[] options, string color)
    {
        var payload = options.Select(o => new { name = o, color, description = "" }).ToArray();
        await _gh.GraphQlAsync(
            """
            mutation($project: ID!, $name: String!, $options: [ProjectV2SingleSelectFieldOptionInput!]) {
              createProjectV2Field(input: {
                projectId: $project, dataType: SINGLE_SELECT, name: $name, singleSelectOptions: $options
              }) { projectV2Field { ... on ProjectV2SingleSelectField { id name } } }
            }
            """,
            new { project = _projectId, name, options = payload });

        Step($"Создано поле «{name}» ({string.Join(", ", options)})");
    }

    private static async Task CreateFieldAsync(string name, string dataType)
    {
        await _gh.GraphQlAsync(
            $$"""
            mutation($project: ID!, $name: String!) {
              createProjectV2Field(input: {projectId: $project, dataType: {{dataType}}, name: $name}) {
                projectV2Field { ... on ProjectV2Field { id name } }
              }
            }
            """,
            new { project = _projectId, name });

        Step($"Создано поле «{name}»");
    }

    private static async Task<Dictionary<string, ProjectField>> ReadFieldsAsync()
    {
        var data = await _gh.GraphQlAsync(
            """
            query($id: ID!) {
              node(id: $id) {
                ... on ProjectV2 {
                  fields(first: 50) {
                    nodes {
                      ... on ProjectV2Field {
                        id name dataType isIssueField
                        issueField {
                          ... on IssueFieldNumber { id }
                          ... on IssueFieldText { id }
                          ... on IssueFieldDate { id }
                        }
                      }
                      ... on ProjectV2IterationField { id name dataType }
                      ... on ProjectV2SingleSelectField {
                        id name dataType isIssueField options { id name }
                        issueField {
                          ... on IssueFieldSingleSelect { id options { id name } }
                        }
                      }
                    }
                  }
                }
              }
            }
            """,
            new { id = _projectId });

        var result = new Dictionary<string, ProjectField>();
        foreach (var node in data.GetProperty("node").GetProperty("fields").GetProperty("nodes").EnumerateArray())
        {
            if (!node.TryGetProperty("name", out var nameProp)) continue;

            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (node.TryGetProperty("options", out var opts))
            {
                foreach (var option in opts.EnumerateArray())
                {
                    options[option.GetProperty("name").GetString()!] = option.GetProperty("id").GetString()!;
                }
            }

            var name = nameProp.GetString()!;

            // Часть полей GitHub перенёс на уровень issue: их значения пишутся
            // мутацией setIssueFieldValue, а не updateProjectV2ItemFieldValue.
            var isIssueField = node.TryGetProperty("isIssueField", out var flag)
                               && flag.ValueKind == JsonValueKind.True;
            string? issueFieldId = null;
            if (isIssueField && node.TryGetProperty("issueField", out var issueField)
                             && issueField.ValueKind == JsonValueKind.Object
                             && issueField.TryGetProperty("id", out var issueFieldIdProp))
            {
                issueFieldId = issueFieldIdProp.GetString();
                if (issueField.TryGetProperty("options", out var issueOptions))
                {
                    foreach (var option in issueOptions.EnumerateArray())
                    {
                        options[option.GetProperty("name").GetString()!] = option.GetProperty("id").GetString()!;
                    }
                }
            }

            result[name] = new ProjectField(node.GetProperty("id").GetString()!, name, options, issueFieldId);
        }

        return result;
    }

    private static async Task SeedAsync(Dictionary<string, ProjectField> fields, List<Iteration> sprints)
    {
        Console.WriteLine();
        await LoadExistingIssuesAsync();
        Info("Создание рабочих элементов...");

        foreach (var epic in SeedData.Epics)
        {
            var epicIssue = await CreateIssueAsync(epic.Title, epic.Body, "Epic", []);
            await AddToProjectAsync(epicIssue, fields, sprints, null, "Backlog", epic.Area, null, null);
            Step($"Эпик: {epic.Title}");

            foreach (var feature in epic.Features)
            {
                var featureIssue = await CreateIssueAsync(feature.Title, feature.Body, "Feature", []);
                await LinkSubIssueAsync(epicIssue, featureIssue);
                await AddToProjectAsync(featureIssue, fields, sprints, null, "Backlog", feature.Area, null, null);
                Info($"  Функциональность: {feature.Title}");

                foreach (var item in feature.Items)
                {
                    var pbi = await CreateIssueAsync(item.Title, item.Body, "Product Backlog Item", item.Labels);
                    await LinkSubIssueAsync(featureIssue, pbi);
                    await AddToProjectAsync(pbi, fields, sprints, item.Sprint, item.Status, item.Area, item.Estimate, null);
                    if (item.Closed) await CloseIssueAsync(pbi);
                    Info($"    Элемент бэклога: {item.Title}");

                    foreach (var task in item.Tasks)
                    {
                        var taskIssue = await CreateIssueAsync(task.Title, $"Задача для: {item.Title}", "Task", []);
                        await LinkSubIssueAsync(pbi, taskIssue);
                        await AddToProjectAsync(taskIssue, fields, sprints, item.Sprint,
                            item.Closed ? "Done" : item.Status, item.Area, null, (task.Activity, task.RemainingWork));
                        if (item.Closed) await CloseIssueAsync(taskIssue);
                        Info($"      Задача: {task.Title}");
                    }
                }
            }
        }

        foreach (var bug in SeedData.Bugs)
        {
            var issue = await CreateIssueAsync(bug.Title, bug.Body, "Bug", bug.Labels);
            await AddToProjectAsync(issue, fields, sprints, bug.Sprint, bug.Status, bug.Area, null, null);
            if (bug.Closed) await CloseIssueAsync(issue);
            Step($"Ошибка: {bug.Title}");
        }
    }

    /// <summary>Уже существующие задачи репозитория: нужны, чтобы повторный запуск не создавал дубликаты.</summary>
    private static async Task LoadExistingIssuesAsync()
    {
        _existing.Clear();
        for (var page = 1; page <= 10; page++)
        {
            var issues = await _gh.GetAsync($"repos/{_org}/{_repo}/issues?state=all&per_page=100&page={page}");
            var count = 0;
            foreach (var issue in issues.EnumerateArray())
            {
                count++;
                if (issue.TryGetProperty("pull_request", out _)) continue;

                _existing[issue.GetProperty("title").GetString()!] = new Issue(
                    issue.GetProperty("number").GetInt32(),
                    issue.GetProperty("id").GetInt64(),
                    issue.GetProperty("node_id").GetString()!);
            }

            if (count < 100) break;
        }

        if (_existing.Count > 0)
        {
            Info($"В репозитории уже есть задач: {_existing.Count}, они будут переиспользованы");
        }
    }

    private static async Task<Issue> CreateIssueAsync(string title, string body, string type, string[] labels)
    {
        if (_existing.TryGetValue(title, out var known))
        {
            return known;
        }

        var payload = new Dictionary<string, object>
        {
            ["title"] = title,
            ["body"] = body,
            ["type"] = type,
            ["assignees"] = new[] { _login },
        };

        if (labels.Length > 0)
        {
            payload["labels"] = labels;
        }

        var issue = await _gh.PostAsync($"repos/{_org}/{_repo}/issues", payload);
        var created = new Issue(
            issue.GetProperty("number").GetInt32(),
            issue.GetProperty("id").GetInt64(),
            issue.GetProperty("node_id").GetString()!);
        _existing[title] = created;
        return created;
    }

    /// <summary>Связь «родитель — вложенная задача». Повторная связь игнорируется.</summary>
    private static async Task LinkSubIssueAsync(Issue parent, Issue child) =>
        await _gh.TryPostAsync($"repos/{_org}/{_repo}/issues/{parent.Number}/sub_issues",
            new { sub_issue_id = child.Id });

    private static async Task CloseIssueAsync(Issue issue) =>
        await _gh.PatchAsync($"repos/{_org}/{_repo}/issues/{issue.Number}",
            new { state = "closed", state_reason = "completed" });

    private static async Task AddToProjectAsync(
        Issue issue,
        Dictionary<string, ProjectField> fields,
        List<Iteration> sprints,
        string? sprint,
        string status,
        string area,
        int? estimate,
        (string Activity, int Remaining)? task)
    {
        var data = await _gh.GraphQlAsync(
            """
            mutation($project: ID!, $content: ID!) {
              addProjectV2ItemById(input: {projectId: $project, contentId: $content}) { item { id } }
            }
            """,
            new { project = _projectId, content = issue.NodeId });

        var itemId = data.GetProperty("addProjectV2ItemById").GetProperty("item").GetProperty("id").GetString()!;

        var issueNodeId = issue.NodeId;
        await SetSingleSelectAsync(itemId, issueNodeId, fields, "Status", status);
        await SetSingleSelectAsync(itemId, issueNodeId, fields, "Area", area);

        if (estimate is not null)
        {
            await SetNumberAsync(itemId, issueNodeId, fields, "Estimate", estimate.Value);
        }

        if (task is not null)
        {
            await SetSingleSelectAsync(itemId, issueNodeId, fields, "Activity", task.Value.Activity);
            await SetNumberAsync(itemId, issueNodeId, fields, "Remaining Work", task.Value.Remaining);
        }

        if (sprint is not null && int.TryParse(sprint, out var index) && index <= sprints.Count)
        {
            var iteration = sprints[index - 1];
            await SetIterationAsync(itemId, iteration);
            await SetDateAsync(itemId, issueNodeId, fields, "Start date", iteration.StartDate);
            await SetDateAsync(itemId, issueNodeId, fields, "Target date", iteration.EndDate);
        }
    }

    private static async Task SetSingleSelectAsync(
        string itemId, string issueNodeId, Dictionary<string, ProjectField> fields, string fieldName, string optionName)
    {
        if (!fields.TryGetValue(fieldName, out var field)) return;
        if (!field.Options.TryGetValue(optionName, out var optionId))
        {
            // Названия колонок различаются между шаблонами (Backlog / Todo / New и т. п.),
            // поэтому сначала пробуем известные синонимы, затем поиск по вхождению.
            var candidates = StatusAliases.TryGetValue(optionName, out var aliases) ? aliases : [];
            var match = candidates.FirstOrDefault(field.Options.ContainsKey)
                        ?? field.Options.Keys.FirstOrDefault(k =>
                            k.Contains(optionName, StringComparison.OrdinalIgnoreCase) ||
                            optionName.Contains(k, StringComparison.OrdinalIgnoreCase));
            if (match is null) return;
            optionId = field.Options[match];
        }

        if (field.IssueFieldId is not null)
        {
            await SetIssueFieldAsync(issueNodeId, new
            {
                fieldId = field.IssueFieldId,
                singleSelectOptionId = optionId,
            });
            return;
        }

        await _gh.GraphQlAsync(
            """
            mutation($project: ID!, $item: ID!, $field: ID!, $value: String!) {
              updateProjectV2ItemFieldValue(input: {
                projectId: $project, itemId: $item, fieldId: $field,
                value: {singleSelectOptionId: $value}
              }) { projectV2Item { id } }
            }
            """,
            new { project = _projectId, item = itemId, field = field.Id, value = optionId });
    }

    private static async Task SetNumberAsync(
        string itemId, string issueNodeId, Dictionary<string, ProjectField> fields, string fieldName, double value)
    {
        if (!fields.TryGetValue(fieldName, out var field)) return;

        if (field.IssueFieldId is not null)
        {
            await SetIssueFieldAsync(issueNodeId, new { fieldId = field.IssueFieldId, numberValue = value });
            return;
        }

        await _gh.GraphQlAsync(
            """
            mutation($project: ID!, $item: ID!, $field: ID!, $value: Float!) {
              updateProjectV2ItemFieldValue(input: {
                projectId: $project, itemId: $item, fieldId: $field, value: {number: $value}
              }) { projectV2Item { id } }
            }
            """,
            new { project = _projectId, item = itemId, field = field.Id, value });
    }

    private static async Task SetDateAsync(
        string itemId, string issueNodeId, Dictionary<string, ProjectField> fields, string fieldName, DateOnly value)
    {
        if (!fields.TryGetValue(fieldName, out var field)) return;

        if (field.IssueFieldId is not null)
        {
            await SetIssueFieldAsync(issueNodeId, new
            {
                fieldId = field.IssueFieldId,
                dateValue = value.ToString("yyyy-MM-dd"),
            });
            return;
        }

        await _gh.GraphQlAsync(
            """
            mutation($project: ID!, $item: ID!, $field: ID!, $value: Date!) {
              updateProjectV2ItemFieldValue(input: {
                projectId: $project, itemId: $item, fieldId: $field, value: {date: $value}
              }) { projectV2Item { id } }
            }
            """,
            new { project = _projectId, item = itemId, field = field.Id, value = value.ToString("yyyy-MM-dd") });
    }

    /// <summary>Запись значения поля, которое GitHub хранит на уровне issue.</summary>
    private static async Task SetIssueFieldAsync(string issueNodeId, object issueField) =>
        await _gh.GraphQlAsync(
            """
            mutation($issue: ID!, $fields: [IssueFieldCreateOrUpdateInput!]!) {
              setIssueFieldValue(input: {issueId: $issue, issueFields: $fields}) {
                issue { id }
              }
            }
            """,
            new { issue = issueNodeId, fields = new[] { issueField } });

    private static async Task SetIterationAsync(string itemId, Iteration iteration) =>
        await _gh.GraphQlAsync(
            """
            mutation($project: ID!, $item: ID!, $field: ID!, $value: String!) {
              updateProjectV2ItemFieldValue(input: {
                projectId: $project, itemId: $item, fieldId: $field, value: {iterationId: $value}
              }) { projectV2Item { id } }
            }
            """,
            new { project = _projectId, item = itemId, field = iteration.FieldId, value = iteration.Id });
}

public sealed record Issue(int Number, long Id, string NodeId);

public sealed record ProjectField(
    string Id,
    string Name,
    Dictionary<string, string> Options,
    string? IssueFieldId = null);

public sealed record Iteration(string Id, string Title, DateOnly StartDate, DateOnly EndDate, string FieldId);
