Our shop needs a product catalog. Add it to this application:

- A product has a name, required and at most 100 characters, and a price in euros, which must be greater than zero.
- The shop registers a new product with its name and its price; the API answers with the identifier of the product.
- The shop reads a product by its identifier: its name and its price. An unknown identifier is answered with 404.
- When a product is registered, the catalog opens its stock at a quantity of zero. A stock holds the identifier of its product and its quantity, which is never negative. React to the registration rather than changing the use case that registers the product.

Use these names: the module Catalog, the aggregates Product and Stock, the use cases RegisterProduct and GetProduct, the domain event ProductRegistered and its reaction OpenStockOnProductRegistered.
