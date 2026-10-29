# Products API — інструкція з використання

Цей документ пояснює, як працювати з товарами через API Foxtrot Store Backend: переглядати каталог, шукати товари, створювати й редагувати їх, керувати характеристиками, завантажувати зображення, а також деактивувати та відновлювати товари.

## Перед початком

У прикладах `{{baseUrl}}` означає адресу запущеного API, наприклад `https://localhost:7001`. Порт тут наведено лише як приклад — використовуйте адресу, яку показує ваш застосунок під час запуску.

Для адміністративних операцій потрібен JWT-токен користувача з роллю `Admin`. Отримайте його через наявний у проєкті API авторизації.

У Postman для захищених запитів передавайте заголовок:

```http
Authorization: Bearer {{adminToken}}
```

## Короткий список запитів

| Метод | URL | Для чого |
|---|---|---|
| `GET` | `/api/products` | Переглянути каталог товарів |
| `GET` | `/api/products/{id}` | Отримати товар за ID |
| `GET` | `/api/products/slug/{slug}` | Отримати товар за slug |
| `GET` | `/api/products/images/{imageKey}` | Відкрити зображення товару |
| `POST` | `/api/products` | Створити товар — потрібен `Admin` |
| `PUT` | `/api/products/{id}` | Оновити товар — потрібен `Admin` |
| `DELETE` | `/api/products/{id}` | Деактивувати товар — потрібен `Admin` |
| `POST` | `/api/products/{id}/restore` | Відновити товар — потрібен `Admin` |
| `POST` | `/api/products/{id}/images` | Завантажити зображення — потрібен `Admin` |
| `GET` | `/api/products/{productId}/specifications` | Отримати характеристики товару |
| `GET` | `/api/products/{productId}/specifications/{specificationId}` | Отримати одну характеристику |
| `POST` | `/api/products/{productId}/specifications` | Створити характеристику — потрібен `Admin` |
| `PUT` | `/api/products/{productId}/specifications/{specificationId}` | Оновити характеристику — потрібен `Admin` |
| `DELETE` | `/api/products/{productId}/specifications/{specificationId}` | Видалити характеристику — потрібен `Admin` |

## 1. Перегляд каталогу

Найпростіший запит:

```http
GET {{baseUrl}}/api/products
```

За замовчуванням запит повертає першу сторінку, до 12 товарів, відсортованих за назвою за зростанням.

### Параметри каталогу

| Параметр | Приклад | Опис |
|---|---|---|
| `page` | `page=2` | Номер сторінки, починаючи з 1 |
| `pageSize` | `pageSize=20` | Кількість товарів на сторінці: від 1 до 100 |
| `search` | `search=phone` | Пошук за назвою, SKU або описом, максимум 100 символів |
| `minPrice` | `minPrice=100` | Мінімальна ціна включно |
| `maxPrice` | `maxPrice=1000` | Максимальна ціна включно |
| `brandId` | `brandId=<GUID>` | Фільтр за ID бренду |
| `categoryId` | `categoryId=<GUID>` | Фільтр за ID категорії |
| `inStock` | `inStock=true` | `true` — є залишок; `false` — залишку немає |
| `sortBy` | `sortBy=price` | Поле сортування: `name`, `price` або `createdAt` |
| `sortDirection` | `sortDirection=desc` | Напрямок: `asc` або `desc` |

Приклад: знайти товари з ціною від 100 до 1000, які є в наявності, і показати спочатку дорожчі:

```http
GET {{baseUrl}}/api/products?minPrice=100&maxPrice=1000&inStock=true&page=1&pageSize=20&sortBy=price&sortDirection=desc
```

Відповідь містить список товарів у `data`, а інформацію про сторінки — у `meta.pagination`.

Приклад відповіді (значення наведено для ілюстрації):

```json
{
  "status": {
    "isOk": true,
    "code": 200,
    "message": "OK"
  },
  "meta": {
    "apiName": "Products",
    "pagination": {
      "page": 1,
      "pageSize": 12,
      "totalPages": 3,
      "totalItems": 29
    },
    "parameters": {
      "sortBy": "name",
      "sortDirection": "asc"
    }
  },
  "data": [
    {
      "id": "00000000-0000-0000-0000-000000000001",
      "sku": "PHONE-001",
      "name": "Приклад смартфона",
      "slug": "pryklad-smartfona",
      "price": 499.99,
      "stockQuantity": 7,
      "brandName": "Приклад бренду",
      "primaryImageUrl": "/api/products/images/a1b2c3d4e5f678901234567890abcdef.webp",
      "categories": ["Смартфони"]
    }
  ]
}
```

