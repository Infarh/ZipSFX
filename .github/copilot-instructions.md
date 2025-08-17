# Copilot configuration: C# coding style (Russian comments, English identifiers)

# Priority rules (число = приоритет, 1 — самое важное)
1. Language: Все ответы и все комментарии в коде — только на русском языке. Имена сущностей — преимущественно на английском.
2. XML comments: Для типов (class/struct/enum/delegate) и всех их публичных/внутренних членов обязателен XML-style комментарий (///).
3. XML single-sentence: Если xml-<summary> — одно предложение, писать в одну строку: /// <summary>Краткое описание</summary> (без точки в конце).
4. Each xml element on its own line: Каждый элемент xml-комментария (summary, param, returns и т.д.) — на отдельной строке.
5. Inline comments: Короткие пояснения к коду — в конце той же строки через //.
6. Avoid trivial comments: Не писать комментарии, описывающие очевидный код.
7. Naming:
   - Types, methods, properties, enums: PascalCase.
   - Method parameters: PascalCase.
   - Instance fields: _PascalCase (одно подчёркивание).
   - Static fields: __PascalCase (два подчёркивания).
   - Local variables and local functions: snake_case.
   - Prefer English for identifiers.
8. var usage: По умолчанию использовать var для локальных переменных, если тип очевиден.
9. Modern C# constructs: Использовать expression-bodied members, target-typed new, using declarations, records, pattern matching, switch expressions, Span/Memory, async/await, ValueTask — но только когда это улучшает код.
10. Minimize braces and verbosity only when it does not reduce readability.
11. Collection initialization: Использовать литералы и инициализаторы коллекций и массивов.
12. System/service comments (TODO/NOTE): Коротко в одну строку.
13. Examples: Примеры кода должны быть минимальными, корректными и компилируемыми.
14. Honesty: Не придумывать фактов. Если информации недостаточно — указать это прямо.
15. Keep code size small by default, если не запрошено иное.

# Small canonical examples (обязательно соблюдать стиль именования и комменты)
/// <summary>Управляет подключением к сервису</summary>
public class ConnectionManager
{
    /// <summary>Подключение к удалённому сервису</summary>
    private readonly IService _Service;

    /// <summary>Создаёт экземпляр менеджера</summary>
    /// <param name="Service">Экземпляр сервиса</param>
    public ConnectionManager(IService Service) => _Service = Service;

    /// <summary>Выполняет операцию и возвращает результат</summary>
    /// <param name="Input">Входные данные</param>
    /// <returns>Результат операции</returns>
    public Result Handle(OperationInput Input)
    {
        var local_counter = 0; // счётчик попыток
        var items = new[] { 1, 2, 3 }; // инициализация массива
        return new Result();
    }
}

# Конец конфигурации
