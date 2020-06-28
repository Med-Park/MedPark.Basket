using Consul;
using MedPark.Basket.Domain;
using MedPark.Basket.Messaging.Commands;
using MedPark.Basket.Queries;
using MedPark.Common;
using MedPark.Common.Consul;
using MedPark.Common.Dispatchers;
using MedPark.Common.Handlers;
using MedPark.Common.RabbitMq;
using MedPark.Common.Services;
using MedPark.Common.Types;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MedPark.Basket.Handlers.Basket
{
    public class AddProductToBasketHandler : ICommandHandler<AddProductToBasket>
    {
        private IMedParkRepository<CustomerBasket> _basketRepo { get; }
        private IMedParkRepository<BasketItem> _itemRepo { get; }
        private IMedParkRepository<Product> _productRepo { get; }
        private readonly IResponseCacheService _cacheService;

        private IDispatcher _dispatcher { get; }
        private IConsulHttpClient _consulHttpClient { get; }

        public AddProductToBasketHandler(IMedParkRepository<CustomerBasket> basketRepo, IMedParkRepository<BasketItem> itemRepo, IMedParkRepository<Product> productRepo, IResponseCacheService cacheService, IDispatcher dispatcher, IConsulHttpClient consulHttpClient)
        {
            _basketRepo = basketRepo;
            _itemRepo = itemRepo;
            _productRepo = productRepo;

            _dispatcher = dispatcher;
            _consulHttpClient = consulHttpClient;

            //Cache
            _cacheService = cacheService;
        }

        public async Task HandleAsync(AddProductToBasket command, ICorrelationContext context)
        {
            CustomerBasket basket = await _basketRepo.GetAsync(command.BasketId);

            if (basket is null)
                throw new MedParkException("basket_does_not_exist", $"The basket {command.BasketId} does not exist.");

            Product prod = await _productRepo.GetAsync(command.ProductId);

            if (prod is null)
                throw new MedParkException("prod_does_not_exist", $"The product {command.ProductId} does not exist. Please try again later.");

            var isAvailable = await _consulHttpClient.GetAsync<bool>($"catalog-service/product/isproductavailable/{command.ProductId}");

            if (!isAvailable)
                throw new MedParkException("prod_insufficient_quantity", $"The product {prod.Name} is currently not available.");

            BasketItem bItem = await _itemRepo.GetAsync(x => x.BasketId == basket.Id && x.ProductId == command.ProductId);

            if (bItem is null)
            {
                bItem = new BasketItem(command.BasketItemtId);
                bItem.BasketId = command.BasketId;
                bItem.ProductId = command.ProductId;
                bItem.Quantity = command.Quantity;

                bItem.UpdatedModifiedDate();

                //Add to basket
                await _itemRepo.AddAsync(bItem);

                //Get the updated Basket
                var updatedBasket = await _dispatcher.QueryAsync(new BasketQuery { CustomerId = basket.CustomerId });

                //Update cached value for customer's basket
                await _cacheService.CacheResponseAsync("/api/basket/" + basket.CustomerId, updatedBasket, TimeSpan.FromSeconds(Constants.Day_In_Seconds));
            }
        }
    }
}
