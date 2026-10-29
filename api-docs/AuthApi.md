# Auth API — документація автентифікації та керування користувачами

Документ описує фактичні маршрути `AuthController` у Foxtrot Store Backend: реєстрацію, вхід, доступ до власного профілю, зміну пароля та адміністративне керування користувачами й ролями.

## Перед початком

У прикладах `{{baseUrl}}` — базова адреса запущеного API, наприклад `https://localhost:7001`. Використовуйте адресу, яку показує застосунок під час запуску.

Для захищених endpoint передавайте JWT access token у заголовку:

```http
Authorization: Bearer {{accessToken}}
```

Токен видається endpoint-ом `POST /api/auth/login` і має строк дії **1 година**. Окремий refresh token не видається. Endpoint-ів logout, відновлення пароля та підтвердження email у наданій реалізації немає.

## Короткий список endpoint-ів

| Метод | URL | Доступ | Призначення |
|---|---|---|---|
| `POST` | `/api/auth/register` | Публічний | Реєстрація нового користувача з роллю `User` |
| `POST` | `/api/auth/login` | Публічний | Вхід і отримання JWT |
| `GET` | `/api/auth/me` | Будь-який авторизований користувач | Отримати власний профіль |
| `PUT` | `/api/auth/me` | Будь-який авторизований користувач | Оновити власне ім’я користувача та email |
| `PUT` | `/api/auth/me/password` | Будь-який авторизований користувач | Змінити власний пароль |
| `GET` | `/api/auth/users` | `Admin` | Отримати список користувачів |
| `GET` | `/api/auth/users/{id}` | `Admin` | Отримати користувача за ID |
| `POST` | `/api/auth/users` | `Admin` | Створити користувача з вибраною роллю |
| `PUT` | `/api/auth/users/{id}` | `Admin` | Оновити ім’я користувача та email |
| `PUT` | `/api/auth/users/{id}/role` | `Admin` | Змінити роль користувача |
| `DELETE` | `/api/auth/users/{id}` | `Admin` | Видалити користувача |

Дозволені ролі: `User` та `Admin`. Публічна реєстрація не приймає поле `role` і не дозволяє зареєструвати адміністратора.

## Формат успішних відповідей

Успішні відповіді контролера зазвичай мають оболонку `RestResponse`:

```json
{
  "status": {
    "isOk": true,
    "code": 200,
    "message": "OK"
  },
  "meta": {
    "apiName": "",
    "serverTime": 1790000000000,
    "dataType": "application/json",
    "manipulations": [],
    "cacheTime": null,
    "links": {},
    "pagination": null,
    "parameters": {}
  },
  "data": {}
}
```

Це ілюстрація структури, а не фіксовані значення. `serverTime` генерується під час відповіді. У наданому `AuthController` поле `meta.apiName` не заповнюється, тому зараз воно має значення порожнього рядка. У залежних від endpoint випадках тіло помилки може мати іншу структуру; див. розділ «Помилки та статуси».

## 1. Реєстрація

### Запит

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

### Поля

| Поле | Правила |
|---|---|
| `userName` | Обов’язкове, від 2 до 100 символів |
| `email` | Обов’язкове, має бути email-адресою, максимум 256 символів |
| `password` | Обов’язкове, від 8 до 100 символів; також має відповідати політиці Identity |

Політика пароля в `Program.cs` вимагає щонайменше 8 символів, одну цифру, одну велику та одну малу літеру. Спеціальний символ не є обов’язковим. Email має бути унікальним.

Реєстрація створює користувача з роллю `User`. Access token при реєстрації не повертається — після успішної реєстрації виконайте вхід.

### Успішна відповідь

HTTP `201 Created`. У `data` повертається `UserDto`:

```json
{
  "status": {
    "isOk": true,
    "code": 201,
    "message": "Created"
  },
  "meta": {
    "apiName": "",
    "dataType": "application/json"
  },
  "data": {
    "id": "USER_ID",
    "userName": "john_doe",
    "email": "john@example.com",
    "emailConfirmed": false,
    "isLockedOut": false,
    "roles": ["User"]
  }
}
```

Приклад показує основні поля; фактичний об’єкт `meta` також містить інші властивості `RestMeta`.

