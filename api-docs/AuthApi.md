# Auth API — документація автентифікації та керування користувачами

Документ описує фактичні маршрути `AuthController` у Foxtrot Store Backend: реєстрацію, вхід, роботу з власним профілем, зміну пароля та адміністративне керування користувачами й ролями.

## Перед початком

У прикладах `{{baseUrl}}` — базова адреса запущеного API, наприклад `https://localhost:7001`. Використовуйте адресу, яку показує застосунок під час запуску.

Для захищених endpoint-ів передавайте JWT access token:

```http
Authorization: Bearer {{accessToken}}
```

Токен видається через `POST /api/auth/login` і має строк дії одну годину. Refresh token не видається. Окремих endpoint-ів logout, відновлення пароля та підтвердження email у поточному контролері немає.

## Ролі застосунку

У застосунку визначені три ролі:

| Роль | Призначення |
|---|---|
| `User` | Покупець: профіль, кошик, оформлення замовлень і перегляд власних замовлень |
| `Manager` | Обробка замовлень: перегляд усіх замовлень, деталей і зміна статусів |
| `Admin` | Адміністрування користувачів і каталогу; також доступ до адміністративних операцій із замовленнями |

Публічна реєстрація створює тільки `User`. Адміністратор може створити користувача з однією з ролей `User`, `Manager` або `Admin`, а також змінити роль існуючого користувача.

`Manager` не отримує адміністративних прав на керування товарами, категоріями, брендами, акціями чи користувачами. Доступ до цих можливостей обмежується відповідними контролерами.

## Короткий список endpoint-ів

| Метод | URL | Доступ | Призначення |
|---|---|---|---|
| `POST` | `/api/auth/register` | Публічний | Реєстрація з роллю `User` |
| `POST` | `/api/auth/login` | Публічний | Вхід і отримання JWT |
| `GET` | `/api/auth/me` | Будь-який авторизований користувач | Отримати власний профіль |
| `PUT` | `/api/auth/me` | Будь-який авторизований користувач | Оновити власне ім'я користувача та email |
| `PUT` | `/api/auth/me/password` | Будь-який авторизований користувач | Змінити власний пароль |
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

Це скорочений приклад. `RestMeta` має додаткові властивості. У поточному `AuthController` `Meta.ApiName` окремо не заповнюється. Не всі помилки мають оболонку `RestResponse`: `IdentityErrorResponse` повертає `{ "errors": [...] }`, а деякі `NotFound()`/авторизаційні відповіді не мають такого самого тіла.

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
| `password` | Обов'язкове, від 8 до 100 символів і має відповідати політиці Identity |

Політика пароля в `Program.cs` вимагає щонайменше 8 символів, одну цифру, одну велику та одну малу літеру. Спеціальний символ не обов'язковий. Email має бути унікальним.

Реєстрація створює користувача з роллю `User`. Поле `role` у публічному запиті не використовується. Access token під час реєстрації не повертається — після успішної реєстрації виконайте вхід.

Успіх — `201 Created`, `data` містить `UserDto`. Типові помилки — `400 Bad Request` для помилок валідації/Identity та `409 Conflict` для зайнятого імені користувача або email.

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

