## 1. Введение

Документ является соглашением по оформлению и написанию кода на языке C#. В документе приведены основные правила оформления кода и приемы, используемые при написании игр.

Цели документа:

- предоставить общие правила, позволяющие сохранить единый стиль написания кода, облегчив тем самым его понимание всеми сотрудниками компании;
- ввести базовые правила написания кода, что позволит повысить предсказуемость выполнения кода, а также избежать ошибок при написании кода новыми сотрудниками компании, не знакомыми с внутренними стандартами разработки.

**Важно**. Если что-то в базе кода не соответствует соглашению, это не является руководством к действию или разрешением нарушать и далее это соглашение.

## 2. Наименование

Стили наименования:

- **Pascal Case** – первая буква каждого слова в имени идентификатора начинается с верхнего регистра;
- **Camel Case** – первая буква первого слова в идентификаторе в нижнем регистре, все первые буквы последующих слов – в верхнем;
- **Upper Case** – стиль используется для сокращений и констант, все буквы в имени идентификатора в верхнем регистре, при необходимости разделяются нижним подчеркиванием;

Для пространств имен используется Pascal Case

```java
namespace Project.Server.Dom
{
}
```

Для классов и структур используется Pascal Case

```java
public class PlayerModel
{
}
```

Для интерфейсов используется Pascal Case с префиксом “I”

```java
public interface IPlayerModel
{
}
```

Для публичных полей используется Pascal Case

Для приватных полей - Camel Case с префиксом “_”

Для константных полей - Upper Case

Для статических полей используются те же правила, что и для не статических

Для аргументов и переменных внутри метода используется Camel Case

```java
public const float CAMERA_FOV = 50f;

private static readonly int _vertical = Animator.StringToHash("Vertical");

public int MaxHealth;

private int _maxArmor;

public void Hit(int damage)
{
    int counter = 0;
    counter++;
}
```

Для свойств используется Pascal Case

`public int MaxArmor => _hasArmor ? _maxArmor : 0;`

Для методов используется Pascal Case

```java
public void Shoot()
{
}
```

Для асинхронных методов используется Pascal Case c постфиксом Async

```
public async void ShootAsync()
{
    await UniTask.NextFrame();
}
```

Для событий используется Pascal Case с префиксом On

Для обработчика события используется Pascal case без префикса On, но с суффиксом Handler

```java
public event Action OnMessageReceived;

private void MessageReceivedHandler()
{
}
```

Для Enum-ов используется Pascal Case

```java
public enum RenderMode
{
    FullLighting = 0,
    Indirect = 1
}
```

## 3. Форматирование

В одном файле не больше одного класса/структуры/enum-а. Если есть необходимость создать класс внутри класса, то лучше сделать внешний класс partial-ом и вынести внутренний класс в отдельный файл.

Ограничение на максимальную длину строки - 150.

Если сигнатура метода (или конструктора) превышает максимальную длину строки, то каждый параметр переносится на новую строку. Первый параметр можно оставить на той же строке.

Пример короткой сигнатуры:

```java
public Vip(PlayerProfileService playerProfileService, Backend backend)
{
    _playerProfileService = playerProfileService;
    _backend = backend;
}
```

Пример длинной сигнатуры:

```java
public BattlePassEntityModel(BattlePassEntityServerModel battlePassEntityServerModel,
    IObservable<PlayerProfile> playerProfile,
    IObservable<int> requiredPlayerLevel,
    int mainBattlePassLastLevel,
    IObservable<int> mainBattlePassLevel,
    IObservable<bool> wasMainBpCompleted,
    IReactiveProperty<VipProgressModel> vipProgressModel,
    Func<RewardModel, bool> hasItemExceptConsumable,
    VipSeasonServerStaticModel vipSeasonServerStaticModel,
    ProjectConfig projectConfig)
{

}
```

Фигурные скобки всегда располагаются на новой строке

```java
public void Shoot()
{
}
```

Для выравнивания кода используются пробелы, блок отступа - 4 пробела.

Каждая переменная объявляется на отдельной строке

```java
private int _maxHealth;
private int _maxArmor;
```

Тело для if и else оформляется в скобки, даже если оно состоит из одной строки

```java
public void Hit()
{
    if (_isDead)
    {
        return;
    }

    ...
}
```