Некоректні параметри, наприклад `page=0`, `pageSize=200`, від'ємна ціна, `minPrice` більша за `maxPrice` або невідоме поле сортування, повинні повертати `400 Bad Request`.

## 2. Отримати один товар

За ID:

```http
GET {{baseUrl}}/api/products/{{productId}}
```

За slug:

```http
GET {{baseUrl}}/api/products/slug/pryklad-smartfona
```

ID — це GUID товару, який повертає API. `slug` — адресне ім'я товару.

Успішна відповідь містить товар у полі `data`, зокрема категорії, характеристики та список зображень.

```json
{
  "id": "00000000-0000-0000-0000-000000000001",
  "sku": "PHONE-001",
  "name": "Приклад смартфона",
  "slug": "pryklad-smartfona",
  "description": "Опис товару",
  "price": 499.99,
  "stockQuantity": 7,
  "isActive": true,
  "brandName": "Приклад бренду",
  "categories": ["Смартфони"],
  "images": [
    {
      "imageUrl": "/api/products/images/a1b2c3d4e5f678901234567890abcdef.webp",
      "isPrimary": true,
      "sortOrder": 0
    }
  ],
  "specifications": [
    {
      "id": "00000000-0000-0000-0000-000000000010",
      "name": "Колір",
      "value": "Чорний",
      "sortOrder": 0
    }
  ]
}
```

Якщо товар не знайдено або він деактивований, публічний запит повертає `404 Not Found`.

## 3. Створити товар

Потрібен токен адміністратора.

```http
POST {{baseUrl}}/api/products
Authorization: Bearer {{adminToken}}
Content-Type: application/json
```

Тіло запиту:

```json
{
  "sku": "PHONE-001",
  "name": "Приклад смартфона",
  "slug": "pryklad-smartfona",
  "description": "Опис товару",
  "price": 499.99,
  "stockQuantity": 7,
  "brandId": null,
  "categoryIds": [],
  "specifications": [
    {
      "name": "Колір",
      "value": "Чорний",
      "sortOrder": 0
    },
    {
      "name": "Пам'ять",
      "value": "256 GB",
      "sortOrder": 1
    }
  ]
}
```

Основні правила для полів:

- `sku` — обов'язковий, 1–100 символів;
- `name` — обов'язкова назва, 1–150 символів;
- `slug` — обов'язковий, 1–150 символів;
- `description` — обов'язковий, до 5000 символів;
- `price` — від `0.01` до `999999999`;
- `stockQuantity` — ціле число, не менше нуля;
- `brandId` — `null` або ID активного бренду;
- `categoryIds` — список ID активних категорій без повторів;
- `specifications` — список характеристик із назвою, значенням та `sortOrder`. Назви характеристик одного товару не повинні повторюватися.

Зображення не додаються до JSON. Спочатку створіть товар, скопіюйте його `id` з відповіді, а потім завантажте зображення окремим запитом (див. розділ 6).

Успішне створення повертає `201 Created`. Некоректні дані або неактивний чи відсутній бренд/категорія мають повертати `400 Bad Request`; зайняті SKU або slug — `409 Conflict`.

## 4. Оновити товар

Потрібен токен адміністратора. Це запит `PUT`, тому надсилайте повний набір полів, які має містити товар після оновлення.

```http
PUT {{baseUrl}}/api/products/{{productId}}
Authorization: Bearer {{adminToken}}
Content-Type: application/json
```

Приклад тіла:

```json
{
  "sku": "PHONE-001",
  "name": "Оновлений смартфон",
  "slug": "pryklad-smartfona",
  "description": "Оновлений опис",
  "price": 459.99,
  "stockQuantity": 10,
  "brandId": null,
  "categoryIds": [],
  "specifications": [
    {
      "name": "Колір",
      "value": "Синій",
      "sortOrder": 0
    }
  ]
}
```

Після оновлення списки категорій і характеристик мають відповідати переданому запиту. Якщо `categoryIds` порожній, товар не матиме категорій. Якщо `specifications` порожній, характеристики буде очищено. Поле `isActive` у цьому запиті не передається: деактивація й відновлення виконуються окремими запитами.

Адміністратор може редагувати неактивний товар; редагування саме по собі не активує його.

Очікувані відповіді: `200 OK` — оновлено; `400 Bad Request` — помилка даних; `404 Not Found` — товару з таким ID немає; `409 Conflict` — SKU або slug уже використовується.

## 5. Деактивувати та відновити товар