Успішна відповідь — `200 OK`. `data` містить access token, час завершення строку його дії та профіль користувача:

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
    "expiresAtUtc": "2026-11-02T14:00:00Z",
    "user": {
      "id": "USER_ID",
      "userName": "john_doe",
      "email": "john@example.com",
      "emailConfirmed": false,
      "isLockedOut": false,
      "roles": ["User"]
    }
  }
}
```

`expiresAtUtc` у прикладі ілюстративний. Реальний час обчислюється сервером як поточний UTC-час плюс одна година.

JWT містить `sub` з ID користувача та окремі claims `role` для його ролей. `Program.cs` налаштований використовувати `role` як тип role claim. Після зміни ролі користувачеві потрібно увійти повторно, щоб отримати JWT з актуальними ролями.

Якщо email або пароль неправильні чи вхід неуспішний, endpoint повертає `401 Unauthorized` з повідомленням `Invalid email or password.`. Після п'яти невдалих спроб Identity налаштований блокувати вхід на 15 хвилин.

## 3. Власний профіль

### Отримати профіль

```http
GET {{baseUrl}}/api/auth/me
Authorization: Bearer {{accessToken}}
```

Успіх — `200 OK`; `data` містить `UserDto` з полями `id`, `userName`, `email`, `emailConfirmed`, `isLockedOut` і `roles`.

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

`currentPassword` обов'язковий. `newPassword` має містити від 8 до 100 символів і відповідати політиці Identity. Успіх — `200 OK` з повідомленням `Password changed successfully.`. Помилки Identity повертаються як `400 Bad Request`, якщо контролер не класифікує їх як конфлікт.

Зміна пароля не відкликає вже виданий JWT миттєво; він може залишатися дійсним до завершення строку дії.

## 4. Адміністративне керування користувачами

Усі endpoint-и цього розділу вимагають валідний JWT користувача з роллю `Admin`. Роль `Manager` доступу до них не має.

### Отримати список користувачів

```http
GET {{baseUrl}}/api/auth/users
Authorization: Bearer {{adminToken}}
```

Успіх — `200 OK`, `data` містить масив `UserDto`. Поточна реалізація повертає весь список без пагінації.

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

Успіх — `201 Created`, `data` містить створений `UserDto`. Невідома або ще не створена роль повертає `400 Bad Request`; помилки унікальності користувача/email класифікуються як `409 Conflict`.

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

Цей endpoint змінює лише ім'я користувача та email. Для зміни ролі використовуйте окремий endpoint. Успіх — `200 OK` з оновленим `UserDto`; відсутній користувач — `404 Not Found`; помилки перевірки — `400 Bad Request`; конфлікт унікальності — `409 Conflict`.

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

Дозволені значення: `User`, `Manager`, `Admin`; перевірка не враховує регістр. Зміна ролі замінює всі поточні ролі користувача однією вибраною роллю.

Останньому адміністратору не дозволено зняти роль `Admin`. Така спроба повертає `409 Conflict`. Невідома роль або порожнє поле повертає `400 Bad Request`, відсутній користувач — `404 Not Found`.

Після успішної зміни ролі користувачеві потрібно повторно увійти, щоб JWT містив нову роль.

### Видалити користувача

```http
DELETE {{baseUrl}}/api/auth/users/{{userId}}
Authorization: Bearer {{adminToken}}
```

Успіх — `200 OK` з повідомленням `User deleted successfully.`.

Обмеження поточного коду:

- адміністратор не може видалити власний обліковий запис через цей endpoint — `400 Bad Request`;
- не можна видалити останнього адміністратора — `409 Conflict`;
- якщо користувача не знайдено — `404 Not Found`;
- у `DataContext` зв'язок `Order.User` має `DeleteBehavior.Restrict`, тому база даних не дозволяє фізично видалити користувача, на якого посилаються замовлення.

**Відомий недолік:** поточний `DeleteUserAsync()` не перевіряє заздалегідь наявність замовлень і не перетворює помилку зовнішнього ключа на контрольований `IdentityResult`. Тому спроба видалити користувача з історією замовлень може завершитися необробленою помилкою сервера. Рекомендовано додати перевірку `Orders.AnyAsync(order => order.UserId == id)` і повертати окрему Identity-помилку, яку `AuthController` класифікує як `409 Conflict`.

## 5. Структура UserDto

```json
{
  "id": "USER_ID",
  "userName": "john_doe",
  "email": "john@example.com",
  "emailConfirmed": false,
  "isLockedOut": false,
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
| `roles` | Список ролей користувача: `User`, `Manager` та/або `Admin` |

## 6. Помилки та HTTP-статуси

| Код | Значення |
|---|---|
| `200 OK` | Операцію виконано успішно |
| `201 Created` | Користувача створено |
| `400 Bad Request` | Некоректні дані, помилка політики пароля, невідома роль або інша помилка Identity |
| `401 Unauthorized` | Відсутній, недійсний або прострочений токен; також неправильні облікові дані під час входу |
| `403 Forbidden` | JWT валідний, але користувач не має потрібної ролі `Admin` |
| `404 Not Found` | Користувача не знайдено |
| `409 Conflict` | Зайняте ім'я користувача/email або спроба понизити чи видалити останнього адміністратора |

Не всі помилки мають оболонку `RestResponse`:

- `IdentityErrorResponse` повертає `{ "errors": [{ "code": "...", "description": "..." }] }`;
- валідація DataAnnotations через `[ApiController]` за замовчуванням повертає `ProblemDetails`;
- деякі гілки `NotFound()` повертають `404` без JSON-тіла;
- `401` та `403`, сформовані middleware авторизації, не проходять через `IdentityErrorResponse`.

Клієнт має орієнтуватися насамперед на HTTP-статус і враховувати, що формат тіла залежить від джерела помилки.

## 7. Безпека та поведінка токена

- Публічна реєстрація створює тільки користувача з роллю `User`.
- Адміністративне керування користувачами та ролями доступне тільки `Admin`.
- `Manager` має окремий доступ до операцій із замовленнями, але не до адміністративного керування користувачами чи каталогом.
- Паролі обробляються через ASP.NET Core Identity; API не повертає пароль у відповідях.
- JWT підписується HMAC SHA-256 і перевіряється за ключем, issuer, audience та строком дії.
- Токен має строк дії одну годину; refresh token не реалізований.
- Поточна CORS-політика дозволяє origin `http://localhost:5173`; для іншого домену або порту frontend потрібно оновити налаштування CORS.

## 8. Перевірка через Postman

1. Зареєструйте користувача через `POST /api/auth/register`.
2. Виконайте `POST /api/auth/login` та скопіюйте `data.accessToken`.
3. Перевірте `GET /api/auth/me` із заголовком `Authorization: Bearer <token>`.
4. Виконайте `GET /api/auth/users` із токеном `User` — очікується `403 Forbidden`.
5. Виконайте `POST /api/auth/users` із токеном `Admin` та роллю `Manager`.
6. Увійдіть під новоствореним менеджером і перевірте, що його `data.user.roles` містить `Manager`.
7. Перевірте доступ Manager до `/api/orders/admin` та відсутність доступу до `/api/auth/users` і адміністративних маршрутів каталогу.
