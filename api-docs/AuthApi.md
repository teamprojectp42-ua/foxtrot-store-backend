# Auth API — документація автентифікації та керування користувачами

Документ описує маршрути `AuthController`: реєстрацію, вхід, роботу з профілем, зміну мови й пароля, а також адміністративне керування користувачами та ролями.

## Перед початком

У прикладах `{{baseUrl}}` — базова адреса запущеного API, наприклад `https://localhost:7001`. Використовуйте адресу, яку застосунок показує під час запуску.

Для захищених endpoint-ів передавайте JWT access token:

```http
Authorization: Bearer {{accessToken}}
```

Токен видається через `POST /api/auth/login` або `PUT /api/auth/me/culture`. Строк дії — одна година. Refresh token не реалізований.

Окремих endpoint-ів logout, відновлення пароля та підтвердження email у поточному `AuthController` немає.

## Ролі застосунку

| Роль | Призначення |
|---|---|
| `User` | Покупець: профіль, кошик, оформлення замовлень і перегляд власних замовлень |
| `Manager` | Робота із замовленнями відповідно до правил `OrdersController` |
| `Admin` | Адміністрування користувачів і каталогу; також доступ до адміністративних операцій із замовленнями |

Публічна реєстрація створює користувача з роллю `User`. Адміністратор може створити користувача з роллю `User`, `Manager` або `Admin`, а також замінити його поточні ролі однією вибраною роллю.

`Manager` не отримує адміністративних прав на керування користувачами або каталогом. Фактичний доступ до кожної операції визначається атрибутами авторизації відповідного контролера.

## Короткий список endpoint-ів

| Метод | URL | Доступ | Призначення |
|---|---|---|---|
| `POST` | `/api/auth/register` | Публічний | Реєстрація користувача з роллю `User` |
| `POST` | `/api/auth/login` | Публічний | Вхід і отримання JWT |
| `GET` | `/api/auth/me` | Авторизований користувач | Отримати власний профіль |
| `PUT` | `/api/auth/me` | Авторизований користувач | Оновити власне ім'я користувача та email |
| `PUT` | `/api/auth/me/culture` | Авторизований користувач | Змінити мову облікового запису й отримати новий JWT |
| `PUT` | `/api/auth/me/password` | Авторизований користувач | Змінити власний пароль |
| `GET` | `/api/auth/users` | `Admin` | Отримати список користувачів |
| `GET` | `/api/auth/users/{id}` | `Admin` | Отримати користувача за ID |
| `POST` | `/api/auth/users` | `Admin` | Створити користувача з вибраною роллю |
| `PUT` | `/api/auth/users/{id}` | `Admin` | Оновити ім'я користувача та email |
| `PUT` | `/api/auth/users/{id}/role` | `Admin` | Замінити роль користувача |
| `DELETE` | `/api/auth/users/{id}` | `Admin` | Видалити користувача |

## Формат відповідей

Більшість успішних відповідей повертає `RestResponse`:

```json
{
  "status": {
    "isOk": true,
    "code": 200,
    "message": "OK"
  },
  "meta": {
    "apiName": "",
    "dataType": "application/json"
  },
  "data": {}
}
```

Це скорочений приклад. `RestMeta` містить додаткові властивості. У показаному `AuthController` `Meta.ApiName` окремо не заповнюється.

Не всі помилки мають оболонку `RestResponse`:

- `IdentityErrorResponse` повертає об'єкт `{ "errors": [...] }`;
- автоматична валідація `[ApiController]` повертає `400 Bad Request` у форматі `ProblemDetails`;
- деякі `NotFound()` повертають `404` без JSON-тіла;
- `401` і `403`, сформовані middleware автентифікації/авторизації, не мають єдиного формату `RestResponse`.

Клієнт має орієнтуватися насамперед на HTTP-статус і не припускати, що кожна відповідь містить однакову оболонку.

## 1. Реєстрація

```http
POST {{baseUrl}}/api/auth/register
Content-Type: application/json
```

```json
{
  "userName": "john_doe",
  "email": "john@example.com",
  "password": "StrongPass123"
}
```

| Поле | Правила |
|---|---|
| `userName` | Обов'язкове, від 2 до 100 символів |
| `email` | Обов'язкове, має бути email-адресою, максимум 256 символів |
| `password` | Обов'язкове, від 8 до 100 символів; додатково перевіряється політикою Identity |

Політика пароля в `Program.cs` вимагає щонайменше 8 символів, одну цифру, одну велику та одну малу літеру. Спеціальний символ не обов'язковий. Для email увімкнено `RequireUniqueEmail`.

