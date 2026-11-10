# eShopOnContainers 2026 – Catálogo de vinilos

Práctica de los seminarios **Desarrollando APIs** (Catalog.API) y **Desarrollando µs** (integración con ShoppingCart.API mediante RabbitMQ).
Temática elegida: **discos de vinilo** (tipo = género, marca = sello discográfico).

## Requisitos

- .NET SDK 10
- Docker Desktop (Seminario 2 y, opcionalmente, Seminario 1)
- Visual Studio 2022 17.14+ (solo si se quiere ejecutar Catalog.API sin Docker con LocalDB)

## Estructura

```
eShopOnContainers.sln
docker-compose.yml                  catalog.api + shoppingcart.api + sqldata + rabbitmq
postman/                            colección con las 4 peticiones del Seminario 2
src/Services/Catalog.API/
    Controllers/CatalogItemsController.cs
    Data/CatalogContext.cs          Model/ (entidades y DTOs)      Migrations/
    Integration/                    RabbitOptions, RabbitMqExchange, ItemEvents/ (publicación del evento)
```

`ShoppingCart.API` no tiene código en este repositorio: se usa la imagen de la asignatura `eanarion/eshop:version3.0`.

## Seminario 1 – Catalog.API

**Opción A (Docker, la misma que el Seminario 2):**

```bash
docker compose up --build -d
```

Swagger: <http://localhost/swagger>

**Opción B (Visual Studio + LocalDB):** abrir `eShopOnContainers.sln`, ejecutar `Catalog.API` con el perfil *https* o *http* (Swagger se abre solo). La base de datos se crea sola con `Database.Migrate()`.

| Operación | Endpoint |
|---|---|
| Insertar producto | `POST /api/CatalogItems` |
| Consultar un producto | `GET /api/CatalogItems/{id}` (el id viene en la cabecera `Location` del POST) |
| Consultar el catálogo | `GET /api/CatalogItems?pageSize=10&pageIndex=0` |
| Modificar un producto por nombre (cambia el precio y publica evento) | `PUT /api/CatalogItems` |
| Modificar un producto por id (permite **cambiar el nombre**) | `PUT /api/CatalogItems/{id}` |

JSON del POST:

```json
{
  "name": "Kind of Blue - Miles Davis (LP)",
  "description": "Edición en vinilo de 180 g",
  "price": 29.9,
  "catalogType": "Jazz",
  "catalogBrand": "Columbia",
  "availableStock": 25,
  "restockThreshold": 5,
  "maxStockThreshold": 50,
  "onReorder": false
}
```

JSON del PUT `/api/CatalogItems/1` (cambia el nombre):

```json
{
  "name": "Kind of Blue (Miles Davis) - LP",
  "description": "Edición en vinilo de 180 g",
  "price": 29.9,
  "catalogType": "Jazz",
  "catalogBrand": "Columbia",
  "availableStock": 25,
  "restockThreshold": 5,
  "maxStockThreshold": 50,
  "onReorder": false
}
```

## Seminario 2 – Integración con ShoppingCart.API

```bash
docker compose up --force-recreate --build -d
```

| Servicio | URL |
|---|---|
| Catalog.API (Swagger) | <http://localhost/swagger> |
| ShoppingCart.API | <http://localhost:81> |
| RabbitMQ Management | <http://localhost:15672> (guest / guest) |

Peticiones, en este orden (también en `postman/eShopForMS.postman_collection.json`):

**1. POST** `http://localhost/api/CatalogItems`

```json
{
  "name": "Abbey Road - The Beatles (LP)",
  "description": "Edición remasterizada en vinilo de 180 g",
  "price": 24.9,
  "catalogType": "Rock",
  "catalogBrand": "Apple Records",
  "availableStock": 100,
  "restockThreshold": 10,
  "maxStockThreshold": 100,
  "onReorder": false
}
```

**2. POST** `http://localhost:81/api/ShoppingCart/AddProductToShoppingCart`

```json
{
  "userName": "cliente.vinilos",
  "firstName": "Ana",
  "lastName": "García",
  "emailAddress": "ana.garcia@example.com",
  "addressLine": "Calle Mayor 1, Albacete",
  "productDTO": {
    "name": "Abbey Road - The Beatles (LP)",
    "price": 24.9,
    "quantity": 1
  }
}
```

**3. PUT** `http://localhost/api/CatalogItems` (cambia el precio 24.9 → 32.5)

```json
{
  "name": "Abbey Road - The Beatles (LP)",
  "description": "Edición remasterizada en vinilo de 180 g",
  "price": 32.5,
  "catalogType": "Rock",
  "catalogBrand": "Apple Records",
  "availableStock": 100,
  "restockThreshold": 10,
  "maxStockThreshold": 100,
  "onReorder": false
}
```

**4. GET** `http://localhost:81/api/ShoppingCart?UserName=cliente.vinilos` → el carrito debe mostrar `"price": 32.5`.

Flujo del evento: `Catalog.API` publica `ItemPriceChangedEvent { Name, Price }` en el exchange **catalogitems** (direct), routing key `myRoutingKey`, cola **itemspricechanged**, de la que consume `ShoppingCart.API`.

## Notas

- La base de datos de SQL Server vive en el volumen `sqlserver`. Para empezar de cero: `docker compose down -v`.
- Si en el primer arranque Swagger responde 500 (SQL Server aún arrancando), basta con `docker compose restart catalog.api`.
- Sin Docker, Catalog.API arranca igualmente, pero registra avisos de que no puede conectar con RabbitMQ.
