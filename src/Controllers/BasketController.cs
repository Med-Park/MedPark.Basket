using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MedPark.Basket.Dto;
using MedPark.Basket.Messaging.Commands;
using MedPark.Basket.Queries;
using MedPark.Common;
using MedPark.Common.Cache;
using MedPark.Common.Dispatchers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MedPark.Basket.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class BasketController : ControllerBase
    {
        readonly ILogger<HomeController> _log;
        private IDispatcher _dispatcher { get; }

        public BasketController(IDispatcher dispatcher, ILogger<HomeController> log)
        {
            _dispatcher = dispatcher;
            _log = log;
        }

        [HttpGet("{customerid}")]
        //[Cached(Constants.Day_In_Seconds)]
        public async Task<IActionResult> Get([FromRoute] BasketQuery query)
        {
            _log.LogInformation($"Query customer basket for {query.CustomerId}");

            var basket = await _dispatcher.QueryAsync<BasketDto>(query);

            return Ok(basket);
        }
    }
}