Реєстрація створює користувача з роллю `User`. Поле `role` у публічному запиті відсутнє й не використовується. Access token під час реєстрації не повертається — після успішної реєстрації виконайте вхід.

Успіх — `201 Created`, `data` містить `UserDto`. Помилки валідації DataAnnotations повертаються як `400 Bad Request` (`ProblemDetails`). Помилки Identity обробляються окремо; конфлікт зайнятого імені користувача або email повертається як `409 Conflict`.

## 2. Вхід

```http
POST {{baseUrl}}/api/auth/login
Content-Type: application/json
```

```json
{
  "email": "john@example.com",
  "password": "StrongPass123"
}
```

Успішна відповідь — `200 OK`. `data` містить access token, його тип, час завершення строку дії та профіль користувача:

```json
{
  "status": {
    "isOk": true,
    "code": 200,
    "message": "OK"
  },
  "data": {
    "accessToken": "eyJ...",
    "tokenType": "Bearer",
    "expiresAtUtc": "2026-11-09T20:00:00Z",
    "user": {
      "id": "USER_ID",
      "userName": "john_doe",
      "email": "john@example.com",
      "emailConfirmed": false,
      "isLockedOut": false,
      "cultureCode": "uk-UA",
      "roles": ["User"]
    }
  }
}
```

`expiresAtUtc` у прикладі ілюстративний. Реальний час обчислюється сервером як поточний UTC-час плюс одна година.

JWT містить:

- `sub` — ID користувача;
- `jti` — унікальний ID токена;
- `unique_name` — ім'я користувача;
- `email` — email;
- `security_stamp` — поточний security stamp Identity;
- один або декілька claim-ів `role`;
- `locale`, якщо для користувача збережено мову.

`Program.cs` налаштований використовувати `unique_name` як claim імені та `role` як тип claim ролі. Перевіряються підпис HS256, issuer, audience і строк дії токена. Допустимий clock skew — 30 секунд.

Під час кожного запиту з JWT додатково перевіряється, що користувач досі існує, його `security_stamp` збігається з токеном, обліковий запис не заблокований, а набір ролей у токені відповідає поточним ролям Identity. Тому токен може бути відхилений до завершення його строку дії.

Якщо email або пароль неправильні, користувач заблокований або перевірка входу неуспішна, endpoint повертає `401 Unauthorized` з узагальненим повідомленням `Invalid email or password.`. Налаштовано блокування після п'яти невдалих спроб на 15 хвилин.

Підтвердження email у показаній конфігурації не є обов'язковою умовою входу. Поле `emailConfirmed` у DTO відображає стан Identity, але окремого endpoint-а підтвердження email немає.

## 3. Власний профіль

### Отримати профіль

```http
GET {{baseUrl}}/api/auth/me
Authorization: Bearer {{accessToken}}
```

Успіх — `200 OK`; `data` містить `UserDto`.

### Оновити ім'я користувача та email

```http
PUT {{baseUrl}}/api/auth/me
Authorization: Bearer {{accessToken}}
Content-Type: application/json
```

```json
{
  "userName": "john_updated",
  "email": "john.updated@example.com"
}
```

Обидва поля обов'язкові. `userName` має містити від 2 до 100 символів, `email` — коректну email-адресу довжиною до 256 символів. Email має бути унікальним. Успіх — `200 OK` з оновленим `UserDto`.

### Змінити мову облікового запису

```http
PUT {{baseUrl}}/api/auth/me/culture
Authorization: Bearer {{accessToken}}
Content-Type: application/json
```

```json
{
  "cultureCode": "en-US"
}
```

Підтримуються тільки `uk-UA` та `en-US`. Значення нормалізується через `CultureInfo`; регістр і пробіли по краях не мають значення.

Endpoint зберігає мову користувача в claim `locale` і повертає `200 OK` з `AuthResponseDto`, тобто новим `accessToken`, `tokenType`, `expiresAtUtc` і `user`.

**Frontend повинен замінити поточний access token на токен із відповіді.** Старий токен не містить нового значення `locale`, тому запити зі старим токеном можуть і надалі оброблятися з попередньою мовою. Некоректний або непідтримуваний код мови повертає `400 Bad Request` із повідомленням `Unsupported language.`.

Під час визначення мови запиту `RequestLocalization` використовує такий порядок:

1. query string (`culture` / `ui-culture`);
2. claim `locale` з авторизованого користувача;
3. заголовок `Accept-Language`;
4. мова за замовчуванням — `uk-UA`.

### Змінити пароль

