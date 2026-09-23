namespace PuDemoGenerator;

public sealed record SeedTask(string Title, string Activity, int RemainingWork);

public sealed record SeedItem(
    string Title,
    string Body,
    string Sprint,          // "1", "2", "3" — номер спринта
    string Status,          // Backlog | In progress | Done
    bool Closed,
    int Estimate,
    string Area,
    string[] Labels,
    SeedTask[] Tasks);

public sealed record SeedFeature(string Title, string Body, string Area, SeedItem[] Items);

public sealed record SeedEpic(string Title, string Body, string Area, SeedFeature[] Features);

public sealed record SeedBug(string Title, string Body, string Sprint, string Status, bool Closed, string Area, string[] Labels);

/// <summary>
/// Шаблон «Parts Unlimited»: содержимое, которым наполняется демонстрационный проект.
/// Намеренно не содержит эпик «Обучение продуктам» и связанные с ним элементы —
/// они создаются вручную в практике 2.1.
/// </summary>
public static class SeedData
{
    public const string AreaWeb = "PUL-Web";
    public const string AreaMobile = "PUL-Mobile";
    public const string AreaCore = "PartsUnlimited";

    public static readonly string[] Areas = [AreaCore, AreaWeb, AreaMobile];

    public static readonly string[] Activities =
        ["Development", "Design", "Testing", "Deployment", "Requirements"];

    public static readonly (string Name, string Color, string Description)[] Labels =
    [
        ("данные", "fbca04", "Работа связана с хранением или обработкой данных пользователей"),
        ("ux", "1d76db", "Пользовательский интерфейс и удобство использования"),
        ("тех-долг", "5319e7", "Технический долг"),
        ("производительность", "0e8a16", "Быстродействие приложения"),
        ("безопасность", "b60205", "Вопросы безопасности"),
    ];

    public static readonly SeedEpic[] Epics =
    [
        new SeedEpic(
            "Модернизация витрины интернет-магазина",
            "Обновление публичной части сайта Parts Unlimited: каталог, карточка товара, корзина.",
            AreaWeb,
            [
                new SeedFeature(
                    "Каталог товаров",
                    "Просмотр и поиск автозапчастей в каталоге.",
                    AreaWeb,
                    [
                        new SeedItem(
                            "Как клиент, я хочу искать запчасти по названию",
                            "Строка поиска по каталогу с подсказками.",
                            "1", "Done", true, 5, AreaWeb, ["ux"],
                            [
                                new SeedTask("Реализовать поиск по каталогу", "Development", 0),
                                new SeedTask("Сверстать строку поиска", "Design", 0),
                            ]),
                        new SeedItem(
                            "Как клиент, я хочу фильтровать товары по категории",
                            "Фильтрация каталога по категориям запчастей.",
                            "1", "Done", true, 3, AreaWeb, [],
                            [
                                new SeedTask("Добавить фильтр категорий", "Development", 0),
                            ]),
                        new SeedItem(
                            "Как клиент, я хочу видеть рекомендованные товары",
                            "Блок рекомендаций на главной странице.",
                            "2", "In progress", false, 5, AreaWeb, ["ux"],
                            [
                                new SeedTask("Запрос рекомендаций к сервису каталога", "Development", 4),
                                new SeedTask("Сверстать блок рекомендаций", "Design", 2),
                            ]),
                    ]),
                new SeedFeature(
                    "Корзина и оформление заказа",
                    "Оформление покупки от корзины до подтверждения заказа.",
                    AreaWeb,
                    [
                        new SeedItem(
                            "Как клиент, я хочу добавлять товары в корзину",
                            "Добавление и удаление позиций в корзине.",
                            "1", "Done", true, 8, AreaWeb, [],
                            [
                                new SeedTask("Хранение корзины в сессии", "Development", 0),
                            ]),
                        new SeedItem(
                            "Как клиент, я хочу оплачивать заказ картой",
                            "Приём платежей через внешний платёжный сервис.",
                            "2", "In progress", false, 8, AreaWeb, ["данные", "безопасность"],
                            [
                                new SeedTask("Интеграция с платёжным шлюзом", "Development", 6),
                                new SeedTask("Тест-кейсы на оплату заказа", "Testing", 3),
                            ]),
                        new SeedItem(
                            "Как клиент, я хочу отслеживать статус доставки посылки",
                            "Отображение статуса доставки в личном кабинете. Требует данных компании-перевозчика.",
                            "1", "In progress", false, 5, AreaWeb, ["данные"],
                            [
                                new SeedTask("Экран статуса доставки", "Development", 5),
                            ]),
                    ]),
            ]),

        new SeedEpic(
            "Личный кабинет клиента",
            "Регистрация, профиль, история заказов и уведомления.",
            AreaCore,
            [
                new SeedFeature(
                    "Профиль пользователя",
                    "Регистрация, вход и редактирование профиля.",
                    AreaCore,
                    [
                        new SeedItem(
                            "Как клиент, я хочу зарегистрироваться на сайте",
                            "Регистрация по электронной почте с подтверждением.",
                            "1", "Done", true, 5, AreaCore, ["данные"],
                            [
                                new SeedTask("Форма регистрации", "Development", 0),
                            ]),
                        new SeedItem(
                            "Как клиент, я хочу видеть историю своих заказов",
                            "Список заказов с фильтром по датам.",
                            "2", "Backlog", false, 3, AreaCore, [],
                            [
                                new SeedTask("Запрос истории заказов", "Development", 3),
                            ]),
                        new SeedItem(
                            "Как клиент, я хочу получать уведомления о статусе заказа",
                            "Почтовые уведомления об изменении статуса заказа.",
                            "3", "Backlog", false, 5, AreaCore, [],
                            [
                                new SeedTask("Шаблоны писем", "Design", 2),
                                new SeedTask("Отправка уведомлений", "Development", 5),
                            ]),
                    ]),
            ]),

        new SeedEpic(
            "Мобильное приложение",
            "Мобильный клиент Parts Unlimited для покупателей.",
            AreaMobile,
            [
                new SeedFeature(
                    "Мобильный каталог",
                    "Просмотр каталога с телефона.",
                    AreaMobile,
                    [
                        new SeedItem(
                            "Как клиент, я хочу просматривать каталог с телефона",
                            "Адаптивная версия каталога.",
                            "2", "In progress", false, 8, AreaMobile, ["ux"],
                            [
                                new SeedTask("Адаптивная вёрстка каталога", "Design", 4),
                                new SeedTask("Оптимизация загрузки изображений", "Development", 3),
                            ]),
                        new SeedItem(
                            "Как клиент, я хочу сохранять товары в избранное",
                            "Список избранных товаров в мобильном приложении.",
                            "3", "Backlog", false, 3, AreaMobile, [],
                            [
                                new SeedTask("Хранение избранного", "Development", 3),
                            ]),
                    ]),
            ]),
    ];

    public static readonly SeedBug[] Bugs =
    [
        new SeedBug(
            "Корзина очищается при смене языка сайта",
            "Шаги воспроизведения: добавить товар в корзину, переключить язык. Корзина пуста.",
            "1", "Done", true, AreaWeb, ["данные"]),
        new SeedBug(
            "Некорректная цена в карточке товара со скидкой",
            "Для товаров со скидкой отображается цена без учёта скидки.",
            "2", "In progress", false, AreaWeb, []),
        new SeedBug(
            "Долгая загрузка списка категорий",
            "Список категорий загружается более трёх секунд при первом открытии.",
            "2", "Backlog", false, AreaCore, ["производительность"]),
    ];
}