### Деактивація

```http
DELETE {{baseUrl}}/api/products/{{productId}}
Authorization: Bearer {{adminToken}}
```

Успішна відповідь — `200 OK`. Товар деактивується й перестає відображатися у публічному каталозі, але не видаляється остаточно з бази.

### Відновлення

```http
POST {{baseUrl}}/api/products/{{productId}}/restore
Authorization: Bearer {{adminToken}}
```

Успішна відповідь — `200 OK`. Після цього товар знову можна побачити у публічному каталозі.

Повторне видалення вже деактивованого товару або спроба відновити вже активний чи відсутній товар повертає `404 Not Found`.

## 6. Завантажити зображення товару

Для завантаження потрібен токен адміністратора. Запит передає файл через `multipart/form-data`.

```http
POST {{baseUrl}}/api/products/{{productId}}/images
Authorization: Bearer {{adminToken}}
```

У Postman:

1. Виберіть метод `POST` і URL вище, замінивши `{{productId}}` на ID товару.
2. Відкрийте **Body → form-data**.
3. Додайте поле `file`, змініть його тип на **File** і виберіть зображення.
4. За потреби додайте `isPrimary` типу Text зі значенням `true` або `false`.
5. За потреби додайте `sortOrder` типу Text зі значенням `0` або більшим числом.
6. Додайте заголовок авторизації та надішліть запит.

Дозволені формати — JPEG, PNG і WEBP. Файл не повинен бути порожнім; максимальний розмір — 5 MiB; зображення — до 12 000 000 пікселів. Анімовані зображення не підтримуються.

Приклад відповіді:

```json
{
  "productId": "00000000-0000-0000-0000-000000000001",
  "imageKey": "a1b2c3d4e5f678901234567890abcdef.webp",
  "isPrimary": true,
  "sortOrder": 0
}
```

Збережіть `imageKey` або відкрийте товар через `GET /api/products/{id}` — у полі зображення має бути адреса API.

Якщо `isPrimary=true`, це зображення позначається головним. Для відсутнього або неактивного товару завантаження повертає `404 Not Found`; якщо файл не пройшов перевірку, повертається `400 Bad Request`.

## 7. Відкрити зображення

Зображення отримується окремим запитом:

```http
GET {{baseUrl}}/api/products/images/{{imageKey}}
```

Наприклад:

```text
https://localhost:<port>/api/products/images/a1b2c3d4e5f678901234567890abcdef.webp
```

Підставте справжні адресу API та `imageKey`. Успішний запит повертає файл зображення, а не JSON. У Postman можна скористатися попереднім переглядом відповіді або зберегти її як файл. У браузері відкрийте повну адресу зображення.

Коли API повертає товар у JSON, поле `imageUrl` — це шлях до зображення, наприклад:

```text
/api/products/images/a1b2c3d4e5f678901234567890abcdef.webp
```

Якщо інтерфейс сайту працює на іншому домені або порту, додайте перед цим шляхом базову адресу API.

Якщо зображення не знайдено, очікується `404 Not Found`; якщо розширення не підтримується — `400 Bad Request`.

## 8. Окреме керування характеристиками товару

Характеристики зберігаються в `ProductSpecifications` і належать конкретному товару. Вони також повертаються в `specifications` під час отримання товару через `GET /api/products/{id}` або `GET /api/products/slug/{slug}`.

Для незалежного керування характеристиками доступні окремі endpoints. Читання публічне для активних товарів; створення, редагування та видалення доступні лише користувачам із роллю `Admin`.

### Список характеристик

```http
GET {{baseUrl}}/api/products/{{productId}}/specifications
```

Відповідь повертає масив `data`, відсортований за `sortOrder` за зростанням, потім за `name` і `id`. Якщо передати дійсний JWT адміністратора, можна отримати характеристики також неактивного товару. Для публічного запиту неактивний або відсутній товар повертає `404 Not Found`.

Приклад структури відповіді:

```json
{
  "status": {
    "isOk": true,
    "code": 200,
    "message": "OK"
  },
  "meta": {
    "apiName": "ProductSpecifications",
    "dataType": "json/array",
    "cacheTime": 0,
    "manipulations": ["GET"],
    "links": {
      "parent": "/api/products",
      "self": "/api/products/00000000-0000-0000-0000-000000000001/specifications"
    }
  },
  "data": [
    {
      "id": "00000000-0000-0000-0000-000000000010",
      "name": "Колір",
      "value": "Чорний",
      "sortOrder": 0
    },
    {
      "id": "00000000-0000-0000-0000-000000000011",
      "name": "Пам'ять",
      "value": "256 GB",
      "sortOrder": 1
    }
  ]
}
```