```http
PUT {{baseUrl}}/api/auth/me/password
Authorization: Bearer {{accessToken}}
Content-Type: application/json
```

```json
{
  "currentPassword": "StrongPass123",
  "newPassword": "NewStrongPass456"
}
```

`currentPassword` обов'язковий. `newPassword` має містити від 8 до 100 символів і відповідати політиці Identity. Успіх — `200 OK` з повідомленням `Password changed successfully.`. Помилки Identity повертаються через `IdentityErrorResponse`.

У `Program.cs` токен перевіряється за `security_stamp`. Якщо `DataAccessor.ChangeUserPasswordAsync()` використовує стандартний `UserManager.ChangePasswordAsync()`, Identity змінює security stamp, а вже видані токени користувача перестають проходити перевірку. Це потрібно остаточно звірити з реалізацією методу в `DataAccessor`.

## 4. Адміністративне керування користувачами

Усі endpoint-и цього розділу вимагають валідний JWT користувача з роллю `Admin`. Роль `Manager` доступу до них не має.

### Отримати список користувачів

```http
GET {{baseUrl}}/api/auth/users
Authorization: Bearer {{adminToken}}
```

Успіх — `200 OK`, `data` містить масив `UserDto`. Пагінації в показаному endpoint-і немає.

### Отримати користувача за ID

```http
GET {{baseUrl}}/api/auth/users/{{userId}}
Authorization: Bearer {{adminToken}}
```

Успіх — `200 OK` із `UserDto`; якщо користувача немає, повертається `404 Not Found`.

### Створити користувача

```http
POST {{baseUrl}}/api/auth/users
Authorization: Bearer {{adminToken}}
Content-Type: application/json
```

```json
{
  "userName": "new_manager",
  "email": "manager@example.com",
  "password": "StrongPass123",
  "role": "Manager"
}
```

`userName`, `email` і `password` мають ті самі обмеження, що й під час публічної реєстрації. `role` може бути `User`, `Manager` або `Admin`; якщо поле не передано, `UserCreateRequest.Role` за замовчуванням має значення `User`.

Успіх — `201 Created`, `data` містить створений `UserDto`. Невідома або відсутня в системі роль має повертати `400 Bad Request`; помилки унікальності користувача/email класифікуються як `409 Conflict`.

### Оновити користувача

```http
PUT {{baseUrl}}/api/auth/users/{{userId}}
Authorization: Bearer {{adminToken}}
Content-Type: application/json
```

```json
{
  "userName": "new_manager_updated",
  "email": "manager.updated@example.com"
}
```

Цей endpoint змінює лише ім'я користувача та email. Для зміни ролі використовуйте окремий endpoint. Успіх — `200 OK` з оновленим `UserDto`; відсутній користувач — `404 Not Found`; помилки валідації — `400 Bad Request`; конфлікт унікальності — `409 Conflict`.

### Змінити роль користувача

```http
PUT {{baseUrl}}/api/auth/users/{{userId}}/role
Authorization: Bearer {{adminToken}}
Content-Type: application/json
```

```json
{
  "role": "Manager"
}
```

Дозволені значення: `User`, `Manager`, `Admin`. Контролер передає перевірку ролі в `DataAccessor.UpdateUserRoleAsync()`.

Поточна реалізація JWT-перевірки порівнює ролі в токені з актуальними ролями Identity. Після успішної зміни ролі старий токен перестає проходити цю перевірку; користувачеві потрібно увійти повторно.

Останньому адміністратору не можна зняти роль `Admin`. За умови, що `DataAccessor` повертає помилку з кодом `LastAdmin`, контролер відповідає `409 Conflict`. Невідома роль повертає `400 Bad Request`, відсутній користувач — `404 Not Found`.

### Видалити користувача

```http
DELETE {{baseUrl}}/api/auth/users/{{userId}}
Authorization: Bearer {{adminToken}}
```

Успіх — `200 OK` з повідомленням `User deleted successfully.`.

Обмеження, які обробляє контролер:

- адміністратор не може видалити власний обліковий запис через цей endpoint — `400 Bad Request`;
- відсутній користувач — `404 Not Found`;
- якщо `DataAccessor` повертає код `LastAdmin`, контролер відповідає `409 Conflict`;
- якщо `DataAccessor` повертає код `UserHasOrders`, контролер відповідає `409 Conflict`.

**Важливо для завершення перевірки:** сам `AuthController` правильно класифікує `LastAdmin` і `UserHasOrders`, але наданий фрагмент `DataAccessor` не містить `DeleteUserAsync()`. Тому необхідно перевірити, що сервіс справді перевіряє наявність замовлень до видалення та повертає `IdentityResult.Failed(...)` із кодом `UserHasOrders`, а не дозволяє необробленому винятку зовнішнього ключа дійти до middleware.