Для однострочных свойств используется Expression Body

```csharp
public int MaxArmor => _hasArmor ? _maxArmor : 0;

public int MaxHealth
{
    get => _maxHealth;
    set
    {
        _maxHealth = value;
        OnHealthUpdated?.Invoke(value);
    }
}
```

Члены класса/структуры объявляются в следующем порядке:

Инструкция для настроек в IDE 

- Константы
- Публичные статические переменные
- Приватные статические переменные
- Публичные события
- Приватные события
- Публичные поля
- Приватные поля с атрибутом [SerializeField]
- Приватные поля
- Публичные свойства
- Protected свойства
- Приватные свойства
- Конструктор
- Методы с атрибутом [Inject], [Create] или [InjectEvent]
- Приватные методы Monobehaviour - Awake, Start, OnEnable, OnDisable, OnDestroy, …
- Публичные методы
- Protected методы
- Приватные методы
- Статические методы

```csharp
public class PlayerModel : MonoBehaviour
{
    public const float CAMERA_FOV = 50f;

    private static readonly int _vertical = Animator.StringToHash("Vertical");

    public event Action OnMessageReceived;

    [SerializeField] private int _maxHealth;

    private int _health;

    private Backend _backend;
    private PlayerProfile _playerProfile;

    public bool IsAlive => _health > 0;

    [Inject]
    public void Init(Backend backend, PlayerProfile)
    {
        _backend = backend;
        _playerProfile = playerProfile;
    }

    private void Awake()
    {
        OnMessageReceived += MessageReceivedHandler;
    }

    private void OnDestroy()
    {
        OnMessageReceived -= MessageReceivedHandler;
    }

    public void Hit(HitInfo hitInfo)
    {
    }

    private void MessageReceivedHandler()
    {
    }
}
```

## 4. Общее

Использование var допустимо только в тех случаях, когда тип переменной очевиден

```java
private void MessageReceivedHandler()
{
    var list = new List<string>();

    List<INetworkPlayer> players = GetPlayers();
}
```

Если зависимости внедряются в класс, наследуемый от MonoBehaviour, то рекомендуется передавать зависимости через публичный метод Init с атрибутом [Inject]

```java
[Inject]
public void Init(IStaticData staticData, Backend backend)
{
}
```

Если зависимости внедряются в класс, не наследуемый от MonoBehaviour, то рекомендуется передавать зависимости через конструктор

```java
private readonly Backend _backend;
private readonly IStaticData _staticData;

public BlackMarket(Backend backend, IStaticData staticData)
{
    _backend = backend;
    _staticData = staticData;
}
```

Атрибуты [Inject] и [SerializeField] рекомендуется писать в одну строку, если не нарушается визуальная целостность блока переменных. Если появляются другие атрибуты (напримеры, ShowIf, BoxGroup, …), то во всем блоке с атрибутами атрибуты лучше вынести на отдельную строку.

```java
[SerializeField]
private SkinMeshGroupId _groupId;
[SerializeField, ReadOnly]
private int _bonesHashCode;
[Space(20), Title("Import bones from original FBX", bold: false), SerializeField, FilePath(Extensions = "fbx")]
private string _pathToOriginalFBX;
```

Вместо использования приватного поля и публичного свойства с геттером рекомендуется использовать автосвойство.

```java
// Не рекомендуется
private int _count;
public int Count => _count;

// Рекомендуется
public int Count { get; private set; }

// Не рекомендуется для большого количества префабов или новых классов.
// Располагать нужно ниже сериализованных полей.
[field: SerializeField] public int Count { get; private set; }

// Рекомендуется вместо сериализованного автосвойства [field: SerializeField]
[SerializeField] private int _count;
public int Count => _count;
```

Вместо возврата из метода значения `null`, используйте возврат `bool` с `out` аргументом. В существующие методы рекомендуется добавить атрибут `[CanBeNull]`, чтобы IDE подсветил необходимость проверки значения.

```java
// Не рекомендуется
public object GetObject()
{
    if (...)
    {
        return null;
    }
    ...
}

// Рекомендуется
public bool TryGetObject(out object obj)
{
    if (...)
    {
        obj = null;
        return false;
    }
    ...
}

// Рекомендуется для существующих методов
[CanBeNull]
public object GetObject()
{
    ...
}
```