### Одна характеристика

```http
GET {{baseUrl}}/api/products/{{productId}}/specifications/{{specificationId}}
```

Успішна відповідь повертає один об'єкт `ProductSpecificationDto` у полі `data`, з полями `id`, `name`, `value` і `sortOrder`. Якщо товар недоступний для поточного запиту або характеристику з таким ID не знайдено в межах цього товару, повертається `404 Not Found`.

### Створення характеристики

Потрібен JWT-токен адміністратора.

```http
POST {{baseUrl}}/api/products/{{productId}}/specifications
Authorization: Bearer {{adminToken}}
Content-Type: application/json
```

Тіло запиту:

```json
{
  "name": "RAM",
  "value": "16 GB",
  "sortOrder": 1
}
```

`id` і `productId` створюються/визначаються сервером і не передаються в тілі запиту. Приклад успішної відповіді:

```json
{
  "status": {
    "isOk": true,
    "code": 201,
    "message": "Created"
  },
  "meta": {
    "apiName": "ProductSpecifications",
    "dataType": "json/object",
    "cacheTime": 0,
    "manipulations": ["GET"],
    "links": {
      "parent": "/api/products",
      "self": "/api/products/00000000-0000-0000-0000-000000000001/specifications/00000000-0000-0000-0000-000000000010"
    }
  },
  "data": {
    "id": "00000000-0000-0000-0000-000000000010",
    "name": "RAM",
    "value": "16 GB",
    "sortOrder": 1
  }
}
```

Очікувані помилки: `400 Bad Request` для порожнього/некоректного значення або від'ємного `sortOrder`; `404 Not Found`, якщо товар відсутній; `409 Conflict`, якщо така назва характеристики вже існує в межах товару.

### Редагування характеристики

Потрібен JWT-токен адміністратора.

```http
PUT {{baseUrl}}/api/products/{{productId}}/specifications/{{specificationId}}
Authorization: Bearer {{adminToken}}
Content-Type: application/json
```

Передавайте повний набір полів характеристики:

```json
{
  "name": "RAM",
  "value": "32 GB",
  "sortOrder": 1
}
```

Успішна відповідь — `200 OK` із оновленим об'єктом у `data`. API перевіряє, що характеристика належить указаному товару. Якщо товар або характеристику не знайдено, повертається `404 Not Found`; помилки валідації — `400 Bad Request`; повторна назва в межах того самого товару — `409 Conflict`.

### Видалення характеристики

Потрібен JWT-токен адміністратора.

```http
DELETE {{baseUrl}}/api/products/{{productId}}/specifications/{{specificationId}}
Authorization: Bearer {{adminToken}}
```

Успішна відповідь — `200 OK`:

```json
{
  "status": {
    "isOk": true,
    "code": 200,
    "message": "OK"
  },
  "meta": {
    "apiName": "ProductSpecifications",
    "dataType": "json/object",
    "cacheTime": 0,
    "manipulations": ["DELETE"],
    "links": {
      "parent": "/api/products",
      "self": "/api/products/00000000-0000-0000-0000-000000000001/specifications/00000000-0000-0000-0000-000000000010"
    }
  },
  "data": {
    "message": "Product specification deleted successfully."
  }
}
```

Якщо товар або характеристику не знайдено — `404 Not Found`. Видалення характеристики є остаточним; на відміну від товару, характеристика не деактивується окремим прапорцем.

### Правила характеристик

| Поле | Правила |
|---|---|
| `id` | GUID, створюється сервером і повертається у відповідях |
| `name` | Обов'язкове, після обрізання пробілів не може бути порожнім; максимум 150 символів |
| `value` | Обов'язкове, після обрізання пробілів не може бути порожнім; максимум 1000 символів |
| `sortOrder` | Ціле число від 0 і більше; менше значення відображається раніше |

Назви не можуть повторюватися в межах одного товару без урахування регістру. Однакові назви в різних товарах дозволені. Оновлення або видалення перевіряє і `productId`, і `specificationId`, щоб не змінити характеристику іншого товару.

Характеристики можна також створювати разом із товаром через `POST /api/products` і синхронізувати під час `PUT /api/products/{id}`. У цьому випадку `specifications` — повний бажаний список: характеристики, яких немає в переданому списку, видаляються, а наявні оновлюються за назвою. Для редагування або видалення однієї конкретної характеристики без надсилання всіх полів товару використовуйте окремі endpoints з цього розділу.