## 5. Структура UserDto

```json
{
  "id": "USER_ID",
  "userName": "john_doe",
  "email": "john@example.com",
  "emailConfirmed": false,
  "isLockedOut": false,
  "cultureCode": "uk-UA",
  "roles": ["User"]
}
```

| Поле | Значення |
|---|---|
| `id` | Внутрішній ID користувача Identity |
| `userName` | Ім'я користувача |
| `email` | Email користувача |
| `emailConfirmed` | Чи підтверджено email |
| `isLockedOut` | Чи заблоковано користувача механізмом Identity |
| `cultureCode` | Код мови, збережений у claim `locale`; може бути `null`, якщо мову не задано |
| `roles` | Список ролей користувача |

Наявність властивості `CultureCode` підтверджена моделлю `UserDto`. Коректність її заповнення залежить від мапінгу в `DataAccessor.MapUserAsync()`.

## 6. Помилки та HTTP-статуси

| Код | Значення |
|---|---|
| `200 OK` | Операцію виконано успішно |
| `201 Created` | Користувача створено |
| `400 Bad Request` | Некоректні дані, помилка політики пароля, непідтримувана мова, невідома роль або інша помилка Identity |
| `401 Unauthorized` | Відсутній, недійсний, відкликаний або прострочений токен; також неправильні облікові дані під час входу |
| `403 Forbidden` | JWT валідний, але користувач не має потрібної ролі `Admin` |
| `404 Not Found` | Користувача не знайдено |
| `409 Conflict` | Зайняте ім'я користувача/email, останній адміністратор або користувач із замовленнями — якщо відповідну помилку повернув `DataAccessor` |

`IdentityErrorResponse` формує масив `errors` з `code` та `description`. До `409 Conflict` контролер відносить коди `DuplicateUserName`, `DuplicateEmail`, `UserHasOrders` і `LastAdmin`. Інші помилки Identity через цей helper повертаються як `400 Bad Request`.

Не всі помилки мають однакове тіло:

- `IdentityErrorResponse` повертає `{ "errors": [{ "code": "...", "description": "..." }] }`;
- DataAnnotations через `[ApiController]` повертає `ProblemDetails`;
- деякі `NotFound()` повертають `404` без JSON-тіла;
- `401` та `403` від middleware не проходять через `IdentityErrorResponse`.

## 7. Безпека та поведінка токена

- Публічна реєстрація створює тільки користувача з роллю `User`.
- Адміністративне керування користувачами та ролями доступне тільки `Admin`.
- Паролі обробляються ASP.NET Core Identity; API не повертає пароль у відповідях.
- JWT підписується HMAC SHA-256 і перевіряється за ключем, issuer, audience та строком дії.
- Строк дії access token — одна година; refresh token не реалізований.
- На кожному авторизованому запиті перевіряються існування користувача, `security_stamp`, блокування та відповідність ролей у токені поточним ролям Identity.
- Якщо користувача видалено, його `security_stamp` змінено або ролі більше не відповідають токену, старий JWT відхиляється.
- Для зміни мови повертається новий JWT із новим `locale`; frontend має зберегти його.
- Поточна CORS-політика дозволяє origin `http://localhost:5173`; для іншого домену або порту frontend потрібно оновити налаштування CORS.

## 8. Перевірка через Postman

1. Зареєструйте користувача через `POST /api/auth/register`.
2. Виконайте `POST /api/auth/login` та скопіюйте `data.accessToken`.
3. Перевірте `GET /api/auth/me` із заголовком `Authorization: Bearer <token>`.
4. Перевірте `PUT /api/auth/me/culture` зі значенням `en-US`; збережіть новий `data.accessToken` і перевірте, що наступні відповіді локалізуються англійською.
5. Виконайте `GET /api/auth/users` із токеном `User` — очікується `403 Forbidden`.
6. Виконайте `POST /api/auth/users` із токеном `Admin` та роллю `Manager`.
7. Увійдіть під новоствореним менеджером і перевірте, що `data.user.roles` містить `Manager`.
8. Перевірте доступ Manager до дозволених маршрутів `/api/orders` і відсутність доступу до `/api/auth/users` та адміністративних маршрутів каталогу.
9. Змініть роль тестового користувача й перевірте, що його старий JWT більше не проходить перевірку актуальних ролей.
10. Перевірте видалення користувача без замовлень, із замовленнями, останнього адміністратора та власного облікового запису адміністратора.
