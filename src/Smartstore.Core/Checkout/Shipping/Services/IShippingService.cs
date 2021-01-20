﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Smartstore.Core.Catalog.Attributes;
using Smartstore.Core.Checkout.Attributes;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Common;
using Smartstore.Core.Data;
using Smartstore.Engine.Modularity;

namespace Smartstore.Core.Checkout.Shipping
{
    /// <summary>
    /// Shipping service interface
    /// </summary>
    public partial interface IShippingService
    {
        /// <summary>
        /// Gets active shipping rate computation methods from all providers by system name async
        /// </summary>
        /// <param name="systemName">System name for method to search after. Is <see cref="ProviderMetadata.SystemName"/> if <c>null</c></param>
		/// <param name="storeId">Loads methods restricted to specific store. 0 loads all methods</param>
        /// <remarks>
        /// Tries to get any fallback computation method when no active methods were found. Throws a <see cref="SmartException"/> when no computation method was found at all.
        /// </remarks>
        /// <returns>Shipping rate computation methods</returns>
        IEnumerable<Provider<IShippingRateComputationMethod>> LoadActiveShippingRateComputationMethods(int storeId = 0, string systemName = null);

        /// <summary>
        /// Gets all <see cref="ShippingMethod"/>s async with store mappings if active
        /// </summary>
        /// <param name="matchRules">Indicator whether to filter result query after matching cart rules</param>
        /// <param name="storeId">Filters methods by store identifier. 0 to load all methods</param>
        /// <remarks>
        /// Joins with store mappings if <see cref="DbQuerySettings.IgnoreMultiStore"/> is <c>false</c>
        /// </remarks>
        /// <returns>Shipping method collection</returns>
        Task<List<ShippingMethod>> GetAllShippingMethodsAsync(bool matchRules = false, int storeId = 0);

        /// <summary>
        /// Gets shopping cart items (total) weight async
        /// </summary>
        /// <param name="cartItem">Organized shopping cart item</param>
        /// <param name="multipliedByQuantity">Indicator whether the item weight is to be multiplied by the quantity (for several of the same item)</param>
        /// <remarks>
        /// Includes additional <see cref="ProductVariantAttribute"/> weight calculations, if <c>cartItem</c> has <see cref="ShoppingCartItem.RawAttributes"/>
        /// </remarks>
        /// <returns>Shopping cart item weight</returns>
        Task<decimal> GetCartItemWeightAsync(OrganizedShoppingCartItem cartItem, bool multipliedByQuantity = true);

        /// <summary>
        /// Gets shopping cart total weight async. Includes products with free shipping by default
        /// </summary>
        /// <param name="cart">The shopping cart</param>
		/// <param name="includeFreeShippingProducts">Indicator whether to include products with free shipping</param>
        /// <remarks>
        /// Includes <see cref="CheckoutAttribute"/> of the customer in the calculations, if available
        /// </remarks>
        /// <returns>Shopping cart total weight</returns>
        Task<decimal> GetCartTotalWeightAsync(IList<OrganizedShoppingCartItem> cart, bool includeFreeShippingProducts = true);

        /// <summary>
        /// Creates a shipping option request.
        /// </summary>
        /// <param name="cart">List of organized shopping cart items.</param>
        /// <param name="shippingAddress">Shipping address.</param>
        /// <param name="storeId">Store identifier.</param>
        /// <returns><see cref="ShippingOptionRequest"/>.</returns>
        ShippingOptionRequest CreateShippingOptionRequest(IList<OrganizedShoppingCartItem> cart, Address shippingAddress, int storeId);

        /// <summary>
        /// Gets shipping options from shopping cart async.
        /// </summary>
        /// <param name="cart">Shopping cart</param>
        /// <param name="shippingAddress">Shipping address</param>
        /// <param name="computationMethodSystemName">Allowed shipping rate computation method system name</param>
        /// <param name="storeId">Filters methods by store identifier. 0 to load all methods</param>
        /// <remarks>
        /// Always returns <see cref="ShippingOption"/> if there are any, even when there are warnings
        /// </remarks>
        /// <returns>Get shipping option resopnse</returns>
        ShippingOptionResponse GetShippingOptions(ShippingOptionRequest shippingOptionRequest, string allowedShippingRateComputationMethodSystemName = "");
    }
}