Типові статуси: `201 Created`, `400 Bad Request` для перевірок Identity або вхідних даних, `409 Conflict` для зайнятого імені користувача чи email.

## 2. Вхід

### Запит

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

`email` є обов’язковим і має відповідати формату email. `password` є обов’язковим.

### Успішна відповідь

HTTP `200 OK`. Поле `data` містить access token, час його завершення та профіль користувача:

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
  "data": {
    "accessToken": "eyJ...",
    "tokenType": "Bearer",
    "expiresAtUtc": "2026-10-29T12:00:00Z",
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

`expiresAtUtc` — приклад формату дати, а не гарантований час завершення. Реальне значення обчислюється сервером як поточний UTC-час плюс одна година.

Якщо email або пароль неправильні чи обліковий запис заблокований, endpoint повертає `401 Unauthorized` з повідомленням `Invalid email or password.`. Повідомлення однакове для цих випадків.

Після п’яти невдалих спроб входу Identity налаштований блокувати вхід на 15 хвилин. Після успішного входу передавайте отриманий токен у заголовку `Authorization` для захищених endpoint-ів.

## 3. Власний профіль

### Отримати профіль

```http
GET {{baseUrl}}/api/auth/me
Authorization: Bearer {{accessToken}}
```

Успішна відповідь — `200 OK`, `data` містить `UserDto` з полями `id`, `userName`, `email`, `emailConfirmed`, `isLockedOut` і `roles`.

### Оновити ім’я користувача та email

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

Обидва поля обов’язкові. `userName` має містити від 2 до 100 символів, `email` — коректну email-адресу довжиною до 256 символів. Email має бути унікальним.

Успіх: `200 OK` з оновленим `UserDto`. Помилки валідації зазвичай повертають `400 Bad Request`; конфлікт унікальності — `409 Conflict`.

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

| Поле | Правила |
|---|---|
| `currentPassword` | Обов’язковий поточний пароль |
| `newPassword` | Обов’язковий, від 8 до 100 символів і має відповідати політиці Identity |

Успішна відповідь — `200 OK`:

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
  "data": {
    "message": "Password changed successfully."
  }
}
```

Якщо поточний пароль неправильний або новий пароль не відповідає політиці Identity, операція не виконується. Помилки Identity повертаються зі статусом `400 Bad Request`, крім кодів, які явно класифікуються контролером як конфлікт.

Зміна пароля не відкликає вже виданий JWT миттєво. Токен, який уже видано, може залишатися дійсним до завершення строку його дії.

## 4. Адміністративне керування користувачами

Усі endpoint-и цього розділу вимагають валідний JWT користувача з роллю `Admin`.

### Отримати список користувачів

```http
GET {{baseUrl}}/api/auth/users
Authorization: Bearer {{adminToken}}
```

Успіх: `200 OK`, `data` — масив `UserDto`. Поточна реалізація повертає весь список без пагінації.

### Отримати користувача за ID

```http
GET {{baseUrl}}/api/auth/users/{{userId}}
Authorization: Bearer {{adminToken}}
```

Успіх: `200 OK` із `UserDto`; якщо користувача немає, повертається `404 Not Found`.

### Створити користувача

```http
POST {{baseUrl}}/api/auth/users
Authorization: Bearer {{adminToken}}
Content-Type: application/json
```

```json
{
  "userName": "new_user",
  "email": "new.user@example.com",
  "password": "StrongPass123",
  "role": "User"
}
```

Поля `userName`, `email` і `password` мають ті самі обмеження, що й під час публічної реєстрації. `role` може бути `User` або `Admin`; якщо поле пропущене, модель запиту за замовчуванням використовує `User`.

Успіх: `201 Created`, `data` — створений `UserDto`. Невірні дані або пароль повертають `400 Bad Request`, конфлікт унікальності імені користувача чи email — `409 Conflict`.

### Оновити користувача

```http
PUT {{baseUrl}}/api/auth/users/{{userId}}
Authorization: Bearer {{adminToken}}
Content-Type: application/json
```

```json
{
  "userName": "new_user_updated",
  "email": "new.user.updated@example.com"
}
```

Цей endpoint змінює лише ім’я користувача та email. Для зміни ролі використовуйте окремий endpoint. Успіх: `200 OK` з оновленим `UserDto`; користувача не знайдено — `404 Not Found`; помилки перевірки — `400 Bad Request`; конфлікт унікальності — `409 Conflict`.

### Змінити роль

```http
PUT {{baseUrl}}/api/auth/users/{{userId}}/role
Authorization: Bearer {{adminToken}}
Content-Type: application/json
```

```json
{
  "role": "Admin"
}
```

Дозволені значення: `User` або `Admin`, без урахування регістру під час перевірки. Зміна ролі замінює поточні ролі користувача на одну вибрану роль.

Останньому адміністратору не дозволено зняти роль `Admin`. Така спроба повертає `409 Conflict`. Невідома роль повертає `400 Bad Request`, відсутній користувач — `404 Not Found`.

Поле `role` є обов’язковим. Якщо його не передано або значення порожнє, API має повернути `400 Bad Request`.

### Видалити користувача

```http
DELETE {{baseUrl}}/api/auth/users/{{userId}}
Authorization: Bearer {{adminToken}}
```

Успіх: `200 OK` з повідомленням `User deleted successfully.`.

Обмеження:

- адміністратор не може видалити власний обліковий запис через цей endpoint — `400 Bad Request`;
- не можна видалити останнього адміністратора — `409 Conflict`;
- якщо користувача не знайдено, повертається `404 Not Found`.

## 5. Структура `UserDto`

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
| `userName` | Ім’я користувача |
| `email` | Email користувача |
| `emailConfirmed` | Чи підтверджено email; окремого endpoint підтвердження в наданому контролері немає |
| `isLockedOut` | Чи заблоковано користувача через механізм Identity |
| `roles` | Список ролей користувача; штатні ролі застосунку — `User` та `Admin` |

## 6. Помилки та HTTP-статуси

| Код | Значення |
|---|---|
| `200 OK` | Операцію виконано успішно |
| `201 Created` | Користувача створено |
| `400 Bad Request` | Неправильні дані, помилка політики пароля або інша помилка Identity |
| `401 Unauthorized` | Відсутній, недійсний або прострочений токен; також неправильні облікові дані під час входу |
| `403 Forbidden` | Токен валідний, але користувач не має ролі `Admin` для адміністративного endpoint-а |
| `404 Not Found` | Користувача не знайдено |
| `409 Conflict` | Зайняті ім’я користувача чи email або спроба видалити/понизити останнього адміністратора |

### Формати помилок у поточному контролері

Не всі помилки мають оболонку `RestResponse`:

- `IdentityErrorResponse` повертає об’єкт виду `{ "errors": [{ "code": "...", "description": "..." }] }` без `status` та `meta`;
- помилки валідації атрибутів моделі через `[ApiController]` формуються ASP.NET Core автоматично і за замовчуванням мають формат `ProblemDetails`;
- деякі гілки `NotFound()` повертають `404` без JSON-тіла;
- `401` та `403`, сформовані middleware авторизації до виконання методу контролера, не проходять через `IdentityErrorResponse`.

Клієнт має орієнтуватися насамперед на HTTP-статус і враховувати, що тіло помилки залежить від джерела помилки. Для майбутнього покращення API бажано уніфікувати ці формати.

## 7. Правила безпеки та поведінка токена

- Публічна реєстрація створює лише користувача з роллю `User`.
- Адміністративні операції доступні тільки ролі `Admin`.
- Паролі обробляються через ASP.NET Core Identity, який зберігає їх у вигляді хешів; API не повертає пароль у відповідях.
- JWT підписується HMAC SHA-256 і перевіряється за ключем, issuer, audience та часом завершення.
- Токен має строк дії одну годину; refresh token не реалізований.
- У поточній конфігурації CORS дозволено origin `http://localhost:5173`. Для іншого домену або порту frontend потрібно оновити налаштування CORS.

## 8. Перевірка через Postman

1. Зареєструйте користувача через `POST /api/auth/register`.
2. Виконайте `POST /api/auth/login` і скопіюйте `data.accessToken`.
3. Виконайте `GET /api/auth/me` із заголовком `Authorization: Bearer <token>`.
4. Спробуйте адміністративний `GET /api/auth/users` із токеном звичайного користувача — очікується `403 Forbidden`.
5. Повторіть адміністративний запит із токеном користувача ролі `Admin` — очікується `200 OK`.
