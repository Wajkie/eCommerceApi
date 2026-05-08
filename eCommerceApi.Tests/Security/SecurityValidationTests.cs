using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using eCommerceApi.Controllers;
using eCommerceApi.Models;
using eCommerceApi.Services;
using eCommerceApi.Models.Dto;

namespace eCommerceApi.Tests.Security
{
    public class SecurityValidationTests
    {
        #region Cascade Delete Tests

        [Fact(Skip = "This is an integration test that requires a database. It needs to be refactored to use WebApplicationFactory.")]
        public async Task DeleteCustomer_CascadeDeletesAssociatedOrders()
        {
            await Task.CompletedTask;
        }

        #endregion
    }
}
